using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace MiniPdf {
    internal static partial class N {
        [DllImport(D)]internal static extern void FPDF_RenderPage(IntPtr dc,IntPtr page,int x,int y,int width,int height,int rotate,int flags);
        [DllImport(D)]internal static extern int FPDF_SetPrintMode(int mode);
    }
    internal static class NativePrinting {
        [StructLayout(LayoutKind.Sequential)]struct Transform {internal float A,B,C,D,X,Y;}
        [DllImport("gdi32.dll")]static extern int SaveDC(IntPtr dc);
        [DllImport("gdi32.dll")]static extern bool RestoreDC(IntPtr dc,int state);
        [DllImport("gdi32.dll")]static extern int SetMapMode(IntPtr dc,int mode);
        [DllImport("gdi32.dll")]static extern int SetGraphicsMode(IntPtr dc,int mode);
        [DllImport("gdi32.dll")]static extern bool SetWorldTransform(IntPtr dc,ref Transform transform);
        [DllImport("gdi32.dll")]static extern bool SetViewportOrgEx(IntPtr dc,int x,int y,IntPtr old);
        [DllImport("gdi32.dll")]static extern bool SetWindowOrgEx(IntPtr dc,int x,int y,IntPtr old);
        [DllImport("gdi32.dll")]static extern int IntersectClipRect(IntPtr dc,int left,int top,int right,int bottom);
        internal static void Draw(PdfDocument document,PrintOptions options,PrintSheet sheet,Graphics graphics,PageSettings settings){
            // Render PDF vectors directly into the printer's device context.
            // No fixed-DPI page screenshot is inserted into the spool job.
            float dx=graphics.DpiX/100,dy=graphics.DpiY/100,left=settings.HardMarginX,top=settings.HardMarginY;
            foreach(var item in sheet.Items)using(var prepared=document.PreparePrintPage(item.Page,options.Annotations)){
                var source=prepared??document;int index=prepared==null?item.Page:0;IntPtr page=index==source.Index?source.Page:N.FPDF_LoadPage(source.Handle,index);PdfDocument.Check(page!=IntPtr.Zero,UiText.Get("无法读取页面。"));
                try{IntPtr dc=graphics.GetHdc();int saved=SaveDC(dc);try{PdfDocument.Check(saved!=0,UiText.Get("无法访问打印设备。"));SetMapMode(dc,1);SetGraphicsMode(dc,2);var transform=new Transform{A=1,D=1};SetWorldTransform(dc,ref transform);SetWindowOrgEx(dc,0,0,IntPtr.Zero);SetViewportOrgEx(dc,0,0,IntPtr.Zero);
                        IntersectClipRect(dc,(int)Math.Ceiling((item.Clip.Left-left)*dx),(int)Math.Ceiling((item.Clip.Top-top)*dy),(int)Math.Floor((item.Clip.Right-left)*dx),(int)Math.Floor((item.Clip.Bottom-top)*dy));
                        N.FPDF_RenderPage(dc,page,(int)Math.Round((item.Bounds.X-left)*dx),(int)Math.Round((item.Bounds.Y-top)*dy),Math.Max(1,(int)Math.Round(item.Bounds.Width*dx)),Math.Max(1,(int)Math.Round(item.Bounds.Height*dy)),item.Rotation,0x800|(options.Annotations?1:0)|(options.Gray?8:0));
                    }finally{if(saved!=0)RestoreDC(dc,saved);graphics.ReleaseHdc(dc);}}
                finally{if(page!=source.Page)N.FPDF_ClosePage(page);}
            }
            var state=graphics.Save();try{graphics.TranslateTransform(-left,-top);PrintEngine.Decorations(options,sheet,graphics);}finally{graphics.Restore(state);}
        }
    }
}
