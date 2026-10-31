using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace MiniAI.Desktop {
    // Shared, dependency-free screen selector. No file or clipboard is written.
    internal sealed class CaptureOverlay : Form {
        readonly Bitmap screen;
        Point start,current;
        bool selecting;
        internal Bitmap Result;
        internal Rectangle SelectedScreen;
        CaptureOverlay(Bitmap image,Rectangle bounds) {
            screen=image;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=bounds;
            TopMost=true;ShowInTaskbar=false;DoubleBuffered=true;Cursor=Cursors.Cross;KeyPreview=true;
        }
        internal static Bitmap Pick(IWin32Window owner,out Rectangle selected) {
            selected=Rectangle.Empty;Rectangle area=SystemInformation.VirtualScreen;
            if(area.Width<1||area.Height<1||(long)area.Width*area.Height>80000000)throw new InvalidOperationException("The desktop is too large to capture.");
            using(var screen=new Bitmap(area.Width,area.Height,PixelFormat.Format32bppArgb)) {
                using(var graphics=Graphics.FromImage(screen))graphics.CopyFromScreen(area.Location,Point.Empty,area.Size,CopyPixelOperation.SourceCopy);
                using(var picker=new CaptureOverlay(screen,area)){if(picker.ShowDialog(owner)!=DialogResult.OK)return null;selected=picker.SelectedScreen;return picker.Result;}
            }
        }
        internal static Rectangle Selection(Point a,Point b,Size size){return Rectangle.Intersect(Rectangle.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)),new Rectangle(Point.Empty,size));}
        internal static Bitmap Crop(Bitmap image,Rectangle box) {
            box=Rectangle.Intersect(box,new Rectangle(Point.Empty,image.Size));if(box.Width<4||box.Height<4)return null;
            double scale=Math.Min(1,Math.Min(4096.0/Math.Max(box.Width,box.Height),Math.Sqrt(16000000.0/((long)box.Width*box.Height))));
            var result=new Bitmap(Math.Max(1,(int)(box.Width*scale)),Math.Max(1,(int)(box.Height*scale)),PixelFormat.Format32bppArgb);
            using(var graphics=Graphics.FromImage(result)){graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.DrawImage(image,new Rectangle(Point.Empty,result.Size),box,GraphicsUnit.Pixel);}return result;
        }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.DrawImageUnscaled(screen,Point.Empty);using(var shade=new SolidBrush(Color.FromArgb(130,0,0,0)))e.Graphics.FillRectangle(shade,ClientRectangle);
            var box=Selection(start,current,screen.Size);if(selecting&&!box.IsEmpty){e.Graphics.DrawImage(screen,box,box,GraphicsUnit.Pixel);using(var pen=new Pen(Color.FromArgb(58,158,245),2))e.Graphics.DrawRectangle(pen,box);}
            string text="Drag to select · Esc to cancel / 拖动框选 · Esc 取消";
            var point=selecting?new Point(Math.Max(8,box.Left),Math.Max(8,box.Top-32)):new Point(30,30);
            using(var font=new Font("Segoe UI",11))TextRenderer.DrawText(e.Graphics,text,font,point,Color.White,Color.FromArgb(50,50,50));
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Right){DialogResult=DialogResult.Cancel;return;}if(e.Button!=MouseButtons.Left)return;start=current=e.Location;selecting=true;Capture=true;Invalidate();}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(selecting){current=e.Location;Invalidate();}}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!selecting||e.Button!=MouseButtons.Left)return;current=e.Location;Capture=false;var box=Selection(start,current,screen.Size);Result=Crop(screen,box);if(Result==null){selecting=false;Invalidate();return;}SelectedScreen=box;SelectedScreen.Offset(Location);DialogResult=DialogResult.OK;}
        protected override bool ProcessCmdKey(ref Message msg,Keys keys){if(keys==Keys.Escape){DialogResult=DialogResult.Cancel;return true;}return base.ProcessCmdKey(ref msg,keys);}
    }
}
