using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace MiniPdf {
    internal static partial class N {
        [DllImport(D)] internal static extern int FPDFFont_GetFlags(IntPtr font);
        [DllImport(D)] internal static extern int FPDFText_IsHyphen(IntPtr page,int index);
        [DllImport(D)] internal static extern int FPDFText_GetMatrix(IntPtr page,int index,out Matrix matrix);
        [DllImport(D)] internal static extern int FPDFText_GetCharBox(IntPtr page,int index,out double left,out double right,out double bottom,out double top);
    }
    internal sealed class OfficeGlyph {
        internal IntPtr Object;
        internal int Index,FontFlags;
        internal string Text,Font;
        internal float X,Right,Y,Top,Bottom,Size;
        internal Color Color;
        internal bool Bad,Unsupported;
    }
    internal static class OfficeGeometry {
        // PageToDevice uses integers. A 1/1000 point grid retains sub-point positions,
        // including CropBox offsets and page rotation, without rounding text to pixels.
        internal static PointF Point(PdfDocument document,double x,double y,SizeF size) {
            int width=(int)Math.Ceiling(size.Width*1000),height=(int)Math.Ceiling(size.Height*1000),dx=0,dy=0;
            PdfDocument.Check(width>0&&height>0&&N.FPDF_PageToDevice(document.Page,0,0,width,height,0,x,y,out dx,out dy)!=0,"Cannot read PDF coordinates.");
            return new PointF(dx*size.Width/width,dy*size.Height/height);
        }
        internal static List<OfficeGlyph> Read(PdfDocument document,int index) {
            document.OpenPage(index);var size=document.PageSize(index);
            int count=N.FPDFText_CountChars(document.TextPage);
            PdfDocument.Check(count>=0&&count<=500000,UiText.Get("此页文字过多，无法转换。"));
            var glyphs=new List<OfficeGlyph>();var fonts=new Dictionary<IntPtr,string>();var flags=new Dictionary<IntPtr,int>();
            for(int i=0;i<count;i++) {
                IntPtr obj=N.FPDFText_GetTextObject(document.TextPage,i);
                if(obj==IntPtr.Zero)continue;
                uint ch=N.FPDFText_GetUnicode(document.TextPage,i);
                if(N.FPDFText_IsHyphen(document.TextPage,i)==1)ch=45;
                if(ch==10||ch==13)continue;
                bool generated=N.FPDFText_IsGenerated(document.TextPage,i)==1;
                N.Rect loose;double ox,oy,l,r,b,t;
                bool valid=ch>=32&&ch!=0xfffe&&ch!=0xffff&&ch<=0x10ffff&&(ch<0xd800||ch>0xdfff);
                if(N.FPDFText_GetLooseCharBox(document.TextPage,i,out loose)==0||
                   N.FPDFText_GetCharOrigin(document.TextPage,i,out ox,out oy)==0)continue;
                if(N.FPDFText_GetCharBox(document.TextPage,i,out l,out r,out b,out t)==0){l=loose.left;r=loose.right;b=loose.bottom;t=loose.top;}
                var start=Point(document,ox,oy,size);var a=Point(document,l,t,size);var z=Point(document,r,b,size);
                var end=Point(document,loose.right,loose.bottom,size);
                float em=12;N.Matrix matrix;N.FPDFTextObj_GetFontSize(obj,out em);
                if((generated?N.FPDFPageObj_GetMatrix(obj,out matrix):N.FPDFText_GetMatrix(document.TextPage,i,out matrix))!=0)em*=(float)Math.Sqrt(matrix.c*matrix.c+matrix.d*matrix.d);
                bool unsupported=Single.IsNaN(em)||Single.IsInfinity(em)||em<1||em>200;
                if(Single.IsNaN(em)||Single.IsInfinity(em)||em<.1f)em=Math.Max(1,Math.Abs(z.Y-a.Y));
                IntPtr font=N.FPDFTextObj_GetFont(obj);string name;
                if(!fonts.TryGetValue(font,out name)){name=PdfDocument.TextFontName(font);fonts[font]=name;flags[font]=Math.Max(0,N.FPDFFont_GetFlags(font));}
                uint red,green,blue,alpha;var ink=N.FPDFPageObj_GetFillColor(obj,out red,out green,out blue,out alpha)!=0?Color.FromArgb((int)alpha,(int)red,(int)green,(int)blue):Color.Black;
                glyphs.Add(new OfficeGlyph{Unsupported=unsupported,FontFlags=flags[font],Index=i,Object=obj,Text=valid?Char.ConvertFromUtf32((int)ch):"",Bad=!valid||N.FPDFText_HasUnicodeMapError(document.TextPage,i)==1,Font=name,
                    X=start.X,Right=Math.Max(start.X,end.X),Y=start.Y,Top=Math.Min(a.Y,z.Y),Bottom=Math.Max(a.Y,z.Y),Size=Math.Max(1,Math.Min(200,em)),Color=ink});
            }
            return glyphs;
        }
    }
}
