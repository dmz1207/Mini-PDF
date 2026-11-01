using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniAI.Desktop {
    // Shared, dependency-free screen selector. No file or clipboard is written.
    internal sealed class CaptureOverlay : Form {
        readonly Bitmap screen;
        Point start,current;
        bool selecting;
        IntPtr crossHandle;
        Cursor crossCursor;
        internal Bitmap Result;
        internal Rectangle SelectedScreen;
        CaptureOverlay(Bitmap image,Rectangle bounds) {
            screen=image;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=bounds;
            TopMost=true;ShowInTaskbar=false;DoubleBuffered=true;KeyPreview=true;
            using(var graphics=Graphics.FromHwnd(IntPtr.Zero)){
                int size=Math.Max(32,Math.Min(64,(int)Math.Round(32*graphics.DpiX/96)));
                using(var color=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(var mask=new Bitmap(size,size,PixelFormat.Format1bppIndexed)){
                    using(var drawing=Graphics.FromImage(color))DrawCrosshair(drawing,size);
                    IntPtr colorHandle=color.GetHbitmap(Color.FromArgb(0)),maskHandle=mask.GetHbitmap();
                    try{var info=new CursorInfo{IsIcon=false,X=(uint)(size/2),Y=(uint)(size/2),Color=colorHandle,Mask=maskHandle};crossHandle=CreateIconIndirect(ref info);}finally{DeleteObject(colorHandle);DeleteObject(maskHandle);}
                }
            }
            crossCursor=crossHandle==IntPtr.Zero?null:new Cursor(crossHandle);Cursor=crossCursor??Cursors.Cross;
        }
        internal static void DrawCrosshair(Graphics graphics,int size){
            graphics.Clear(Color.Transparent);float middle=size/2f,edge=Math.Max(2,size/12f),width=Math.Max(2,size/16f);
            using(var outline=new Pen(Color.FromArgb(255,18,24,30),width+3))using(var bright=new Pen(Color.FromArgb(255,40,255,100),width)){
                graphics.DrawLine(outline,middle,edge,middle,size-edge);graphics.DrawLine(outline,edge,middle,size-edge,middle);
                graphics.DrawLine(bright,middle,edge,middle,size-edge);graphics.DrawLine(bright,edge,middle,size-edge,middle);
            }
        }
        [StructLayout(LayoutKind.Sequential)]struct CursorInfo{[MarshalAs(UnmanagedType.Bool)]internal bool IsIcon;internal uint X,Y;internal IntPtr Mask,Color;}
        [DllImport("user32.dll")]static extern IntPtr CreateIconIndirect(ref CursorInfo info);
        [DllImport("user32.dll")]static extern bool DestroyCursor(IntPtr cursor);
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr value);
        protected override void Dispose(bool disposing){if(disposing&&crossCursor!=null){Cursor=Cursors.Default;crossCursor.Dispose();crossCursor=null;}if(crossHandle!=IntPtr.Zero){DestroyCursor(crossHandle);crossHandle=IntPtr.Zero;}base.Dispose(disposing);}
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
