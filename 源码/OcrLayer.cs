using System;
using System.Collections.Generic;
namespace MiniPdf {
    internal sealed partial class PdfDocument {
        internal void AddOcrLayer(IList<OcrLine> lines) {
            Check(lines!=null&&lines.Count>0,"No recognized text to insert.");int original=Index;
            Edit(delegate {
                int current=-1;
                foreach(var line in lines) {
                    Check(line.Page>=0&&line.Page<Count&&line.Corners!=null&&line.Corners.Length==4,"Invalid OCR location.");if(current!=line.Page){if(current>=0)Check(N.FPDFPage_GenerateContent(Page)!=0,"Cannot generate the OCR layer.");OpenPage(line.Page);current=line.Page;for(int i=N.FPDFPage_CountObjects(Page)-1;i>=0;i--){IntPtr old=N.FPDFPage_GetObject(Page,i);if(HasMark(old,"MiniPDF.OCR")){Check(N.FPDFPage_RemoveObject(Page,old)!=0,"Cannot replace the prior OCR layer.");N.FPDFPageObj_Destroy(old);}}}
                    Check(!String.IsNullOrWhiteSpace(line.Text)&&line.Text.Length<=10000,"Invalid OCR text.");foreach(var point in line.Corners)Check(!Single.IsNaN(point.X)&&!Single.IsInfinity(point.X)&&!Single.IsNaN(point.Y)&&!Single.IsInfinity(point.Y)&&Math.Abs(point.X)<=2&&Math.Abs(point.Y)<=2,"Invalid OCR coordinates.");
                    var points=new N.Pt[4];for(int i=0;i<4;i++){double x,y;N.FPDF_DeviceToPage(Page,0,0,10000,10000,0,(int)Math.Round(line.Corners[i].X*10000),(int)Math.Round(line.Corners[i].Y*10000),out x,out y);points[i]=new N.Pt((float)x,(float)y);}
                    IntPtr obj=N.FPDFPageObj_CreateTextObj(Handle,FontFor(line.Text,"自动"),1);Check(obj!=IntPtr.Zero,"Cannot create the OCR text.");
                    try {
                        Check(N.FPDFText_SetText(obj,line.Text)!=0&&N.FPDFTextObj_SetTextRenderMode(obj,3)!=0,"Cannot set invisible OCR text.");var bounds=ObjectBounds(obj);double w=bounds.right-bounds.left,h=bounds.top-bounds.bottom;Check(w>0&&h>0,"Invalid OCR text bounds.");
                        var matrix=new N.Matrix{a=(float)((points[2].x-points[3].x)/w),b=(float)((points[2].y-points[3].y)/w),c=(float)((points[0].x-points[3].x)/h),d=(float)((points[0].y-points[3].y)/h)};matrix.e=points[3].x-matrix.a*bounds.left-matrix.c*bounds.bottom;matrix.f=points[3].y-matrix.b*bounds.left-matrix.d*bounds.bottom;
                        Check(N.FPDFPageObj_SetMatrix(obj,ref matrix)!=0&&N.FPDFPageObj_AddMark(obj,"MiniPDF.OCR")!=IntPtr.Zero,"Cannot position the OCR text.");Check(N.FPDFPage_InsertObject(Page,obj)!=0,"Cannot insert the OCR text.");obj=IntPtr.Zero;
                    }finally{if(obj!=IntPtr.Zero)N.FPDFPageObj_Destroy(obj);}
                }
                Check(N.FPDFPage_GenerateContent(Page)!=0,"Cannot generate the OCR layer.");OpenPage(original);
            });
        }
    }
}
