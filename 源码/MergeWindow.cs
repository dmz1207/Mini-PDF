using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniPdf
{
    internal sealed class MergePage
    {
        internal PdfDocument Source;
        internal string Name;
        internal int Index, Rotation;
    }
    internal sealed class MergeGroup
    {
        internal string Name;
        internal bool Expanded;
        internal readonly List<MergePage> Pages = new List<MergePage>();
    }
    internal sealed class MergePlan : IDisposable
    {
        internal readonly List<MergeGroup> Groups = new List<MergeGroup>();
        readonly List<PdfDocument> sources = new List<PdfDocument>();
        readonly List<List<MergeGroup>> undo = new List<List<MergeGroup>>(), redo = new List<List<MergeGroup>>();
        internal int Count
        {
            get {
                int count = 0;
                foreach (var g in Groups)
                    count += g.Pages.Count;
                return count;
            }
        }
        internal bool CanUndo
        {
            get {
                return undo.Count > 0;
            }
        }
        internal bool CanRedo
        {
            get {
                return redo.Count > 0;
            }
        }
        internal string LayoutKey
        {
            get {
                var key = new StringBuilder();
                foreach (var group in Groups)
                    foreach (var page in group.Pages)
                        key.Append(page.Source.Handle.ToInt64())
                            .Append(':')
                            .Append(page.Index)
                            .Append(':')
                            .Append(page.Rotation)
                            .Append(';');
                return key.ToString();
            }
        }
        List<MergeGroup> Snapshot()
        {
            var copy = new List<MergeGroup>();
            foreach (var group in Groups)
            {
                var g = new MergeGroup { Name = group.Name, Expanded = group.Expanded };
                foreach (var p in group.Pages)
                    g.Pages.Add(
                        new MergePage { Source = p.Source, Name = p.Name, Index = p.Index, Rotation = p.Rotation });
                copy.Add(g);
            }
            return copy;
        }
        internal void Change(Action action)
        {
            undo.Add(Snapshot());
            if (undo.Count > 20)
                undo.RemoveAt(0);
            redo.Clear();
            action();
        }
        internal void Undo()
        {
            if (!CanUndo)
                return;
            redo.Add(Snapshot());
            var state = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Groups.Clear();
            Groups.AddRange(state);
        }
        internal void Redo()
        {
            if (!CanRedo)
                return;
            undo.Add(Snapshot());
            var state = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Groups.Clear();
            Groups.AddRange(state);
        }
        internal void Add(PdfDocument document, string name)
        {
            PdfDocument.Check(document.CanEdit && document.SignatureCount == 0,
                              UiText.Get("合并需要未签名且允许编辑的 PDF。"));
            PdfDocument.Check((long)Count + document.Count <= 10000, UiText.Get("合并最多支持 10000 页。"));
            Change(delegate {
                sources.Add(document);
                var group = new MergeGroup { Name = name };
                for (int i = 0; i < document.Count; i++)
                    group.Pages.Add(new MergePage { Source = document, Name = name, Index = i });
                Groups.Add(group);
            });
        }
        internal MergeGroup Owner(MergePage page)
        {
            foreach (var g in Groups)
                if (g.Pages.Contains(page))
                    return g;
            return null;
        }
        internal void MovePage(MergePage page, MergeGroup group, int index)
        {
            var old = Owner(page);
            if (old == null || !Groups.Contains(group))
                return;
            index = Math.Max(0, Math.Min(group.Pages.Count, index));
            int before = old.Pages.IndexOf(page);
            if (old == group && (index == before || index == before + 1))
                return;
            Change(delegate {
                old.Pages.Remove(page);
                if (old == group && before < index)
                    index--;
                group.Pages.Insert(index, page);
                group.Expanded = true;
            });
        }
        internal void MovePages(List<MergePage> pages, MergeGroup group, int index)
        {
            if (pages.Count == 0 || !Groups.Contains(group))
                return;
            index = Math.Max(0, Math.Min(group.Pages.Count, index));
            int earlier = 0;
            for (int i = 0; i < index; i++)
                if (pages.Contains(group.Pages[i]))
                    earlier++;
            if (pages.TrueForAll(delegate(MergePage p) { return Owner(p) == group; }))
            {
                var next = new List<MergePage>(group.Pages);
                next.RemoveAll(delegate(MergePage p) { return pages.Contains(p); });
                next.InsertRange(index - earlier, pages);
                bool same = next.Count == group.Pages.Count;
                for (int i = 0; same && i < next.Count; i++)
                    same = next[i] == group.Pages[i];
                if (same)
                    return;
            }
            Change(delegate {
                foreach (var p in pages)
                {
                    var owner = Owner(p);
                    if (owner != null)
                        owner.Pages.Remove(p);
                }
                group.Pages.InsertRange(index - earlier, pages);
                group.Expanded = true;
            });
        }
        internal void MoveGroup(MergeGroup group, int index)
        {
            int old = Groups.IndexOf(group);
            if (old < 0)
                return;
            index = Math.Max(0, Math.Min(Groups.Count, index));
            if (index == old || index == old + 1)
                return;
            Change(delegate {
                Groups.RemoveAt(old);
                if (old < index)
                    index--;
                Groups.Insert(index, group);
            });
        }
        internal List<MergePage> AllPages()
        {
            var pages = new List<MergePage>();
            foreach (var group in Groups)
                pages.AddRange(group.Pages);
            return pages;
        }
        internal void Arrange(int mode, int block = 1)
        {
            PdfDocument.Check(mode >= 0 && mode <= 8 && block >= 1 && block <= 10000, UiText.Get("排列参数无效。"));
            if (mode == 0)
            {
                var ordered = new List<MergeGroup>();
                var ranks = new Dictionary<PdfDocument, int>();
                for (int i = 0; i < sources.Count; i++)
                    if (!ranks.ContainsKey(sources[i]))
                        ranks.Add(sources[i], i);
                foreach (var page in AllPages())
                {
                    var group = ordered.Find(
                        delegate(MergeGroup g) { return g.Pages.Count > 0 && g.Pages[0].Source == page.Source; });
                    if (group == null)
                    {
                        group = new MergeGroup { Name = page.Name, Expanded = true };
                        ordered.Add(group);
                    }
                    int insert = group.Pages.FindIndex(delegate(MergePage p) { return p.Index > page.Index; });
                    if (insert < 0)
                        group.Pages.Add(page);
                    else
                        group.Pages.Insert(insert, page);
                }
                ordered.Sort(delegate(MergeGroup left, MergeGroup right) {
                    return ranks[left.Pages[0].Source].CompareTo(ranks[right.Pages[0].Source]);
                });
                Change(delegate {
                    Groups.Clear();
                    Groups.AddRange(ordered);
                });
                return;
            }
            if (mode >= 5)
            {
                ArrangeMany(mode, block);
                return;
            }
            var inputs = Groups.FindAll(delegate(MergeGroup g) { return g.Pages.Count > 0; });
            PdfDocument.Check(inputs.Count >= 2, UiText.Get("请先添加两份非空文档。"));
            var first = inputs[0];
            var second = inputs[1];
            var a = new List<MergePage>(first.Pages);
            var b = new List<MergePage>(second.Pages);
            if (mode == 2)
            {
                var swap = a;
                a = b;
                b = swap;
            }
            if (mode == 3)
                b.Reverse();
            int step = mode == 4 ? block : 1;
            var combined =
                new MergeGroup { Name = String.Format(UiText.Get("交错：{0} + {1}"), first.Name, second.Name),
                                 Expanded = true };
            for (int i = 0; i < Math.Max(a.Count, b.Count); i += step)
            {
                for (int k = i; k < Math.Min(i + step, a.Count); k++)
                    combined.Pages.Add(a[k]);
                for (int k = i; k < Math.Min(i + step, b.Count); k++)
                    combined.Pages.Add(b[k]);
            }
            int at = Groups.IndexOf(first);
            Change(delegate {
                Groups.Remove(first);
                Groups.Remove(second);
                Groups.Insert(at, combined);
            });
        }
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int StrCmpLogicalW(string left, string right);
        void ArrangeMany(int mode, int block)
        {
            if (mode == 7)
            {
                var order = new List<MergeGroup>(Groups);
                var position = new Dictionary<MergeGroup, int>();
                for (int i = 0; i < Groups.Count; i++)
                    position[Groups[i]] = i;
                order.Sort(delegate(MergeGroup a, MergeGroup b) {
                    int compared = StrCmpLogicalW(a.Name, b.Name);
                    return compared == 0 ? position[a].CompareTo(position[b]) : compared;
                });
                bool same = true;
                for (int i = 0; i < order.Count; i++)
                    if (order[i] != Groups[i])
                        same = false;
                if (!same)
                    Change(delegate {
                        Groups.Clear();
                        Groups.AddRange(order);
                    });
                return;
            }
            if (mode == 8)
            {
                if (Groups.Exists(delegate(MergeGroup g) { return g.Pages.Count > 1; }))
                    Change(delegate {
                        foreach (var group in Groups)
                            group.Pages.Reverse();
                    });
                return;
            }
            var inputs = Groups.FindAll(delegate(MergeGroup g) { return g.Pages.Count > 0; });
            PdfDocument.Check(inputs.Count >= 2, UiText.Get("请先添加两份非空文档。"));
            int step = mode == 6 ? block : 1, max = 0;
            foreach (var group in inputs)
                max = Math.Max(max, group.Pages.Count);
            var combined = new MergeGroup { Name = String.Format(UiText.Get("交错：全部 {0} 个文件"), inputs.Count),
                                            Expanded = true };
            for (int i = 0; i < max; i += step)
                foreach (var group in inputs)
                    for (int j = i; j < Math.Min(i + step, group.Pages.Count); j++)
                        combined.Pages.Add(group.Pages[j]);
            int at = Groups.IndexOf(inputs[0]);
            Change(delegate {
                foreach (var group in inputs)
                    Groups.Remove(group);
                Groups.Insert(at, combined);
            });
        }
        internal PdfDocument Build()
        {
            PdfDocument.Check(Count > 0, UiText.Get("请至少添加一页。"));
            var pages = new List<MergePage>();
            foreach (var group in Groups)
                pages.AddRange(group.Pages);
            return PdfDocument.MergePages(pages);
        }
        public void Dispose()
        {
            foreach (var d in sources)
                d.Dispose();
            sources.Clear();
            Groups.Clear();
            undo.Clear();
            redo.Clear();
        }
    }
    internal sealed partial class MergeWindow : Form
    {
        internal readonly MergePlan Plan = new MergePlan();
        readonly MergeBoard board;
        readonly Label status;
        readonly ContextMenuStrip arrangeMenu = new ContextMenuStrip();
        readonly string initialLayout;
        readonly bool organizing;
        internal PdfDocument BaseSource;
        internal MergeWindow(PdfDocument initial, bool organize = false)
        {
            organizing = organize;
            FormUi.Setup(this, organize ? "管理页面" : "合并文件", new Size(1000, 700));
            MinimumSize = new Size(700, 460);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10, 6, 4, 4) };
            Controls.Add(toolbar);
            toolbar.Controls.Add(FormUi.Button("添加文件…", delegate { Choose(); }));
            toolbar.Controls.Add(FormUi.Button("展开全部", delegate {
                foreach (var g in Plan.Groups)
                    g.Expanded = true;
                board.Rebuild();
            }));
            toolbar.Controls.Add(FormUi.Button("收起全部", delegate {
                foreach (var g in Plan.Groups)
                    g.Expanded = false;
                board.Rebuild();
            }));
            toolbar.Controls.Add(FormUi.Button("复制页面", delegate { board.Duplicate(); }));
            toolbar.Controls.Add(FormUi.Button("旋转", delegate { board.Rotate(); }));
            toolbar.Controls.Add(FormUi.Button("移除", delegate { board.Delete(); }));
            toolbar.Controls.Add(FormUi.Button("撤销", delegate {
                Plan.Undo();
                board.ResetSelection();
            }));
            toolbar.Controls.Add(FormUi.Button("重做", delegate {
                Plan.Redo();
                board.ResetSelection();
            }));
            toolbar.AutoSize = true;
            var arrange = FormUi.Button("自动排列 ▾", null);
            arrange.Name = "arrangePages";
            toolbar.Controls.Add(arrange);
            arrangeMenu.Font = Font;
            string[] modes = { "按来源和原页码归组",   "逐页交错：A 在前", "逐页交错：B 在前",
                               "第二份倒序后交错",     "每 N 页交错…",     "全部文件逐页交错",
                               "全部文件每 N 页交错…", "按文件名自然排序", "各文件内部倒序" };
            for (int i = 0; i < modes.Length; i++)
            {
                if (i == 5)
                    arrangeMenu.Items.Add(new ToolStripSeparator());
                int mode = i;
                var item = new ToolStripMenuItem(UiText.Get(modes[i])) { ShortcutKeyDisplayString = "Ctrl+" + (i + 1) };
                item.Click += delegate
                {
                    Arrange(mode);
                };
                arrangeMenu.Items.Add(item);
            }
            arrange.Click += delegate
            {
                arrangeMenu.Show(arrange, new Point(0, arrange.Height));
            };
            var footer =
                new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(10, 7, 4, 4) };
            status = new Label { AutoSize = true, Margin = new Padding(8, 7, 15, 0) };
            footer.Controls.Add(status);
            var zoom = new TrackBar { Minimum = 90, Maximum = 180, Value = 120, TickStyle = TickStyle.None,
                                      Width = 120,  Height = 34 };
            zoom.ValueChanged += delegate
            {
                board.CardWidth = zoom.Value;
                board.Rebuild();
            };
            footer.Controls.Add(zoom);
            footer.Controls.Add(FormUi.Button(organize ? "应用页面调整" : "合并为新文档", delegate {
                if (Plan.Count > 0)
                    DialogResult = DialogResult.OK;
                else
                    MessageBox.Show(this, UiText.Get("请至少保留一页。"), Text);
            }));
            footer.Controls.Add(FormUi.Button("取消", delegate { DialogResult = DialogResult.Cancel; }));
            Controls.Add(footer);
            CancelButton = (IButtonControl)footer.Controls[footer.Controls.Count - 1];
            board = new MergeBoard(Plan) { Dock = DockStyle.Fill };
            board.Changed += delegate
            {
                status.Text = String.Format(UiText.Get("{0} 个文件组 · {1} 页"), Plan.Groups.Count, Plan.Count);
            };
            Controls.Add(board);
            board.BringToFront();
            AllowDrop = true;
            DragEnter += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };
            DragDrop += delegate(object s, DragEventArgs e)
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                    AddFiles(files);
            };
            if (initial != null && initial.CanEdit && initial.SignatureCount == 0)
            {
                var copy = PdfDocument.FromBytes(initial.Serialize(false), initial.Password);
                BaseSource = copy;
                try
                {
                    Plan.Add(copy, String.IsNullOrEmpty(initial.FilePath) ? UiText.Get("当前文档")
                                                                          : Path.GetFileName(initial.FilePath));
                }
                catch
                {
                    copy.Dispose();
                    throw;
                }
                if (organize)
                    Plan.Groups[0].Expanded = true;
            }
            initialLayout = Plan.LayoutKey;
            board.Rebuild();
        }
        void Choose()
        {
            using (var dialog = new OpenFileDialog {
                Filter = UiText.Get("PDF 和图片 (*.pdf;*.png;*.jpg;*.jpeg;*.bmp)|*.pdf;*.png;*.jpg;*.jpeg;*.bmp"),
                Multiselect = true, RestoreDirectory = true
            }) if (dialog.ShowDialog(this) == DialogResult.OK) AddFiles(dialog.FileNames);
        }
        void AddFiles(string[] paths)
        {
            foreach (string path in paths)
            {
                PdfDocument document = null;
                try
                {
                    if (String.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        string password = "";
                        while (true)
                            try
                            {
                                document = PdfDocument.Open(path, password);
                                break;
                            }
                            catch (PdfException e)
                            {
                                if (e.Code != 4)
                                    throw;
                                password = Dialogs.Ask(this, UiText.Get("打开受保护的 PDF"),
                                                       Path.GetFileName(path) + UiText.Get(" 的密码："), "", true);
                                if (password == null)
                                    break;
                            }
                    }
                    else
                    {
                        string ext = Path.GetExtension(path).ToLowerInvariant();
                        PdfDocument.Check(ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp",
                                          UiText.Get("仅支持 PDF、PNG、JPEG 和 BMP 文件。"));
                        document = PdfDocument.New();
                        using (var image = Image.FromFile(path))
                        {
                            float scale = Math.Min(((float)document.Width - 36) / image.Width,
                                                   ((float)document.Height - 36) / image.Height);
                            float w = image.Width * scale, h = image.Height * scale;
                            document.AddImage(
                                path, new N.Rect(((float)document.Width - w) / 2, ((float)document.Height - h) / 2,
                                                 ((float)document.Width + w) / 2, ((float)document.Height + h) / 2));
                        }
                    }
                    if (document != null)
                    {
                        Plan.Add(document, Path.GetFileName(path));
                        document = null;
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(this, Path.GetFileName(path) + Environment.NewLine + e.Message,
                                    UiText.Get("添加文件未完成"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    if (document != null)
                        document.Dispose();
                }
            }
            board.Rebuild();
        }
        internal PdfDocument Build()
        {
            return Plan.Build();
        }
        void Arrange(int mode)
        {
            try
            {
                int block = 1;
                if (mode == 4 || mode == 6)
                {
                    string value = Dialogs.Ask(this, UiText.Get("每 N 页交错"),
                                               UiText.Get(mode == 6 ? "每份连续取几页？例如 2：A1、A2、B1、B2、C1、C2…"
                                                                    : "每份连续取几页？例如 2：A1、A2、B1、B2…"),
                                               "2", false);
                    if (value == null)
                        return;
                    PdfDocument.Check(int.TryParse(value, out block) && block >= 1 && block <= 10000,
                                      UiText.Get("请输入 1–10000 的整数。"));
                }
                Plan.Arrange(mode, block);
                board.ResetSelection();
            }
            catch (Exception e)
            {
                MessageBox.Show(this, e.Message, UiText.Get("自动排列"), MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
            }
        }
        protected override bool ProcessCmdKey(ref Message message, Keys keys)
        {
            int mode = (int)(keys & Keys.KeyCode) - (int)Keys.D1;
            bool typing = ActiveControl is TextBoxBase || ActiveControl is NumericUpDown;
            if (!typing && (keys & Keys.Modifiers) == Keys.Control && mode >= 0 && mode < 9)
            {
                Arrange(mode);
                return true;
            }
            return base.ProcessCmdKey(ref message, keys);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK && Plan.LayoutKey != initialLayout)
            {
                if (Plan.Count == 0)
                    e.Cancel =
                        MessageBox.Show(this, UiText.Get("页面已清空，放弃调整并关闭吗？"), UiText.Get("未保存的修改"),
                                        MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                                        MessageBoxDefaultButton.Button2) != DialogResult.Yes;
                else
                {
                    var answer = MessageBox.Show(
                        this,
                        UiText.Get(organizing ? "应用当前页面调整吗？\n是：应用；否：放弃；取消：继续编辑。"
                                              : "将当前排列保存为 PDF 吗？\n是：保存；否：不保存；取消：继续编辑。"),
                        UiText.Get("未保存的修改"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button3);
                    if (answer == DialogResult.Cancel)
                        e.Cancel = true;
                    else if (answer == DialogResult.Yes)
                    {
                        if (organizing)
                            DialogResult = DialogResult.OK;
                        else
                            e.Cancel = !SaveCombined();
                    }
                }
                if (e.Cancel)
                    DialogResult = DialogResult.None;
            }
            base.OnFormClosing(e);
        }
        bool SaveCombined()
        {
            using (var save =
                       new SaveFileDialog { Filter = UiText.Get("PDF 文档 (*.pdf)|*.pdf"),
                                            Title = UiText.Get("保存合并的 PDF"), FileName = UiText.Get("合并文档.pdf"),
                                            DefaultExt = "pdf", AddExtension = true, RestoreDirectory = true })
            {
                if (save.ShowDialog(this) != DialogResult.OK)
                    return false;
                try
                {
                    var window = Owner as MainWindow;
                    PdfDocument.Check(window == null || window.CanSaveToPath(save.FileName, true),
                                      UiText.Get("该文件已在另一个标签中打开，请选择其他文件名。"));
                    UseWaitCursor = true;
                    using (var combined = Plan.Build()) combined.Save(save.FileName);
                    return true;
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, error.Message, UiText.Get("保存失败"), MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    return false;
                }
                finally
                {
                    UseWaitCursor = false;
                }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                arrangeMenu.Dispose();
                Plan.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed partial class PdfDocument
    {
        internal static PdfDocument MergePages(IList<MergePage> pages)
        {
            var output = new PdfDocument();
            try
            {
                output.Handle = N.FPDF_CreateNewDocument();
                Check(output.Handle != IntPtr.Zero, UiText.Get("无法创建 PDF。"));
                Check(pages != null && pages.Count > 0 && pages.Count <= 10000,
                      UiText.Get("请至少保留一页，最多支持 10000 页。"));
                // Share resources within each batch, but import repeated source pages
                // in separate batches: PDFium also shares indirect annotation dictionaries.
                // Restore the requested order with one page-tree permutation.
                var sources = new List<PdfDocument>();
                var positions = new Dictionary<PdfDocument, List<int>>();
                for (int i = 0; i < pages.Count; i++)
                {
                    var page = pages[i];
                    Check(page != null && page.Source != null && page.Source.Handle != IntPtr.Zero && page.Index >= 0 &&
                              page.Index < page.Source.Count,
                          UiText.Get("无法合并页面。"));
                    List<int> list;
                    if (!positions.TryGetValue(page.Source, out list))
                    {
                        list = new List<int>();
                        positions.Add(page.Source, list);
                        sources.Add(page.Source);
                    }
                    list.Add(i);
                }
                var order = new int[pages.Count];
                int at = 0;
                bool reordered = false;
                foreach (var source in sources)
                {
                    var layers = new List<List<int>>();
                    var occurrences = new Dictionary<int, int>();
                    foreach (int position in positions[source])
                    {
                        int index = pages[position].Index, layer;
                        occurrences.TryGetValue(index, out layer);
                        occurrences[index] = layer + 1;
                        if (layer == layers.Count)
                            layers.Add(new List<int>());
                        layers[layer].Add(position);
                    }
                    foreach (var selected in layers)
                    {
                        var indices = new int[selected.Count];
                        for (int i = 0; i < indices.Length; i++)
                        {
                            indices[i] = pages[selected[i]].Index;
                            order[selected[i]] = at + i;
                            if (selected[i] != at + i)
                                reordered = true;
                        }
                        Check(N.FPDF_ImportPagesByIndex(output.Handle, source.Handle, indices, (uint)indices.Length,
                                                        at) != 0,
                              UiText.Get("无法合并页面。"));
                        at += indices.Length;
                    }
                }
                if (reordered)
                    Check(N.FPDF_MovePages(output.Handle, order, (uint)order.Length, 0) != 0,
                          UiText.Get("无法移动页面。"));
                for (int i = 0; i < pages.Count; i++)
                {
                    int rotation = (pages[i].Rotation % 4 + 4) % 4;
                    if (rotation == 0)
                        continue;
                    IntPtr page = N.FPDF_LoadPage(output.Handle, i);
                    try
                    {
                        Check(page != IntPtr.Zero, UiText.Get("无法读取页面。"));
                        N.FPDFPage_SetRotation(page, (N.FPDFPage_GetRotation(page) + rotation) % 4);
                    }
                    finally
                    {
                        if (page != IntPtr.Zero)
                            N.FPDF_ClosePage(page);
                    }
                }
                output.CanEdit = output.CanPrint = true;
                output.revision = output.clock = 1;
                output.OpenPage(0);
                return output;
            }
            catch
            {
                output.Dispose();
                throw;
            }
        }
    }
}
