using System;
using System.Runtime.InteropServices;

namespace MiniPdf {
    // PDFium's Windows ABI uses 32-bit unsigned long, including on x64.
    internal static class N {
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window,int command);
        [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(uint processId);
        [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool GetNamedPipeServerProcessId(IntPtr pipe,out uint processId);
        const string D = "pdfium.dll";
        [StructLayout(LayoutKind.Sequential)] internal struct Matrix { public float a,b,c,d,e,f; }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public float left,top,right,bottom; public Rect(float l,float b,float r,float t) {left=l;bottom=b;right=r;top=t;} }
        [StructLayout(LayoutKind.Sequential)] internal struct Pt { public float x,y; public Pt(float a,float b){x=a;y=b;} }
        [StructLayout(LayoutKind.Sequential)] internal struct PageSize { public float width,height; }
        [StructLayout(LayoutKind.Sequential)] internal struct Quad { public float x1,y1,x2,y2,x3,y3,x4,y4; }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int WriteBlock(IntPtr self,IntPtr data,uint size);
        [StructLayout(LayoutKind.Sequential)] internal struct Writer { public int version; public WriteBlock write; }
        [DllImport(D)] internal static extern void FPDF_InitLibrary();
        [DllImport(D)] internal static extern void FPDF_DestroyLibrary();
        [DllImport(D)] internal static extern IntPtr FPDF_LoadMemDocument64(IntPtr data,UIntPtr size,byte[] password);
        [DllImport(D)] internal static extern uint FPDF_GetLastError();
        [DllImport(D)] internal static extern void FPDF_CloseDocument(IntPtr doc);
        [DllImport(D)] internal static extern int FPDF_GetPageCount(IntPtr doc);
        [DllImport(D)] internal static extern uint FPDF_GetDocPermissions(IntPtr doc);
        [DllImport(D)] internal static extern int FPDF_GetSecurityHandlerRevision(IntPtr doc);
        [DllImport(D)] internal static extern IntPtr FPDF_LoadPage(IntPtr doc,int index);
        [DllImport(D)] internal static extern void FPDF_ClosePage(IntPtr page);
        [DllImport(D)] internal static extern double FPDF_GetPageWidth(IntPtr page);
        [DllImport(D)] internal static extern double FPDF_GetPageHeight(IntPtr page);
        [DllImport(D)] internal static extern int FPDF_GetPageSizeByIndexF(IntPtr doc,int index,out PageSize size);
        [DllImport(D)] internal static extern int FPDF_DeviceToPage(IntPtr page,int x,int y,int w,int h,int rotation,int dx,int dy,out double px,out double py);
        [DllImport(D)] internal static extern int FPDF_PageToDevice(IntPtr page,int x,int y,int w,int h,int rotation,double px,double py,out int dx,out int dy);
        [DllImport(D)] internal static extern IntPtr FPDFBitmap_Create(int w,int h,int alpha);
        [DllImport(D)] internal static extern IntPtr FPDFBitmap_CreateEx(int w,int h,int format,IntPtr buffer,int stride);
        [DllImport(D)] internal static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
        [DllImport(D)] internal static extern int FPDFBitmap_GetStride(IntPtr bitmap);
        [DllImport(D)] internal static extern void FPDFBitmap_FillRect(IntPtr bitmap,int x,int y,int w,int h,uint color);
        [DllImport(D)] internal static extern void FPDFBitmap_Destroy(IntPtr bitmap);
        [DllImport(D)] internal static extern void FPDF_RenderPageBitmap(IntPtr bitmap,IntPtr page,int x,int y,int w,int h,int rotate,int flags);
        [DllImport(D)] internal static extern IntPtr FPDF_CreateNewDocument();
        [DllImport(D)] internal static extern IntPtr FPDFPage_New(IntPtr doc,int index,double w,double h);
        [DllImport(D)] internal static extern void FPDFPage_Delete(IntPtr doc,int index);
        [DllImport(D)] internal static extern int FPDFPage_GetRotation(IntPtr page);
        [DllImport(D)] internal static extern void FPDFPage_SetRotation(IntPtr page,int rotate);
        [DllImport(D)] internal static extern int FPDF_MovePages(IntPtr doc,int[] pages,uint length,int destination);
        [DllImport(D)] internal static extern int FPDF_ImportPagesByIndex(IntPtr dest,IntPtr source,int[] pages,uint length,int index);
        [DllImport(D)] internal static extern int FPDFPage_GenerateContent(IntPtr page);
        [DllImport(D)] internal static extern int FPDFPage_CountObjects(IntPtr page);
        [DllImport(D)] internal static extern IntPtr FPDFPage_GetObject(IntPtr page,int index);
        [DllImport(D)] internal static extern int FPDFPage_InsertObject(IntPtr page,IntPtr obj);
        [DllImport(D)] internal static extern int FPDFPage_InsertObjectAtIndex(IntPtr page,IntPtr obj,UIntPtr index);
        [DllImport(D)] internal static extern int FPDFPage_RemoveObject(IntPtr page,IntPtr obj);
        [DllImport(D)] internal static extern void FPDFPageObj_Destroy(IntPtr obj);
        [DllImport(D)] internal static extern int FPDFPageObj_GetType(IntPtr obj);
        [DllImport(D)] internal static extern int FPDFPageObj_GetBounds(IntPtr obj,out float l,out float b,out float r,out float t);
        [DllImport(D)] internal static extern int FPDFPageObj_GetMatrix(IntPtr obj,out Matrix m);
        [DllImport(D)] internal static extern int FPDFPageObj_SetMatrix(IntPtr obj,ref Matrix m);
        [DllImport(D)] internal static extern void FPDFPageObj_Transform(IntPtr obj,double a,double b,double c,double d,double e,double f);
        [DllImport(D)] internal static extern int FPDFPageObj_GetFillColor(IntPtr obj,out uint r,out uint g,out uint b,out uint a);
        [DllImport(D)] internal static extern int FPDFPageObj_SetFillColor(IntPtr obj,uint r,uint g,uint b,uint a);
        [DllImport(D)] internal static extern int FPDFPageObj_SetStrokeColor(IntPtr obj,uint r,uint g,uint b,uint a);
        [DllImport(D)] internal static extern int FPDFPageObj_SetStrokeWidth(IntPtr obj,float width);
        [DllImport(D)] internal static extern IntPtr FPDFPageObj_CreateNewRect(float x,float y,float w,float h);
        [DllImport(D)] internal static extern int FPDFPath_SetDrawMode(IntPtr obj,int fill,int stroke);
        [DllImport(D)] internal static extern IntPtr FPDFText_LoadStandardFont(IntPtr doc,[MarshalAs(UnmanagedType.LPStr)] string name);
        [DllImport(D)] internal static extern IntPtr FPDFText_LoadFont(IntPtr doc,byte[] data,uint size,int type,int cid);
        [DllImport(D)] internal static extern IntPtr FPDFText_LoadCidType2Font(IntPtr doc,byte[] data,uint size,[MarshalAs(UnmanagedType.LPStr)] string cmap,byte[] cidToGid,uint mapSize);
        [DllImport(D)] internal static extern void FPDFFont_Close(IntPtr font);
        [DllImport(D)] internal static extern IntPtr FPDFPageObj_CreateTextObj(IntPtr doc,IntPtr font,float size);
        [DllImport(D,CharSet=CharSet.Unicode)] internal static extern int FPDFText_SetText(IntPtr obj,string text);
        [DllImport(D)] internal static extern int FPDFTextObj_GetFontSize(IntPtr obj,out float size);
        [DllImport(D)] internal static extern int FPDFTextObj_GetTextRenderMode(IntPtr obj);
        [DllImport(D)] internal static extern int FPDFTextObj_SetTextRenderMode(IntPtr obj,int mode);
        [DllImport(D)] internal static extern IntPtr FPDFText_GetTextObject(IntPtr textPage,int index);
        [DllImport(D)] internal static extern uint FPDFTextObj_GetText(IntPtr obj,IntPtr textPage,[Out] byte[] buffer,uint length);
        [DllImport(D)] internal static extern IntPtr FPDFText_LoadPage(IntPtr page);
        [DllImport(D)] internal static extern void FPDFText_ClosePage(IntPtr textPage);
        [DllImport(D)] internal static extern int FPDFText_CountChars(IntPtr textPage);
        [DllImport(D)] internal static extern int FPDFText_GetText(IntPtr textPage,int start,int count,[Out] byte[] buffer);
        [DllImport(D)] internal static extern int FPDFText_GetCharIndexAtPos(IntPtr textPage,double x,double y,double xtol,double ytol);
        [DllImport(D)] internal static extern int FPDFText_CountRects(IntPtr textPage,int start,int count);
        [DllImport(D)] internal static extern int FPDFText_GetRect(IntPtr textPage,int index,out double l,out double t,out double r,out double b);
        [DllImport(D,CharSet=CharSet.Unicode)] internal static extern IntPtr FPDFText_FindStart(IntPtr textPage,string query,uint flags,int start);
        [DllImport(D)] internal static extern int FPDFText_FindNext(IntPtr search);
        [DllImport(D)] internal static extern int FPDFText_GetSchResultIndex(IntPtr search);
        [DllImport(D)] internal static extern int FPDFText_GetSchCount(IntPtr search);
        [DllImport(D)] internal static extern void FPDFText_FindClose(IntPtr search);
        [DllImport(D)] internal static extern IntPtr FPDFPageObj_NewImageObj(IntPtr doc);
        [DllImport(D)] internal static extern int FPDFImageObj_SetBitmap(IntPtr[] pages,int count,IntPtr obj,IntPtr bitmap);
        [DllImport(D)] internal static extern int FPDF_SaveAsCopy(IntPtr doc,ref Writer writer,uint flags);
        [DllImport(D)] internal static extern IntPtr FPDFPage_CreateAnnot(IntPtr page,int subtype);
        [DllImport(D)] internal static extern int FPDFPage_GetAnnotCount(IntPtr page);
        [DllImport(D)] internal static extern IntPtr FPDFPage_GetAnnot(IntPtr page,int index);
        [DllImport(D)] internal static extern void FPDFPage_CloseAnnot(IntPtr annot);
        [DllImport(D)] internal static extern int FPDFPage_RemoveAnnot(IntPtr page,int index);
        [DllImport(D)] internal static extern int FPDFAnnot_GetSubtype(IntPtr annot);
        [DllImport(D)] internal static extern int FPDFAnnot_GetRect(IntPtr annot,out Rect rect);
        [DllImport(D)] internal static extern int FPDFAnnot_SetRect(IntPtr annot,ref Rect rect);
        [DllImport(D)] internal static extern int FPDFAnnot_SetFlags(IntPtr annot,int flags);
        [DllImport(D)] internal static extern int FPDFAnnot_SetColor(IntPtr annot,int type,uint r,uint g,uint b,uint a);
        [DllImport(D)] internal static extern int FPDFAnnot_SetBorder(IntPtr annot,float h,float v,float width);
        [DllImport(D)] internal static extern int FPDFAnnot_AppendAttachmentPoints(IntPtr annot,ref Quad points);
        [DllImport(D)] internal static extern int FPDFAnnot_AddInkStroke(IntPtr annot,Pt[] points,UIntPtr count);
        [DllImport(D,CharSet=CharSet.Ansi)] internal static extern int FPDFAnnot_SetStringValue(IntPtr annot,string key,[MarshalAs(UnmanagedType.LPWStr)] string value);
        [DllImport(D,CharSet=CharSet.Ansi)] internal static extern uint FPDFAnnot_GetStringValue(IntPtr annot,string key,[Out] byte[] buffer,uint length);
        [DllImport(D)] internal static extern IntPtr FPDFBookmark_GetFirstChild(IntPtr doc,IntPtr bookmark);
        [DllImport(D)] internal static extern IntPtr FPDFBookmark_GetNextSibling(IntPtr doc,IntPtr bookmark);
        [DllImport(D)] internal static extern uint FPDFBookmark_GetTitle(IntPtr bookmark,[Out] byte[] buffer,uint length);
        [DllImport(D)] internal static extern IntPtr FPDFBookmark_GetDest(IntPtr doc,IntPtr bookmark);
        [DllImport(D)] internal static extern IntPtr FPDFBookmark_GetAction(IntPtr bookmark);
        [DllImport(D)] internal static extern IntPtr FPDFAction_GetDest(IntPtr doc,IntPtr action);
        [DllImport(D)] internal static extern int FPDFDest_GetDestPageIndex(IntPtr doc,IntPtr dest);
        [DllImport(D)] internal static extern IntPtr FPDFLink_GetLinkAtPoint(IntPtr page,double x,double y);
        [DllImport(D)] internal static extern IntPtr FPDFLink_GetDest(IntPtr doc,IntPtr link);
        [DllImport(D)] internal static extern IntPtr FPDFLink_GetAction(IntPtr link);
        [DllImport(D)] internal static extern uint FPDFAction_GetType(IntPtr action);
        [DllImport(D)] internal static extern uint FPDFAction_GetURIPath(IntPtr doc,IntPtr action,[Out] byte[] buffer,uint length);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")] internal static extern uint GetGuiResources(IntPtr process,uint flags);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern IntPtr LoadLibraryEx(string file,IntPtr reserved,uint flags);
        [DllImport("kernel32.dll")] internal static extern bool SetDefaultDllDirectories(uint flags);
        [DllImport("gdi32.dll")] internal static extern uint GetFontData(IntPtr hdc,uint table,uint offset,[Out] byte[] buffer,uint count);
        [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] internal static extern uint GetGlyphIndicesW(IntPtr hdc,[In] char[] text,int count,[Out] ushort[] glyphs,uint flags);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr hdc,IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
    }
}
