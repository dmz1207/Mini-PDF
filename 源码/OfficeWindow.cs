using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed class OfficeWindow : Form {
        readonly PdfDocument document;
        readonly bool excel;
        readonly ComboBox range,layoutMode;
        readonly TextBox pages;
        readonly CheckBox option,equations;
        readonly Button mathButton;
        readonly OfficeMathSettings mathSettings=new OfficeMathSettings();
        readonly DataGridView preview;
        readonly PictureBox pagePreview;
        readonly Label info;
        RenderFrame previewFrame;
        int previewGeneration;
        internal bool PreviewReadyForTest {get{return previewFrame!=null;}}
        internal OfficeWindow(PdfDocument source,bool spreadsheet) {
            document=source;excel=spreadsheet;FormUi.Setup(this,excel?"PDF 转 Excel":"PDF 转 Word",new Size(880,600));MinimumSize=new Size(720,480);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=4};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
            var bar=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
            range=FormUi.Combo(130);range.Items.AddRange(new object[]{UiText.Get("所有页面"),UiText.Get("当前页面"),UiText.Get("页面范围")});range.SelectedIndex=0;
            pages=new TextBox{Text="1-"+source.Count,Width=130,Enabled=false};range.SelectedIndexChanged+=delegate{pages.Enabled=range.SelectedIndex==2;};
            option=new CheckBox{Text=UiText.Get(excel?"自动检测列":"包含图片"),Checked=true,AutoSize=true};
            layoutMode=FormUi.Combo(240);layoutMode.Name="WordExportMode";layoutMode.Items.AddRange(new object[]{UiText.Get("保留版式（推荐）"),UiText.Get("连续正文（便于编辑）")});layoutMode.SelectedIndex=0;
            equations=new CheckBox{Text=UiText.Get("简单公式可编辑（离线）"),AutoSize=true,Checked=false};
            mathButton=FormUi.Button("公式设置…",delegate{using(var dialog=new OfficeMathWindow(mathSettings))dialog.ShowDialog(this);});
            bar.Controls.AddRange(new Control[]{range,pages});if(!excel)bar.Controls.Add(layoutMode);bar.Controls.Add(option);
            if(!excel)bar.Controls.AddRange(new Control[]{equations,mathButton});bar.Controls.Add(FormUi.Button("预览第一页",delegate{Preview();}));root.Controls.Add(bar,0,0);
            info=new Label{AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(0,8,0,10)};root.Controls.Add(info,0,1);
            preview=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None};
            pagePreview=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(238,240,243)};
            var host=new Panel{Dock=DockStyle.Fill};host.Controls.Add(preview);host.Controls.Add(pagePreview);root.Controls.Add(host,0,2);
            var footer=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
            footer.Controls.Add(FormUi.Button("取消",delegate{Close();}));footer.Controls.Add(FormUi.Button("导出…",delegate{Export();}));root.Controls.Add(footer,0,3);
            layoutMode.SelectedIndexChanged+=delegate{UpdateOptions();};equations.CheckedChanged+=delegate{UpdateOptions();};UpdateOptions();UiText.Apply(this);Shown+=delegate{if(!excel)BeginInvoke(new Action(delegate{if(!IsDisposed&&Visible)Preview();}));};
        }
        void UpdateOptions() {
            bool layout=!excel&&layoutMode.SelectedIndex==0;
            option.Visible=!layout;equations.Enabled=!excel&&!layout;mathButton.Enabled=equations.Enabled&&equations.Checked;
            pagePreview.Visible=!excel;preview.Visible=excel;
            info.Text=UiText.Get(excel?"按位置整理为可编辑单元格，每个 PDF 页一个工作表；复杂表格请核对。无文字层的扫描页不识别。":
                layout?"保留页面位置，普通正文可编辑，公式和图形保留高清外观。下方显示原文参考预览；扫描页不识别文字。":
                "使用普通 Word 段落合并正文，可连续编辑全文。可选离线转换简单公式；公式和图片位置可能变化。扫描页不识别文字。");
        }
        int[] Pages(){return range.SelectedIndex==1?new[]{document.Index}:range.SelectedIndex==2?PdfDocument.ParsePages(pages.Text,document.Count):PrintEngine.SelectPages(document.Count,0,0,"",0,false);}
        void Preview() {
            try {
                int first=Pages()[0];UpdateOptions();
                if(!excel) {
                    int generation=++previewGeneration;RenderPool.Cancel(pagePreview,true);
                    var size=document.PageSize(first);float scale=Math.Min(1000/size.Width,1300/size.Height);
                    int width=Math.Max(1,(int)(size.Width*scale)),height=Math.Max(1,(int)(size.Height*scale));
                    if(!RenderPool.Request(pagePreview,document.GetRenderSource(),document.Password,first,width,height,0,new Rectangle(0,0,width,height),false,
                        delegate(RenderFrame frame,Exception error) {
                            if(IsDisposed||generation!=previewGeneration){if(frame!=null)frame.Dispose();return;}
                            var prior=previewFrame;previewFrame=frame;pagePreview.Image=frame==null?null:frame.Image;if(prior!=null)prior.Dispose();
                            if(error!=null)info.Text=UiText.Get("无法生成预览。");
                        }))info.Text=UiText.Get("正在等待预览…");
                    return;
                }
                using(var copy=PdfDocument.FromBytes(document.GetRenderSource().ReadAll(),document.Password)) {
                    var page=OfficeLayout.Read(copy,first,false);if(N.FPDF_GetFormType(copy.Handle)==1)OfficeLayout.FormValues(copy,page,PdfForms.Read(copy),first);
                    var rows=OfficeExport.Cells(page,option.Checked);preview.Rows.Clear();preview.Columns.Clear();int columns=OfficeLayout.Columns(page,option.Checked).Count;
                    for(int c=0;c<columns;c++)preview.Columns.Add("c"+c,OfficeExport.ColumnName(c));
                    for(int i=0;i<Math.Min(200,rows.Count);i++)preview.Rows.Add(rows[i].ToArray());
                    if(rows.Count==0)info.Text=UiText.Get("此页没有可提取文字；未启用 OCR。");
                }
            } catch(Exception e){MessageBox.Show(this,e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        void Export() {
            try{PdfDocument.Check(!document.CompatibilityPreview,UiText.Get("此文件处于兼容预览，无法转换 Office 文件。"));var selected=Pages();using(var save=new SaveFileDialog{Filter=excel?"Excel (*.xlsx)|*.xlsx":"Word (*.docx)|*.docx",DefaultExt=excel?"xlsx":"docx",AddExtension=true,RestoreDirectory=true,FileName=Path.GetFileNameWithoutExtension(String.IsNullOrEmpty(document.FilePath)?"document":document.FilePath)+(excel?".xlsx":".docx")})if(save.ShowDialog(this)==DialogResult.OK) {
                var options=new OfficeExportOptions{Excel=excel,Pages=selected,Images=option.Checked,DetectColumns=option.Checked,Layout=!excel&&layoutMode.SelectedIndex==0,Continuous=!excel&&layoutMode.SelectedIndex==1,Equations=equations.Checked,MathSettings=mathSettings};
                using(var progress=new OfficeProgress(document,options,save.FileName))if(progress.ShowDialog(this)==DialogResult.OK)DialogResult=DialogResult.OK;
            }}catch(Exception e){MessageBox.Show(this,e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        protected override void Dispose(bool disposing) {
            if(disposing&&pagePreview!=null){previewGeneration++;RenderPool.Cancel(pagePreview,true);pagePreview.Image=null;if(previewFrame!=null){previewFrame.Dispose();previewFrame=null;}}
            base.Dispose(disposing);
        }
    }
    internal sealed class OfficeMathWindow : Form {
        internal OfficeMathWindow(OfficeMathSettings settings) {
            FormUi.Setup(this,"公式设置",new Size(570,330));MinimumSize=new Size(540,330);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14),ColumnCount=2,RowCount=6};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));Controls.Add(root);
            var hint=new Label{Text=UiText.Get("参数越宽松，越可能误判。复杂或不明确的公式始终保留原貌。"),AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(0,0,0,10)};
            root.Controls.Add(hint,0,0);root.SetColumnSpan(hint,2);
            string[] names={"脚标最大字号比例","最小基线偏移比例","最大符号间距比例","分式横线最大厚度（磅）"};
            float[] defaults={.82f,.18f,.8f,.8f},current={settings.ScriptRatio,settings.BaselineOffset,settings.SymbolGap,settings.BarHeight};
            decimal[] low={.55m,.12m,.35m,.2m},high={.92m,.5m,1.2m,1.5m};var values=new NumericUpDown[4];
            for(int i=0;i<4;i++) {
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.Controls.Add(new Label{Text=UiText.Get(names[i]),AutoSize=true,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,i+1);
                values[i]=new NumericUpDown{Minimum=low[i],Maximum=high[i],DecimalPlaces=2,Increment=.01m,Value=(decimal)current[i],Dock=DockStyle.Fill};root.Controls.Add(values[i],1,i+1);
            }
            var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,12,0,0)};
            var cancel=FormUi.Button("取消",delegate{DialogResult=DialogResult.Cancel;});var apply=FormUi.Button("确定",delegate{settings.ScriptRatio=(float)values[0].Value;settings.BaselineOffset=(float)values[1].Value;settings.SymbolGap=(float)values[2].Value;settings.BarHeight=(float)values[3].Value;settings.Validate();DialogResult=DialogResult.OK;});
            buttons.Controls.Add(cancel);buttons.Controls.Add(apply);buttons.Controls.Add(FormUi.Button("恢复默认",delegate{for(int i=0;i<4;i++)values[i].Value=(decimal)defaults[i];}));
            root.Controls.Add(buttons,0,5);root.SetColumnSpan(buttons,2);AcceptButton=apply;CancelButton=cancel;UiText.Apply(this);
        }
    }
    internal sealed class OfficeProgress : Form {
        readonly PdfSource source;readonly string password,path;readonly OfficeExportOptions options;
        readonly Label status;readonly ProgressBar progress;readonly object connectionLock=new object();Process worker;ProcessBudget budget;NamedPipeServerStream pipe;volatile bool canceled;bool completed,sourceReleased;
        internal OfficeProgress(PdfDocument document,OfficeExportOptions settings,string output) {
            source=document.GetRenderSource().Retain();password=document.Password;options=settings;path=output;options.TempPath=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)),".minipdf-export-"+Guid.NewGuid().ToString("N")+".tmp");FormUi.Setup(this,"转换文档",new Size(460,150));MinimizeBox=MaximizeBox=false;
            status=new Label{Dock=DockStyle.Top,Height=40,Padding=new Padding(12),Text=UiText.Get("正在转换…")};progress=new ProgressBar{Dock=DockStyle.Top,Height=22};Controls.Add(FormUi.Button("取消",delegate{canceled=true;Stop();Close();}));Controls[0].Dock=DockStyle.Bottom;Controls.Add(progress);Controls.Add(status);
            Shown+=delegate{new Thread(Work){IsBackground=true,Name="MiniPDF Office export"}.Start();};FormClosing+=delegate{if(!completed)canceled=true;Stop();};UiText.Apply(this);
        }
        void Post(Action action){try{if(!IsDisposed&&IsHandleCreated)BeginInvoke(action);}catch(InvalidOperationException){}}
        void Work() {
            try {
                string name="MiniPDF-office-"+Guid.NewGuid().ToString("N");var security=new PipeSecurity();using(var identity=WindowsIdentity.GetCurrent())security.AddAccessRule(new PipeAccessRule(identity.User,PipeAccessRights.FullControl,AccessControlType.Allow));
                var connection=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security);pipe=connection;
                var ready=connection.BeginWaitForConnection(null,null);
                try {
                    if(canceled)return;
                    lock(connectionLock){if(canceled)throw new OperationCanceledException();worker=Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniPDF.Core.exe"),"--office-worker "+name){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});budget=new ProcessBudget(worker);}
                    if(!ready.AsyncWaitHandle.WaitOne(15000))throw new TimeoutException(UiText.Get("转换进程启动超时。"));connection.EndWaitForConnection(ready);
                }finally{ready.AsyncWaitHandle.Close();}
                using(var writer=new BinaryWriter(connection,Encoding.UTF8,true))using(var reader=new BinaryReader(connection,Encoding.UTF8,true)) {
                    writer.Write(source.Name);writer.Write(source.Length);writer.Write(password);writer.Write(path);writer.Write(options.TempPath);writer.Write(options.Excel);writer.Write(options.Images);writer.Write(options.DetectColumns);writer.Write(options.Layout);writer.Write(options.Continuous);writer.Write(options.Equations);writer.Write(options.MathSettings.ScriptRatio);writer.Write(options.MathSettings.BaselineOffset);writer.Write(options.MathSettings.SymbolGap);writer.Write(options.MathSettings.BarHeight);writer.Write(options.Pages.Length);foreach(int page in options.Pages)writer.Write(page);writer.Flush();
                    while(!canceled) {
                        int code=reader.ReadInt32();if(code==0){int done=reader.ReadInt32(),total=reader.ReadInt32();Post(delegate{progress.Maximum=Math.Max(1,total);progress.Value=Math.Min(done,total);status.Text=UiText.Get("正在转换…")+" "+done+" / "+total;});}
                        else if(code==1){int skipped=reader.ReadInt32();completed=true;Post(delegate{if(skipped>0)MessageBox.Show(this,UiText.Get("部分图片超过导出限制或无法读取：")+skipped,Text);DialogResult=DialogResult.OK;});return;}
                        else throw new IOException(reader.ReadString());
                    }
                }
            }catch(Exception e){if(!canceled)Post(delegate{MessageBox.Show(this,e.Message,UiText.Get("转换未完成"),MessageBoxButtons.OK,MessageBoxIcon.Warning);Close();});}
            finally{Stop();}
        }
        void Stop(){lock(connectionLock){var process=worker;worker=null;if(process!=null){try{if(!process.HasExited)process.Kill();process.WaitForExit(500);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}finally{process.Dispose();}}var connection=pipe;pipe=null;if(connection!=null)connection.Dispose();if(budget!=null){budget.Dispose();budget=null;}try{if(File.Exists(options.TempPath))File.Delete(options.TempPath);}catch(IOException){}catch(UnauthorizedAccessException){}}}
        protected override void Dispose(bool disposing){if(disposing){canceled=true;Stop();if(!sourceReleased){sourceReleased=true;source.Dispose();}}base.Dispose(disposing);}
    }
    internal static class OfficeWorker {
        internal static int Run(string name) {
            using(var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut)) {
                pipe.Connect(15000);using(var reader=new BinaryReader(pipe,Encoding.UTF8,true))using(var writer=new BinaryWriter(pipe,Encoding.UTF8,true)) {
                    try{string mapping=reader.ReadString();long length=reader.ReadInt64();string password=reader.ReadString(),path=reader.ReadString(),temp=reader.ReadString();var options=new OfficeExportOptions{TempPath=temp,Excel=reader.ReadBoolean(),Images=reader.ReadBoolean(),DetectColumns=reader.ReadBoolean(),Layout=reader.ReadBoolean(),Continuous=reader.ReadBoolean(),Equations=reader.ReadBoolean(),MathSettings=new OfficeMathSettings{ScriptRatio=reader.ReadSingle(),BaselineOffset=reader.ReadSingle(),SymbolGap=reader.ReadSingle(),BarHeight=reader.ReadSingle()}};options.MathSettings.Validate();int count=reader.ReadInt32();PdfDocument.Check(count>0&&count<=2000,"Invalid export page count.");options.Pages=new int[count];for(int i=0;i<count;i++)options.Pages[i]=reader.ReadInt32();
                        using(var source=PdfSource.OpenShared(mapping,length))using(var document=PdfDocument.FromBytes(source.ReadAll(),password)) {
                            int skipped=OfficeExport.Save(document,options,path,delegate(int done,int total){writer.Write(0);writer.Write(done);writer.Write(total);writer.Flush();});writer.Write(1);writer.Write(skipped);writer.Flush();return 0;
                        }
                    }catch(Exception e){writer.Write(-1);writer.Write(e.Message);writer.Flush();return 1;}
                }
            }
        }
    }
    internal sealed partial class MainWindow {
        void ConvertOffice(bool excel){if(Document==null||Document.CompatibilityPreview)return;using(var dialog=new OfficeWindow(Document,excel))dialog.ShowDialog(this);}
    }
}
