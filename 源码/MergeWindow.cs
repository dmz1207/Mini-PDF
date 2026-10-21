using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed class MergePage {
        internal PdfDocument Source;
        internal string Name;
        internal int Index,Rotation;
    }
    internal sealed class MergeGroup {
        internal string Name;
        internal bool Expanded;
        internal readonly List<MergePage> Pages=new List<MergePage>();
    }
    internal sealed class MergePlan : IDisposable {
        internal readonly List<MergeGroup> Groups=new List<MergeGroup>();
        readonly List<PdfDocument> sources=new List<PdfDocument>();
        readonly List<List<MergeGroup>> undo=new List<List<MergeGroup>>(),redo=new List<List<MergeGroup>>();
        internal int Count {get{int count=0;foreach(var g in Groups)count+=g.Pages.Count;return count;}}
        internal bool CanUndo {get{return undo.Count>0;}}internal bool CanRedo {get{return redo.Count>0;}}
        List<MergeGroup> Snapshot(){var copy=new List<MergeGroup>();foreach(var group in Groups){var g=new MergeGroup{Name=group.Name,Expanded=group.Expanded};foreach(var p in group.Pages)g.Pages.Add(new MergePage{Source=p.Source,Name=p.Name,Index=p.Index,Rotation=p.Rotation});copy.Add(g);}return copy;}
        internal void Change(Action action){undo.Add(Snapshot());if(undo.Count>20)undo.RemoveAt(0);redo.Clear();action();}
        internal void Undo(){if(!CanUndo)return;redo.Add(Snapshot());var state=undo[undo.Count-1];undo.RemoveAt(undo.Count-1);Groups.Clear();Groups.AddRange(state);}
        internal void Redo(){if(!CanRedo)return;undo.Add(Snapshot());var state=redo[redo.Count-1];redo.RemoveAt(redo.Count-1);Groups.Clear();Groups.AddRange(state);}
        internal void Add(PdfDocument document,string name){PdfDocument.Check(document.CanEdit&&document.SignatureCount==0,UiText.Get("合并需要未签名且允许编辑的 PDF。"));PdfDocument.Check((long)Count+document.Count<=10000,UiText.Get("合并最多支持 10000 页。"));Change(delegate{sources.Add(document);var group=new MergeGroup{Name=name};for(int i=0;i<document.Count;i++)group.Pages.Add(new MergePage{Source=document,Name=name,Index=i});Groups.Add(group);});}
        internal MergeGroup Owner(MergePage page){foreach(var g in Groups)if(g.Pages.Contains(page))return g;return null;}
        internal void MovePage(MergePage page,MergeGroup group,int index){var old=Owner(page);if(old==null||!Groups.Contains(group))return;index=Math.Max(0,Math.Min(group.Pages.Count,index));int before=old.Pages.IndexOf(page);if(old==group&&(index==before||index==before+1))return;Change(delegate{old.Pages.Remove(page);if(old==group&&before<index)index--;group.Pages.Insert(index,page);group.Expanded=true;});}
        internal void MovePages(List<MergePage> pages,MergeGroup group,int index){if(pages.Count==0||!Groups.Contains(group))return;index=Math.Max(0,Math.Min(group.Pages.Count,index));int earlier=0;for(int i=0;i<index;i++)if(pages.Contains(group.Pages[i]))earlier++;Change(delegate{foreach(var p in pages){var owner=Owner(p);if(owner!=null)owner.Pages.Remove(p);}group.Pages.InsertRange(index-earlier,pages);group.Expanded=true;});}
        internal void MoveGroup(MergeGroup group,int index){int old=Groups.IndexOf(group);if(old<0)return;index=Math.Max(0,Math.Min(Groups.Count,index));if(index==old||index==old+1)return;Change(delegate{Groups.RemoveAt(old);if(old<index)index--;Groups.Insert(index,group);});}
        internal List<MergePage> AllPages(){var pages=new List<MergePage>();foreach(var group in Groups)pages.AddRange(group.Pages);return pages;}
        internal PdfDocument Build(){PdfDocument.Check(Count>0,UiText.Get("请至少添加一页。"));var pages=new List<MergePage>();foreach(var group in Groups)pages.AddRange(group.Pages);return PdfDocument.MergePages(pages);}
        public void Dispose(){foreach(var d in sources)d.Dispose();sources.Clear();Groups.Clear();undo.Clear();redo.Clear();}
    }
    internal sealed class MergeWindow : Form {
        internal readonly MergePlan Plan=new MergePlan();
        readonly MergeBoard board;
        readonly Label status;
        internal PdfDocument BaseSource;
        internal MergeWindow(PdfDocument initial,bool organize=false){FormUi.Setup(this,organize?"管理页面":"合并文件",new Size(1000,700));MinimumSize=new Size(700,460);var toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=50,Padding=new Padding(10,6,4,4)};Controls.Add(toolbar);
            toolbar.Controls.Add(FormUi.Button("添加文件…",delegate{Choose();}));toolbar.Controls.Add(FormUi.Button("展开全部",delegate{foreach(var g in Plan.Groups)g.Expanded=true;board.Rebuild();}));toolbar.Controls.Add(FormUi.Button("收起全部",delegate{foreach(var g in Plan.Groups)g.Expanded=false;board.Rebuild();}));toolbar.Controls.Add(FormUi.Button("复制页面",delegate{board.Duplicate();}));toolbar.Controls.Add(FormUi.Button("旋转",delegate{board.Rotate();}));toolbar.Controls.Add(FormUi.Button("移除",delegate{board.Delete();}));toolbar.Controls.Add(FormUi.Button("撤销",delegate{Plan.Undo();board.ResetSelection();}));toolbar.Controls.Add(FormUi.Button("重做",delegate{Plan.Redo();board.ResetSelection();}));
            var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=54,Padding=new Padding(10,7,4,4)};status=new Label{AutoSize=true,Margin=new Padding(8,7,15,0)};footer.Controls.Add(status);var zoom=new TrackBar{Minimum=90,Maximum=180,Value=120,TickStyle=TickStyle.None,Width=120,Height=34};zoom.ValueChanged+=delegate{board.CardWidth=zoom.Value;board.Rebuild();};footer.Controls.Add(zoom);footer.Controls.Add(FormUi.Button(organize?"应用页面调整":"合并为新文档",delegate{if(Plan.Count>0)DialogResult=DialogResult.OK;else MessageBox.Show(this,UiText.Get("请至少保留一页。"),Text);}));footer.Controls.Add(FormUi.Button("取消",delegate{DialogResult=DialogResult.Cancel;}));Controls.Add(footer);CancelButton=(IButtonControl)footer.Controls[footer.Controls.Count-1];
            board=new MergeBoard(Plan){Dock=DockStyle.Fill};board.Changed+=delegate{status.Text=String.Format(UiText.Get("{0} 个文件组 · {1} 页"),Plan.Groups.Count,Plan.Count);};Controls.Add(board);board.BringToFront();
            AllowDrop=true;DragEnter+=delegate(object s,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};DragDrop+=delegate(object s,DragEventArgs e){var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null)AddFiles(files);};
            if(initial!=null&&initial.CanEdit&&initial.SignatureCount==0){var copy=PdfDocument.FromBytes(initial.Serialize(false),initial.Password);BaseSource=copy;try{Plan.Add(copy,String.IsNullOrEmpty(initial.FilePath)?UiText.Get("当前文档"):Path.GetFileName(initial.FilePath));}catch{copy.Dispose();throw;}if(organize)Plan.Groups[0].Expanded=true;}board.Rebuild();
        }
        void Choose(){using(var dialog=new OpenFileDialog{Filter=UiText.Get("PDF 和图片 (*.pdf;*.png;*.jpg;*.jpeg;*.bmp)|*.pdf;*.png;*.jpg;*.jpeg;*.bmp"),Multiselect=true,RestoreDirectory=true})if(dialog.ShowDialog(this)==DialogResult.OK)AddFiles(dialog.FileNames);}
        void AddFiles(string[] paths){foreach(string path in paths){PdfDocument document=null;try{if(String.Equals(Path.GetExtension(path),".pdf",StringComparison.OrdinalIgnoreCase)){string password="";while(true)try{document=PdfDocument.Open(path,password);break;}catch(PdfException e){if(e.Code!=4)throw;password=Dialogs.Ask(this,UiText.Get("打开受保护的 PDF"),Path.GetFileName(path)+UiText.Get(" 的密码："),"",true);if(password==null)break;}}else {string ext=Path.GetExtension(path).ToLowerInvariant();PdfDocument.Check(ext==".png"||ext==".jpg"||ext==".jpeg"||ext==".bmp",UiText.Get("仅支持 PDF、PNG、JPEG 和 BMP 文件。"));document=PdfDocument.New();using(var image=Image.FromFile(path)){float scale=Math.Min(((float)document.Width-36)/image.Width,((float)document.Height-36)/image.Height);float w=image.Width*scale,h=image.Height*scale;document.AddImage(path,new N.Rect(((float)document.Width-w)/2,((float)document.Height-h)/2,((float)document.Width+w)/2,((float)document.Height+h)/2));}}
                    if(document!=null){Plan.Add(document,Path.GetFileName(path));document=null;}
                }catch(Exception e){MessageBox.Show(this,Path.GetFileName(path)+Environment.NewLine+e.Message,UiText.Get("添加文件未完成"),MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{if(document!=null)document.Dispose();}}
            board.Rebuild();}
        internal PdfDocument Build(){return Plan.Build();}
        protected override void Dispose(bool disposing){if(disposing)Plan.Dispose();base.Dispose(disposing);}
        internal sealed class MergeBoard : ScrollableControl {
            sealed class Card {internal MergeGroup Group;internal MergePage Page;internal Rectangle Bounds;}
            readonly MergePlan plan;
            readonly List<Card> cards=new List<Card>();
            readonly Dictionary<string,Bitmap> thumbnails=new Dictionary<string,Bitmap>();
            readonly Queue<string> thumbOrder=new Queue<string>();
            readonly HashSet<MergePage> selectedPages=new HashSet<MergePage>();
            readonly Timer scroll=new Timer{Interval=60};
            Card selected,dragged,target;
            Point down,last;
            internal int CardWidth=120;
            internal event EventHandler Changed;
            internal MergeBoard(MergePlan data){plan=data;DoubleBuffered=true;AutoScroll=true;BackColor=Color.FromArgb(245,247,250);TabStop=true;scroll.Tick+=delegate{if(dragged==null)return;int delta=last.Y<28?-28:last.Y>ClientSize.Height-28?28:0;if(delta!=0){AutoScrollPosition=new Point(0,Math.Max(0,-AutoScrollPosition.Y+delta));HitTarget(last);Invalidate();}};}
            internal void Rebuild(){cards.Clear();int gap=12,w=CardWidth+24,h=CardWidth*4/3+72,columns=Math.Max(1,(ClientSize.Width-SystemInformation.VerticalScrollBarWidth-gap)/(w+gap)),index=0;foreach(var group in plan.Groups){cards.Add(new Card{Group=group,Bounds=new Rectangle(gap+(index%columns)*(w+gap),gap+(index/columns)*(h+gap),w,h)});index++;if(group.Expanded)foreach(var page in group.Pages){cards.Add(new Card{Group=group,Page=page,Bounds=new Rectangle(gap+(index%columns)*(w+gap),gap+(index/columns)*(h+gap),w,h)});index++;}}AutoScrollMinSize=new Size(0,gap+(index+columns-1)/columns*(h+gap));Invalidate();if(Changed!=null)Changed(this,EventArgs.Empty);}
            internal void ResetSelection(){selected=dragged=target=null;selectedPages.Clear();Rebuild();}
            protected override void OnResize(EventArgs e){base.OnResize(e);if(plan!=null)Rebuild();}
            Card Hit(Point p){p.Offset(-AutoScrollPosition.X,-AutoScrollPosition.Y);foreach(var card in cards)if(card.Bounds.Contains(p))return card;return null;}
            Bitmap Thumb(MergePage page){string key=page.Source.Handle.ToInt64()+":"+page.Index+":"+page.Rotation;Bitmap image;if(thumbnails.TryGetValue(key,out image))return image;var size=page.Source.PageSize(page.Index);if((page.Rotation&1)!=0)size=new SizeF(size.Height,size.Width);float scale=Math.Min(180/size.Width,220/size.Height);image=page.Source.Render(page.Index,Math.Max(1,(int)(size.Width*scale)),Math.Max(1,(int)(size.Height*scale)),page.Rotation);thumbnails.Add(key,image);thumbOrder.Enqueue(key);while(thumbnails.Count>64){string oldest=thumbOrder.Dequeue();thumbnails[oldest].Dispose();thumbnails.Remove(oldest);}return image;}
            protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(cards.Count==0){TextRenderer.DrawText(e.Graphics,UiText.Get("将 PDF 或图片拖到这里\n展开文件组后，可拖动页面调整顺序"),Font,ClientRectangle,Color.DimGray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}e.Graphics.TranslateTransform(AutoScrollPosition.X,AutoScrollPosition.Y);foreach(var card in cards){var r=card.Bounds;if(r.Bottom+AutoScrollPosition.Y<0||r.Top+AutoScrollPosition.Y>Height)continue;bool active=selected!=null&&(selected.Page!=null?card.Page!=null&&selectedPages.Contains(card.Page):card.Page==null&&card.Group==selected.Group);using(var brush=new SolidBrush(active?Color.FromArgb(220,237,251):Color.White))e.Graphics.FillRectangle(brush,r);using(var pen=new Pen(active?Color.FromArgb(35,131,207):Color.FromArgb(220,225,232),active?2:1))e.Graphics.DrawRectangle(pen,r);
                    Rectangle imageBox=new Rectangle(r.X+12,r.Y+30,r.Width-24,r.Height-58);var page=card.Page??(card.Group.Pages.Count>0?card.Group.Pages[0]:null);if(page!=null)try{var image=Thumb(page);float scale=Math.Min((float)imageBox.Width/image.Width,(float)imageBox.Height/image.Height);var dest=new Rectangle(imageBox.X+(imageBox.Width-(int)(image.Width*scale))/2,imageBox.Y+(imageBox.Height-(int)(image.Height*scale))/2,(int)(image.Width*scale),(int)(image.Height*scale));if(card.Page==null&&!card.Group.Expanded){e.Graphics.FillRectangle(Brushes.LightGray,dest.X+4,dest.Y-4,dest.Width,dest.Height);e.Graphics.FillRectangle(Brushes.WhiteSmoke,dest.X+2,dest.Y-2,dest.Width,dest.Height);}e.Graphics.DrawImage(image,dest);}catch{}
                    string title=card.Page==null?(card.Group.Expanded?"− ":"+ ")+card.Group.Name:card.Page.Name;string label=card.Page==null?String.Format(UiText.Get("{0} 页"),card.Group.Pages.Count):UiText.Get("页面")+" "+(card.Page.Index+1)+(card.Page.Rotation==0?"":" ↻");TextRenderer.DrawText(e.Graphics,title,Font,new Rectangle(r.X+6,r.Y+5,r.Width-12,28),Color.FromArgb(35,57,78),TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);TextRenderer.DrawText(e.Graphics,label,Font,new Rectangle(r.X+6,r.Bottom-31,r.Width-12,22),Color.DimGray,TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);if(dragged!=null&&target==card)using(var pen=new Pen(Color.FromArgb(28,132,217),4))e.Graphics.DrawLine(pen,r.Left-5,r.Top,r.Left-5,r.Bottom);}}
            protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();var hit=Hit(e.Location);if(hit!=null&&hit.Page!=null){if((ModifierKeys&Keys.Control)!=0){if(!selectedPages.Add(hit.Page))selectedPages.Remove(hit.Page);}else if((ModifierKeys&Keys.Shift)!=0&&selected!=null){int a=Math.Max(0,cards.FindIndex(delegate(Card c){return selected.Page==null?c.Page==null&&c.Group==selected.Group:c.Page==selected.Page;})),b=cards.IndexOf(hit);for(int i=Math.Min(a,b);i<=Math.Max(a,b);i++)if(cards[i].Page!=null)selectedPages.Add(cards[i].Page);}else if(!selectedPages.Contains(hit.Page)){selectedPages.Clear();selectedPages.Add(hit.Page);}}else selectedPages.Clear();selected=hit;down=last=e.Location;if(selected!=null)Capture=true;Invalidate();}
            protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);last=e.Location;if(!Capture||selected==null)return;if(dragged==null&&(Math.Abs(e.X-down.X)>SystemInformation.DragSize.Width/2||Math.Abs(e.Y-down.Y)>SystemInformation.DragSize.Height/2)){dragged=selected;Cursor=Cursors.SizeAll;scroll.Start();}if(dragged!=null){HitTarget(e.Location);Invalidate();}}
            void HitTarget(Point p){target=Hit(p);if(target==null&&cards.Count>0&&p.Y>=cards[cards.Count-1].Bounds.Top+AutoScrollPosition.Y)target=cards[cards.Count-1];}
            protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button!=MouseButtons.Left)return;Card source=dragged,destination=target;bool after=destination!=null&&e.X>destination.Bounds.Left+AutoScrollPosition.X+destination.Bounds.Width/2;dragged=target=null;scroll.Stop();Capture=false;Cursor=Cursors.Default;
                if(source!=null&&destination!=null&&source!=destination){if(source.Page==null)plan.MoveGroup(source.Group,plan.Groups.IndexOf(destination.Group)+(after?1:0));else{int index=destination.Page==null?destination.Group.Pages.Count:destination.Group.Pages.IndexOf(destination.Page)+(after?1:0);plan.MovePages(Selection(),destination.Group,index);}selected=null;Rebuild();}
                else if(source==null&&selected!=null&&selected.Page==null&&Math.Abs(e.Location.X-down.X)<4&&Math.Abs(e.Location.Y-down.Y)<4){selected.Group.Expanded=!selected.Group.Expanded;Rebuild();}Invalidate();}
            protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){dragged=target=null;scroll.Stop();Cursor=Cursors.Default;Invalidate();}}
            internal List<MergePage> Selection(){var pages=new List<MergePage>();foreach(var p in plan.AllPages())if(selected!=null&&(selected.Page==null?selected.Group.Pages.Contains(p):selectedPages.Contains(p)))pages.Add(p);return pages;}
            internal void Rotate(){if(selected==null)return;var pages=Selection();plan.Change(delegate{foreach(var page in pages)page.Rotation=(page.Rotation+1)%4;});Rebuild();}
            internal void Duplicate(){var pages=Selection();if(pages.Count==0)return;if(plan.Count+pages.Count>10000)return;plan.Change(delegate{foreach(var p in pages){var group=plan.Owner(p);group.Pages.Insert(group.Pages.IndexOf(p)+1,new MergePage{Source=p.Source,Name=p.Name,Index=p.Index,Rotation=p.Rotation});group.Expanded=true;}});ResetSelection();}
            internal void Delete(){if(selected==null)return;plan.Change(delegate{if(selected.Page!=null){foreach(var p in Selection())plan.Owner(p).Pages.Remove(p);}else plan.Groups.Remove(selected.Group);});ResetSelection();}
            protected override bool ProcessCmdKey(ref Message msg,Keys keys){if(keys==Keys.Delete){Delete();return true;}if(keys==(Keys.Control|Keys.Z)){plan.Undo();ResetSelection();return true;}if(keys==(Keys.Control|Keys.Y)){plan.Redo();ResetSelection();return true;}if(keys==Keys.Escape&&Capture){dragged=target=null;Capture=false;return true;}return base.ProcessCmdKey(ref msg,keys);}
            protected override void Dispose(bool disposing){if(disposing){scroll.Dispose();foreach(var image in thumbnails.Values)image.Dispose();thumbnails.Clear();}base.Dispose(disposing);}
        }
    }
    internal sealed partial class PdfDocument {
        internal static PdfDocument MergePages(IList<MergePage> pages){var output=new PdfDocument();try{output.Handle=N.FPDF_CreateNewDocument();Check(output.Handle!=IntPtr.Zero,UiText.Get("无法创建 PDF。"));int at=0;foreach(var page in pages){Check(N.FPDF_ImportPagesByIndex(output.Handle,page.Source.Handle,new[]{page.Index},1,at)!=0,UiText.Get("无法合并页面。"));IntPtr handle=N.FPDF_LoadPage(output.Handle,at);Check(handle!=IntPtr.Zero,UiText.Get("无法读取页面。"));try{N.FPDFPage_SetRotation(handle,(N.FPDFPage_GetRotation(handle)+page.Rotation)%4);}finally{N.FPDF_ClosePage(handle);}at++;}output.CanEdit=output.CanPrint=true;output.revision=output.clock=1;output.OpenPage(0);return output;}catch{output.Dispose();throw;}}
    }
}
