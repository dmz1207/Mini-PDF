using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiniPdf {
    internal enum Tool { Read, Object, Text, Image, Highlight, Ink, Rectangle, Note, Underline, Strike }
    internal enum PageLayout { Single, Double, Book, Grid }
    internal sealed class PdfCanvas : ScrollableControl {
        readonly MainWindow owner;
        readonly Dictionary<int,Bitmap> images=new Dictionary<int,Bitmap>();
        readonly Queue<int> imageOrder=new Queue<int>();
        long cacheBytes;
        Rectangle[] pageRects=new Rectangle[0];
        public PageLayout LayoutMode=PageLayout.Single;
        public bool Continuous=true;
        public int ViewRotation;
        public int ColumnCount {get;private set;}
        public Tool Mode=Tool.Read;
        public float Zoom=1;
        public int Fit=2;
        public Pick Selected;
        public List<N.Rect> Markers=new List<N.Rect>();
        public int TextStart=-1,TextCount;
        int pixelWidth,pixelHeight;
        bool down,resizing,panning;
        Point origin,current,scrollOrigin;
        N.Pt startPoint;
        int startChar=-1;
        readonly List<N.Pt> stroke=new List<N.Pt>();
        readonly List<Point> screenStroke=new List<Point>();
        bool rebuilding;
        Size lastViewport;
        PdfDocument Doc {get{return owner.Document;}}
        public PdfCanvas(MainWindow parent){owner=parent;AutoScroll=true;BackColor=Color.FromArgb(234,237,241);TabStop=true;DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);}
        public Rectangle PageRectangle {get{return Doc==null||Doc.Index>=pageRects.Length?Rectangle.Empty:ScreenRect(pageRects[Doc.Index]);}}
        Rectangle ScreenRect(Rectangle rect){rect.Offset(AutoScrollPosition);return rect;}
        void ClearImages(){foreach(var image in images.Values)image.Dispose();images.Clear();imageOrder.Clear();cacheBytes=0;}
        Bitmap ImageFor(int index){Bitmap image;if(images.TryGetValue(index,out image))return image;var r=pageRects[index];double rasterScale=Math.Min(1,Math.Sqrt(24000000.0/((double)r.Width*r.Height)));rasterScale=Math.Min(rasterScale,14000.0/Math.Max(r.Width,r.Height));int w=Math.Max(1,(int)(r.Width*rasterScale)),h=Math.Max(1,(int)(r.Height*rasterScale));image=Doc.Render(index,w,h,ViewRotation);images.Add(index,image);imageOrder.Enqueue(index);cacheBytes+=(long)w*h*4;while(images.Count>1&&(images.Count>48||cacheBytes>64L*1024*1024)){int oldest=imageOrder.Dequeue();var prior=images[oldest];cacheBytes-=(long)prior.Width*prior.Height*4;prior.Dispose();images.Remove(oldest);}return image;}
        void SyncPageSize(){if(Doc!=null&&Doc.Index<pageRects.Length){pixelWidth=pageRects[Doc.Index].Width;pixelHeight=pageRects[Doc.Index].Height;}}
        internal int HitPage(Point p){for(int i=0;i<pageRects.Length;i++)if(!pageRects[i].IsEmpty&&ScreenRect(pageRects[i]).Contains(p))return i;return -1;}
        internal Rectangle[] LayoutRectangles {get{return pageRects;}}
        internal void UseLayout(PageLayout mode){LayoutMode=mode;if(mode==PageLayout.Grid){Continuous=true;Fit=0;Zoom=0.22f;}else Fit=2;ClearSelection();Rebuild(true);}
        internal int PageTurnTarget(int direction){if(Doc==null)return 0;if(LayoutMode==PageLayout.Single||LayoutMode==PageLayout.Grid)return Doc.Index+direction;if(LayoutMode==PageLayout.Double)return Doc.Index/2*2+direction*2;int row=Doc.Index==0?0:(Doc.Index+1)/2;int next=row+direction;return next<0?-1:next==0?0:next*2-1;}
        public void ClearSelection(){Selected=null;TextStart=-1;TextCount=0;Markers.Clear();down=false;Invalidate();}
        public void Rebuild(bool resetScroll) {
            if(rebuilding||IsDisposed)return;rebuilding=true;
            try{
                bool hasAnchor=!resetScroll&&Doc!=null&&Doc.Index<pageRects.Length&&!pageRects[Doc.Index].IsEmpty;
                double anchorX=0,anchorY=0;if(hasAnchor){var prior=pageRects[Doc.Index];var viewport=lastViewport.IsEmpty?ClientSize:lastViewport;anchorX=(-AutoScrollPosition.X+viewport.Width/2.0-prior.X)/prior.Width;anchorY=(-AutoScrollPosition.Y+viewport.Height/2.0-prior.Y)/prior.Height;}
                ClearImages();
                if(Doc==null){pageRects=new Rectangle[0];AutoScrollMinSize=Size.Empty;Invalidate();return;}
                if(ClientSize.Width<100||ClientSize.Height<100)return;
                float dpi;using(var g=CreateGraphics())dpi=g.DpiX;
                var sizes=new SizeF[Doc.Count];float maxWidth=1,maxHeight=1;for(int i=0;i<sizes.Length;i++){sizes[i]=Doc.PageSize(i);if(ViewRotation%2!=0)sizes[i]=new SizeF(sizes[i].Height,sizes[i].Width);maxWidth=Math.Max(maxWidth,sizes[i].Width);maxHeight=Math.Max(maxHeight,sizes[i].Height);}
                bool coverAlone=LayoutMode==PageLayout.Book&&!Continuous&&Doc.Index==0;
                int fixedColumns=LayoutMode==PageLayout.Single||coverAlone?1:LayoutMode==PageLayout.Grid?3:2;const int gap=24,margin=24;int rowGap=LayoutMode==PageLayout.Grid?34:24;
                if(Fit==1)Zoom=(float)((ClientSize.Width-64-gap*(fixedColumns-1))/(maxWidth*fixedColumns*dpi/72));
                if(Fit==2)Zoom=(float)Math.Min((ClientSize.Width-64-gap*(fixedColumns-1))/(maxWidth*fixedColumns*dpi/72),(ClientSize.Height-48)/(maxHeight*dpi/72));
                Zoom=Math.Max(0.04f,Math.Min(4,Zoom));double scale=Zoom*dpi/72;
                int slotWidth=Math.Max(1,(int)Math.Round(maxWidth*scale));ColumnCount=LayoutMode==PageLayout.Grid?Math.Max(1,Math.Min(24,(ClientSize.Width-48+gap)/(slotWidth+gap))):fixedColumns;
                int contentWidth=ColumnCount*slotWidth+(ColumnCount-1)*gap,virtualWidth=Math.Max(ClientSize.Width,contentWidth+2*margin),left=(virtualWidth-contentWidth)/2;
                pageRects=new Rectangle[Doc.Count];int activeRow=LayoutMode==PageLayout.Book?(Doc.Index==0?0:(Doc.Index+1)/2):Doc.Index/ColumnCount;
                int previousRow=-1,rowHeight=0,y=margin;
                for(int i=0;i<sizes.Length;i++){
                    int row=LayoutMode==PageLayout.Book?(i==0?0:(i+1)/2):i/ColumnCount;
                    int column=LayoutMode==PageLayout.Book?(i==0?(coverAlone?0:1):(i+1)%2):i%ColumnCount;
                    if(!Continuous&&row!=activeRow)continue;
                    if(previousRow!=-1&&row!=previousRow){y=checked(y+rowHeight+rowGap);rowHeight=0;}previousRow=row;
                    int w=Math.Max(1,(int)Math.Round(sizes[i].Width*scale)),h=Math.Max(1,(int)Math.Round(sizes[i].Height*scale));pageRects[i]=new Rectangle(left+column*(slotWidth+gap)+(slotWidth-w)/2,y,w,h);rowHeight=Math.Max(rowHeight,h);
                }
                SyncPageSize();AutoScrollMinSize=new Size(contentWidth+2*margin,checked(y+rowHeight+margin));var active=pageRects[Doc.Index];if(resetScroll)AutoScrollPosition=new Point(Math.Max(0,active.X-24),Math.Max(0,active.Y-margin));else if(hasAnchor)AutoScrollPosition=new Point(Math.Max(0,(int)Math.Round(active.X+anchorX*active.Width-ClientSize.Width/2.0)),Math.Max(0,(int)Math.Round(active.Y+anchorY*active.Height-ClientSize.Height/2.0)));lastViewport=ClientSize;owner.UpdateZoom(Zoom);
            }catch(Exception e){owner.Report(e.Message);}finally{rebuilding=false;Invalidate();}
        }
        protected override void OnResize(EventArgs e){base.OnResize(e);if(owner!=null&&Doc!=null&&Fit!=0&&Visible)Rebuild(false);}
        protected override void Dispose(bool disposing){if(disposing)ClearImages();base.Dispose(disposing);}
        public Rectangle DeviceRect(N.Rect r){Point p1=Doc.ToDevice(r.left,r.top,pixelWidth,pixelHeight,ViewRotation),p2=Doc.ToDevice(r.right,r.bottom,pixelWidth,pixelHeight,ViewRotation);var pr=PageRectangle;return new Rectangle(Math.Min(p1.X,p2.X)+pr.X,Math.Min(p1.Y,p2.Y)+pr.Y,Math.Max(1,Math.Abs(p1.X-p2.X)),Math.Max(1,Math.Abs(p1.Y-p2.Y)));}
        N.Pt PagePoint(Point p){var r=PageRectangle;return Doc.ToPage(new Point(p.X-r.X,p.Y-r.Y),pixelWidth,pixelHeight,ViewRotation);}
        internal N.Pt PointAt(Point p){return PagePoint(p);}
        internal Pick PickAt(Point p){return Doc==null?null:Doc.PickObject(PagePoint(p));}
        static Rectangle DragBox(Point p,Point q){return new Rectangle(Math.Min(p.X,q.X),Math.Min(p.Y,q.Y),Math.Abs(p.X-q.X),Math.Abs(p.Y-q.Y));}
        N.Rect PdfBox(Rectangle r){var a=PagePoint(r.Location);var b=PagePoint(new Point(r.Right,r.Bottom));return new N.Rect(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Max(a.x,b.x),Math.Max(a.y,b.y));}
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);var g=e.Graphics;
            if(Doc==null){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;using(var title=new Font("Microsoft YaHei UI",28,FontStyle.Bold))using(var sub=new Font("Microsoft YaHei UI",11))using(var dark=new SolidBrush(Color.FromArgb(38,54,69)))using(var gray=new SolidBrush(Color.FromArgb(110,121,132))){var center=new StringFormat{Alignment=StringAlignment.Center};float y=Math.Max(65,Height/2-65);g.DrawString("Mini PDF",title,dark,new RectangleF(0,y,Width,65),center);g.DrawString("Ctrl+O 打开文档，或将 PDF 拖到这里",sub,gray,new RectangleF(0,y+72,Width,40),center);}return;}
            if(pageRects.Length==0)return;
            var viewport=ClientRectangle;viewport.Inflate(80,80);
            for(int i=0;i<pageRects.Length;i++){if(pageRects[i].IsEmpty)continue;var page=ScreenRect(pageRects[i]);if(!viewport.IntersectsWith(page))continue;
                using(var shadow=new SolidBrush(Color.FromArgb(35,30,45,60)))g.FillRectangle(shadow,page.X+3,page.Y+4,page.Width+1,page.Height+1);
                try{var rendered=ImageFor(i);if(rendered.Width==page.Width&&rendered.Height==page.Height)g.DrawImageUnscaled(rendered,page.Location);else{g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(rendered,page);}}catch(Exception ex){g.FillRectangle(Brushes.White,page);owner.Report(ex.Message);}
                using(var border=new Pen(i==Doc.Index&&LayoutMode==PageLayout.Grid?Color.FromArgb(24,117,152):Color.FromArgb(205,212,218),i==Doc.Index&&LayoutMode==PageLayout.Grid?2:1))g.DrawRectangle(border,page.X-1,page.Y-1,page.Width+1,page.Height+1);
                if(LayoutMode==PageLayout.Grid)TextRenderer.DrawText(g,(i+1).ToString(),Font,new Rectangle(page.X,page.Bottom+3,page.Width,26),Color.FromArgb(100,110,120),TextFormatFlags.HorizontalCenter);
            }
            using(var highlight=new SolidBrush(Color.FromArgb(95,255,211,73)))foreach(var r in Markers)g.FillRectangle(highlight,DeviceRect(r));
            if(TextStart>=0&&TextCount>0){using(var brush=new SolidBrush(Color.FromArgb(75,48,129,220)))foreach(var r in Doc.TextRects(TextStart,TextCount))g.FillRectangle(brush,DeviceRect(r));}
            if(Selected!=null){Rectangle r=DeviceRect(Selected.Bounds);if(down&&Mode==Tool.Object){if(resizing)r=DragBox(r.Location,current);else r.Offset(current.X-origin.X,current.Y-origin.Y);}r.Inflate(3,3);using(var pen=new Pen(Color.FromArgb(25,119,155),1.5f)){pen.DashStyle=DashStyle.Dash;g.DrawRectangle(pen,r);}if(!Selected.IsAnnotation){using(var b=new SolidBrush(Color.White))g.FillRectangle(b,r.Right-4,r.Bottom-4,8,8);using(var p=new Pen(Color.FromArgb(25,119,155)))g.DrawRectangle(p,r.Right-4,r.Bottom-4,8,8);}}
            if(down&&(Mode==Tool.Highlight||Mode==Tool.Rectangle||Mode==Tool.Image||Mode==Tool.Underline||Mode==Tool.Strike)){var r=DragBox(origin,current);using(var b=new SolidBrush(Color.FromArgb(50,owner.Ink)))g.FillRectangle(b,r);using(var p=new Pen(owner.Ink,1))g.DrawRectangle(p,r);}
            if(down&&Mode==Tool.Ink&&screenStroke.Count>1){g.SmoothingMode=SmoothingMode.AntiAlias;using(var pen=new Pen(owner.Ink,Math.Max(1,owner.LineWidth*pixelWidth/(float)Doc.Width))){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLines(pen,screenStroke.ToArray());}}
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(Doc==null||pageRects.Length==0)return;
            int hitPage=HitPage(e.Location);if(hitPage>=0&&hitPage!=Doc.Index){owner.ActivatePageFromCanvas(hitPage);SyncPageSize();}
            if(e.Button==MouseButtons.Middle){panning=true;origin=e.Location;scrollOrigin=AutoScrollPosition;Capture=true;Cursor=Cursors.SizeAll;return;}
            if(e.Button==MouseButtons.Right){owner.ShowContext(e.Location);return;}
            if(e.Button!=MouseButtons.Left||!PageRectangle.Contains(e.Location))return;
            down=true;origin=current=e.Location;startPoint=PagePoint(e.Location);Capture=true;
            if(Mode==Tool.Object){resizing=false;if(Selected!=null&&!Selected.IsAnnotation){var r=DeviceRect(Selected.Bounds);r.Inflate(3,3);resizing=new Rectangle(r.Right-8,r.Bottom-8,16,16).Contains(e.Location)&&N.FPDFPage_GetRotation(Doc.Page)==0&&ViewRotation==0;}if(!resizing)Selected=Doc.PickObject(startPoint);owner.SelectionChanged();}
            else if(Mode==Tool.Read){Selected=null;Markers.Clear();startChar=Doc.CharAt(startPoint);TextStart=startChar;TextCount=startChar<0?0:1;}
            else if(Mode==Tool.Ink){stroke.Clear();screenStroke.Clear();stroke.Add(startPoint);screenStroke.Add(e.Location);}
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Doc==null||pageRects.Length==0)return;
            if(panning){AutoScrollPosition=new Point(-scrollOrigin.X+origin.X-e.X,-scrollOrigin.Y+origin.Y-e.Y);return;}
            if(down){current=e.Location;if(Mode==Tool.Read&&startChar>=0){int end=Doc.CharAt(PagePoint(e.Location));if(end>=0){TextStart=Math.Min(startChar,end);TextCount=Math.Abs(end-startChar)+1;}}
                if(Mode==Tool.Ink&&stroke.Count<20000&&(Math.Abs(e.X-screenStroke[screenStroke.Count-1].X)+Math.Abs(e.Y-screenStroke[screenStroke.Count-1].Y)>1)){stroke.Add(PagePoint(e.Location));screenStroke.Add(e.Location);}Invalidate();}
            else {if(!PageRectangle.Contains(e.Location))Cursor=Cursors.Default;else if(Mode==Tool.Read)Cursor=Doc.CharAt(PagePoint(e.Location))>=0?Cursors.IBeam:Cursors.Default;else Cursor=Mode==Tool.Object?Cursors.Default:Cursors.Cross;}
        }
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(panning){panning=false;Capture=false;Cursor=Cursors.Default;return;}if(!down||e.Button!=MouseButtons.Left||Doc==null)return;
            down=false;Capture=false;current=e.Location;var endPoint=PagePoint(e.Location);int distance=Math.Abs(current.X-origin.X)+Math.Abs(current.Y-origin.Y);
            if(Mode==Tool.Read){if(distance<5&&TextCount<=1)owner.OpenLink(startPoint);Invalidate();return;}
            if(Mode==Tool.Object){if(Selected!=null&&!Selected.IsAnnotation&&distance>3){var pick=Selected;if(resizing){float w=Math.Abs(endPoint.x-pick.Bounds.left),h=Math.Abs(pick.Bounds.top-endPoint.y);owner.Run(delegate{Doc.Resize(pick,w,h);});}else{float dx=endPoint.x-startPoint.x,dy=endPoint.y-startPoint.y;owner.Run(delegate{Doc.Move(pick,dx,dy);});}}Invalidate();return;}
            if(Mode==Tool.Text){owner.InsertText(startPoint);return;}if(Mode==Tool.Note){owner.InsertNote(startPoint);return;}
            if(Mode==Tool.Ink){if(stroke.Count>1)owner.Run(delegate{Doc.AddInk(stroke,owner.Ink,owner.LineWidth);});return;}
            var box=DragBox(origin,current);if(box.Width<4||box.Height<4)return;var rect=PdfBox(box);
            if(Mode==Tool.Image)owner.InsertImage(rect);
            else if(Mode==Tool.Rectangle)owner.Run(delegate{Doc.AddRectangle(rect,owner.Ink,owner.LineWidth);});
            else if(Mode==Tool.Highlight||Mode==Tool.Underline||Mode==Tool.Strike){int kind=Mode==Tool.Highlight?9:Mode==Tool.Underline?10:12;owner.Run(delegate{Doc.AddMarkup(new List<N.Rect>{rect},kind,owner.Ink);});}
        }
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&(down||panning)){down=false;panning=false;Invalidate();}}
        protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(LayoutMode==PageLayout.Grid&&Doc!=null&&Mode==Tool.Read){owner.SetLayout(PageLayout.Single);return;}if(Mode==Tool.Object&&Selected!=null)owner.EditSelected();}
        protected override void OnMouseWheel(MouseEventArgs e){if(Doc==null){base.OnMouseWheel(e);return;}if((ModifierKeys&Keys.Control)!=0){Fit=0;Zoom*=e.Delta>0?1.15f:1/1.15f;Rebuild(false);return;}
            if(Continuous){base.OnMouseWheel(e);return;}
            bool scrolls=AutoScrollMinSize.Height>ClientSize.Height;if(!scrolls){owner.TurnPage(e.Delta<0?1:-1);return;}
            int bottom=AutoScrollMinSize.Height-ClientSize.Height;bool edge=e.Delta<0?-AutoScrollPosition.Y>=bottom-3:AutoScrollPosition.Y>=0;if(edge){int prior=Doc.Index;owner.TurnPage(e.Delta<0?1:-1);if(Doc.Index!=prior&&e.Delta>0)AutoScrollPosition=new Point(0,AutoScrollMinSize.Height);return;}base.OnMouseWheel(e);
        }
        protected override void OnScroll(ScrollEventArgs e){base.OnScroll(e);if(Doc==null||rebuilding||down||panning||!Continuous||pageRects.Length==0)return;long bestArea=0,activeArea=0;int best=Doc.Index;for(int i=0;i<pageRects.Length;i++){var intersection=Rectangle.Intersect(ClientRectangle,ScreenRect(pageRects[i]));long area=(long)Math.Max(0,intersection.Width)*Math.Max(0,intersection.Height);if(i==Doc.Index)activeArea=area;if(area>bestArea){bestArea=area;best=i;}}if(best!=Doc.Index&&bestArea>0&&activeArea<bestArea*0.8){owner.ActivatePageFromCanvas(best);SyncPageSize();}}
        public void ShowMarkers(List<N.Rect> rects){Markers=rects;Invalidate();if(rects.Count>0){var r=DeviceRect(rects[0]);if(r.Top<0||r.Bottom>ClientSize.Height)AutoScrollPosition=new Point(-AutoScrollPosition.X,Math.Max(0,r.Top-AutoScrollPosition.Y-80));}}
        public string CopiedText(){if(Selected!=null&&Selected.Type==1)return Selected.Text;return Doc==null?"":Doc.SelectedText(TextStart,TextCount);}
        internal long CacheBytesForTest {get{return cacheBytes;}}
    }
}
