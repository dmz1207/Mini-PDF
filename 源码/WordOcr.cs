using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MiniPdf {
    internal static class WordOcr {
        internal static bool Needed(PdfDocument document,int page) {
            document.OpenPage(page);var glyphs=OfficeGeometry.Read(document,page);
            return glyphs.Count(g=>!g.Bad&&!String.IsNullOrWhiteSpace(g.Text)&&g.Text.Any(Char.IsLetter)&&N.FPDFTextObj_GetTextRenderMode(g.Object)==0)<20;
        }
        static RectangleF Box(OcrLine line,Size size){return RectangleF.FromLTRB(line.Corners.Min(p=>p.X)*size.Width,line.Corners.Min(p=>p.Y)*size.Height,line.Corners.Max(p=>p.X)*size.Width,line.Corners.Max(p=>p.Y)*size.Height);}
        internal static bool Prose(OcrLine line) {
            if(line.Confidence<.78f||String.IsNullOrWhiteSpace(line.Text)||line.Text.Count(Char.IsLetter)<2)return false;
            if(Regex.IsMatch(line.Text,@"[=∑∏∫√∞≈≠≤≥±×÷^_{}\\]|(?:^|\s)[xyz]\s*[+−-]\s*\d"))return false;
            return Math.Abs(line.Corners[1].Y-line.Corners[0].Y)<Math.Max(.003f,(line.Corners[3].Y-line.Corners[0].Y)*.15f);
        }
        static bool Paper(Bitmap bitmap,Rectangle area,out Color color) {
            int r=0,g=0,b=0,lo=255,hi=0,count=0;
            for(int side=0;side<4;side++)for(int n=0;n<12;n++) {
                int x=side<2?area.Left+(area.Width-1)*n/11:side==2?area.Left-2:area.Right+1;
                int y=side>=2?area.Top+(area.Height-1)*n/11:side==0?area.Top-2:area.Bottom+1;
                if(x<0||y<0||x>=bitmap.Width||y>=bitmap.Height)continue;Color c=bitmap.GetPixel(x,y);
                int value=Math.Min(c.R,Math.Min(c.G,c.B));lo=Math.Min(lo,value);hi=Math.Max(hi,Math.Max(c.R,Math.Max(c.G,c.B)));r+=c.R;g+=c.G;b+=c.B;count++;
            }
            color=count==0?Color.White:Color.FromArgb(r/count,g/count,b/count);
            return count>=24&&lo>=235&&hi-lo<=20;
        }
        internal static WordPage Read(PdfDocument document,int index,string root,Action<string> status) {
            var size=document.PageSize(index);float scale=Math.Min(300/72f,4096/Math.Max(size.Width,size.Height));
            using(var bitmap=document.Render(index,Math.Max(1,(int)Math.Ceiling(size.Width*scale)),Math.Max(1,(int)Math.Ceiling(size.Height*scale)))) {
                byte[] input;using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);input=stream.ToArray();}
                if(status!=null)status(UiText.Get("正在识别扫描页…")+" "+(index+1));
                var lines=MiniAiClient.Recognize(root,input,index);return Prepare(bitmap,size,lines);
            }
        }
        internal static WordPage Prepare(Bitmap original,SizeF size,List<OcrLine> lines) {
            var result=new WordPage{Size=size};var glyphs=new List<OfficeGlyph>();
            using(var bitmap=new Bitmap(original)) {
                foreach(var line in lines) {
                    if(!Prose(line))continue;var box=Box(line,bitmap.Size);Rectangle area=Rectangle.Intersect(Rectangle.Ceiling(box),new Rectangle(Point.Empty,bitmap.Size));Color paper;
                    if(area.Width<2||area.Height<2||!Paper(bitmap,area,out paper))continue;
                    // Replace only confidently recognized, horizontal prose on plain light paper.
                    // Formula regions, low confidence lines and artwork remain in the source raster.
                    area.Inflate(1,1);using(var graphics=Graphics.FromImage(bitmap))using(var fill=new SolidBrush(paper))graphics.FillRectangle(fill,area);
                    float x=box.Left/bitmap.Width*size.Width,top=box.Top/bitmap.Height*size.Height,width=box.Width/bitmap.Width*size.Width,height=box.Height/bitmap.Height*size.Height;
                    float font=Math.Max(4,Math.Min(72,height*.95f));bool chinese=line.Text.Any(c=>c>=0x4e00&&c<=0x9fff);
                    var text=new WordText{Text=line.Text,Font=chinese?"Microsoft YaHei":"Times New Roman",X=x,Top=top,Y=top+font*.807f,Width=width,Size=font,Height=height,Color=Color.Black};result.Text.Add(text);
                    int i=0;foreach(char c in line.Text){glyphs.Add(new OfficeGlyph{Text=c.ToString(),Font=text.Font,X=x+width*i/line.Text.Length,Right=x+width*(i+1)/line.Text.Length,Y=text.Y,Top=top,Bottom=top+height,Size=font});i++;}
                }
                result.Profile=AcademicLayout.Read(glyphs,size);result.Columns=result.Profile.Columns;
                using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);result.Background=stream.ToArray();}
            }
            return result;
        }
    }
}
