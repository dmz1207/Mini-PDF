using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniPdf {
    internal static partial class N {
        [DllImport(D)] internal static extern IntPtr FPDFImageObj_GetBitmap(IntPtr obj);
        [DllImport(D)] internal static extern int FPDFBitmap_GetWidth(IntPtr bitmap);
        [DllImport(D)] internal static extern int FPDFBitmap_GetHeight(IntPtr bitmap);
        [DllImport(D)] internal static extern int FPDFBitmap_GetStride(IntPtr bitmap);
        [DllImport(D)] internal static extern int FPDFBitmap_GetFormat(IntPtr bitmap);
        [DllImport(D)] internal static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
    }
    internal sealed class OfficeCell {
        internal string Text,Font;
        internal float X,Right,Y,Size;
        internal Color Color;
        internal bool Bold;
    }
    internal sealed class OfficeRow {
        internal readonly List<OfficeCell> Cells=new List<OfficeCell>();
        internal float Y,Size;
    }
    internal sealed class OfficePage {
        internal SizeF Size;
        internal readonly List<OfficeRow> Rows=new List<OfficeRow>();
        internal readonly List<byte[]> Images=new List<byte[]>();
        internal int SkippedImages;
    }
    internal static class OfficeLayout {
        internal static OfficePage Read(PdfDocument document,int index,bool images) {
            var glyphs=OfficeGeometry.Read(document,index).FindAll(g=>!g.Bad);
            var result=new OfficePage{Size=document.PageSize(index)};
            glyphs.Sort((a,b)=>{int y=a.Y.CompareTo(b.Y);return y!=0?y:a.X.CompareTo(b.X);});
            var rowGlyphs=new List<List<OfficeGlyph>>();
            foreach(var glyph in glyphs) {
                List<OfficeGlyph> row=null;
                for(int j=rowGlyphs.Count-1;j>=Math.Max(0,rowGlyphs.Count-3);j--)if(Math.Abs(rowGlyphs[j][0].Y-glyph.Y)<=Math.Max(2,Math.Min(rowGlyphs[j][0].Size,glyph.Size)*.35f)){row=rowGlyphs[j];break;}
                if(row==null){row=new List<OfficeGlyph>();rowGlyphs.Add(row);}row.Add(glyph);
            }
            foreach(var line in rowGlyphs) {
                line.Sort((a,b)=>{int x=a.X.CompareTo(b.X);return x!=0?x:a.Index.CompareTo(b.Index);});var row=new OfficeRow{Y=line[0].Y,Size=line[0].Size};
                var text=new StringBuilder();OfficeGlyph first=null,prior=null;
                Action flush=delegate {if(first!=null&&!String.IsNullOrWhiteSpace(text.ToString()))row.Cells.Add(new OfficeCell{Text=text.ToString().Trim(),Font=first.Font,Size=first.Size,Color=first.Color,Bold=first.Font.IndexOf("Bold",StringComparison.OrdinalIgnoreCase)>=0,X=first.X,Right=prior.Right,Y=first.Y});text.Clear();first=null;};
                foreach(var glyph in line) {
                    if(prior!=null&&glyph.X-prior.Right>Math.Max(8,Math.Min(prior.Size,glyph.Size)*.8f))flush();
                    else if(prior!=null&&text.Length>0&&glyph.X-prior.Right>Math.Min(prior.Size,glyph.Size)*.2f&&!Char.IsWhiteSpace(text[text.Length-1])&&!Char.IsWhiteSpace(glyph.Text[0]))text.Append(' ');
                    if(first==null)first=glyph;text.Append(glyph.Text);prior=glyph;
                }
                flush();if(row.Cells.Count>0)result.Rows.Add(row);
            }
            if(images)Images(document.Page,IntPtr.Zero,0,result);
            return result;
        }
        static void Images(IntPtr page,IntPtr form,int depth,OfficePage output) {
            if(depth>8)return;int count=form==IntPtr.Zero?N.FPDFPage_CountObjects(page):N.FPDFFormObj_CountObjects(form);
            for(int i=0;i<Math.Min(count,10000);i++) {
                IntPtr obj=form==IntPtr.Zero?N.FPDFPage_GetObject(page,i):N.FPDFFormObj_GetObject(form,(uint)i);int type=N.FPDFPageObj_GetType(obj);
                if(type==5){Images(page,obj,depth+1,output);continue;}if(type!=3)continue;
                // Full-page scans with a text layer export as editable text, avoiding a duplicate scan.
                float l,b,r,t;if(output.Rows.Count>0&&N.FPDFPageObj_GetBounds(obj,out l,out b,out r,out t)!=0&&(r-l)*(t-b)>output.Size.Width*output.Size.Height*.65f)continue;
                IntPtr native=N.FPDFImageObj_GetBitmap(obj);if(native==IntPtr.Zero){output.SkippedImages++;continue;}
                try {
                    int w=N.FPDFBitmap_GetWidth(native),h=N.FPDFBitmap_GetHeight(native),stride=N.FPDFBitmap_GetStride(native),format=N.FPDFBitmap_GetFormat(native);
                    if(w<1||h<1||(long)w*h>16000000||output.Images.Count>=128){output.SkippedImages++;continue;}
                    int bytes=format==1?1:format==2?3:format==3||format==4?4:0;if(bytes==0){output.SkippedImages++;continue;}
                    var source=N.FPDFBitmap_GetBuffer(native);var row=new byte[stride];var target=new byte[w*4];
                    using(var bitmap=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
                        var data=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                        try{for(int y=0;y<h;y++){Marshal.Copy(IntPtr.Add(source,y*stride),row,0,stride);for(int x=0;x<w;x++){int p=x*bytes,q=x*4;target[q]=row[p];target[q+1]=bytes==1?row[p]:row[p+1];target[q+2]=bytes==1?row[p]:row[p+2];target[q+3]=format==4?row[p+3]:(byte)255;}Marshal.Copy(target,0,IntPtr.Add(data.Scan0,y*data.Stride),target.Length);}}
                        finally{bitmap.UnlockBits(data);}
                        using(var stream=new System.IO.MemoryStream()){bitmap.Save(stream,ImageFormat.Png);output.Images.Add(stream.ToArray());}
                    }
                } finally {N.FPDFBitmap_Destroy(native);}
            }
        }
        internal static void FormValues(PdfDocument document,OfficePage page,PdfForms forms,int index) {
            if(forms==null)return;
            foreach(var field in forms.Fields)if(field.Supported)foreach(var widget in field.Widgets)if(widget.Page==index&&(widget.Flags&(2|32))==0) {
                string value=field.Kind==FieldKind.Check?(field.Values.Contains(widget.On)?"☑":"☐"):field.Kind==FieldKind.Radio?(field.Values.Contains(widget.On)?"●":"○"):field.Kind==FieldKind.Password?new string('*',field.Value.Length):field.Value;
                if(field.Kind>=FieldKind.Combo&&field.Kind<=FieldKind.MultiList)value=String.Join("\n",field.Values.Select(v=>{var choice=field.Choices.Find(c=>c.Value==v);return choice==null?v:choice.Label;}));
                var a=document.ToDevice(widget.Bounds.left,widget.Bounds.top,(int)Math.Ceiling(page.Size.Width),(int)Math.Ceiling(page.Size.Height));var b=document.ToDevice(widget.Bounds.right,widget.Bounds.bottom,(int)Math.Ceiling(page.Size.Width),(int)Math.Ceiling(page.Size.Height));float size=field.Size>0?field.Size:Math.Min(12,Math.Max(4,widget.Bounds.top-widget.Bounds.bottom-6));int line=0;
                foreach(string text in value.Replace("\r","").Split('\n')){if(text.Length>0){var row=new OfficeRow{Y=Math.Min(a.Y,b.Y)+4+size/2+line*size*1.25f,Size=size};row.Cells.Add(new OfficeCell{Text=text,Font="Arial",Size=size,Color=Color.Black,X=Math.Min(a.X,b.X)+4,Right=Math.Max(a.X,b.X)-4,Y=row.Y});page.Rows.Add(row);}line++;}
            }
            page.Rows.Sort((a,b)=>a.Y.CompareTo(b.Y));
        }
        internal static List<OfficeRow> ReadingOrder(OfficePage page) {
            float middle=page.Size.Width/2;
            // A repeated pair of long text columns is prose, rather than a table.
            int prose=page.Rows.Count(r=>r.Cells.Count==2&&r.Cells.All(c=>c.Text.Length>35)&&r.Cells[0].Right<middle+2&&r.Cells[1].X>middle-2);
            if(prose<6)return page.Rows;
            var output=new List<OfficeRow>();var band=new List<OfficeRow>();
            Action flush=delegate {
                for(int side=0;side<2;side++)foreach(var source in band) {
                    var cells=source.Cells.Where(c=>side==0?c.X<middle:c.X>=middle).ToList();
                    if(cells.Count==0)continue;var row=new OfficeRow{Y=source.Y,Size=source.Size};row.Cells.AddRange(cells);output.Add(row);
                }
                band.Clear();
            };
            foreach(var row in page.Rows) {
                bool spanning=row.Cells.Any(c=>c.X<middle-4&&c.Right>middle+4);
                if(spanning){flush();output.Add(row);}else band.Add(row);
            }
            flush();return output;
        }
        internal static List<float> Columns(OfficePage page,bool detect) {
            var columns=new List<float>();if(!detect){columns.Add(0);return columns;}
            foreach(var row in page.Rows)if(row.Cells.Count>1)foreach(var cell in row.Cells) {
                bool near=false;foreach(float x in columns)if(Math.Abs(x-cell.X)<8){near=true;break;}if(!near)columns.Add(cell.X);
            }
            if(columns.Count==0)columns.Add(0);columns.Sort();
            if(columns.Count>256)throw new InvalidOperationException(UiText.Get("检测到过多列，请使用单列文本模式。"));return columns;
        }
    }
}
