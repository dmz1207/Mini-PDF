using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed partial class MainWindow {
        [StructLayout(LayoutKind.Sequential)] struct FrameRect { internal int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct FrameMargins { internal int Left,Right,Top,Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct FrameMouseTrack { internal int Size,Flags;internal IntPtr Window;internal int HoverTime; }
        [StructLayout(LayoutKind.Sequential)] struct FrameStyleChange {internal uint Before,After;}
        [StructLayout(LayoutKind.Sequential)]struct FramePoint {internal int X,Y;}
        [StructLayout(LayoutKind.Sequential)]struct FrameMinMax {internal FramePoint Reserved,MaxSize,MaxPosition,MinTrack,MaxTrack;}
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr window,ref FrameMargins margins);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetFrameStyle(IntPtr window,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetFrameStyle(IntPtr window,int index,IntPtr value);
        [DllImport("user32.dll")]static extern bool AdjustWindowRectEx(ref FrameRect rectangle,uint style,bool menu,uint extendedStyle);
        [DllImport("user32.dll")] static extern bool TrackMouseEvent(ref FrameMouseTrack track);
        bool refreshingFrame,frameInTitle,titleMaximized,adjustingRestore;
        Rectangle normalTitleBounds;
        int captionReserve,pressedCaption;
        Panel captionButtons;
        CaptionButton minimizeCaption,maximizeCaption,closeCaption;
        bool TitleTabsActive {get{return documentHeader!=null&&!readingMode&&!fullScreen&&FormBorderStyle!=FormBorderStyle.None;}}
        protected override CreateParams CreateParams {get{var parameters=base.CreateParams;if(TitleTabsActive)parameters.Style&=~0x00c00000;return parameters;}}
        // Native hit codes retain system window movement, resizing and caption actions.
        sealed class TitlePanel : Panel {
            internal TitlePanel(){SetStyle(ControlStyles.OptimizedDoubleBuffer,true);}
            protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
        }
        sealed class CaptionButton : Button {
            internal int Hit;internal bool Hot,Restore;
            internal CaptionButton(){TabStop=false;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
            protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
            protected override void OnPaint(PaintEventArgs e){
                e.Graphics.Clear(Hot?(Hit==20?Color.FromArgb(220,48,53):Color.FromArgb(231,237,242)):Color.White);
                int unit=Math.Max(4,Font.Height/4),x=Width/2,y=Height/2;
                using(var pen=new Pen(Hot&&Hit==20?Color.White:Color.FromArgb(57,73,87),1.2f)){
                    if(Hit==8)e.Graphics.DrawLine(pen,x-unit,y+unit/2,x+unit,y+unit/2);
                    else if(Hit==20){e.Graphics.DrawLine(pen,x-unit,y-unit,x+unit,y+unit);e.Graphics.DrawLine(pen,x-unit,y+unit,x+unit,y-unit);}
                    else if(Restore){e.Graphics.DrawLines(pen,new[]{new Point(x-unit+2,y-unit),new Point(x+unit,y-unit),new Point(x+unit,y+unit-2)});e.Graphics.DrawRectangle(pen,x-unit,y-unit+2,unit*2-2,unit*2-2);}
                    else e.Graphics.DrawRectangle(pen,x-unit,y-unit,unit*2,unit*2);
                }
            }
        }
        sealed class TitleTabs : TabControl {
            [StructLayout(LayoutKind.Sequential)]struct TabPaint {internal IntPtr Dc;internal int Erase;internal FrameRect Rectangle;internal int Restore,Update;internal int Reserved0,Reserved1,Reserved2,Reserved3,Reserved4,Reserved5,Reserved6,Reserved7;}
            [DllImport("user32.dll")]static extern IntPtr BeginPaint(IntPtr window,out TabPaint paint);
            [DllImport("user32.dll")]static extern bool EndPaint(IntPtr window,ref TabPaint paint);
            internal Action<Graphics> PaintOverlay;
            internal Rectangle ItemBounds(int index){var rect=GetTabRect(index);rect.Y=0;rect.Height=ClientSize.Height;return rect;}
            internal TitleTabs(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
            protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Color.White);for(int i=0;i<TabCount;i++){var bounds=ItemBounds(i);if(bounds.IntersectsWith(ClientRectangle))OnDrawItem(new DrawItemEventArgs(e.Graphics,Font,bounds,i,i==SelectedIndex?DrawItemState.Selected:DrawItemState.None));}if(PaintOverlay!=null)PaintOverlay(e.Graphics);}
            protected override void WndProc(ref Message m){
                if(m.Msg==15){TabPaint paint;IntPtr dc=BeginPaint(Handle,out paint);try{if(ClientSize.Width>0&&ClientSize.Height>0)using(var buffer=BufferedGraphicsManager.Current.Allocate(dc,ClientRectangle)){OnPaint(new PaintEventArgs(buffer.Graphics,ClientRectangle));buffer.Render(dc);}}finally{EndPaint(Handle,ref paint);}m.Result=IntPtr.Zero;return;}
                if((m.Msg==0x317||m.Msg==0x318)&&m.WParam!=IntPtr.Zero){using(var graphics=Graphics.FromHdc(m.WParam))OnPaint(new PaintEventArgs(graphics,ClientRectangle));m.Result=IntPtr.Zero;return;}
                if(m.Msg==20){m.Result=new IntPtr(1);return;}
                if(m.Msg==0x84){var point=PointToClient(new Point((short)((long)m.LParam&65535),(short)(((long)m.LParam>>16)&65535)));bool onTab=false;for(int i=0;i<TabCount;i++)if(ItemBounds(i).Contains(point)){onTab=true;break;}if(!onTab&&point.X<ClientSize.Width-24){m.Result=new IntPtr(-1);return;}}
                base.WndProc(ref m);
            }
        }
        void InitializeCaptionButtons(){
            captionButtons=new TitlePanel{Name="CaptionButtons",BackColor=Color.White};
            minimizeCaption=new CaptionButton{Hit=8,AccessibleName=UiText.Get("最小化")};maximizeCaption=new CaptionButton{Hit=9};closeCaption=new CaptionButton{Hit=20,AccessibleName=UiText.Get("关闭窗口")};
            foreach(var button in new[]{minimizeCaption,maximizeCaption,closeCaption}){captionButtons.Controls.Add(button);button.Click+=delegate(object sender,EventArgs e){ExecuteCaption(((CaptionButton)sender).Hit);};}
            documentHeader.Controls.Add(captionButtons);captionButtons.BringToFront();
        }
        void RefreshWindowFrame(){
            if(!IsHandleCreated||refreshingFrame||IsDisposed)return;refreshingFrame=true;
            try{
                bool integrated=TitleTabsActive;
                long style=(long)GetFrameStyle(Handle,-16),caption=integrated?0:base.CreateParams.Style&0x00c00000;
                long desired=(style&~0x00c00000L)|caption;bool styleChanged=style!=desired;
                if(styleChanged)SetFrameStyle(Handle,-16,new IntPtr(desired));
                if(frameInTitle!=integrated||styleChanged){frameInTitle=integrated;titleMaximized=integrated&&IsZoomed(Handle);SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x37);}
                // One pixel keeps DWM shadows without making GDI text transparent.
                var margins=new FrameMargins{Top=integrated?1:0};DwmExtendFrameIntoClientArea(Handle,ref margins);
                if(!integrated)return;
                int buttonWidth;using(var graphics=CreateGraphics())buttonWidth=Math.Max(40,(int)Math.Round(46*graphics.DpiX/96));captionReserve=buttonWidth*3;
                int grip=Math.Max(24,Font.Height+8);documentHeader.Padding=new Padding(0,0,captionReserve+grip,0);
                captionButtons.SetBounds(Math.Max(0,documentHeader.Width-captionReserve),0,captionReserve,documentHeader.Height);
                minimizeCaption.SetBounds(0,0,buttonWidth,documentHeader.Height);maximizeCaption.SetBounds(buttonWidth,0,buttonWidth,documentHeader.Height);closeCaption.SetBounds(buttonWidth*2,0,buttonWidth,documentHeader.Height);
                minimizeCaption.AccessibleName=UiText.Get("最小化");closeCaption.AccessibleName=UiText.Get("关闭窗口");maximizeCaption.AccessibleName=UiText.Get(WindowState==FormWindowState.Maximized?"还原":"最大化");maximizeCaption.Restore=WindowState==FormWindowState.Maximized;maximizeCaption.Invalidate();
            }finally{refreshingFrame=false;}
        }
        int CaptionHit(Point point){if(point.X<ClientSize.Width-captionReserve)return 0;int index=Math.Min(2,Math.Max(0,(point.X-(ClientSize.Width-captionReserve))*3/captionReserve));return index==0?8:index==1?9:20;}
        void ExecuteCaption(int hit){N.SendMessage(Handle,0x112,new IntPtr(hit==8?0xf020:hit==20?0xf060:IsZoomed(Handle)?0xf120:0xf030),IntPtr.Zero);}
        static Point FrameMessagePoint(IntPtr value){return new Point((short)((long)value&65535),(short)(((long)value>>16)&65535));}
        void HighlightCaption(int hit){if(captionButtons==null)return;foreach(var button in new[]{minimizeCaption,maximizeCaption,closeCaption}){bool hot=button.Hit==hit;if(button.Hot!=hot){button.Hot=hot;button.Invalidate();}}}
        void RememberNormalTitleBounds(){if(IsHandleCreated&&TitleTabsActive&&!adjustingRestore&&!titleMaximized&&!IsZoomed(Handle)&&!IsIconic(Handle)&&WindowState==FormWindowState.Normal)normalTitleBounds=Bounds;}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);RefreshWindowFrame();RememberNormalTitleBounds();}
        protected override void WndProc(ref Message m){
            if(TitleTabsActive&&m.Msg==0x24){base.WndProc(ref m);var screen=Screen.FromHandle(Handle);var area=screen.WorkingArea;var border=new FrameRect();AdjustWindowRectEx(ref border,(uint)(long)GetFrameStyle(Handle,-16),false,(uint)(long)GetFrameStyle(Handle,-20));var limits=(FrameMinMax)Marshal.PtrToStructure(m.LParam,typeof(FrameMinMax));limits.MaxPosition=new FramePoint{X=area.Left-screen.Bounds.Left+border.Left,Y=area.Top-screen.Bounds.Top+border.Top};limits.MaxSize=new FramePoint{X=area.Width+border.Right-border.Left,Y=area.Height+border.Bottom-border.Top};Marshal.StructureToPtr(limits,m.LParam,false);m.Result=IntPtr.Zero;return;}
            if(TitleTabsActive&&m.Msg==0xa3&&(int)m.WParam==2){ExecuteCaption(9);m.Result=IntPtr.Zero;return;}
            if(TitleTabsActive&&m.Msg==0xa1&&((int)m.WParam==8||(int)m.WParam==9||(int)m.WParam==20)){pressedCaption=(int)m.WParam;Capture=true;HighlightCaption(pressedCaption);m.Result=IntPtr.Zero;return;}
            if(pressedCaption!=0&&m.Msg==0x202){var point=FrameMessagePoint(m.LParam);int hit=point.Y>=0&&point.Y<documentHeader.Height&&point.X<ClientSize.Width?CaptionHit(point):0;int pressed=pressedCaption;pressedCaption=0;Capture=false;HighlightCaption(hit);if(hit==pressed)ExecuteCaption(hit);m.Result=IntPtr.Zero;return;}
            if(m.Msg==0x215||m.Msg==0x1f){pressedCaption=0;HighlightCaption(0);}
            if(TitleTabsActive&&m.Msg==0x7c&&m.WParam==new IntPtr(-16)){base.WndProc(ref m);var style=(FrameStyleChange)Marshal.PtrToStructure(m.LParam,typeof(FrameStyleChange));style.After&=~0x00c00000u;Marshal.StructureToPtr(style,m.LParam,false);return;}
            if(TitleTabsActive&&m.Msg==0x112){int command=(int)((long)m.WParam&0xfff0);if(command==0xf030&&!IsZoomed(Handle)&&!IsIconic(Handle))normalTitleBounds=Bounds;if(command==0xf120&&IsZoomed(Handle)&&!normalTitleBounds.IsEmpty){var restore=normalTitleBounds.Size;adjustingRestore=true;try{base.WndProc(ref m);if(!IsZoomed(Handle)&&!IsIconic(Handle)&&Size!=restore)Size=restore;}finally{adjustingRestore=false;}titleMaximized=IsZoomed(Handle);RememberNormalTitleBounds();return;}}
            if(TitleTabsActive&&m.Msg==5){if((int)m.WParam==2)titleMaximized=true;else if((int)m.WParam==0&&titleMaximized&&!normalTitleBounds.IsEmpty){var restore=normalTitleBounds.Size;titleMaximized=false;adjustingRestore=true;try{base.WndProc(ref m);if(Size!=restore)Size=restore;}finally{adjustingRestore=false;}RememberNormalTitleBounds();return;}}
            if(TitleTabsActive&&m.Msg==0x83&&m.LParam!=IntPtr.Zero){var proposed=(FrameRect)Marshal.PtrToStructure(m.LParam,typeof(FrameRect));base.WndProc(ref m);var client=(FrameRect)Marshal.PtrToStructure(m.LParam,typeof(FrameRect));client.Top=proposed.Top+(IsZoomed(Handle)?Math.Max(0,client.Left-proposed.Left):1);Marshal.StructureToPtr(client,m.LParam,false);m.Result=IntPtr.Zero;return;}
            if(TitleTabsActive&&m.Msg==0x84){
                var point=PointToClient(new Point((short)((long)m.LParam&65535),(short)(((long)m.LParam>>16)&65535)));
                base.WndProc(ref m);int hit=(int)m.Result;if(hit>=10&&hit<=17)return;
                if(WindowState!=FormWindowState.Maximized&&point.Y<Math.Max(4,SystemInformation.FrameBorderSize.Height-1)){m.Result=new IntPtr(point.X<12?13:point.X>ClientSize.Width-12?14:12);return;}
                if(point.Y>=0&&point.Y<documentHeader.Height){
                    int button=CaptionHit(point);if(button!=0){m.Result=new IntPtr(button);return;}
                    var local=documentTabs.PointToClient(PointToScreen(point));if(documentTabs.Visible&&TabAt(local)>=0){m.Result=new IntPtr(1);return;}
                    if(point.X<documentMenu.Right){m.Result=new IntPtr(1);return;}
                    m.Result=new IntPtr(2);return;
                }
                return;
            }
            if(TitleTabsActive&&m.Msg==0xa0){HighlightCaption((int)m.WParam);var track=new FrameMouseTrack{Size=Marshal.SizeOf(typeof(FrameMouseTrack)),Flags=0x12,Window=Handle};TrackMouseEvent(ref track);}
            if(m.Msg==0x2a2||m.Msg==0x200||m.Msg==0x86&&m.WParam==IntPtr.Zero)HighlightCaption(0);
            base.WndProc(ref m);
            if(m.Msg==6||m.Msg==0x320)RefreshWindowFrame();
            if(m.Msg==0x47||m.Msg==0x232)RememberNormalTitleBounds();
        }
        internal bool TitleTabsForTest {get{return TitleTabsActive&&frameInTitle;}}
        internal int CaptionReserveForTest {get{return captionReserve;}}
        internal int HeaderHeightForTest {get{return documentHeader.Height;}}
        internal bool NativeCaptionForTest {get{return ((long)GetFrameStyle(Handle,-16)&0x00c00000)!=0;}}
    }
}
