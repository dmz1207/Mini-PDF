using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace MiniPdf {
    // Small geometric profiles, shared across publishers. A name never forces a column count.
    internal sealed class AcademicLayout {
        internal string Publisher;
        internal bool Columns;
        internal float Split,BodySize,LineStep,Indent;
        sealed class Span { internal float Left,Right,Y;internal int Count; }
        internal static string PublisherOf(IEnumerable<OfficeGlyph> glyphs) {
            string text=String.Concat(glyphs.Take(12000).Select(g=>g.Text));
            string[] names={"IEEE","Elsevier","Springer Nature","Wiley","ACM","SIAM","Taylor & Francis","SAGE","APS","IOP","Oxford","Cambridge","MDPI","PLOS","Frontiers","ACS"};
            string[] signals={@"10\.1109/|IEEE TRANSACTIONS|IEEE CONTROL SYSTEMS|ieeexplore\.ieee",@"10\.1016/|sciencedirect\.com|elsevier\.com|Elsevier (?:Ltd|B\.V)",@"10\.1007/|10\.1038/|link\.springer|Springer[- ]Verlag|Springer Nature|springer\.com|nature\.com/",@"10\.1002/|10\.1111/|wiley(?:onlinelibrary)?\.com|John Wiley",@"10\.1145/|Association for Computing Machinery|dl\.acm\.org",@"10\.1137/|Society for Industrial and Applied Mathematics|SIAM J\.",@"10\.1080/|tandfonline\.com|Taylor & Francis",@"10\.1177/|sagepub\.com|SAGE Publications",@"10\.1103/|American Physical Society|PHYSICAL REVIEW",@"10\.1088/|iopscience\.iop\.org|IOP Publishing",@"10\.1093/|Oxford University Press",@"10\.1017/|Cambridge University Press",@"10\.3390/|mdpi\.com",@"10\.1371/|PLOS ONE",@"10\.3389/|frontiersin\.org",@"10\.1021/|pubs\.acs\.org|American Chemical Society"};
            // Publication marks in the page margins are stronger than a cited publisher in prose.
            var margin=String.Concat(glyphs.Take(30000).Where(g=>g.Y<170||g.Y>650).Select(g=>g.Text));
            for(int pass=0;pass<2;pass++) {
                string source=pass==0?margin:text;int winner=-1,best=0;
                for(int i=0;i<signals.Length;i++){int score=Regex.Matches(source,signals[i],RegexOptions.IgnoreCase).Count;if(score>best){winner=i;best=score;}}
                if(winner>=0)return names[winner];
            }
            return null;
        }
        internal static AcademicLayout Read(List<OfficeGlyph> glyphs,SizeF size,string publisher=null) {
            var body=glyphs.Take(30000).Where(g=>!g.Bad&&!g.Unsupported&&!String.IsNullOrWhiteSpace(g.Text)&&g.Size>=5&&g.Size<=24).ToList();
            var mode=body.Where(g=>!OfficeMath.MathFont(g.Font)).GroupBy(g=>Math.Round(g.Size*2)).OrderByDescending(g=>g.Count()).FirstOrDefault();
            float nominal=mode==null?10:(float)mode.Key/2;
            var profile=new AcademicLayout{Publisher=publisher??PublisherOf(glyphs),Split=size.Width/2,BodySize=nominal,LineStep=nominal*1.2f,Indent=Math.Max(4,nominal*.55f)};
            var spans=new List<Span>();
            foreach(var row in body.Where(g=>g.Size>=nominal*.85f&&g.Y>size.Height*.05f&&g.Y<size.Height*.94f).GroupBy(g=>Math.Round(g.Y*2))) {
                Span current=null;float last=0;
                foreach(var glyph in row.OrderBy(g=>g.X)) {
                    if(current==null||glyph.X-last>nominal*1.25f){current=new Span{Left=glyph.X,Right=glyph.Right,Y=glyph.Y};spans.Add(current);}
                    current.Right=Math.Max(current.Right,glyph.Right);current.Count++;last=Math.Max(last,glyph.Right);
                }
            }
            spans=spans.Where(s=>s.Count>=12&&s.Right-s.Left>nominal*5).ToList();
            var candidates=new List<float>{size.Width/2};
            foreach(var row in spans.GroupBy(s=>Math.Round(s.Y*2))) {
                var parts=row.OrderBy(s=>s.Left).ToList();
                for(int i=1;i<parts.Count;i++)if(parts[i].Left-parts[i-1].Right>=nominal*1.25f)candidates.Add((parts[i].Left+parts[i-1].Right)/2);
            }
            foreach(var span in spans)if(span.Right<size.Width*.65f)candidates.Add(span.Right+nominal);
            foreach(var span in spans)if(span.Left>size.Width*.35f)candidates.Add(span.Left-nominal);
            int bestScore=0;
            foreach(float split in candidates.Where(x=>x>size.Width*.3f&&x<size.Width*.7f).Select(x=>(float)Math.Round(x)).Distinct()) {
                int left=spans.Count(s=>s.Right<split-1&&s.Left<split-nominal*5);
                int right=spans.Count(s=>s.Left>split+1&&s.Right>split+nominal*5);
                int crossing=spans.Count(s=>s.Left<split-1&&s.Right>split+1);
                int needed=profile.Publisher==null?6:5;
                if(left<needed||right<needed||crossing>Math.Min(left,right))continue;
                int score=Math.Min(left,right)*6+Math.Min(left+right,Math.Min(left,right)*3)-crossing*5;
                score-=(int)(Math.Abs(split-size.Width/2)*.05f);
                if(score>bestScore){bestScore=score;profile.Columns=true;profile.Split=split;}
            }
            if(profile.Columns) {
                var leftEdges=spans.Where(s=>s.Right<profile.Split-1).Select(s=>s.Right).OrderBy(x=>x).ToList();
                var rightEdges=spans.Where(s=>s.Left>profile.Split+1).Select(s=>s.Left).OrderBy(x=>x).ToList();
                if(leftEdges.Count>0&&rightEdges.Count>0) {
                    float left=leftEdges[(leftEdges.Count-1)*9/10],right=rightEdges[(rightEdges.Count-1)/10];
                    if(right-left>nominal*1.25f)profile.Split=Math.Max(leftEdges[leftEdges.Count-1]+1,Math.Min(rightEdges[0]-1,(left+right)/2));
                }
            }
            var steps=new List<float>();
            foreach(var side in spans.GroupBy(s=>profile.Columns&&s.Left>=profile.Split)) {
                var ys=side.Select(s=>s.Y).OrderBy(y=>y).ToList();
                for(int i=1;i<ys.Count;i++){float gap=ys[i]-ys[i-1];if(gap>nominal*.85f&&gap<nominal*1.8f)steps.Add(gap);}
            }
            if(steps.Count>=4){steps.Sort();profile.LineStep=steps[steps.Count/2];}
            return profile;
        }
    }
}
