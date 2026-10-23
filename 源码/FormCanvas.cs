using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed class FormCanvas : Control {
        internal PdfForms Model;internal PdfDocument View;internal PdfField Selected;internal FormWidget Widget;internal FieldKind? AddKind {get;set;}internal bool Editable;
        internal event Action<PdfField,FormWidget> Picked;
        internal event Action<FieldKind,N.Rect> FieldCreated;
        internal event Action<FormWidget,N.Rect> Moved;
        internal bool HasImage {get{return image!=null;}}
        internal Rectangle WidgetBoundsForTest(FormWidget value){return Device(value.Bounds);}
        Bitmap image;Rectangle destination;Point start,last;bool dragging,resizing;FormWidget active;N.Rect initial;
        internal FormCanvas(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.ResizeRedraw,true);BackColor=Color.FromArgb(234,237,241);TabStop=true;Cursor=Cursors.Default;}
        internal void SetImage(Bitmap next){var old=image;image=next;if(old!=null)old.Dispose();Invalidate();}
        internal void CancelDrag(){dragging=false;active=null;Capture=false;Invalidate();}
        Rectangle Device(N.Rect bounds){if(View==null||image==null)return Rectangle.Empty;var a=View.ToDevice(bounds.left,bounds.top,image.Width,image.Height);var b=View.ToDevice(bounds.right,bounds.bottom,image.Width,image.Height);return Rectangle.FromLTRB(destination.Left+(int)(Math.Min(a.X,b.X)*(double)destination.Width/image.Width),destination.Top+(int)(Math.Min(a.Y,b.Y)*(double)destination.Height/image.Height),destination.Left+(int)(Math.Max(a.X,b.X)*(double)destination.Width/image.Width),destination.Top+(int)(Math.Max(a.Y,b.Y)*(double)destination.Height/image.Height));}
        N.Pt Pdf(Point point){int x=(int)((point.X-destination.X)*(double)image.Width/Math.Max(1,destination.Width)),y=(int)((point.Y-destination.Y)*(double)image.Height/Math.Max(1,destination.Height));return View.ToPage(new Point(x,y),image.Width,image.Height);}
        N.Rect DragBounds() {
            var a=Pdf(start);var b=Pdf(last);float dx=b.x-a.x,dy=b.y-a.y;var bounds=initial;
            if(resizing){var opposite=Pdf(Device(initial).Location);bounds=new N.Rect(Math.Min(opposite.x,b.x),Math.Min(opposite.y,b.y),Math.Max(opposite.x,b.x),Math.Max(opposite.y,b.y));}
            else{bounds.left+=dx;bounds.right+=dx;bounds.bottom+=dy;bounds.top+=dy;}return bounds;
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(image==null)return;float scale=Math.Min((ClientSize.Width-12f)/image.Width,(ClientSize.Height-12f)/image.Height);if(scale<=0)return;int w=(int)(image.Width*scale),h=(int)(image.Height*scale);destination=new Rectangle((ClientSize.Width-w)/2,(ClientSize.Height-h)/2,w,h);e.Graphics.DrawImage(image,destination);
            if(Model!=null&&View!=null)foreach(var f in Model.Fields)foreach(var item in f.Widgets)if(item.Page==View.Index){Rectangle rect=Device(item.Bounds);bool selected=item==Widget;using(var fill=new SolidBrush(Color.FromArgb(selected?32:15,30,125,220)))e.Graphics.FillRectangle(fill,rect);using(var pen=new Pen(selected?Color.FromArgb(0,105,210):Color.FromArgb(130,160,185),selected?2:1))e.Graphics.DrawRectangle(pen,rect);if(selected&&Editable){e.Graphics.FillRectangle(Brushes.White,rect.Right-5,rect.Bottom-5,9,9);e.Graphics.DrawRectangle(Pens.RoyalBlue,rect.Right-5,rect.Bottom-5,9,9);}}
            if(dragging){Rectangle rect=active==null?Rectangle.FromLTRB(Math.Min(start.X,last.X),Math.Min(start.Y,last.Y),Math.Max(start.X,last.X),Math.Max(start.Y,last.Y)):Device(initial);if(active!=null){rect=Device(DragBounds());}using(var pen=new Pen(Color.RoyalBlue,2){DashStyle=System.Drawing.Drawing2D.DashStyle.Dash})e.Graphics.DrawRectangle(pen,rect);}
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||image==null||View==null||!destination.Contains(e.Location))return;Focus();start=last=e.Location;active=null;resizing=false;
            if(AddKind.HasValue&&Editable){dragging=true;Capture=true;return;}
            if(Model!=null)foreach(var f in Model.Fields.AsEnumerable().Reverse())foreach(var w in f.Widgets.AsEnumerable().Reverse())if(w.Page==View.Index&&Device(w.Bounds).Contains(e.Location)){Selected=f;Widget=w;if(Picked!=null)Picked(f,w);if(Editable&&f.Supported){active=w;initial=w.Bounds;var rect=Device(w.Bounds);resizing=Math.Abs(e.X-rect.Right)<=10&&Math.Abs(e.Y-rect.Bottom)<=10;dragging=true;Capture=true;}Invalidate();return;}
            Widget=null;Selected=null;if(Picked!=null)Picked(null,null);Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging){last=e.Location;Invalidate();}}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging)return;last=e.Location;var a=Pdf(start);var b=Pdf(last);bool moved=Math.Abs(e.X-start.X)+Math.Abs(e.Y-start.Y)>4;dragging=false;Capture=false;
            if(moved&&active==null&&AddKind.HasValue){var bounds=new N.Rect(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Max(a.x,b.x),Math.Max(a.y,b.y));if(bounds.right-bounds.left>=4&&bounds.top-bounds.bottom>=4&&FieldCreated!=null)FieldCreated(AddKind.Value,bounds);}
            else if(moved&&active!=null){var bounds=DragBounds();if(bounds.right-bounds.left>=4&&bounds.top-bounds.bottom>=4&&Moved!=null)Moved(active,bounds);}
            active=null;Invalidate();
        }
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&dragging){dragging=false;active=null;Invalidate();}}
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData==Keys.Escape){CancelDrag();AddKind=null;return true;}return base.ProcessCmdKey(ref msg,keyData);}
        protected override void Dispose(bool disposing){if(disposing){RenderPool.Cancel(this,true);if(image!=null)image.Dispose();}base.Dispose(disposing);}
    }
}
