using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MiniPdf {
    internal static partial class N {
        [DllImport(D)] internal static extern int FPDFPath_GetDrawMode(IntPtr obj,out int fill,out int stroke);
    }
    internal static class WordRegions {
        sealed class Line {
            internal readonly List<OfficeGlyph> Glyphs=new List<OfficeGlyph>();
            internal float Y;
            internal RectangleF Bounds;
        }
        static RectangleF Bounds(IEnumerable<OfficeGlyph> chars) {
            return RectangleF.FromLTRB(chars.Min(g=>g.X),chars.Min(g=>g.Top),chars.Max(g=>g.Right),chars.Max(g=>g.Bottom));
        }
        static bool Contains(RectangleF outer,RectangleF inner) {
            return inner.Left>=outer.Left-.05f&&inner.Top>=outer.Top-.05f&&inner.Right<=outer.Right+.05f&&inner.Bottom<=outer.Bottom+.05f;
        }
        internal static bool Columns(IEnumerable<OfficeGlyph> glyphs,SizeF size) {
            float middle=size.Width/2;
            var rows=glyphs.Where(g=>!g.Bad&&!String.IsNullOrWhiteSpace(g.Text)).GroupBy(g=>Math.Round(g.Y*2)).Where(g=>g.Count()>35).ToList();
            int pairs=rows.Count(g=>g.Count(c=>c.Right<middle-1)>=15&&g.Count(c=>c.X>middle+1)>=15&&!g.Any(c=>c.X<middle+1&&c.Right>middle-1));
            int left=rows.Count(g=>g.Min(c=>c.X)<middle-36&&g.Max(c=>c.Right)<middle+1);
            int right=rows.Count(g=>g.Min(c=>c.X)>middle-1&&g.Max(c=>c.Right)>middle+36);
            return left+pairs>=6&&right+pairs>=6;
        }
        internal static float Nominal(IEnumerable<OfficeGlyph> glyphs) {
            var sizes=glyphs.Where(g=>!g.Bad&&!String.IsNullOrWhiteSpace(g.Text)&&WordLayout.Family(g.Font)!=null).GroupBy(g=>Math.Round(g.Size*2)).OrderByDescending(g=>g.Count()).FirstOrDefault();
            return sizes==null?10:(float)sizes.Key/2;
        }
        internal static void Figures(PdfDocument document,WordPage page,List<OfficeGlyph> glyphs,Dictionary<IntPtr,int> objects,HashSet<IntPtr> removed) {
            var regions=new List<RectangleF>();
            foreach(var obj in objects.Keys) {
                int kind=N.FPDFPageObj_GetType(obj);if(kind!=2&&kind!=3&&kind!=5)continue;
                var box=WordLayout.Bounds(obj,page.Size,document);if(box.Width<36||box.Height<24)continue;
                if(kind==2) {
                    int fill,stroke;uint r,g,b,a;
                    if(N.FPDFPath_GetDrawMode(obj,out fill,out stroke)==0)continue;
                    bool visible=stroke!=0&&N.FPDFPageObj_GetStrokeColor(obj,out r,out g,out b,out a)!=0&&a>0;
                    visible|=fill!=0&&N.FPDFPageObj_GetFillColor(obj,out r,out g,out b,out a)!=0&&a>0&&(r<245||g<245||b<245);
                    if(!visible)continue;
                }
                if(box.Width*box.Height>page.Size.Width*page.Size.Height*.65f){regions.Clear();regions.Add(new RectangleF(PointF.Empty,page.Size));break;}
                box.Inflate(8,8);regions.Add(box);
                if(regions.Count>256){regions.Clear();regions.Add(new RectangleF(PointF.Empty,page.Size));break;}
            }
            Capture(document,page,regions,objects,removed,true);
        }
        internal static void Equations(PdfDocument document,WordPage page,List<OfficeGlyph> glyphs,Dictionary<IntPtr,int> objects,HashSet<IntPtr> removed) {
            var protectedObjects=new HashSet<IntPtr>(page.Pictures.Where(p=>p.Figure&&p.Objects!=null).SelectMany(p=>p.Objects));
            var chars=glyphs.Where(g=>!protectedObjects.Contains(g.Object)&&!String.IsNullOrWhiteSpace(g.Text)&&WordLayout.Safe(g.Object)).ToList();
            if(chars.Count>20000)return;
            float nominal=Nominal(chars);var lines=new List<Line>();
            float middle=page.Size.Width/2;
            for(int side=0;side<2;side++) {
                var column=chars.Where(g=>side==0?g.X<middle:g.X>=middle).ToList();
                var bases=new List<Line>();
                foreach(var glyph in column.Where(g=>g.Size>=nominal*.85f).OrderBy(g=>g.Y)) {
                    var line=bases.LastOrDefault(l=>Math.Abs(l.Y-glyph.Y)<Math.Max(.7f,glyph.Size*.12f));
                    if(line==null){line=new Line{Y=glyph.Y};bases.Add(line);}line.Glyphs.Add(glyph);
                }
                foreach(var glyph in column.Where(g=>g.Size<nominal*.85f)) {
                    var line=bases.OrderBy(l=>Math.Abs(l.Y-glyph.Y)).FirstOrDefault();
                    if(line==null||Math.Abs(line.Y-glyph.Y)>nominal*.9f){line=new Line{Y=glyph.Y};bases.Add(line);}
                    line.Glyphs.Add(glyph);
                }
                foreach(var line in bases)if(line.Glyphs.Count>0){line.Bounds=Bounds(line.Glyphs);lines.Add(line);}
            }
            var native=new HashSet<IntPtr>(page.Text.Where(t=>t.Math!=null&&t.Objects!=null).SelectMany(t=>t.Objects));
            var areas=new List<RectangleF>();
            foreach(var side in lines.GroupBy(l=>l.Bounds.X<middle)) {
                RectangleF? current=null;float last=0;
                foreach(var line in side.OrderBy(l=>l.Y)) {
                    int math=line.Glyphs.Count(g=>OfficeMath.MathFont(g.Font)||g.Text.Any(c=>c>=0x370&&c<=0x3ff||c>=0x2200&&c<=0x22ff));
                    int letters=line.Glyphs.Where(g=>!OfficeMath.MathFont(g.Font)).Sum(g=>g.Text.Count(Char.IsLetter));
                    bool op=line.Glyphs.Any(g=>g.Text=="="||g.Text=="≤"||g.Text=="≥");
                    bool complex=line.Glyphs.Any(g=>OfficeMath.MathFont(g.Font)&&(g.Bad||g.Font.IndexOf("CMEX",StringComparison.OrdinalIgnoreCase)>=0||!OfficeMath.Token(g.Text)));
                    bool mathematical=complex||math>=2&&(letters<25||math>letters*.45f)||op&&letters<15;
                    var candidates=line.Glyphs.Where(g=>OfficeMath.MathFont(g.Font)||g.Text=="="||g.Text=="≤"||g.Text=="≥"||g.Size<nominal*.85f).ToList();
                    bool covered=candidates.Count>0&&candidates.All(g=>native.Contains(g.Object));
                    if(mathematical&&!covered) {
                        var box=line.Bounds;box.Inflate(1.5f,1.5f);
                        if(current.HasValue&&line.Y-last<nominal*1.9f)current=RectangleF.Union(current.Value,box);
                        else {if(current.HasValue)areas.Add(current.Value);current=box;}
                        last=line.Y;
                    } else {if(current.HasValue)areas.Add(current.Value);current=null;}
                }
                if(current.HasValue)areas.Add(current.Value);
            }
            Capture(document,page,areas,objects,removed,false);
        }
        static void Capture(PdfDocument document,WordPage page,List<RectangleF> regions,Dictionary<IntPtr,int> objects,HashSet<IntPtr> removed,bool figure) {
            if(regions.Count==0)return;
            var boxes=objects.Keys.ToDictionary(o=>o,o=>WordLayout.Bounds(o,page.Size,document));
            var pageBox=new RectangleF(PointF.Empty,page.Size);
            for(int i=0;i<regions.Count;i++) {
                var region=RectangleF.Intersect(regions[i],pageBox);
                for(int pass=0;pass<8;pass++) {
                    var prior=region;
                    foreach(var item in boxes)if(!item.Value.IsEmpty&&item.Value.IntersectsWith(region)&&
                        (item.Value.Width*item.Value.Height<pageBox.Width*pageBox.Height*.65f||Contains(region,pageBox)))region=RectangleF.Union(region,item.Value);
                    region=RectangleF.Intersect(region,pageBox);if(region==prior)break;
                }
                regions[i]=region;
            }
            for(int i=0;i<regions.Count;i++)for(int j=regions.Count-1;j>i;j--)if(regions[i].IntersectsWith(regions[j])) {
                regions[i]=RectangleF.Union(regions[i],regions[j]);regions.RemoveAt(j);i=-1;break;
            }
            foreach(var region in regions) {
                if(region.Width<=0||region.Height<=0)continue;
                var members=new HashSet<IntPtr>(boxes.Where(b=>!b.Value.IsEmpty&&Contains(region,b.Value)).Select(b=>b.Key));
                if(members.Count==0||boxes.Any(b=>b.Value.IntersectsWith(region)&&!Contains(region,b.Value)&&b.Value.Width*b.Value.Height<pageBox.Width*pageBox.Height*.65f))continue;
                page.Text.RemoveAll(t=>t.Math!=null&&t.Objects!=null&&t.Objects.Any(members.Contains));
                page.Pictures.RemoveAll(p=>p.Objects!=null&&p.Objects.Any(members.Contains));
                var picture=Crop(document,page.Size,region);picture.Figure=figure;picture.Block=true;picture.Objects=members.ToArray();page.Pictures.Add(picture);
                foreach(var obj in members)removed.Add(obj);
            }
        }
        static WordPicture Crop(PdfDocument document,SizeF size,RectangleF box) {
            double scale=Math.Min(300.0/72,Math.Sqrt(16000000.0/(box.Width*box.Height)));
            int width=Math.Max(1,(int)Math.Floor(size.Width*scale)),height=Math.Max(1,(int)Math.Floor(size.Height*scale));
            double sx=width/size.Width,sy=height/size.Height;
            int left=Math.Max(0,(int)Math.Floor(box.Left*sx)),top=Math.Max(0,(int)Math.Floor(box.Top*sy));
            int right=Math.Min(width,(int)Math.Ceiling(box.Right*sx)),bottom=Math.Min(height,(int)Math.Ceiling(box.Bottom*sy));
            using(var image=document.RenderRegion(document.Index,width,height,0,Rectangle.FromLTRB(left,top,right,bottom)))
            using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);return new WordPicture{X=(float)(left/sx),Y=(float)(top/sy),Width=(float)((right-left)/sx),Height=(float)((bottom-top)/sy),Data=stream.ToArray()};}
        }
    }
}
