using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniPdf {
    internal struct RedactionRegion {internal int Page;internal N.Rect Bounds;}
    internal struct RedactionGlyph {internal int Unicode;internal float X,Y,A,B,C,D;}
    internal sealed class RedactionPage {
        internal int Width,Height,Length;
        internal SizeF Size;
        internal byte[] Image;
        internal readonly List<RedactionGlyph> Glyphs=new List<RedactionGlyph>();
    }
    internal static class RedactionExport {
        internal const int MaxImageBytes=96*1024*1024,MaxGlyphs=200000,MaxRegions=10000;
        internal static bool Finite(float value){return !Single.IsNaN(value)&&!Single.IsInfinity(value);}
        internal static bool ValidUnicode(int value){return value>=32&&value<=0x10ffff&&value!=0xfffe&&value!=0xffff&&!(value>=0xd800&&value<=0xdfff);}
        internal static void Validate(PdfDocument document,RedactionRegion[] regions,int dpi){
            PdfDocument.Check(document.CanPrint&&!document.CompatibilityPreview,UiText.Get("此文档不能导出脱敏副本。"));
            PdfDocument.Check(dpi==300||dpi==600,UiText.Get("请选择 300 或 600 dpi。"));
            PdfDocument.Check(document.Count>0&&document.Count<=10000&&regions!=null&&regions.Length>0&&regions.Length<=MaxRegions,UiText.Get("请先标记需要永久涂黑的区域。"));
            foreach(var region in regions){var r=region.Bounds;PdfDocument.Check(region.Page>=0&&region.Page<document.Count&&Finite(r.left)&&Finite(r.bottom)&&Finite(r.right)&&Finite(r.top)&&r.right>r.left&&r.top>r.bottom&&Math.Abs(r.left)<1000000&&Math.Abs(r.right)<1000000&&Math.Abs(r.top)<1000000&&Math.Abs(r.bottom)<1000000,UiText.Get("涂黑区域无效，请重新标记。"));}
        }
        static Rectangle Pixels(PdfDocument document,N.Rect r,int width,int height){
            var points=new[]{document.ToDevice(r.left,r.bottom,width,height),document.ToDevice(r.left,r.top,width,height),document.ToDevice(r.right,r.bottom,width,height),document.ToDevice(r.right,r.top,width,height)};
            int l=points[0].X,t=points[0].Y,right=l,b=t;foreach(var p in points){l=Math.Min(l,p.X);t=Math.Min(t,p.Y);right=Math.Max(right,p.X);b=Math.Max(b,p.Y);}return Rectangle.FromLTRB(l,t,right,b);
        }
        static RedactionGlyph Glyph(PdfDocument document,int index,int unicode,N.Rect bounds,Rectangle box,RedactionPage page){
            N.Matrix matrix;if(N.FPDFText_GetMatrix(document.TextPage,index,out matrix)==0)matrix=new N.Matrix{a=1,d=1};
            double u=Math.Sqrt(matrix.a*matrix.a+matrix.b*matrix.b),v=Math.Sqrt(matrix.c*matrix.c+matrix.d*matrix.d);
            if(u<.00001||v<.00001)return new RedactionGlyph();
            double x=(bounds.left+bounds.right)/2.0,y=(bounds.bottom+bounds.top)/2.0;
            var origin=OfficeGeometry.Point(document,x,y,page.Size);var along=OfficeGeometry.Point(document,x+matrix.a/u,y+matrix.b/u,page.Size);var up=OfficeGeometry.Point(document,x+matrix.c/v,y+matrix.d/v,page.Size);
            double ux=along.X-origin.X,uy=origin.Y-along.Y,vx=up.X-origin.X,vy=origin.Y-up.Y;
            u=Math.Sqrt(ux*ux+uy*uy);v=Math.Sqrt(vx*vx+vy*vy);if(u<.00001||v<.00001)return new RedactionGlyph();ux/=u;uy/=u;vx/=v;vy/=v;
            double width=box.Width*page.Size.Width/page.Width,height=box.Height*page.Size.Height/page.Height;
            double advance=width*Math.Abs(ux)+height*Math.Abs(uy),em=width*Math.Abs(vx)+height*Math.Abs(vy);
            double a=ux*advance,b=uy*advance,c=vx*em,d=vy*em;
            double fit=Math.Min(width/Math.Max(.00001,Math.Abs(a)+Math.Abs(c)),height/Math.Max(.00001,Math.Abs(b)+Math.Abs(d)));a*=fit;b*=fit;c*=fit;d*=fit;
            return new RedactionGlyph{Unicode=unicode,A=(float)a,B=(float)b,C=(float)c,D=(float)d,X=box.Left*page.Size.Width/page.Width-(float)Math.Min(0,Math.Min(a,Math.Min(c,a+c))),Y=(page.Height-box.Bottom)*page.Size.Height/page.Height-(float)Math.Min(0,Math.Min(b,Math.Min(d,b+d)))};
        }
        internal static RedactionPage Render(PdfDocument document,int index,RedactionRegion[] regions,int dpi,bool keepText,Action heartbeat=null){
            document.OpenPage(index);var size=document.PageSize(index);
            double wide=Math.Ceiling(size.Width*dpi/72.0),high=Math.Ceiling(size.Height*dpi/72.0);
            PdfDocument.Check(wide>=1&&wide<=14000&&high>=1&&high<=20000&&wide*high<=200000000,UiText.Get("页面超出此精度的处理范围，请选择 300 dpi。"));
            var page=new RedactionPage{Width=(int)wide,Height=(int)high,Size=size};var masks=new List<Rectangle>();
            foreach(var region in regions)if(region.Page==index){var box=Pixels(document,region.Bounds,page.Width,page.Height);box.Inflate(2,2);box.Intersect(new Rectangle(0,0,page.Width,page.Height));PdfDocument.Check(box.Width>0&&box.Height>0,UiText.Get("涂黑区域不在页面内，请重新标记。"));masks.Add(box);}
            if(keepText){
                var textMasks=new List<Rectangle>(masks);
                // Existing opaque Mini PDF fills must not regain their hidden text in a searchable copy.
                for(int p=0;p<N.FPDFPage_CountObjects(document.Page);p++){
                    IntPtr obj=N.FPDFPage_GetObject(document.Page,p);int fill,stroke;uint r,g,b,a;
                    if(N.FPDFPageObj_GetType(obj)==2&&PdfDocument.HasMark(obj,PdfDocument.RectangleTag)&&N.FPDFPath_GetDrawMode(obj,out fill,out stroke)!=0&&fill!=0&&N.FPDFPageObj_GetFillColor(obj,out r,out g,out b,out a)!=0&&a==255)textMasks.Add(Pixels(document,PdfDocument.ObjectBounds(obj),page.Width,page.Height));
                }
                int count=N.FPDFText_CountChars(document.TextPage);PdfDocument.Check(count<=MaxGlyphs,UiText.Get("页面文字过多，请选择高清图像型副本。"));
                for(int i=0;i<count;i++){
                    int unicode=(int)N.FPDFText_GetUnicode(document.TextPage,i);N.Rect bounds;
                    if(!ValidUnicode(unicode)||(unicode!=32&&N.FPDFText_GetTextObject(document.TextPage,i)==IntPtr.Zero)||N.FPDFText_GetLooseCharBox(document.TextPage,i,out bounds)==0||!Finite(bounds.left)||!Finite(bounds.bottom)||!Finite(bounds.right)||!Finite(bounds.top))continue;
                    var box=Pixels(document,bounds,page.Width,page.Height);
                    if(box.Width<=0||box.Height<=0||box.Left<0||box.Top<0||box.Right>page.Width||box.Bottom>page.Height)continue;
                    bool hidden=false;var expanded=box;expanded.Inflate(Math.Max(2,dpi/72),Math.Max(2,dpi/72));foreach(var mask in textMasks)if(mask.IntersectsWith(expanded)){hidden=true;break;}if(hidden)continue;
                    var glyph=Glyph(document,i,unicode,bounds,box,page);if(glyph.Unicode!=0)page.Glyphs.Add(glyph);
                }
            }
            using(var compressed=new MemoryStream()){
                compressed.WriteByte(0x78);compressed.WriteByte(0x9c);uint s1=1,s2=0;
                using(var deflate=new DeflateStream(compressed,CompressionLevel.Optimal,true)){
                    for(int y=0;y<page.Height;y+=512){
                        if(heartbeat!=null)heartbeat();int rows=Math.Min(512,page.Height-y);
                        using(var strip=document.RenderRegion(index,page.Width,page.Height,0,new Rectangle(0,y,page.Width,rows))){
                            using(var graphics=Graphics.FromImage(strip)){graphics.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;foreach(var mask in masks){var local=mask;local.Offset(0,-y);graphics.FillRectangle(Brushes.Black,local);}}
                            var bits=strip.LockBits(new Rectangle(0,0,strip.Width,strip.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                            try{
                                var bgra=new byte[page.Width*4];var rgb=new byte[page.Width*3];
                                for(int row=0;row<rows;row++){
                                    Marshal.Copy(IntPtr.Add(bits.Scan0,row*bits.Stride),bgra,0,bgra.Length);
                                    for(int x=0,p=0;x<bgra.Length;x+=4){rgb[p++]=bgra[x+2];rgb[p++]=bgra[x+1];rgb[p++]=bgra[x];}
                                    for(int start=0;start<rgb.Length;start+=5552){int end=Math.Min(start+5552,rgb.Length);for(int p=start;p<end;p++){s1+=rgb[p];s2+=s1;}s1%=65521;s2%=65521;}
                                    deflate.Write(rgb,0,rgb.Length);
                                }
                            }finally{strip.UnlockBits(bits);}
                        }
                        PdfDocument.Check(compressed.Length<=MaxImageBytes,UiText.Get("页面图像过大，请选择 300 dpi。"));
                    }
                }
                uint adler=s2<<16|s1;for(int shift=24;shift>=0;shift-=8)compressed.WriteByte((byte)(adler>>shift));
                PdfDocument.Check(compressed.Length<=MaxImageBytes,UiText.Get("页面图像过大，请选择 300 dpi。"));page.Length=(int)compressed.Length;page.Image=compressed.GetBuffer();return page;
            }
        }
        internal static void Work(PdfDocument document,BinaryReader reader,BinaryWriter writer){
            int dpi=reader.ReadInt32();bool keep=reader.ReadBoolean();int count=reader.ReadInt32();PdfDocument.Check(count>0&&count<=MaxRegions,"Invalid redaction count.");var regions=new RedactionRegion[count];for(int i=0;i<count;i++)regions[i]=new RedactionRegion{Page=reader.ReadInt32(),Bounds=new N.Rect(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle())};Validate(document,regions,dpi);
            for(int i=0;i<document.Count;i++){
                int index=i;Action pulse=delegate{writer.Write(0);writer.Write(index);writer.Write(document.Count);writer.Flush();};
                var page=Render(document,i,regions,dpi,keep,pulse);writer.Write(1);writer.Write(i);writer.Write(page.Width);writer.Write(page.Height);writer.Write(page.Size.Width);writer.Write(page.Size.Height);writer.Write(page.Glyphs.Count);
                foreach(var glyph in page.Glyphs){writer.Write(glyph.Unicode);writer.Write(glyph.X);writer.Write(glyph.Y);writer.Write(glyph.A);writer.Write(glyph.B);writer.Write(glyph.C);writer.Write(glyph.D);}writer.Write(page.Length);writer.Write(page.Image,0,page.Length);writer.Flush();
            }
            writer.Write(2);writer.Write(document.Count);writer.Flush();
        }
        internal static void SaveForTest(PdfDocument document,RedactionRegion[] regions,int dpi,bool keep,string path){Validate(document,regions,dpi);int active=document.Index;try{using(var file=File.Create(path))using(var writer=new RedactionPdfWriter(file,document.Count,keep)){for(int i=0;i<document.Count;i++){var page=Render(document,i,regions,dpi,keep);writer.Page(page,delegate(Stream output){output.Write(page.Image,0,page.Length);});}writer.Complete();}}finally{document.OpenPage(active);}}
    }

    // Fresh, allowlisted PDF objects only. Original streams, fonts and metadata never enter this writer.
    internal sealed class RedactionPdfWriter : IDisposable {
        readonly Stream stream;
        readonly int count,font;
        readonly long[] offsets;
        readonly Dictionary<int,int> codes=new Dictionary<int,int>();
        int written;
        bool completed;
        internal RedactionPdfWriter(Stream output,int pages,bool keepText){
            PdfDocument.Check(pages>0&&pages<=10000,"Invalid redacted page count.");stream=output;count=pages;font=keepText?3+3*pages:0;offsets=new long[3+3*pages+(keepText?6:0)];Text("%PDF-1.7\n%MiniPDF sanitized copy\n");Object(1,"<< /Type /Catalog /Pages 2 0 R >>");var kids=new StringBuilder("<< /Type /Pages /Count "+pages+" /Kids [");for(int i=0;i<pages;i++)kids.Append(3+3*i).Append(" 0 R ");Object(2,kids.Append("] >>").ToString());
        }
        static string Number(float v){PdfDocument.Check(RedactionExport.Finite(v)&&Math.Abs(v)<=1000000,"Invalid page geometry.");return v.ToString("0.######",CultureInfo.InvariantCulture);}
        void Text(string text){var bytes=Encoding.ASCII.GetBytes(text);stream.Write(bytes,0,bytes.Length);}
        void Begin(int id){PdfDocument.Check(id>0&&id<offsets.Length&&offsets[id]==0,"Invalid output object.");offsets[id]=stream.Position;Text(id+" 0 obj\n");}
        void Object(int id,string body){Begin(id);Text(body+"\nendobj\n");}
        void Data(int id,string dictionary,byte[] bytes){Begin(id);Text("<< "+dictionary+" /Length "+bytes.Length+" >>\nstream\n");stream.Write(bytes,0,bytes.Length);Text("\nendstream\nendobj\n");}
        internal void Page(RedactionPage page,Action<Stream> image){
            PdfDocument.Check(!completed&&written<count&&page.Width>0&&page.Width<=14000&&page.Height>0&&page.Height<=20000&&(long)page.Width*page.Height<=200000000&&page.Size.Width>0&&page.Size.Height>0&&page.Length>0&&page.Length<=RedactionExport.MaxImageBytes&&page.Glyphs.Count<=RedactionExport.MaxGlyphs,"Invalid redacted page.");
            int id=3+3*written;Object(id,"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 "+Number(page.Size.Width)+" "+Number(page.Size.Height)+"] /Resources << /XObject << /Im "+(id+2)+" 0 R >>"+(font==0?"":" /Font << /F "+font+" 0 R >>")+" >> /Contents "+(id+1)+" 0 R >>");
            var content=new StringBuilder("q "+Number(page.Size.Width)+" 0 0 "+Number(page.Size.Height)+" 0 0 cm /Im Do Q\n");
            if(font!=0&&page.Glyphs.Count>0){content.Append("BT /F 1 Tf 3 Tr\n");foreach(var glyph in page.Glyphs){PdfDocument.Check(RedactionExport.ValidUnicode(glyph.Unicode),"Invalid output glyph.");int code;if(!codes.TryGetValue(glyph.Unicode,out code)){PdfDocument.Check(codes.Count<65534,"Too many text characters; use image-only output.");code=codes.Count+1;codes.Add(glyph.Unicode,code);}content.Append(Number(glyph.A)).Append(' ').Append(Number(glyph.B)).Append(' ').Append(Number(glyph.C)).Append(' ').Append(Number(glyph.D)).Append(' ').Append(Number(glyph.X)).Append(' ').Append(Number(glyph.Y)).Append(" Tm <").Append(code.ToString("X4",CultureInfo.InvariantCulture)).Append("> Tj\n");}content.Append("ET\n");}
            Data(id+1,"",Encoding.ASCII.GetBytes(content.ToString()));Begin(id+2);Text("<< /Type /XObject /Subtype /Image /Width "+page.Width+" /Height "+page.Height+" /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length "+page.Length+" >>\nstream\n");long start=stream.Position;image(stream);PdfDocument.Check(stream.Position-start==page.Length,"Incomplete image response.");Text("\nendstream\nendobj\n");written++;
        }
        internal void Complete(){
            PdfDocument.Check(!completed&&written==count,"Incomplete redacted document.");
            if(font!=0){
                Object(font,"<< /Type /Font /Subtype /Type0 /BaseFont /MiniPDFText /Encoding /Identity-H /DescendantFonts ["+(font+1)+" 0 R] /ToUnicode "+(font+3)+" 0 R >>");
                Object(font+1,"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /MiniPDFText /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor "+(font+2)+" 0 R /DW 1000 /CIDToGIDMap "+(font+5)+" 0 R >>");
                Object(font+2,"<< /Type /FontDescriptor /FontName /MiniPDFText /Flags 4 /FontBBox [0 0 1000 1000] /ItalicAngle 0 /Ascent 1000 /Descent 0 /CapHeight 1000 /StemV 80 /MissingWidth 1000 /FontFile2 "+(font+4)+" 0 R >>");
                var cmap=new StringBuilder("/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def /CMapName /MiniPDFText def /CMapType 2 def 1 begincodespacerange <0000> <FFFF> endcodespacerange\n");
                var entries=new List<KeyValuePair<int,int>>(codes);for(int start=0;start<entries.Count;start+=100){int end=Math.Min(entries.Count,start+100);cmap.Append(end-start).Append(" beginbfchar\n");for(int i=start;i<end;i++){var entry=entries[i];string unicode=Char.ConvertFromUtf32(entry.Key);var hex=new StringBuilder();foreach(byte b in Encoding.BigEndianUnicode.GetBytes(unicode))hex.Append(b.ToString("X2",CultureInfo.InvariantCulture));cmap.Append('<').Append(entry.Value.ToString("X4",CultureInfo.InvariantCulture)).Append("> <").Append(hex).Append(">\n");}cmap.Append("endbfchar\n");}cmap.Append("endcmap CMapName currentdict /CMap defineresource pop end end\n");Data(font+3,"",Encoding.ASCII.GetBytes(cmap.ToString()));
                using(var resource=typeof(RedactionPdfWriter).Assembly.GetManifestResourceStream("MiniPDF.TextFont"))using(var bytes=new MemoryStream()){PdfDocument.Check(resource!=null,"Missing rebuilt-text font.");resource.CopyTo(bytes);Data(font+4,"/Length1 "+bytes.Length,bytes.ToArray());}
                var glyphMap=new byte[(codes.Count+1)*2];for(int i=1;i<=codes.Count;i++)glyphMap[i*2+1]=1;Data(font+5,"",glyphMap);
            }
            long xref=stream.Position;PdfDocument.Check(xref<9999999999,"Output PDF is too large.");Text("xref\n0 "+offsets.Length+"\n0000000000 65535 f \n");for(int i=1;i<offsets.Length;i++){PdfDocument.Check(offsets[i]>0,"Missing output object.");Text(offsets[i].ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");}Text("trailer\n<< /Size "+offsets.Length+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF\n");completed=true;
        }
        public void Dispose(){}
    }
}
