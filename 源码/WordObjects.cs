using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace MiniPdf {
    internal static partial class N {
        [DllImport(D)] internal static extern int FPDFFormObj_RemoveObject(IntPtr form,IntPtr obj);
        [DllImport(D)] internal static extern int FPDFClipPath_CountPathSegments(IntPtr clip,int path);
        [DllImport(D)] internal static extern IntPtr FPDFClipPath_GetPathSegment(IntPtr clip,int path,int segment);
        [DllImport(D)] internal static extern int FPDFPathSegment_GetType(IntPtr segment);
        [DllImport(D)] internal static extern int FPDFPathSegment_GetPoint(IntPtr segment,out float x,out float y);
    }
    internal static class WordObjects {
        static N.Matrix Multiply(N.Matrix a,N.Matrix b) {
            return new N.Matrix{a=a.a*b.a+a.c*b.b,b=a.b*b.a+a.d*b.b,c=a.a*b.c+a.c*b.d,d=a.b*b.c+a.d*b.d,e=a.a*b.e+a.c*b.f+a.e,f=a.b*b.e+a.d*b.f+a.f};
        }
        static PointF Point(PdfDocument doc,N.Matrix matrix,float x,float y,SizeF size) {
            return OfficeGeometry.Point(doc,matrix.a*x+matrix.c*y+matrix.e,matrix.b*x+matrix.d*y+matrix.f,size);
        }
        static RectangleF Bounds(PdfDocument doc,IntPtr obj,N.Matrix parent,SizeF size) {
            float l,b,r,t;if(N.FPDFPageObj_GetBounds(obj,out l,out b,out r,out t)==0)return RectangleF.Empty;
            var points=new[]{Point(doc,parent,l,b,size),Point(doc,parent,l,t,size),Point(doc,parent,r,b,size),Point(doc,parent,r,t,size)};
            return RectangleF.FromLTRB(points.Min(p=>p.X),points.Min(p=>p.Y),points.Max(p=>p.X),points.Max(p=>p.Y));
        }
        internal static Dictionary<IntPtr,int> Read(PdfDocument doc,WordPage page,List<OfficeGlyph> glyphs,bool nested) {
            foreach(var group in glyphs.GroupBy(g=>g.Object)) {
                string text=String.Concat(group.Select(g=>g.Text));
                if(System.Text.RegularExpressions.Regex.Matches(text,@"[A-Za-z]{3,}").Count>=2||text.Count(c=>c>=0x3400&&c<=0x9fff)>=4)page.ProseLetters[group.Key]=text.Count(Char.IsLetter);
            }
            var objects=new Dictionary<IntPtr,int>();
            Action<IntPtr,N.Matrix,int> visit=null;
            visit=delegate(IntPtr form,N.Matrix parent,int depth) {
                int count=form==IntPtr.Zero?N.FPDFPage_CountObjects(doc.Page):N.FPDFFormObj_CountObjects(form);
                PdfDocument.Check(count>=0&&count<=50000&&objects.Count+count<=50000,"This page contains too many objects to convert.");
                for(int i=0;i<count;i++) {
                    IntPtr obj=form==IntPtr.Zero?N.FPDFPage_GetObject(doc.Page,i):N.FPDFFormObj_GetObject(form,(uint)i);
                    if(objects.ContainsKey(obj))continue;
                    objects[obj]=objects.Count;page.Parents[obj]=form;page.ParentMatrices[obj]=parent;page.ObjectBounds[obj]=Bounds(doc,obj,parent,page.Size);
                    if(nested&&N.FPDFPageObj_GetType(obj)==5&&depth<8) {
                        N.Matrix matrix;if(N.FPDFPageObj_GetMatrix(obj,out matrix)==0)continue;
                        int first=objects.Count;visit(obj,Multiply(parent,matrix),depth+1);
                        var descendants=new HashSet<IntPtr>(objects.Where(p=>p.Value>=first).Select(p=>p.Key));
                        int letters=glyphs.Where(g=>descendants.Contains(g.Object)).Sum(g=>g.Text.Count(Char.IsLetter));
                        if(letters>=160&&page.ObjectBounds[obj].Width*page.ObjectBounds[obj].Height>page.Size.Width*page.Size.Height*.12f)page.TextForms.Add(obj);
                    }
                }
            };
            visit(IntPtr.Zero,new N.Matrix{a=1,d=1},0);return objects;
        }
        static bool ClipContains(PdfDocument doc,IntPtr obj,RectangleF target,N.Matrix parent,SizeF size) {
            IntPtr clip=N.FPDFPageObj_GetClipPath(obj);if(clip==IntPtr.Zero)return true;
            int count=N.FPDFClipPath_CountPaths(clip);if(count<=0)return true;if(count>8)return false;
            for(int i=0;i<count;i++) {
                int segments=N.FPDFClipPath_CountPathSegments(clip,i);if(segments<4||segments>5)return false;
                var points=new List<PointF>();
                for(int j=0;j<segments;j++) {
                    IntPtr segment=N.FPDFClipPath_GetPathSegment(clip,i,j);float x,y;
                    int kind=N.FPDFPathSegment_GetType(segment);
                    if((j==0?kind!=2:kind!=0)||N.FPDFPathSegment_GetPoint(segment,out x,out y)==0)return false;
                    points.Add(Point(doc,parent,x,y,size));
                }
                if(points.Count==5&&Math.Abs(points[0].X-points[4].X)<.05f&&Math.Abs(points[0].Y-points[4].Y)<.05f)points.RemoveAt(4);
                if(points.Count!=4)return false;
                for(int j=0;j<4;j++){var a=points[j];var b=points[(j+1)%4];if(Math.Abs(a.X-b.X)>.05f&&Math.Abs(a.Y-b.Y)>.05f)return false;}
                var box=RectangleF.FromLTRB(points.Min(p=>p.X),points.Min(p=>p.Y),points.Max(p=>p.X),points.Max(p=>p.Y));
                if(target.Left<box.Left-.05f||target.Top<box.Top-.05f||target.Right>box.Right+.05f||target.Bottom>box.Bottom+.05f)return false;
            }
            return true;
        }
        internal static bool Visible(PdfDocument doc,WordPage page,IntPtr obj) {
            RectangleF box=page.ObjectBounds[obj];IntPtr current=obj;
            for(int depth=0;current!=IntPtr.Zero&&depth<=8;depth++) {
                N.Matrix parent;if(!page.ParentMatrices.TryGetValue(current,out parent))return false;
                if(!ClipContains(doc,current,box,parent,page.Size)||PdfDocument.HasMark(current,"OC"))return false;
                uint r,g,b,a;if(N.FPDFPageObj_GetFillColor(current,out r,out g,out b,out a)!=0&&a==0)return false;
                current=page.Parents[current];
            }
            return current==IntPtr.Zero;
        }
    }
}
