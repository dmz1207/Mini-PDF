using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using MiniAI.Desktop;

namespace MiniPdf {
    internal enum CaptureMode {Auto,Text,Formula}
    internal sealed class CapturePart {
        internal string Name,Value;
        internal bool Formula;
        public override string ToString(){return Name;}
    }
    internal sealed class CaptureContent {
        internal readonly List<CapturePart> Parts=new List<CapturePart>();
        internal int Selected;
        internal bool FormulaAvailable,Uncertain;
        internal static CaptureContent Parse(Dictionary<string,object> response,CaptureMode mode=CaptureMode.Auto) {
            object value;var lines=response.TryGetValue("lines",out value)?value as IList:null;
            PdfDocument.Check(lines!=null&&lines.Count<=5000,"Invalid MiniAI OCR response.");
            var result=new CaptureContent();result.FormulaAvailable=response.TryGetValue("formula",out value)&&Convert.ToBoolean(value);
            result.Uncertain=response.TryGetValue("formula_uncertain",out value)&&Convert.ToBoolean(value);
            var all=new StringBuilder();var formulas=new List<string>();bool text=false;
            foreach(Dictionary<string,object> line in lines) {
                string plain=line.ContainsKey("text")?Convert.ToString(line["text"]):"";
                string tex=line.ContainsKey("tex")?Convert.ToString(line["tex"]):"";
                bool formula=line.ContainsKey("kind")&&Convert.ToString(line["kind"])=="formula"&&!String.IsNullOrWhiteSpace(tex);
                if(mode==CaptureMode.Auto&&PlainIdentifier(plain))formula=false;
                PdfDocument.Check(plain.Length<=20000&&tex.Length<=20000,"Invalid OCR result length.");
                if(formula){formulas.Add(tex);if(all.Length>0)all.AppendLine();all.AppendLine("$$").AppendLine(tex).Append("$$");}
                else if(!String.IsNullOrWhiteSpace(plain)){if(all.Length>0)all.AppendLine();all.Append(plain);text=true;}
                PdfDocument.Check(all.Length<=200000,"OCR result exceeds the limit.");
            }
            result.Parts.Add(new CapturePart{Name=UiText.Get(formulas.Count>0?"全部识别结果":"识别文字"),Value=all.ToString()});
            for(int i=0;i<formulas.Count;i++)result.Parts.Add(new CapturePart{Name=UiText.Get("公式")+" "+(i+1),Value=formulas[i],Formula=true});
            result.Selected=formulas.Count==1&&!text?1:0;return result;
        }
        static bool PlainIdentifier(string text){return Regex.IsMatch((text??"").Trim(),@"^(?:\(?[+-]?[0-9][0-9,. ]*\)?|[0-9]{4}[-/年][0-9]{1,2}[-/月][0-9]{1,2}日?|[0-9]{1,2}:[0-9]{2}(?::[0-9]{2})?|(?:Fig(?:ure)?|Table|Eq(?:uation)?)[.\s]*\(?[0-9]+[a-z]?\)?|[0-9]+(?:\.[0-9]+)?\s*(?:ms|s|min|Hz|kHz|MHz|mm|cm|m|kg|g|K|°C|%))$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);}
    }
    internal sealed partial class MainWindow {
        [DllImport("dwmapi.dll")]static extern int DwmFlush();
        ToolStripMenuItem captureOcrItem;
        ToolStripButton captureOcrButton;
        CaptureRecognitionWindow captureOcrWindow;
        void InitializeCaptureRecognition(ToolStripMenuItem tools) {
            captureOcrItem=new ToolStripMenuItem(UiText.Get("截取识别（文字 / 公式）")){ShortcutKeys=Keys.Alt|Keys.Shift|Keys.O};
            captureOcrItem.Click+=delegate{CaptureRecognition();};tools.DropDownItems.Add(captureOcrItem);
            shortcuts[Keys.Alt|Keys.Shift|Keys.O]=CaptureRecognition;
            captureOcrButton=Button(toolbar,UiText.Get("截取识别"),CaptureRecognition,UiText.Get("自动识别文字或公式（Alt+Shift+O）"),null);
            captureOcrButton.Name="CaptureRecognition";
        }
        void CaptureRecognition() {
            if(!MiniAiClient.Available)return;CloseTranslation();var prior=captureOcrWindow;
            try {
                if(prior!=null&&!prior.IsDisposed)prior.Hide();Activate();Refresh();canvas.Update();Application.DoEvents();DwmFlush();Rectangle area;
                using(var image=CaptureOverlay.Pick(this,out area)) {
                    if(prior!=null&&!prior.IsDisposed)prior.Show(this);
                    if(image==null)return;
                    if(prior==null||prior.IsDisposed){var window=new CaptureRecognitionWindow(image,MiniAiClient.PackRoot){Icon=Icon};captureOcrWindow=window;window.FormClosed+=delegate{if(captureOcrWindow==window)captureOcrWindow=null;};window.Show(this);}
                    else {prior.ReplaceImage(image,MiniAiClient.PackRoot);prior.Activate();}
                }
            }catch(Exception error){if(prior!=null&&!prior.IsDisposed)prior.Show(this);MessageBox.Show(this,error.Message,"Mini PDF",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        }
    }
    internal sealed class CaptureRecognitionWindow : Form {
        Bitmap image;
        byte[] png;
        string root;
        readonly PictureBox original;
        readonly TextBox editor;
        readonly ComboBox mode,parts,format;
        readonly Button copy,save,cancel;
        readonly Label status;
        readonly ProgressBar progress;
        readonly Dictionary<CaptureMode,CaptureContent> cache=new Dictionary<CaptureMode,CaptureContent>();
        CaptureContent content;
        int generation,current=-1;
        bool updating,busy;
        internal CaptureRecognitionWindow(Bitmap source,string package) {
            FormUi.Setup(this,"截取识别 · 文字 / 公式",new Size(760,600));MinimumSize=new Size(620,460);BackColor=Color.FromArgb(245,247,250);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=5};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,4));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
            var header=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));
            mode=FormUi.Combo(160,"自动识别","仅文字","仅公式");mode.Name="CaptureMode";mode.Dock=DockStyle.Fill;mode.SelectedIndex=0;header.Controls.Add(mode,0,0);
            parts=FormUi.Combo(260);parts.Name="CaptureParts";parts.Dock=DockStyle.Fill;parts.Visible=false;header.Controls.Add(parts,1,0);layout.Controls.Add(header,0,0);
            progress=new ProgressBar{Dock=DockStyle.Fill,Style=ProgressBarStyle.Marquee,Height=3,Margin=Padding.Empty};layout.Controls.Add(progress,0,1);
            var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,Size=new Size(720,420),SplitterDistance=180,Panel1MinSize=80,Panel2MinSize=100,SplitterWidth=5,Margin=new Padding(0,8,0,8)};
            original=new PictureBox{Name="CaptureOriginal",Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle};split.Panel1.Controls.Add(original);
            editor=new TextBox{Name="CaptureResult",Dock=DockStyle.Fill,Multiline=true,AcceptsReturn=true,AcceptsTab=true,ScrollBars=ScrollBars.Both,MaxLength=200000,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Consolas",11),BackColor=Color.White};split.Panel2.Controls.Add(editor);layout.Controls.Add(split,0,2);
            status=new Label{Name="CaptureStatus",Dock=DockStyle.Fill,AutoSize=true,MaximumSize=new Size(ClientSize.Width-28,0),Padding=new Padding(0,3,0,8),ForeColor=Color.FromArgb(72,85,100)};layout.Controls.Add(status,0,3);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty};format=FormUi.Combo(170,"LaTeX","MathType / MathML","行内 LaTeX","独立 LaTeX");format.Name="CaptureFormat";format.SelectedIndex=0;footer.Controls.Add(format);
            copy=ActionButton("复制",CopyResult);copy.Name="CopyCapture";save=ActionButton("保存…",SaveResult);cancel=ActionButton("取消识别",CancelRecognition);footer.Controls.Add(copy);footer.Controls.Add(save);footer.Controls.Add(cancel);footer.Controls.Add(ActionButton("关闭",Close));layout.Controls.Add(footer,0,4);
            Resize+=delegate{status.MaximumSize=new Size(Math.Max(200,ClientSize.Width-28),0);};
            mode.SelectedIndexChanged+=delegate{if(!updating)StartRecognition();};parts.SelectedIndexChanged+=delegate{if(!updating)SelectPart();};editor.TextChanged+=delegate{UpdateActions();};
            ReplaceImageCore(source,package);SetBusy(true);Shown+=delegate{StartRecognition();};
        }
        Button ActionButton(string text,Action action){return FormUi.Button(text,delegate{try{action();}catch(Exception error){status.Text=error.Message;}});}
        void ReplaceImageCore(Bitmap source,string package) {
            var next=new Bitmap(source);byte[] encoded;try{using(var stream=new MemoryStream()){next.Save(stream,ImageFormat.Png);encoded=stream.ToArray();}}catch{next.Dispose();throw;}
            Interlocked.Increment(ref generation);original.Image=null;if(image!=null)image.Dispose();image=next;original.Image=image;png=encoded;root=package;
            cache.Clear();content=null;current=-1;updating=true;mode.SelectedIndex=0;parts.Items.Clear();parts.Visible=false;editor.Clear();updating=false;SetBusy(false);
        }
        internal void ReplaceImage(Bitmap source,string package){ReplaceImageCore(source,package);StartRecognition();}
        void SaveDraft(){if(content!=null&&current>=0&&current<content.Parts.Count){content.Parts[current].Value=editor.Text;content.Selected=current;}}
        void StartRecognition() {
            SaveDraft();int serial=Interlocked.Increment(ref generation);CaptureMode selected=(CaptureMode)mode.SelectedIndex;CaptureContent saved;
            if(cache.TryGetValue(selected,out saved)){Adopt(saved);return;}
            if(!MiniAiClient.AvailableAt(root)){SetBusy(false);status.Text=UiText.Get("安装 AI 外挂包以启用");return;}
            content=null;current=-1;parts.Visible=false;editor.Clear();SetBusy(true);status.Text=UiText.Get("正在识别文字和公式…");byte[] bytes=png;string package=root;
            new Thread(delegate(){try {
                MiniAiClient.Ensure(package);Func<bool> canceled=delegate{return serial!=Volatile.Read(ref generation);};if(canceled())throw new OperationCanceledException();
                var response=MiniAiClient.Request(package,"/v1/ocr",new{image_base64=Convert.ToBase64String(bytes),mode="quality",formula=selected!=CaptureMode.Text,formula_only=selected==CaptureMode.Formula},300,canceled);
                if(selected==CaptureMode.Auto&&response.ContainsKey("formula")&&Convert.ToBoolean(response["formula"]))using(var stream=new MemoryStream(bytes))using(var bitmap=new Bitmap(stream)){
                    var refined=FormulaRegions.Refine(bitmap,MiniAiClient.ReadOcr(response),package,canceled);var records=new List<Dictionary<string,object>>();foreach(var line in refined){if(line.Kind=="uncertain")response["formula_uncertain"]=true;records.Add(new Dictionary<string,object>{{"text",line.Text},{"kind",line.Kind=="uncertain"?"text":line.Kind},{"tex",line.Tex}});}response["lines"]=records;
                }
                var result=CaptureContent.Parse(response,selected);
                if(selected==CaptureMode.Formula&&result.FormulaAvailable&&result.Parts.Count==1&&String.IsNullOrWhiteSpace(result.Parts[0].Value)){result.Parts.Add(new CapturePart{Name=UiText.Get("公式"),Value="",Formula=true});result.Selected=1;}
                Post(serial,delegate{cache[selected]=result;Adopt(result);});
            }catch(OperationCanceledException){}catch(Exception error){Post(serial,delegate{SetBusy(false);status.Text=error.Message;});}}){IsBackground=true,Name="Mini PDF capture OCR"}.Start();
        }
        void Post(int serial,Action action){if(IsDisposed||Disposing||!IsHandleCreated)return;try{BeginInvoke(new Action(delegate{if(!IsDisposed&&serial==Volatile.Read(ref generation))action();}));}catch(InvalidOperationException){}}
        void Adopt(CaptureContent value) {
            content=value;current=-1;updating=true;parts.Items.Clear();foreach(var part in value.Parts)parts.Items.Add(part);parts.Visible=value.Parts.Count>1;parts.SelectedIndex=Math.Max(0,Math.Min(value.Selected,value.Parts.Count-1));updating=false;SelectPart();SetBusy(false);
            if((CaptureMode)mode.SelectedIndex!=CaptureMode.Text&&!value.FormulaAvailable)status.Text=UiText.Get("当前功能包未提供公式识别，已保留普通 OCR 文字。");
            else if(value.Uncertain)status.Text=UiText.Get("部分公式结构不确定，可缩小选区或切换“仅公式”重试；原图已保留。");
        }
        void SelectPart(){SaveDraft();if(content==null||parts.SelectedIndex<0)return;current=parts.SelectedIndex;content.Selected=current;var part=content.Parts[current];editor.WordWrap=!part.Formula;editor.Text=part.Value;editor.Select(0,0);format.Visible=part.Formula;UpdateActions();status.Text=UiText.Get(part.Formula?"公式已转为代码，请结合原图核对符号和上下标。":content.Parts.Count>1?"正文保留文字，公式使用 LaTeX；可单独选择每个公式。":"识别完成，可修改和复制文字。");if(String.IsNullOrWhiteSpace(part.Value))status.Text=UiText.Get("未识别出内容，可切换识别方式或重新框选。");}
        void SetBusy(bool value){busy=value;progress.Visible=cancel.Visible=value;editor.ReadOnly=value;parts.Enabled=!value;UpdateActions();}
        void UpdateActions(){if(copy==null)return;copy.Enabled=save.Enabled=!busy&&editor.TextLength>0;format.Enabled=!busy;}
        void CancelRecognition(){Interlocked.Increment(ref generation);SetBusy(false);status.Text=UiText.Get("已取消识别，可切换识别方式重试。");}
        internal void CopyResult(){string text=editor.SelectionLength>0?editor.SelectedText:editor.Text;if(text.Length==0)return;bool formula=content!=null&&current>=0&&content.Parts[current].Formula;if(formula&&format.SelectedIndex==1)FormulaMarkup.CopyMathML(text);else Clipboard.SetText(formula?Formatted(text):text);status.Text=UiText.Get("已复制");}
        string Formatted(string text){string tex=FormulaMarkup.Clean(text);return format.SelectedIndex==2?"$"+tex+"$":format.SelectedIndex==3?"$$"+tex+"$$":tex;}
        void SaveResult(){bool formula=content!=null&&current>=0&&content.Parts[current].Formula;string extension=formula?(format.SelectedIndex==1?"mml":"tex"):"txt";using(var dialog=new SaveFileDialog{Filter=extension.ToUpperInvariant()+"|*."+extension,DefaultExt=extension,AddExtension=true,FileName="Capture."+extension,RestoreDirectory=true})if(dialog.ShowDialog(this)==DialogResult.OK)File.WriteAllText(dialog.FileName,formula?(format.SelectedIndex==1?FormulaMarkup.Convert(editor.Text):Formatted(editor.Text)):editor.Text,new UTF8Encoding(false));}
        protected override bool ProcessCmdKey(ref Message msg,Keys keys){if(keys==Keys.Escape){Close();return true;}if(keys==(Keys.Control|Keys.Shift|Keys.C)){try{CopyResult();}catch(Exception error){status.Text=error.Message;}return true;}return base.ProcessCmdKey(ref msg,keys);}
        protected override void Dispose(bool disposing){Interlocked.Increment(ref generation);if(disposing&&image!=null){original.Image=null;image.Dispose();image=null;}base.Dispose(disposing);}
        internal bool BusyForTest{get{return busy;}}
        internal string TextForTest{get{return editor.Text;}}
        internal void ModeForTest(CaptureMode value){mode.SelectedIndex=(int)value;}
    }
}
