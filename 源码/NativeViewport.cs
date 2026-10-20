using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniPdf {
    // Native SCROLLBAR child windows allow a narrow, local scrollbar without changing Windows settings.
    internal class NativeViewport : ScrollableControl {
        sealed class Bar : Control {
            readonly bool vertical;
            internal Bar(bool vertical){this.vertical=vertical;TabStop=false;SetStyle(ControlStyles.UserPaint|ControlStyles.StandardClick,false);}
            protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ClassName="SCROLLBAR";p.Style|=vertical?1:0;return p;}}
        }
        [StructLayout(LayoutKind.Sequential)] struct ScrollInfo {internal uint size,mask;internal int min,max;internal uint page;internal int position,track;}
        [DllImport("user32.dll")] static extern int SetScrollInfo(IntPtr window,int bar,ref ScrollInfo info,bool redraw);
        [DllImport("user32.dll")] static extern bool GetScrollInfo(IntPtr window,int bar,ref ScrollInfo info);
        readonly Bar verticalBar,horizontalBar;
        Size extent;
        Point offset;
        bool updating,vertical,horizontal;
        int thickness=10,wheelDelta;
        internal NativeViewport(){base.AutoScroll=false;verticalBar=new Bar(true){Visible=false};horizontalBar=new Bar(false){Visible=false};Controls.Add(verticalBar);Controls.Add(horizontalBar);}
        internal Size UnscrolledClientSize {get{return base.ClientSize;}}
        internal int ScrollBarThickness {get{return thickness;}}
        public new Size ClientSize {get{return new Size(Math.Max(0,base.ClientSize.Width-(vertical?thickness:0)),Math.Max(0,base.ClientSize.Height-(horizontal?thickness:0)));}}
        public new Rectangle ClientRectangle {get{return new Rectangle(Point.Empty,ClientSize);}}
        public new Size AutoScrollMinSize {get{return extent;}set{extent=value;UpdateBars();}}
        public new Point AutoScrollPosition {get{return new Point(-offset.X,-offset.Y);}set{SetPosition(value.X,value.Y);}}
        internal bool NarrowVerticalBarForTest {get{return verticalBar.Visible&&verticalBar.Width==thickness;}}
        internal IntPtr VerticalBarForTest {get{return verticalBar.Handle;}}
        void SetPosition(int x,int y){var size=ClientSize;var next=new Point(Math.Max(0,Math.Min(x,Math.Max(0,extent.Width-size.Width))),Math.Max(0,Math.Min(y,Math.Max(0,extent.Height-size.Height))));if(next==offset)return;offset=next;UpdateNativeInfo();Invalidate();}
        void UpdateNativeInfo(){SetInfo(verticalBar,extent.Height,ClientSize.Height,offset.Y);SetInfo(horizontalBar,extent.Width,ClientSize.Width,offset.X);}
        static void SetInfo(Control bar,int length,int page,int position){if(!bar.IsHandleCreated)return;var info=new ScrollInfo{size=(uint)Marshal.SizeOf(typeof(ScrollInfo)),mask=7,min=0,max=Math.Max(0,length-1),page=(uint)Math.Max(1,page),position=position};SetScrollInfo(bar.Handle,2,ref info,true);}
        void UpdateBars(){if(updating||verticalBar==null)return;updating=true;try{var size=base.ClientSize;bool v=false,h=false;for(int pass=0;pass<3;pass++){v=extent.Height>Math.Max(0,size.Height-(h?thickness:0));h=extent.Width>Math.Max(0,size.Width-(v?thickness:0));}vertical=v;horizontal=h;verticalBar.Visible=v;horizontalBar.Visible=h;verticalBar.SetBounds(Math.Max(0,size.Width-thickness),0,thickness,Math.Max(0,size.Height-(h?thickness:0)));horizontalBar.SetBounds(0,Math.Max(0,size.Height-thickness),Math.Max(0,size.Width-(v?thickness:0)),thickness);SetPosition(offset.X,offset.Y);UpdateNativeInfo();}finally{updating=false;}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);using(var g=CreateGraphics())thickness=Math.Max(8,(int)Math.Round(10*g.DpiX/96));UpdateBars();}
        protected override void OnResize(EventArgs e){UpdateBars();base.OnResize(e);}
        void ScrollTo(int position,bool alongVertical,ScrollEventType kind){int before=alongVertical?offset.Y:offset.X;SetPosition(alongVertical?offset.X:position,alongVertical?position:offset.Y);OnScroll(new ScrollEventArgs(kind,before,alongVertical?offset.Y:offset.X,alongVertical?ScrollOrientation.VerticalScroll:ScrollOrientation.HorizontalScroll));}
        protected override void WndProc(ref Message m){
            if((m.Msg==0x115||m.Msg==0x114)&&m.LParam!=IntPtr.Zero){bool v=m.Msg==0x115;var bar=v?verticalBar:horizontalBar;if(bar!=null&&bar.IsHandleCreated&&m.LParam==bar.Handle){int command=(int)((long)m.WParam&65535),position=v?offset.Y:offset.X,page=v?ClientSize.Height:ClientSize.Width;var info=new ScrollInfo{size=(uint)Marshal.SizeOf(typeof(ScrollInfo)),mask=16};if(command==5||command==4){GetScrollInfo(bar.Handle,2,ref info);position=info.track;}else if(command==0)position-=40;else if(command==1)position+=40;else if(command==2)position-=page;else if(command==3)position+=page;else if(command==6)position=0;else if(command==7)position=Int32.MaxValue;ScrollTo(position,v,(ScrollEventType)Math.Min(8,command));m.Result=IntPtr.Zero;return;}}
            if(m.Msg==0x20e){int delta=(short)(((long)m.WParam>>16)&65535);ScrollTo(offset.X+delta/3,false,ScrollEventType.ThumbPosition);m.Result=IntPtr.Zero;return;}base.WndProc(ref m);
        }
        protected override void OnMouseWheel(MouseEventArgs e){wheelDelta+=e.Delta;int notches=wheelDelta/120;wheelDelta%=120;if(notches!=0){bool v=(ModifierKeys&Keys.Shift)==0;int lines=SystemInformation.MouseWheelScrollLines,step=lines<0?(v?ClientSize.Height:ClientSize.Width):lines*24;ScrollTo((v?offset.Y:offset.X)-notches*step,v,ScrollEventType.ThumbPosition);}}
        protected override bool ProcessCmdKey(ref Message msg,Keys key){if(ContainsFocus){if(key==Keys.Up||key==Keys.Down){ScrollTo(offset.Y+(key==Keys.Up?-40:40),true,ScrollEventType.ThumbPosition);return true;}if(key==Keys.Left||key==Keys.Right){ScrollTo(offset.X+(key==Keys.Left?-40:40),false,ScrollEventType.ThumbPosition);return true;}if(key==Keys.Home||key==Keys.End){ScrollTo(key==Keys.Home?0:Int32.MaxValue,true,ScrollEventType.ThumbPosition);return true;}}return base.ProcessCmdKey(ref msg,key);}
    }
}
