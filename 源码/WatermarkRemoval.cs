using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace MiniPdf {
    internal static partial class N {
        [DllImport(D)]internal static extern uint FPDFImageObj_GetImageDataRaw(IntPtr obj,byte[] buffer,uint length);
        [DllImport(D)]internal static extern int FPDFFormObj_CountObjects(IntPtr obj);
        [DllImport(D)]internal static extern IntPtr FPDFFormObj_GetObject(IntPtr obj,uint index);
    }
    internal sealed class WatermarkTarget {
        internal int Page,Index,Type;
        internal bool Annotation,Tagged,Suspected;
        internal string Text,Key;
        internal N.Rect Bounds;
        internal float PageWidth,PageHeight;
        internal bool SameSlot(WatermarkTarget other){return Page==other.Page&&Index==other.Index&&Annotation==other.Annotation;}
        internal bool Matches(WatermarkTarget other){if(Tagged&&other.Tagged)return true;if(Key==null||Key!=other.Key||Type!=other.Type||Annotation!=other.Annotation)return false;return Math.Abs(Bounds.left/PageWidth-other.Bounds.left/other.PageWidth)<.025&&Math.Abs(Bounds.bottom/PageHeight-other.Bounds.bottom/other.PageHeight)<.025&&Math.Abs((Bounds.right-Bounds.left)/PageWidth-(other.Bounds.right-other.Bounds.left)/other.PageWidth)<.025&&Math.Abs((Bounds.top-Bounds.bottom)/PageHeight-(other.Bounds.top-other.Bounds.bottom)/other.PageHeight)<.025;}
        public override string ToString(){string kind=Annotation?UiText.Get("图章批注"):Type==1?UiText.Get("文字"):Type==3?UiText.Get("图片"):Type==5?UiText.Get("组合对象"):UiText.Get("图形");return (Index+1)+" · "+kind+(Tagged?" · Mini PDF":"")+(String.IsNullOrWhiteSpace(Text)?"":" · "+Text.Replace('\r',' ').Replace('\n',' '));}
    }
    internal sealed partial class PdfDocument {
        string WatermarkKey(IntPtr obj,int depth,ref int budget){if(obj==IntPtr.Zero||depth>12||--budget<0)return null;int type=N.FPDFPageObj_GetType(obj);if(type==1){float size;N.FPDFTextObj_GetFontSize(obj,out size);uint r,g,b,a;N.FPDFPageObj_GetFillColor(obj,out r,out g,out b,out a);N.Matrix matrix;N.FPDFPageObj_GetMatrix(obj,out matrix);return "T:"+ObjectText(obj).Trim()+":"+size.ToString("0.0",CultureInfo.InvariantCulture)+":"+r+","+g+","+b+","+a+":"+Math.Round(Math.Atan2(matrix.b,matrix.a)*180/Math.PI);}
            if(type==3){uint size=N.FPDFImageObj_GetImageDataRaw(obj,null,0);if(size==0||size>8*1024*1024)return null;var bytes=new byte[size];if(N.FPDFImageObj_GetImageDataRaw(obj,bytes,size)!=size)return null;using(var hash=SHA256.Create())return "I:"+Convert.ToBase64String(hash.ComputeHash(bytes));}
            if(type==5){int count=N.FPDFFormObj_CountObjects(obj);if(count<1||count>10000)return null;var text=new StringBuilder("F:");for(uint i=0;i<count;i++){string key=WatermarkKey(N.FPDFFormObj_GetObject(obj,i),depth+1,ref budget);if(key==null)return null;text.Append(key).Append('\n');}using(var hash=SHA256.Create())return "F:"+Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));}return null;
        }
        internal List<WatermarkTarget> WatermarkCandidates(int page){int prior=Index;try{OpenPage(page);var result=new List<WatermarkTarget>();for(int i=0;i<N.FPDFPage_CountObjects(Page);i++){IntPtr obj=N.FPDFPage_GetObject(Page,i);int type=N.FPDFPageObj_GetType(obj);var bounds=ObjectBounds(obj);bool tagged=HasMark(obj,WatermarkTag);uint r,g,b,a;bool faded=N.FPDFPageObj_GetFillColor(obj,out r,out g,out b,out a)!=0&&a>0&&a<245;float size=0;if(type==1)N.FPDFTextObj_GetFontSize(obj,out size);N.Matrix m;N.FPDFPageObj_GetMatrix(obj,out m);string text=type==1?ObjectText(obj):"";bool suspect=tagged||type==3||type==5||faded||size>=30||Math.Abs(m.b)>.05||Math.Abs(m.c)>.05||text.IndexOf("watermark",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("confidential",StringComparison.OrdinalIgnoreCase)>=0||text.Contains("水印");int budget=10000;result.Add(new WatermarkTarget{Page=page,Index=i,Type=type,Text=text.Length>180?text.Substring(0,180)+"…":text,Key=WatermarkKey(obj,0,ref budget),Bounds=bounds,Tagged=tagged,Suspected=suspect,PageWidth=(float)Width,PageHeight=(float)Height});}
                for(int i=0;i<N.FPDFPage_GetAnnotCount(Page);i++){IntPtr a=N.FPDFPage_GetAnnot(Page,i);try{int type=N.FPDFAnnot_GetSubtype(a);if(type!=13&&type!=24)continue;N.Rect bounds;if(N.FPDFAnnot_GetRect(a,out bounds)==0)continue;string text=AnnotText(a);result.Add(new WatermarkTarget{Page=page,Index=i,Type=type,Text=text,Key=String.IsNullOrWhiteSpace(text)?null:"A:"+type+":"+text,Bounds=bounds,Annotation=true,Suspected=true,PageWidth=(float)Width,PageHeight=(float)Height});}finally{if(a!=IntPtr.Zero)N.FPDFPage_CloseAnnot(a);}}return result;
            }finally{OpenPage(prior);}}
        internal List<WatermarkTarget> MatchWatermarks(IList<WatermarkTarget> seeds,int[] pages){var result=new List<WatermarkTarget>();foreach(int page in pages)foreach(var item in WatermarkCandidates(page))if(seeds.Any(s=>s.SameSlot(item)||s.Matches(item)))result.Add(item);return result;}
        internal void RemoveWatermarkTargets(IList<WatermarkTarget> targets){Check(targets!=null&&targets.Count>0,UiText.Get("请先勾选要移除的对象。"));var unique=targets.GroupBy(t=>t.Page+":"+t.Annotation+":"+t.Index).Select(g=>g.First()).ToList();int prior=Index;Edit(delegate{foreach(var group in unique.GroupBy(t=>t.Page)){OpenPage(group.Key);foreach(var item in group.Where(t=>!t.Annotation).OrderByDescending(t=>t.Index)){Check(item.Index>=0&&item.Index<N.FPDFPage_CountObjects(Page),UiText.Get("对象已失效，请重新选择。"));IntPtr obj=N.FPDFPage_GetObject(Page,item.Index);Check(N.FPDFPage_RemoveObject(Page,obj)!=0,UiText.Get("无法删除原文字。"));N.FPDFPageObj_Destroy(obj);}foreach(var item in group.Where(t=>t.Annotation).OrderByDescending(t=>t.Index))Check(N.FPDFPage_RemoveAnnot(Page,item.Index)!=0,UiText.Get("无法删除批注。"));Check(N.FPDFPage_GenerateContent(Page)!=0,UiText.Get("无法生成页面内容。"));}OpenPage(prior);});}
    }
    internal sealed class WatermarkRemovalWindow : Form {
        readonly PdfDocument snapshot;
        readonly CheckedListBox objects=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true,IntegralHeight=false,HorizontalScrollbar=true,BorderStyle=BorderStyle.None};
        readonly PictureBox preview=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(238,241,245)};
        readonly NumericUpDown page;
        readonly TextBox range,filter;
        readonly CheckBox showAll,after;
        readonly Label status;
        readonly Button apply;
        readonly Timer delay=new Timer{Interval=140};
        internal readonly List<WatermarkTarget> Targets=new List<WatermarkTarget>();
        List<WatermarkTarget> candidates;
        bool loading;
        internal WatermarkRemovalWindow(PdfDocument document){snapshot=PdfDocument.FromBytes(document.Serialize(false),document.Password);snapshot.OpenPage(document.Index);FormUi.Setup(this,"移除水印",new Size(1020,650));MinimumSize=new Size(820,600);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=2,Padding=new Padding(10)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
            page=FormUi.Number(document.Index+1,1,document.Count,72,0);page.Name="watermarkPage";showAll=FormUi.Check("显示全部页面对象",false);after=FormUi.Check("预览移除后",true);var top=FormUi.Row(FormUi.Label("预览页码"),page,showAll,after);top.WrapContents=true;top.Dock=DockStyle.Fill;root.Controls.Add(top,0,0);root.SetColumnSpan(top,2);
            var left=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};left.RowStyles.Add(new RowStyle(SizeType.AutoSize));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.AutoSize));filter=new TextBox{Width=260,BorderStyle=BorderStyle.FixedSingle};left.Controls.Add(FormUi.Row(FormUi.Label("筛选文字"),filter),0,0);left.Controls.Add(objects,0,1);var note=FormUi.Label("勾选要移除的独立文字、图片或图章。\n组合对象会整体移除，请核对预览。\n扫描图像像素内的水印无法作为对象删除。");note.MaximumSize=new Size(600,0);left.Controls.Add(note,0,2);root.Controls.Add(left,0,1);root.Controls.Add(preview,1,1);
            range=new TextBox{Width=180,Text="1-"+document.Count,BorderStyle=BorderStyle.FixedSingle};status=FormUi.Label("");apply=FormUi.Button("移除勾选对象",delegate{DialogResult=DialogResult.OK;});apply.Name="removeSelected";var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};footer.Controls.AddRange(new Control[]{FormUi.Label("匹配页码范围"),range,FormUi.Button("匹配相同水印",Match),FormUi.Button("选择 Mini PDF 水印",SelectTagged),FormUi.Button("清空选择",delegate{Targets.Clear();FillList();}),status,apply,FormUi.Button("取消",delegate{DialogResult=DialogResult.Cancel;})});root.Controls.Add(footer,0,2);root.SetColumnSpan(footer,2);CancelButton=(IButtonControl)footer.Controls[footer.Controls.Count-1];
            page.ValueChanged+=delegate{LoadPage();};showAll.CheckedChanged+=delegate{FillList();};filter.TextChanged+=delegate{FillList();};after.CheckedChanged+=delegate{RenderPreview();};objects.ItemCheck+=delegate(object sender,ItemCheckEventArgs e){if(loading)return;var target=(WatermarkTarget)objects.Items[e.Index];Targets.RemoveAll(t=>t.SameSlot(target));if(e.NewValue==CheckState.Checked)Targets.Add(target);delay.Stop();delay.Start();};delay.Tick+=delegate{delay.Stop();UpdateCount();RenderPreview();};Shown+=delegate{objects.Font=Font;objects.ItemHeight=Math.Max(24,objects.Font.Height+6);LoadPage();};
        }
        void LoadPage(){snapshot.OpenPage((int)page.Value-1);candidates=snapshot.WatermarkCandidates(snapshot.Index);FillList();}
        void FillList(){loading=true;try{objects.BeginUpdate();objects.Items.Clear();if(candidates!=null)foreach(var item in candidates)if((showAll.Checked||item.Suspected)&&(String.IsNullOrEmpty(filter.Text)||item.ToString().IndexOf(filter.Text,StringComparison.CurrentCultureIgnoreCase)>=0))objects.Items.Add(item,Targets.Any(t=>t.SameSlot(item)));}finally{objects.EndUpdate();loading=false;}UpdateCount();RenderPreview();}
        void UpdateCount(){status.Text=String.Format(UiText.Get("已选 {0} 个对象 / {1} 页"),Targets.Count,Targets.Select(t=>t.Page).Distinct().Count());apply.Enabled=Targets.Count>0;}
        int[] Pages(){return PdfDocument.ParsePages(range.Text,snapshot.Count);}
        void Match(object sender,EventArgs e){try{PdfDocument.Check(Targets.Count>0,UiText.Get("请先勾选要移除的对象。"));UseWaitCursor=true;var matches=snapshot.MatchWatermarks(Targets.ToArray(),Pages());Targets.Clear();Targets.AddRange(matches);FillList();}catch(Exception ex){status.Text=ex.Message;}finally{UseWaitCursor=false;}}
        void SelectTagged(object sender,EventArgs e){try{UseWaitCursor=true;Targets.Clear();foreach(int at in Pages())Targets.AddRange(snapshot.WatermarkCandidates(at).Where(t=>t.Tagged));FillList();}catch(Exception ex){status.Text=ex.Message;}finally{UseWaitCursor=false;}}
        void RenderPreview(){if(!Visible)return;try{using(var copy=snapshot.CopyPage(snapshot.Index)){var selected=Targets.Where(t=>t.Page==snapshot.Index).Select(t=>new WatermarkTarget{Page=0,Index=t.Index,Annotation=t.Annotation}).ToList();if(after.Checked&&selected.Count>0)copy.RemoveWatermarkTargets(selected);var size=copy.PageSize(0);float scale=Math.Min(850/size.Width,1050/size.Height);var image=copy.Render(0,Math.Max(1,(int)(size.Width*scale)),Math.Max(1,(int)(size.Height*scale)));if(!after.Checked)using(var g=Graphics.FromImage(image))using(var pen=new Pen(Color.FromArgb(28,130,210),2))foreach(var target in Targets.Where(t=>t.Page==snapshot.Index)){var a=copy.ToDevice(target.Bounds.left,target.Bounds.top,image.Width,image.Height,0);var b=copy.ToDevice(target.Bounds.right,target.Bounds.bottom,image.Width,image.Height,0);g.DrawRectangle(pen,Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(1,Math.Abs(a.X-b.X)),Math.Max(1,Math.Abs(a.Y-b.Y)));}var old=preview.Image;preview.Image=image;if(old!=null)old.Dispose();}}catch(Exception ex){status.Text=ex.Message;}}
        protected override void Dispose(bool disposing){if(disposing){delay.Dispose();if(preview.Image!=null)preview.Image.Dispose();snapshot.Dispose();}base.Dispose(disposing);}
    }
}
