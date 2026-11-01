using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed class OfflineWindow : Form {
        readonly MainWindow owner;
        readonly PdfDocument document;
        readonly TabControl tabs;
        readonly Label pack,status;
        readonly ComboBox range,direction;
        readonly TextBox pages,recognized,input,translated;
        readonly CheckBox skipText;
        readonly Button ocrButton,translateButton,applyButton,cancelButton;
        readonly List<OcrLine> lines=new List<OcrLine>();
        PdfJob job;
        int generation,epoch,skipped;
        bool busy;
        internal OfflineWindow(MainWindow window,bool translate,string selection) {
            owner=window;document=window.Document;FormUi.Setup(this,"离线 OCR 与翻译",new Size(900,670));MinimumSize=new Size(720,520);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=4};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
            var header=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};header.Controls.Add(FormUi.Button("选择 MiniAI EXE…",delegate{LoadPack(this);}));pack=new Label{AutoSize=true,Margin=new Padding(3,8,0,6),MaximumSize=new Size(600,0)};header.Controls.Add(pack);root.Controls.Add(header,0,0);
            tabs=new TabControl{Dock=DockStyle.Fill,Font=Font};var ocrTab=new TabPage(UiText.Get("离线 OCR")){Padding=new Padding(8)};var translateTab=new TabPage(UiText.Get("中英互译")){Padding=new Padding(8)};tabs.TabPages.AddRange(new[]{ocrTab,translateTab});root.Controls.Add(tabs,0,1);
            var ocrLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};ocrLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));ocrLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));ocrLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));ocrTab.Controls.Add(ocrLayout);
            var options=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};range=FormUi.Combo(140,"当前页面","所有页面","页面范围");pages=new TextBox{Width=120,Text=document==null?"1":(document.Index+1).ToString(),Enabled=false};range.SelectedIndexChanged+=delegate{pages.Enabled=!busy&&range.SelectedIndex==2;};skipText=FormUi.Check("跳过已有文字层的页面",true);options.Controls.AddRange(new Control[]{range,pages,skipText});ocrButton=FormUi.Button("开始识别",delegate{StartOcr();});options.Controls.Add(ocrButton);options.Controls.Add(FormUi.Button("识别并翻译",delegate{if(!busy){skipText.Checked=false;StartOcr(true);}}));ocrLayout.Controls.Add(options,0,0);
            recognized=TextArea(true);recognized.Name="OcrText";ocrLayout.Controls.Add(recognized,0,1);var ocrFooter=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};applyButton=FormUi.Button("添加可搜索文字层",delegate{ApplyOcr();});applyButton.Enabled=false;ocrFooter.Controls.Add(applyButton);ocrFooter.Controls.Add(FormUi.Button("复制文字",delegate{CopyText(recognized.Text);}));ocrFooter.Controls.Add(FormUi.Button("发送到翻译",delegate{input.Text=recognized.Text;tabs.SelectedIndex=1;}));ocrLayout.Controls.Add(ocrFooter,0,2);
            var translateLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};translateLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));translateLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));translateTab.Controls.Add(translateLayout);
            var translateOptions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};direction=FormUi.Combo(170);direction.Items.AddRange(MiniAiClient.LanguageLabels);direction.SelectedIndex=ContainsChinese(selection)?1:0;translateButton=FormUi.Button("开始翻译",delegate{StartTranslation();});translateOptions.Controls.Add(direction);translateOptions.Controls.Add(translateButton);translateOptions.Controls.Add(FormUi.Button("复制译文",delegate{CopyText(translated.Text);}));translateLayout.Controls.Add(translateOptions,0,0);
            var textPanels=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,Size=new Size(820,400),SplitterDistance=185,Panel1MinSize=70,Panel2MinSize=70,SplitterWidth=5};input=TextArea(false);input.Name="TranslationSource";input.Text=selection??"";translated=TextArea(true);translated.Name="TranslationResult";textPanels.Panel1.Controls.Add(input);textPanels.Panel2.Controls.Add(translated);translateLayout.Controls.Add(textPanels,0,1);
            status=new Label{AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(2,8,2,8),MaximumSize=new Size(820,0)};root.Controls.Add(status,0,2);var footer=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};footer.Controls.Add(FormUi.Button("关闭",delegate{Close();}));cancelButton=FormUi.Button("取消任务",delegate{Cancel();});cancelButton.Enabled=false;footer.Controls.Add(cancelButton);root.Controls.Add(footer,0,3);
            tabs.SelectedIndex=translate?1:0;tabs.SelectedIndexChanged+=delegate{RefreshPack();};RefreshPack();UiText.Apply(this);
            root.SizeChanged+=delegate{status.MaximumSize=new Size(Math.Max(180,root.ClientSize.Width-root.Padding.Horizontal-8),0);};
            Shown+=delegate{textPanels.SplitterDistance=Math.Max(70,(textPanels.ClientSize.Height-textPanels.SplitterWidth)/2);};
        }
        TextBox TextArea(bool readOnly){return new TextBox{Dock=DockStyle.Fill,Font=Font,Multiline=true,ReadOnly=readOnly,ScrollBars=ScrollBars.Vertical,AcceptsReturn=true,WordWrap=true,BorderStyle=BorderStyle.FixedSingle,MaxLength=100000};}
        static bool ContainsChinese(string text){if(text==null)return false;int chinese=0,latin=0;foreach(char c in text){if(c>=0x4e00&&c<=0x9fff)chinese++;else if(Char.IsLetter(c))latin++;}return chinese>latin/3&&chinese>0;}
        static void CopyText(string text){if(!String.IsNullOrEmpty(text))Clipboard.SetText(text);}
        void RefreshPack(){pack.Text=MiniAiClient.Available?"MiniAI · PP-OCRv6 medium · Hy-MT2 1.25-bit":UiText.Get("请选择 MiniAI-Portable.exe（完整离线包）");ocrButton.Enabled=!busy&&document!=null&&MiniAiClient.Available;translateButton.Enabled=!busy&&MiniAiClient.Available;if(!busy)status.Text=UiText.Get("可直接选择完整 EXE，首次使用时自动准备。识别和翻译均在本机完成。");}
        internal static bool LoadPack(IWin32Window parent) {
            using(var dialog=new OpenFileDialog{Title=UiText.Get("选择 MiniAI-Portable.exe（完整离线包）"),Filter=UiText.Get("MiniAI 可执行程序|*.exe"),InitialDirectory=MiniAiClient.Home,RestoreDirectory=true,CheckFileExists=true,Multiselect=false}) {
                if(dialog.ShowDialog(parent)!=DialogResult.OK)return false;
                try{MiniAiClient.SelectPackage(dialog.FileName);}catch(Exception error){MessageBox.Show(parent,error.Message,"MiniAI",MessageBoxButtons.OK,MessageBoxIcon.Information);return false;}
                var window=parent as OfflineWindow;if(window!=null)window.RefreshPack();return true;
            }
        }
        void SetBusy(bool value){busy=value;cancelButton.Enabled=value;ocrButton.Enabled=!value&&document!=null&&MiniAiClient.Available;translateButton.Enabled=!value&&MiniAiClient.Available;range.Enabled=skipText.Enabled=direction.Enabled=!value;input.ReadOnly=value;pages.Enabled=!value&&range.SelectedIndex==2;applyButton.Enabled=!value&&lines.Count>0&&document!=null&&document.CanEdit;}
        void Cancel(){generation++;if(job!=null){job.Dispose();job=null;}SetBusy(false);status.Text=UiText.Get("任务已取消。");}
        void StartOcr(bool translateAfter=false) {
            if(document==null||busy)return;
            try {
                string root=MiniAiClient.PackRoot;PdfDocument.Check(MiniAiClient.AvailableAt(root),UiText.Get("请先加载 MiniAI 离线功能包。"));int[] selected=range.SelectedIndex==0?new[]{document.Index}:range.SelectedIndex==1?PrintEngine.SelectPages(document.Count,document.Index,0,"",0,false):PdfDocument.ParsePages(pages.Text,document.Count);PdfDocument.Check(selected.Length<=2000,"OCR supports up to 2000 pages per task.");
                Cancel();lines.Clear();recognized.Clear();skipped=0;epoch=document.RenderEpoch;SetBusy(true);int current=generation;bool skip=skipText.Checked;var worker=new PdfJob(this,document);job=worker;status.Text=UiText.Get("正在识别…");
                worker.Start(delegate(BinaryWriter writer){MiniAiClient.Ensure(root);writer.Write("ocr");writer.Write(root);writer.Write(skip);writer.Write(selected.Length);foreach(int page in selected)writer.Write(page);},delegate(BinaryReader reader) {
                    while(true){int code=reader.ReadInt32();if(code<0)throw new IOException(reader.ReadString());if(code==1)return;if(code==0){int done=reader.ReadInt32(),total=reader.ReadInt32(),page=reader.ReadInt32();worker.Post(delegate{if(current==generation)status.Text=UiText.Get("正在识别…")+" "+done+" / "+total+" · "+UiText.Get("页")+" "+(page+1);});}else if(code==3){reader.ReadInt32();worker.Post(delegate{if(current==generation)skipped++;});}else if(code==4){int count=reader.ReadInt32();PdfDocument.Check(count>=0&&count<=5000,"Invalid OCR result count.");var batch=new List<OcrLine>();for(int i=0;i<count;i++)batch.Add(OcrLine.Read(reader));worker.Post(delegate{if(current!=generation)return;foreach(var line in batch){lines.Add(line);recognized.AppendText(line.Text+Environment.NewLine);}});}else throw new IOException("Invalid OCR result.");}
                },delegate(Exception error){if(current!=generation)return;job=null;SetBusy(false);status.Text=error==null?String.Format(UiText.Get("识别完成：{0} 行；跳过 {1} 页已有文字。"),lines.Count,skipped):error.Message;if(error==null&&translateAfter){if(lines.Count==0){status.Text=UiText.Get("未识别出可翻译的文字，请检查页面或扫描清晰度。");return;}input.Text=recognized.Text;direction.SelectedIndex=ContainsChinese(input.Text)?1:0;tabs.SelectedIndex=1;StartTranslation();}},300);
            }catch(Exception error){SetBusy(false);status.Text=error.Message;}
        }
        void ApplyOcr() {
            try{PdfDocument.Check(!busy&&lines.Count>0&&document==owner.Document&&document.RenderEpoch==epoch,UiText.Get("文档已更改，请重新识别后再应用。"));owner.Run(delegate{document.AddOcrLayer(lines);});if(document.RenderEpoch!=epoch){applyButton.Enabled=false;lines.Clear();status.Text=UiText.Get("已添加文字层；请保存 PDF。页面外观保持原样。");}}catch(Exception error){status.Text=error.Message;}
        }
        void StartTranslation() {
            if(busy)return;
            if(String.IsNullOrWhiteSpace(input.Text)){if(document==null){status.Text=UiText.Get("请输入要翻译的文字。");return;}range.SelectedIndex=0;skipText.Checked=false;StartOcr(true);return;}
            try {
                string root=MiniAiClient.PackRoot;PdfDocument.Check(MiniAiClient.AvailableAt(root),UiText.Get("请先加载 MiniAI 离线功能包。"));string text=input.Text,target=MiniAiClient.LanguageCodes[direction.SelectedIndex];Cancel();translated.Clear();SetBusy(true);int current=generation;var worker=new PdfJob(this,null);job=worker;status.Text=UiText.Get("正在加载翻译模型…");
                worker.Start(delegate(BinaryWriter writer){MiniAiClient.Ensure(root);writer.Write("translate");writer.Write(root);writer.Write(target);writer.Write(text);},delegate(BinaryReader reader) {
                    while(true){int code=reader.ReadInt32();if(code<0)throw new IOException(reader.ReadString());if(code==1)return;if(code==0){int done=reader.ReadInt32(),total=reader.ReadInt32();worker.Post(delegate{if(current==generation)status.Text=UiText.Get("正在翻译…")+" "+done+" / "+total;});}else if(code==4){string result=reader.ReadString();worker.Post(delegate{if(current==generation){if(translated.TextLength>0)translated.AppendText(Environment.NewLine+Environment.NewLine);translated.AppendText(result);}});}else throw new IOException("Invalid translation result.");}
                },delegate(Exception error){if(current!=generation)return;job=null;SetBusy(false);status.Text=error==null?UiText.Get("翻译完成。"):error.Message;},700);
            }catch(Exception error){SetBusy(false);status.Text=error.Message;}
        }
        protected override void Dispose(bool disposing){if(disposing&&job!=null){job.Dispose();job=null;}base.Dispose(disposing);}
        internal void OcrForTest(){StartOcr();}
        internal bool BusyForTest {get{return busy;}}
        internal List<OcrLine> OcrLinesForTest {get{return lines;}}
        internal string StatusForTest {get{return status.Text;}}
        internal void ApplyForTest(){ApplyOcr();}
        internal void TranslateForTest(){StartTranslation();}
        internal string TranslationForTest {get{return translated.Text;}}
        internal void OcrTranslateForTest(){skipText.Checked=false;StartOcr(true);}
    }
    internal sealed partial class MainWindow {
        bool EnsureMiniAiPackage(){if(MiniAiClient.Available)return true;MessageBox.Show(this,UiText.Get("将 MiniAI-Portable.exe 放在阅读器旁边，或直接选择完整 EXE。无需预先解包。"),"MiniAI",MessageBoxButtons.OK,MessageBoxIcon.Information);return OfflineWindow.LoadPack(this);}
        void ShowOcr(){if(!EnsureMiniAiPackage())return;using(var dialog=new OfflineWindow(this,false,""))dialog.ShowDialog(this);}
        void ShowTranslation(){if(!EnsureMiniAiPackage())return;string text=Document==null?"":canvas.CopiedText();if(String.IsNullOrWhiteSpace(text)&&Document!=null)text=Document.SelectedText(0,Math.Min(100000,N.FPDFText_CountChars(Document.TextPage)));using(var dialog=new OfflineWindow(this,true,text))dialog.ShowDialog(this);}
        void LoadMiniAi(){if(OfflineWindow.LoadPack(this))RefreshAiAvailability();}
    }
}
