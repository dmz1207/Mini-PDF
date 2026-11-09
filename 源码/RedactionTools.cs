using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed class RedactionPlan {
        internal int Identity,Epoch;
        internal readonly List<RedactionRegion> Regions=new List<RedactionRegion>();
    }
    internal sealed partial class MainWindow {
        ToolStripButton applyRedactionButton,undoRedactionButton,clearRedactionButton,redactionQuick;
        internal bool HasRedactions {get{return activeTab!=null&&activeTab.Redactions!=null&&activeTab.Redactions.Regions.Count>0;}}
        bool RedactionsValid {get{return HasRedactions&&activeTab.Redactions.Identity==Document.Identity&&activeTab.Redactions.Epoch==Document.RenderEpoch;}}
        void InitializeRedactionTools(ToolStripMenuItem tools){
            Add(tools,UiText.Get("永久涂黑：标记区域"),Keys.None,BeginRedaction);Add(tools,UiText.Get("应用永久涂黑…"),Keys.Control|Keys.Shift|Keys.R,delegate{ApplyRedactions();});
            redactionQuick=Button(toolbar,UiText.Get("涂黑"),BeginRedaction,UiText.Get("框选敏感区域，再应用永久涂黑并另存副本"),null);redactionQuick.Overflow=ToolStripItemOverflow.Never;
            ToolButton(Tool.Redact,UiText.Get("永久涂黑"),UiText.Get("框选敏感区域，再应用永久涂黑并另存副本"));
            applyRedactionButton=Button(editbar,UiText.Get("应用永久涂黑…"),delegate{ApplyRedactions();},UiText.Get("生成脱敏副本，可选保留文字层或高清图像型"),null);
            applyRedactionButton.Overflow=ToolStripItemOverflow.Never;
            undoRedactionButton=Button(editbar,UiText.Get("撤销标记"),UndoRedaction,UiText.Get("撤销上一个尚未应用的涂黑区域"),null);
            clearRedactionButton=Button(editbar,UiText.Get("清除标记"),ClearRedactions,UiText.Get("清除本标签全部待应用涂黑标记"),null);UpdateRedactionControls();
        }
        void BeginRedaction(){if(Document==null)return;if(!Document.CanPrint||Document.CompatibilityPreview){Report(UiText.Get("此文档不能导出脱敏副本。"));return;}canvas.ClearSelection();SetTool(Tool.Redact);}
        internal void AddRedaction(N.Rect bounds){
            if(Document==null||!Document.CanPrint)return;
            if(HasRedactions&&!RedactionsValid){MessageBox.Show(this,UiText.Get("页面内容已变化，请清除旧标记并重新框选。"));return;}
            if(activeTab.Redactions==null)activeTab.Redactions=new RedactionPlan{Identity=Document.Identity,Epoch=Document.RenderEpoch};
            if(activeTab.Redactions.Regions.Count>=RedactionExport.MaxRegions)return;
            activeTab.Redactions.Regions.Add(new RedactionRegion{Page=Document.Index,Bounds=bounds});UpdateRedactionControls();UpdateTabStrip();canvas.Invalidate();
        }
        void UpdateRedactionControls(){
            if(applyRedactionButton==null)return;bool pending=HasRedactions;applyRedactionButton.Visible=undoRedactionButton.Visible=clearRedactionButton.Visible=pending;
            applyRedactionButton.Text=UiText.Get(RedactionsValid?"应用永久涂黑":"标记已失效")+(pending?" ("+activeTab.Redactions.Regions.Count+")":"");applyRedactionButton.Enabled=RedactionsValid;
            applyRedactionButton.BackColor=pending?Color.FromArgb(255,235,222):Color.White;
        }
        void UndoRedaction(){if(!HasRedactions)return;var marks=activeTab.Redactions.Regions;marks.RemoveAt(marks.Count-1);if(marks.Count==0)activeTab.Redactions=null;UpdateRedactionControls();UpdateTabStrip();canvas.Invalidate();}
        void ClearRedactions(){if(activeTab==null)return;activeTab.Redactions=null;UpdateRedactionControls();UpdateTabStrip();canvas.Invalidate();}
        internal void DrawRedactions(Graphics graphics){
            if(!HasRedactions)return;
            using(var brush=new SolidBrush(Color.FromArgb(150,0,0,0)))using(var pen=new Pen(RedactionsValid?Color.FromArgb(220,65,45):Color.Gray,2)){
                foreach(var mark in activeTab.Redactions.Regions){var rect=canvas.SelectionRectangle(mark.Page,mark.Bounds);if(!rect.IntersectsWith(canvas.ClientRectangle))continue;graphics.FillRectangle(brush,rect);pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;graphics.DrawRectangle(pen,rect);}
            }
        }
        bool ApplyRedactions(bool openResult=true){
            if(!HasRedactions){Report(UiText.Get("请先标记需要永久涂黑的区域。"));return false;}
            if(!RedactionsValid){MessageBox.Show(this,UiText.Get("页面内容已变化，请清除旧标记并重新框选。"));return false;}
            var tab=activeTab;
            using(var dialog=new RedactionWindow(Document,tab.Redactions.Regions.ToArray(),delegate(string path){return CanSaveToPath(path,true);})){if(dialog.ShowDialog(this)!=DialogResult.OK)return false;tab.Redactions=null;UpdateRedactionControls();UpdateTabStrip();canvas.Invalidate();if(openResult)OpenDocument(dialog.OutputPath);return true;}
        }
        bool ConfirmRedactionClose(){
            if(!HasRedactions)return true;
            var answer=MessageBox.Show(this,UiText.Get("有尚未应用的永久涂黑标记。是否先生成脱敏副本？\n选择“否”将放弃这些标记。"),UiText.Get("待应用的涂黑标记"),MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question,MessageBoxDefaultButton.Button3);
            if(answer==DialogResult.Cancel)return false;if(answer==DialogResult.Yes)return ApplyRedactions(false);ClearRedactions();return true;
        }
        internal void RedactionToolForTest(){BeginRedaction();}
        internal int RedactionCountForTest {get{return HasRedactions?activeTab.Redactions.Regions.Count:0;}}
        internal void UndoRedactionForTest(){UndoRedaction();}
        internal void ClearRedactionsForTest(){ClearRedactions();}
        internal RedactionRegion[] RedactionRegionsForTest {get{return HasRedactions?activeTab.Redactions.Regions.ToArray():new RedactionRegion[0];}}
        internal bool RedactionMarksValidForTest {get{return RedactionsValid;}}
    }
    internal sealed class RedactionWindow : Form {
        readonly PdfDocument document;
        readonly RedactionRegion[] regions;
        readonly Func<string,bool> canSave;
        readonly ComboBox mode,dpi;
        readonly Label detail;
        internal string OutputPath;
        internal RedactionWindow(PdfDocument source,RedactionRegion[] marks,Func<string,bool> validatePath){
            document=source;regions=marks;canSave=validatePath;FormUi.Setup(this,"应用永久涂黑",new Size(620,340));MinimumSize=new Size(500,320);
            var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=5,Padding=new Padding(16)};grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<3;i++)grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(grid);
            var summary=new Label{Text=String.Format(UiText.Get("待涂黑：{0} 处；副本共 {1} 页。"),marks.Length,source.Count),AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,14)};grid.Controls.Add(summary,0,0);grid.SetColumnSpan(summary,2);
            grid.Controls.Add(new Label{Text=UiText.Get("副本类型"),AutoSize=true,Anchor=AnchorStyles.Left},0,1);mode=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};mode.Items.AddRange(new object[]{UiText.Get("高清图像型：清除全部文字层"),UiText.Get("保留区域外文字层：可搜索、复制")});mode.SelectedIndex=0;grid.Controls.Add(mode,1,1);
            grid.Controls.Add(new Label{Text=UiText.Get("输出精度"),AutoSize=true,Anchor=AnchorStyles.Left},0,2);dpi=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};dpi.Items.AddRange(new object[]{"300 dpi","600 dpi"});dpi.SelectedIndex=0;grid.Controls.Add(dpi,1,2);
            detail=new Label{Dock=DockStyle.Fill,AutoSize=false,Padding=new Padding(0,12,0,0)};grid.Controls.Add(detail,0,3);grid.SetColumnSpan(detail,2);mode.SelectedIndexChanged+=delegate{Details();};Details();
            var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=FormUi.Button("取消",delegate{DialogResult=DialogResult.Cancel;});var save=FormUi.Button("生成脱敏副本…",delegate{SaveCopy();});buttons.Controls.Add(cancel);buttons.Controls.Add(save);grid.Controls.Add(buttons,0,4);grid.SetColumnSpan(buttons,2);AcceptButton=save;CancelButton=cancel;
        }
        void Details(){detail.Text=UiText.Get("将重建全部页面并擦除所选区域，不携带原页面对象、附件、批注数据、签名或历史版本。原文件保留。")+Environment.NewLine+Environment.NewLine+UiText.Get(mode.SelectedIndex==0?"图像型副本保留页面外观，全文不能直接选字或编辑。":"按原文字坐标重建涂黑区域以外的文字层，用于搜索和复制；外观仍由高清图像保留。文字坐标异常时请选择图像型。");}
        void SaveCopy(){
            using(var save=new SaveFileDialog{Filter=UiText.Get("PDF 文档 (*.pdf)|*.pdf"),Title=UiText.Get("保存脱敏副本"),DefaultExt="pdf",AddExtension=true,RestoreDirectory=true,FileName=(String.IsNullOrEmpty(document.FilePath)?"Mini PDF":Path.GetFileNameWithoutExtension(document.FilePath))+UiText.Get("_脱敏.pdf")}){
                if(!String.IsNullOrEmpty(document.FilePath))save.InitialDirectory=Path.GetDirectoryName(document.FilePath);
                if(save.ShowDialog(this)!=DialogResult.OK)return;
                if(!canSave(save.FileName)){MessageBox.Show(this,UiText.Get("请使用新文件名，不要覆盖已打开的原文档。"));return;}
                using(var progress=new RedactionProgress(document,regions,dpi.SelectedIndex==0?300:600,mode.SelectedIndex==1,save.FileName)){
                    var result=progress.ShowDialog(this);if(result==DialogResult.OK){OutputPath=save.FileName;DialogResult=DialogResult.OK;}else if(result==DialogResult.Abort)MessageBox.Show(this,progress.Error,UiText.Get("脱敏副本未完成"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
                }
            }
        }
    }
    internal sealed class RedactionProgress : Form {
        readonly PdfDocument document;
        readonly RedactionRegion[] regions;
        readonly int dpi;
        readonly bool keepText;
        readonly string target,temp;
        readonly Label status;
        readonly ProgressBar progress;
        PdfJob job;
        bool received,committed;
        internal string Error;
        internal RedactionProgress(PdfDocument source,RedactionRegion[] marks,int resolution,bool keep,string path){
            document=source;regions=marks;dpi=resolution;keepText=keep;target=Path.GetFullPath(path);temp=Path.Combine(Path.GetDirectoryName(target),".minipdf-redacted-"+Guid.NewGuid().ToString("N")+".tmp");FormUi.Setup(this,"正在生成脱敏副本",new Size(500,170));FormBorderStyle=FormBorderStyle.FixedDialog;MinimizeBox=MaximizeBox=false;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(16)};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);status=new Label{Text=UiText.Get("正在准备…"),AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,12)};progress=new ProgressBar{Dock=DockStyle.Top,Height=22,Maximum=Math.Max(1,source.Count)};var cancel=FormUi.Button("取消",delegate{DialogResult=DialogResult.Cancel;});layout.Controls.Add(status,0,0);layout.Controls.Add(progress,0,1);layout.Controls.Add(cancel,0,2);CancelButton=cancel;Shown+=delegate{Start();};
        }
        void Start(){
            try{PdfDocument.Check(String.IsNullOrEmpty(document.FilePath)||!String.Equals(Path.GetFullPath(document.FilePath),target,StringComparison.OrdinalIgnoreCase),UiText.Get("请使用新文件名，不要覆盖已打开的原文档。"));RedactionExport.Validate(document,regions,dpi);job=new PdfJob(this,document);var task=job;int total=document.Count;
                task.Start(delegate(BinaryWriter writer){writer.Write("redact");writer.Write(dpi);writer.Write(keepText);writer.Write(regions.Length);foreach(var region in regions){writer.Write(region.Page);writer.Write(region.Bounds.left);writer.Write(region.Bounds.bottom);writer.Write(region.Bounds.right);writer.Write(region.Bounds.top);}},delegate(BinaryReader reader){
                    try{using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var pdf=new RedactionPdfWriter(file,total,keepText)){
                        int pages=0;while(true){int code=reader.ReadInt32();if(code<0)throw new IOException(reader.ReadString());if(code==0){int done=reader.ReadInt32(),all=reader.ReadInt32();PdfDocument.Check(done>=0&&done<=total&&all==total,"Invalid redaction progress.");task.Post(delegate{progress.Value=Math.Min(done,progress.Maximum);status.Text=String.Format(UiText.Get("正在涂黑… {0} / {1}"),Math.Min(done+1,total),total);});continue;}
                            if(code==2){PdfDocument.Check(reader.ReadInt32()==total&&pages==total,"Incomplete redaction response.");pdf.Complete();file.Flush(true);received=true;break;}
                            PdfDocument.Check(code==1&&reader.ReadInt32()==pages,"Invalid redaction page order.");var page=new RedactionPage{Width=reader.ReadInt32(),Height=reader.ReadInt32(),Size=new SizeF(reader.ReadSingle(),reader.ReadSingle())};int glyphs=reader.ReadInt32();PdfDocument.Check(glyphs>=0&&glyphs<=RedactionExport.MaxGlyphs&&(keepText||glyphs==0),"Invalid redaction glyph count.");for(int i=0;i<glyphs;i++)page.Glyphs.Add(new RedactionGlyph{Unicode=reader.ReadInt32(),X=reader.ReadSingle(),Y=reader.ReadSingle(),A=reader.ReadSingle(),B=reader.ReadSingle(),C=reader.ReadSingle(),D=reader.ReadSingle()});page.Length=reader.ReadInt32();pdf.Page(page,delegate(Stream output){var buffer=new byte[65536];int left=page.Length;while(left>0){int read=reader.Read(buffer,0,Math.Min(left,buffer.Length));if(read==0)throw new EndOfStreamException();output.Write(buffer,0,read);left-=read;}});pages++;
                        }
                    }}catch{DeleteTemp();throw;}
                },delegate(Exception error){
                    try{if(error!=null)throw error;PdfDocument.Check(received,"Incomplete redacted document.");using(var verify=PdfDocument.Open(temp,"")){PdfDocument.Check(verify.Count==total&&N.FPDF_GetFormType(verify.Handle)==0,"Redacted PDF verification failed.");if(!keepText)PdfDocument.Check(N.FPDFText_CountChars(verify.TextPage)==0,"Unexpected text layer in image-only output.");}if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);committed=true;DialogResult=DialogResult.OK;}
                    catch(Exception failure){Error=failure.Message;DeleteTemp();DialogResult=DialogResult.Abort;}
                },120);
            }catch(Exception error){Error=error.Message;DialogResult=DialogResult.Abort;}
        }
        void DeleteTemp(){try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
        protected override void Dispose(bool disposing){if(disposing){if(job!=null){job.Dispose();job=null;}if(!committed)DeleteTemp();}base.Dispose(disposing);}
    }
}
