using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiniPdf
{
    internal sealed class TextEditWindow : Form
    {
        readonly PdfDocument document;
        readonly Pick pick;
        readonly ComboBox scope, face, previewPage;
        readonly TextBox input;
        readonly NumericUpDown size, width, spacing;
        readonly CheckBox wrap, focusPreview;
        readonly Button apply, color;
        readonly PictureBox preview;
        readonly Label info, status;
        readonly Timer delay = new Timer { Interval = 250 };
        Color ink;
        bool building, colorChanged;
        int previewVersion;
        byte[] previewBytes;
        string scopeNote = "";
        internal TextEditSelection Selection { get; private set; }
        internal TextEditOptions Options { get { return Read(); } }
        internal float DisplaySizeForTest{get{return (float)size.Value;}}
        internal bool ReadyForTest { get { return apply.Enabled; } }
        internal bool LayoutFitsForTest { get { var cancel = CancelButton as Control; return apply.Parent.ClientRectangle.Contains(apply.Bounds) && cancel != null && cancel.Parent.ClientRectangle.Contains(cancel.Bounds); } }
        internal string Draft {get{return input.Text;}}
        internal void SetDraft(string value) { input.Text = value; }
        internal void TextForTest(string value) { SetDraft(value); }
        internal void ScopeForTest(int value) { scope.SelectedIndex = value; }
        internal void SizeForTest(decimal value){size.Value=value;}
        internal void FontForTest(int value) { face.SelectedIndex = value; }
        internal void WrapForTest(bool value, float mm) { width.Value = (decimal)mm; wrap.Checked = value; }
        internal void PreviewPageForTest(int index){previewPage.SelectedIndex=index;}
        internal void ApplyForTest() { apply.PerformClick(); }

        internal TextEditWindow(PdfDocument document, Pick pick) : this(document,pick,null) {}
        internal TextEditWindow(PdfDocument document, TextEditSelection selected) : this(document,null,selected) {}
        TextEditWindow(PdfDocument document, Pick pick, TextEditSelection selected)
        {
            this.document = document; this.pick = pick;
            Selection = selected ?? document.SelectTextEdit(pick, 0); ink = Color.FromArgb(255, Selection.Color.R, Selection.Color.G, Selection.Color.B);
            FormUi.Setup(this, "修改原有文字", new Size(1000, 640));
            MinimumSize = new Size(850, 540);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 6 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);
            var style = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            scope = FormUi.Combo(170); scope.Items.AddRange(new object[] { UiText.Get("当前文字对象"), UiText.Get("同一行文字"), UiText.Get("相邻同样式段落") }); scope.SelectedIndex = 0; if(Selection.Range!=null||Selection.Children!=null){scope.Items.Add(UiText.Get("所选全部文字"));scope.SelectedIndex=3;}
            face = FormUi.Combo(170); face.Items.AddRange(new object[] { UiText.Get("保留原字体"), UiText.Get("自动"), "Microsoft YaHei", "SimSun", "Arial", "Times-Roman", "Courier" }); face.SelectedIndex = 0;
            using (var installed = new System.Drawing.Text.InstalledFontCollection())
                foreach (var family in installed.Families) if (!face.Items.Contains(family.Name)) face.Items.Add(family.Name);
            face.AutoCompleteSource = AutoCompleteSource.ListItems; face.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            size = new NumericUpDown { Minimum = .1m, Maximum = 1000, DecimalPlaces = 2, Width = 65, Value = (decimal)Math.Max(.1,Math.Min(1000,Selection.Size*Selection.SizeScale)) };
            color = FormUi.Button("颜色", delegate { using (var dialog = new ColorDialog { Color = ink, FullOpen = true }) if (dialog.ShowDialog(this) == DialogResult.OK) { ink = dialog.Color; colorChanged = true; color.BackColor = ink; color.ForeColor = ink.GetBrightness() < .5 ? Color.White : Color.Black; Changed(); } });
            color.BackColor = ink; color.ForeColor = ink.GetBrightness() < .5 ? Color.White : Color.Black;
            style.Controls.AddRange(new Control[] { scope, FormUi.Label("字体"), face, FormUi.Label("字号"), size, color }); root.Controls.Add(style, 0, 0);
            var layout = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            wrap = new CheckBox { Text = UiText.Get("自动换行"), AutoSize = true };
            width = new NumericUpDown { Minimum = .4m, Maximum = 5000, DecimalPlaces = 2, Width = 80,
                Value = (decimal)Math.Max(.4, Math.Min(5000, Selection.Width * Selection.Scale * 25.4 / 72)) };
            spacing = new NumericUpDown { Minimum = .8m, Maximum = 3, DecimalPlaces = 2, Increment = .1m, Width = 60, Value = (decimal)Selection.LineHeight };
            focusPreview = new CheckBox { Text = UiText.Get("预览文字区域"), AutoSize = true, Checked = true, Margin = new Padding(18, 4, 0, 0) };
            previewPage=FormUi.Combo(125);if(Selection.Children!=null){foreach(var child in Selection.Children)previewPage.Items.Add(UiText.Get("页面")+" "+(child.Page+1));previewPage.SelectedIndex=0;layout.Controls.Add(FormUi.Label("预览页面"));layout.Controls.Add(previewPage);}else previewPage.Visible=false;
            layout.Controls.AddRange(new Control[] { wrap, FormUi.Label("宽度（毫米）"), width, FormUi.Label("行距倍数"), spacing, focusPreview }); root.Controls.Add(layout, 0, 1);
            info = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 8) }; root.Controls.Add(info, 0, 2);
            var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(960, 400), SplitterDistance = 430, Panel1MinSize = 280, Panel2MinSize = 260, SplitterWidth = 5 };
            input = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, AcceptsTab = false,
                ScrollBars = ScrollBars.Vertical, WordWrap = true, MaxLength = 20000, Font = new Font("Microsoft YaHei UI", 12), Text = Selection.Text.Replace("\r", "").Replace("\n", Environment.NewLine) };
            preview = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(241, 243, 246), SizeMode = PictureBoxSizeMode.Zoom };
            split.Panel1.Controls.Add(input); split.Panel2.Controls.Add(preview); root.Controls.Add(split, 0, 3);
            status = new Label { Dock = DockStyle.Fill, ForeColor = Color.Firebrick, AutoSize = true, MinimumSize = new Size(0, 42), Padding = new Padding(0, 7, 0, 6) }; root.Controls.Add(status, 0, 4);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            apply = FormUi.Button("应用修改", delegate { if (apply.Enabled) DialogResult = DialogResult.OK; });
            var cancel = FormUi.Button("取消", delegate { DialogResult = DialogResult.Cancel; });
            footer.Controls.Add(cancel); footer.Controls.Add(apply); footer.Controls.Add(FormUi.Button("在文档中编辑",delegate{DialogResult=DialogResult.Retry;})); root.Controls.Add(footer, 0, 5);
            AcceptButton = apply; CancelButton = cancel;
            scope.Enabled = Selection.Range==null&&Selection.Children==null&&Selection.Restructure && !Selection.Ocr;
            face.Enabled = Selection.ChangeFont; wrap.Enabled = Selection.Restructure && !Selection.Ocr;
            scope.SelectedIndexChanged += delegate { ChangeScope(); };
            input.TextChanged += delegate { Changed(); }; face.SelectedIndexChanged += delegate { Changed(); };
            size.ValueChanged += delegate { Changed(); }; width.ValueChanged += delegate { Changed(); }; spacing.ValueChanged += delegate { Changed(); };
            previewPage.SelectedIndexChanged += delegate {Changed();};
            wrap.CheckedChanged += delegate { Changed(); }; focusPreview.CheckedChanged += delegate { Changed(); };
            delay.Tick += delegate { delay.Stop(); UpdatePreview(); };
            Shown += delegate { input.Focus(); Changed(); };
            UiText.Apply(this); Changed();
        }
        internal void SetOptions(TextEditOptions options){building=true;try{input.Text=options.Text;face.SelectedItem=UiText.Get(options.Face);size.Value=(decimal)Math.Max(.1,Math.Min(1000,options.Size*Selection.SizeScale));width.Value=(decimal)Math.Max(.4,Math.Min(5000,options.Width*Selection.Scale*25.4/72));spacing.Value=(decimal)options.LineHeight;wrap.Checked=options.Wrap;ink=options.Color;colorChanged=options.ChangeColor;color.BackColor=ink;color.ForeColor=ink.GetBrightness()<.5?Color.White:Color.Black;}finally{building=false;Changed();}}
        void ChangeScope()
        {
            if (building) return;
            try
            {
                building = true;
                Selection = document.SelectTextEdit(pick, scope.SelectedIndex); scopeNote = ""; colorChanged = false;
                input.Text = Selection.Text.Replace("\r", "").Replace("\n", Environment.NewLine); size.Value = (decimal)Math.Max(.1,Math.Min(1000,Selection.Size*Selection.SizeScale)); ink = Color.FromArgb(255, Selection.Color.R, Selection.Color.G, Selection.Color.B); color.BackColor = ink; color.ForeColor = ink.GetBrightness() < .5 ? Color.White : Color.Black;
                width.Value = (decimal)Math.Max(.4, Math.Min(5000, Selection.Width * Selection.Scale * 25.4 / 72));
                spacing.Value = (decimal)Selection.LineHeight;
            }
            catch (Exception error) { scopeNote = error.Message; scope.SelectedIndex = Selection.Scope; }
            finally { building = false; Changed(); }
        }
        TextEditOptions Read()
        {
            return new TextEditOptions { Text = input.Text, Face = UiText.Source(face.Text), Size = (float)size.Value/Selection.SizeScale,
                Color = ink, ChangeColor = colorChanged, Wrap = wrap.Checked, Width = (float)width.Value * 72 / 25.4f / Selection.Scale,
                LineHeight = (float)spacing.Value };
        }
        void Changed()
        {
            if (building) return;
            width.Enabled = wrap.Checked; spacing.Enabled = !Selection.Ocr;
            info.Text = UiText.Get("原字体：") + (Selection.FontName.Length > 120 ? Selection.FontName.Substring(0, 120) + "…" : Selection.FontName) + "   ·   " + Selection.Indices.Length + UiText.Get(" 个文字对象") +
                (Selection.Ocr ? "   ·   " + UiText.Get("仅修改识别层，扫描图像保持原样。") : "") +
                (scopeNote.Length == 0 ? "" : Environment.NewLine + scopeNote);
            apply.Enabled = false; previewVersion++; RenderPool.Cancel(this, true);
            delay.Stop(); delay.Start();
        }
        void UpdatePreview()
        {
            int version = previewVersion;
            try
            {
                var options = Read();
                if (previewBytes == null){var pages=new System.Collections.Generic.List<MergePage>();if(Selection.Children!=null)foreach(var child in Selection.Children)pages.Add(new MergePage{Source=document,Index=child.Page});else pages.Add(new MergePage{Source=document,Index=document.Index});using(var copy=PdfDocument.MergePages(pages))previewBytes=copy.Serialize(false);}
                using (var copy = PdfDocument.FromBytes(previewBytes, ""))
                {
                    copy.ApplyTextEdit(Selection, options, true);
                    int pageIndex=Selection.Children==null?0:Math.Max(0,previewPage.SelectedIndex);copy.OpenPage(pageIndex);
                    var resultBounds=Selection.Children==null?options.ResultBounds:options.PageBounds==null?Selection.Children[pageIndex].Bounds:options.PageBounds[pageIndex];
                    var page = copy.PageSize(pageIndex);
                    float scale = focusPreview.Checked ? Math.Min(4, 3000 / Math.Max(page.Width, page.Height)) : Math.Min(1100 / page.Width, 1500 / page.Height);
                    int pw = Math.Max(1, (int)Math.Ceiling(page.Width * scale)), ph = Math.Max(1, (int)Math.Ceiling(page.Height * scale));
                    var region = new Rectangle(0, 0, pw, ph);
                    var boundsA = copy.ToDevice(resultBounds.left, resultBounds.top, pw, ph);
                    var boundsB = copy.ToDevice(resultBounds.right, resultBounds.bottom, pw, ph);
                    bool overflow = Math.Min(boundsA.X, boundsB.X) < -1 || Math.Min(boundsA.Y, boundsB.Y) < -1 ||
                        Math.Max(boundsA.X, boundsB.X) > pw + 1 || Math.Max(boundsA.Y, boundsB.Y) > ph + 1;
                    if (focusPreview.Checked)
                    {
                        var bounds = resultBounds;
                        var a = copy.ToDevice(bounds.left, bounds.top, pw, ph); var b = copy.ToDevice(bounds.right, bounds.bottom, pw, ph);
                        region = Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
                        region.Inflate((int)(18 * scale), (int)(18 * scale)); region.Intersect(new Rectangle(0, 0, pw, ph));
                        PdfDocument.Check(region.Width > 0 && region.Height > 0, UiText.Get("文字区域不在页面内。"));
                    }
                    status.ForeColor = Color.DimGray; status.Text = UiText.Get("正在生成预览…");
                    bool accepted = RenderPool.Request(this, copy.GetRenderSource(), "", pageIndex, pw, ph, 0, region, false,
                        delegate(RenderFrame frame, Exception error) {
                            try
                            {
                                if (version != previewVersion || IsDisposed) return;
                                if (error != null) { status.ForeColor = Color.Firebrick; status.Text = error.Message; return; }
                                if (frame == null) return;
                                var image = new Bitmap(frame.Image); var old = preview.Image; preview.Image = image; if (old != null) old.Dispose();
                                status.ForeColor = Color.DimGray;
                                status.Text = overflow ? UiText.Get("文字超出页面边界。可启用自动换行并调整宽度。") : UiText.Get("预览通过。应用后可撤销；未修改的对象保持原样。");
                                if (overflow) status.ForeColor = Color.DarkOrange; apply.Enabled = true;
                            }
                            finally { if (frame != null) frame.Dispose(); }
                        });
                    if (!accepted) { status.Text = UiText.Get("正在等待预览…"); delay.Start(); }
                }
            }
            catch (Exception error)
            {
                apply.Enabled = false; status.ForeColor = Color.Firebrick; status.Text = error.Message;
                var old = preview.Image; preview.Image = null; if (old != null) old.Dispose();
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { previewVersion++; delay.Dispose(); RenderPool.Cancel(this, true); if (preview.Image != null) preview.Image.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
