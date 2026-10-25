using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniPdf {
    internal static partial class N {
        [DllImport(D)] internal static extern int FPDFPath_CountSegments(IntPtr path);
        [DllImport(D)] internal static extern int FPDFText_HasUnicodeMapError(IntPtr page,int index);
    }
    internal sealed class OfficeMathSettings {
        internal float ScriptRatio=.82f,BaselineOffset=.18f,SymbolGap=.8f,BarHeight=.8f;
        internal void Validate() {
            PdfDocument.Check(Valid(ScriptRatio,.55f,.92f)&&Valid(BaselineOffset,.12f,.5f)&&Valid(SymbolGap,.35f,1.2f)&&Valid(BarHeight,.2f,1.5f),"Invalid formula settings.");
        }
        static bool Valid(float value,float low,float high){return !Single.IsNaN(value)&&!Single.IsInfinity(value)&&value>=low&&value<=high;}
    }
    internal sealed class WordPicture {
        internal float X,Y,Width,Height,Baseline;
        internal byte[] Data;
        internal IntPtr[] Objects;
        internal bool Figure,Block;
    }
    internal static class OfficeMath {
        sealed class Chunk {
            internal IntPtr Object;
            internal List<OfficeGlyph> Glyphs;
            internal RectangleF Bounds;
            internal bool Math,Bar;
            internal float Size;
        }
        sealed class Atom {
            internal float X,Right,Y,Size;
            internal string Xml,Text;
            internal bool Structured;
        }
        internal static bool MathFont(string name){return Regex.IsMatch(name,@"(?:^|\+)(?:CM(?:MI|SY|EX)[0-9]|MT2|Symbol|CambriaMath|Cambria Math)",RegexOptions.IgnoreCase);}
        internal static bool Token(string text) {
            if(text.Length==0||text.Length>80||!Regex.IsMatch(text,@"^[0-9A-Za-z α-ωΑ-Ω+\-=−×⋅·÷±<>≤≥≠≈∈∉∞∂∇(),.:\[\]{}|/]+$"))return false;
            return !Regex.Matches(text,@"[A-Za-z]{2,}").Cast<Match>().Any(m=>!new[]{"sin","cos","tan","log","lim","min","max","exp"}.Contains(m.Value));
        }
        static RectangleF Rect(IEnumerable<OfficeGlyph> chars) {
            return RectangleF.FromLTRB(chars.Min(g=>g.X),chars.Min(g=>g.Top),chars.Max(g=>g.Right),chars.Max(g=>g.Bottom));
        }
        static int Root(int[] roots,int i){while(roots[i]!=i){roots[i]=roots[roots[i]];i=roots[i];}return i;}
        static void Join(int[] roots,int a,int b){roots[Root(roots,a)]=Root(roots,b);}
        internal static void Read(PdfDocument document,WordPage output,List<OfficeGlyph> glyphs,Dictionary<IntPtr,int> objects,HashSet<IntPtr> removed,bool convert,OfficeMathSettings settings) {
            settings.Validate();if(glyphs.Count>20000)return;var chunks=new List<Chunk>();
            var groups=glyphs.GroupBy(g=>g.Object).ToDictionary(g=>g.Key,g=>g.ToList());
            foreach(var group in groups) {
                var chars=group.Value;
                if(removed.Contains(group.Key)||!objects.ContainsKey(group.Key)||!WordLayout.Safe(group.Key)||chars.Any(g=>g.Unsupported))continue;
                bool math=chars.Any(g=>MathFont(g.Font));
                if(!math&&(chars.Any(g=>g.Bad)||!Token(String.Concat(chars.Select(g=>g.Text)))))continue;
                if(!math&&chars.Any(g=>g.Text.Length>1||g.Text.Any(c=>c>127&&c<0x370)))continue;
                chunks.Add(new Chunk{Object=group.Key,Glyphs=chars,Bounds=Rect(chars),Math=math,Size=chars.Max(g=>g.Size)});
            }
            if(chunks.Count>2000)return;
            foreach(var pair in objects)if(!removed.Contains(pair.Key)&&N.FPDFPageObj_GetType(pair.Key)==2&&N.FPDFPath_CountSegments(pair.Key)<=5&&N.FPDFPageObj_CountMarks(pair.Key)==0) {
                var box=WordLayout.Bounds(pair.Key,output,document);
                uint r,g,b,a;bool dark=N.FPDFPageObj_GetFillColor(pair.Key,out r,out g,out b,out a)!=0&&a==255&&r+g+b<650;
                if(!dark)dark=N.FPDFPageObj_GetStrokeColor(pair.Key,out r,out g,out b,out a)!=0&&a==255&&r+g+b<650;
                if(dark&&box.Width>=3&&box.Width<=200&&box.Height<=settings.BarHeight&&box.Height>=0)
                    chunks.Add(new Chunk{Object=pair.Key,Bounds=box,Bar=true,Size=9});
            }
            if(chunks.Count>2400)return;
            var roots=Enumerable.Range(0,chunks.Count).ToArray();
            for(int i=0;i<chunks.Count;i++)for(int j=0;j<i;j++) {
                var a=chunks[i];var b=chunks[j];float em=Math.Max(a.Size,b.Size);
                float x=Math.Max(0,Math.Max(a.Bounds.Left,b.Bounds.Left)-Math.Min(a.Bounds.Right,b.Bounds.Right));
                float y=Math.Max(0,Math.Max(a.Bounds.Top,b.Bounds.Top)-Math.Min(a.Bounds.Bottom,b.Bounds.Bottom));
                if(!a.Bar&&!b.Bar&&x<=em*settings.SymbolGap&&y<=em*.65f)Join(roots,i,j);
                else if(a.Bar!=b.Bar&&(x==0&&y<=em*1.1f||x<=em*settings.SymbolGap&&y<=em*.5f))Join(roots,i,j);
            }
            foreach(var group in chunks.Select((c,i)=>new{Chunk=c,Root=Root(roots,i)}).GroupBy(c=>c.Root)) {
                var part=group.Select(c=>c.Chunk).ToList();var chars=part.Where(c=>!c.Bar).SelectMany(c=>c.Glyphs).Where(g=>g.Bad||!String.IsNullOrWhiteSpace(g.Text)).ToList();
                if(chars.Count<2||chars.Count>80)continue;
                var bounds=Rect(chars);foreach(var bar in part.Where(c=>c.Bar))bounds=RectangleF.Union(bounds,bar.Bounds);
                if(bounds.Width>260||bounds.Height>80||bounds.Width<=0||bounds.Height<=0)continue;
                var members=new HashSet<IntPtr>(part.Select(c=>c.Object));
                // A whole native object must belong to this region. Anything overlapping
                // from outside makes the grouping ambiguous; keep the original artwork.
                if(glyphs.Any(g=>!members.Contains(g.Object)&&g.X<bounds.Right&&g.Right>bounds.Left&&g.Top<bounds.Bottom&&g.Bottom>bounds.Top))continue;
                bool overlap=false;foreach(var obj in objects.Keys)if(!members.Contains(obj)&&N.FPDFPageObj_GetType(obj)!=1&&WordLayout.Bounds(obj,output,document).IntersectsWith(bounds)){overlap=true;break;}
                if(overlap)continue;
                var bars=part.Where(c=>c.Bar).ToList();bool structured=false;
                string xml=convert&&!chars.Any(g=>g.Bad)&&part.Where(c=>!c.Bar).All(c=>Token(String.Concat(c.Glyphs.Select(g=>g.Text))))?Parse(chars,bars,settings,0,chars.Max(g=>g.Size),ref structured):null;
                bool math=part.Any(c=>c.Math);
                if(xml!=null&&structured) {
                    float em=chars.Max(g=>g.Size);
                    output.Text.Add(new WordText{Objects=members.ToArray(),Math="<m:oMath>"+xml+"</m:oMath>",X=bounds.X,Y=chars.Where(g=>g.Size>em*settings.ScriptRatio).Average(g=>g.Y),Top=bounds.Y,Width=bounds.Width,Height=bounds.Height+em,Size=em,Color=chars[0].Color});
                    output.Equations++;
                } else {
                    if(!math)continue;
                    var picture=Crop(document,output.Size,bounds);picture.Objects=members.ToArray();float em=chars.Max(g=>g.Size);picture.Baseline=chars.Where(g=>g.Size>em*settings.ScriptRatio).Average(g=>g.Y);output.Pictures.Add(picture);
                }
                foreach(IntPtr obj in members)removed.Add(obj);
            }
        }
        static WordPicture Crop(PdfDocument document,SizeF size,RectangleF box) {
            double scale=300.0/72;int width=Math.Max(1,(int)Math.Floor(size.Width*scale)),height=Math.Max(1,(int)Math.Floor(size.Height*scale));
            double sx=width/size.Width,sy=height/size.Height;
            int left=Math.Max(0,(int)Math.Floor((box.Left-1)*sx)),top=Math.Max(0,(int)Math.Floor((box.Top-1)*sy));
            int right=Math.Min(width,(int)Math.Ceiling((box.Right+1)*sx)),bottom=Math.Min(height,(int)Math.Ceiling((box.Bottom+1)*sy));
            using(var image=document.RenderRegion(document.Index,width,height,0,Rectangle.FromLTRB(left,top,right,bottom)))
            using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);return new WordPicture{X=(float)(left/sx),Y=(float)(top/sy),Width=(float)((right-left)/sx),Height=(float)((bottom-top)/sy),Data=stream.ToArray()};}
        }
        static string Run(OfficeGlyph glyph,float size) {
            bool italic=glyph.Text.Any(c=>Char.IsLetter(c));
            return "<m:r><m:rPr><m:sty m:val=\""+(italic?"i":"p")+"\"/></m:rPr><w:rPr><w:rFonts w:ascii=\"Cambria Math\" w:hAnsi=\"Cambria Math\"/><w:sz w:val=\""+Math.Round(size*2)+"\"/><w:color w:val=\""+glyph.Color.R.ToString("X2")+glyph.Color.G.ToString("X2")+glyph.Color.B.ToString("X2")+"\"/></w:rPr><m:t>"+OfficeExport.Xml(glyph.Text)+"</m:t></m:r>";
        }
        static string Parse(List<OfficeGlyph> chars,List<Chunk> bars,OfficeMathSettings settings,int depth,float nominal,ref bool structured) {
            if(chars.Count==0||depth>3)return null;
            float em=chars.Max(g=>g.Size);var atoms=new List<Atom>();var used=new HashSet<OfficeGlyph>();var usedBars=new HashSet<Chunk>();
            foreach(var bar in bars.OrderByDescending(b=>b.Bounds.Width)) {
                if(usedBars.Contains(bar))continue;float y=bar.Bounds.Top+bar.Bounds.Height/2;
                var inside=chars.Where(g=>!used.Contains(g)&&g.X>=bar.Bounds.Left-em*.15&&g.Right<=bar.Bounds.Right+em*.15).ToList();
                var above=inside.Where(g=>g.Bottom<=y+.3&&g.Bottom>=y-em*2).ToList();
                var below=inside.Where(g=>g.Top>=y-.3&&g.Top<=y+em*2).ToList();
                if(above.Count==0||below.Count==0||above.Count+below.Count!=inside.Count)continue;
                var other=bars.Where(b=>b!=bar&&b.Bounds.Left>=bar.Bounds.Left&&b.Bounds.Right<=bar.Bounds.Right).ToList();
                var upBars=other.Where(b=>b.Bounds.Bottom<y).ToList();var downBars=other.Where(b=>b.Bounds.Top>y).ToList();
                string numerator=Parse(above,upBars,settings,depth+1,nominal,ref structured),denominator=Parse(below,downBars,settings,depth+1,nominal,ref structured);
                if(numerator==null||denominator==null)return null;
                atoms.Add(new Atom{X=bar.Bounds.Left,Right=bar.Bounds.Right,Y=y+em*.25f,Size=em,Xml="<m:f><m:fPr><m:type m:val=\"bar\"/></m:fPr><m:num>"+numerator+"</m:num><m:den>"+denominator+"</m:den></m:f>",Structured=true});
                foreach(var g in inside)used.Add(g);usedBars.Add(bar);foreach(var b in other)usedBars.Add(b);structured=true;
            }
            if(bars.Any(b=>!usedBars.Contains(b)))return null;
            foreach(var glyph in chars)if(!used.Contains(glyph))atoms.Add(new Atom{X=glyph.X,Right=glyph.Right,Y=glyph.Y,Size=glyph.Size,Text=glyph.Text,Xml=Run(glyph,nominal)});
            if(atoms.Count==0)return null;
            var main=atoms.Where(a=>a.Size>em*settings.ScriptRatio).OrderBy(a=>a.X).ToList();
            if(main.Count==0)return null;
            float baseline=main.Select(a=>a.Y).OrderBy(y=>y).ElementAt(main.Count/2);
            if(main.Any(a=>Math.Abs(a.Y-baseline)>em*settings.BaselineOffset))return null;
            var small=atoms.Except(main).ToList();var result=new StringBuilder();
            foreach(var atom in main) {
                var scripts=small.Where(a=>a.X>=atom.X&&a.X-atom.Right<=em*settings.SymbolGap&&
                    main.Where(b=>b.X<=a.X+em*.1f).OrderBy(b=>b.X).LastOrDefault()==atom).ToList();
                if(scripts.Count==0){result.Append(atom.Xml);continue;}
                if(atom.Text!=null&&Regex.IsMatch(atom.Text,@"^[+\-=−×⋅·÷<>≤≥≠≈,.:/]+$"))return null;
                var supers=scripts.Where(a=>baseline-a.Y>=em*settings.BaselineOffset).OrderBy(a=>a.X).ToList();
                var subs=scripts.Where(a=>a.Y-baseline>=em*settings.BaselineOffset).OrderBy(a=>a.X).ToList();
                if(supers.Count+subs.Count!=scripts.Count||supers.Count>0&&supers.Max(a=>a.Y)-supers.Min(a=>a.Y)>em*.15f||subs.Count>0&&subs.Max(a=>a.Y)-subs.Min(a=>a.Y)>em*.15f)return null;
                string tag=supers.Count>0?(subs.Count>0?"sSubSup":"sSup"):"sSub";
                result.Append("<m:").Append(tag).Append("><m:e>").Append(atom.Xml).Append("</m:e>");
                if(subs.Count>0)result.Append("<m:sub>").Append(String.Concat(subs.Select(a=>a.Xml))).Append("</m:sub>");
                if(supers.Count>0)result.Append("<m:sup>").Append(String.Concat(supers.Select(a=>a.Xml))).Append("</m:sup>");
                result.Append("</m:").Append(tag).Append(">");foreach(var a in scripts)small.Remove(a);structured=true;
            }
            if(small.Count>0)return null;
            if(chars.Any(g=>g.Text=="="||g.Text=="≤"||g.Text=="≥"))structured=true;
            return result.ToString();
        }
    }
}
