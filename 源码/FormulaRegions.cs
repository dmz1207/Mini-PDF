using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MiniPdf {
    internal static class FormulaRegions {
        internal static RectangleF Bounds(OcrLine line){return RectangleF.FromLTRB(line.Corners.Min(p=>p.X),line.Corners.Min(p=>p.Y),line.Corners.Max(p=>p.X),line.Corners.Max(p=>p.Y));}
        static bool Candidate(OcrLine line){
            string text=(line.Text??"").Trim();if(text.Length==0||text.Length>160)return false;
            if(Regex.Matches(text,@"[A-Za-z]{3,}").Cast<Match>().Count(m=>!new[]{"sin","cos","tan","log","lim","min","max","exp"}.Contains(m.Value))>=2||text.Count(c=>c>=0x3400&&c<=0x9fff)>1)return false;
            if(line.Kind=="formula")return true;
            if(Regex.IsMatch(text,@"^(?:[0-9]{4}[-/][0-9]{1,2}[-/][0-9]{1,2}|(?:Fig|Table|Eq)[.\s]*[0-9]+|[0-9.]+\s*(?:Hz|mm|cm|kg|ms|%))$",RegexOptions.IgnoreCase))return false;
            return Regex.IsMatch(text,@"[=+−*/^_∑∫√≈≠≤≥²³⁰¹⁴⁵⁶⁷⁸⁹\-]|^[A-Za-zα-ωΑ-Ω]{1,2}[0-9]*$|^[0-9]{1,4}$");
        }
        static RectangleF Pixels(OcrLine line,Size size){var box=Bounds(line);return new RectangleF(box.X*size.Width,box.Y*size.Height,box.Width*size.Width,box.Height*size.Height);}
        internal static List<List<OcrLine>> Groups(List<OcrLine> lines,Size size){
            var candidates=lines.Where(Candidate).ToList();var selected=new HashSet<OcrLine>(candidates);var protectedAreas=lines.Where(l=>!selected.Contains(l)).Select(l=>Pixels(l,size)).ToList();var boxes=candidates.ToDictionary(l=>l,l=>Pixels(l,size));var output=new List<List<OcrLine>>();if(candidates.Count>256)return output;
            while(candidates.Count>0){var group=new List<OcrLine>{candidates[0]};candidates.RemoveAt(0);
                for(int pass=0;pass<group.Count;pass++)for(int n=candidates.Count-1;n>=0;n--){RectangleF a=boxes[group[pass]],b=boxes[candidates[n]];float h=Math.Max(a.Height,b.Height),dx=Math.Max(0,Math.Max(a.Left,b.Left)-Math.Min(a.Right,b.Right)),dy=Math.Max(0,Math.Max(a.Top,b.Top)-Math.Min(a.Bottom,b.Bottom));if(dx>h*1.8f||dy>h*.9f)continue;var union=RectangleF.Union(a,b);if(protectedAreas.Any(p=>p.IntersectsWith(union)))continue;group.Add(candidates[n]);candidates.RemoveAt(n);}
                output.Add(group);
            }
            return output;
        }
        internal static List<OcrLine> Refine(Bitmap image,List<OcrLine> input,string root,Func<bool> canceled=null){
            var lines=new List<OcrLine>(input);int count=0;
            foreach(var group in Groups(input,image.Size)){
                if(++count>64)break;if(canceled!=null&&canceled())throw new OperationCanceledException();
                string raw=String.Join(" ",group.Select(l=>l.Text));if(group.Count==1||!group.Any(l=>l.Kind=="formula")&&!Regex.IsMatch(raw,@"[=+−*/^_∑∫√≈≠≤≥²³]"))continue;
                RectangleF region=Bounds(group[0]);foreach(var line in group.Skip(1))region=RectangleF.Union(region,Bounds(line));
                float padding=Math.Max(3,group.Select(l=>Pixels(l,image.Size).Height).OrderBy(h=>h).ElementAt(group.Count/2)*.15f);
                region.Inflate(padding/image.Width,padding/image.Height);region=RectangleF.Intersect(region,new RectangleF(0,0,1,1));
                if(input.Any(l=>!group.Contains(l)&&Bounds(l).IntersectsWith(region)))continue;
                Rectangle pixels=Rectangle.Intersect(Rectangle.FromLTRB((int)Math.Floor(region.Left*image.Width),(int)Math.Floor(region.Top*image.Height),(int)Math.Ceiling(region.Right*image.Width),(int)Math.Ceiling(region.Bottom*image.Height)),new Rectangle(Point.Empty,image.Size));
                if(pixels.Width<4||pixels.Height<4)continue;
                // Do not fall back to replacing a lone numerator/denominator
                // after a complete connected equation fails recognition.
                foreach(var line in group)if(line.Kind=="formula"){line.Kind="uncertain";line.FormulaConfidence=0;}
                try{
                    byte[] png;using(var crop=image.Clone(pixels,PixelFormat.Format24bppRgb))using(var stream=new MemoryStream()){crop.Save(stream,ImageFormat.Png);png=stream.ToArray();}
                    var reply=MiniAiClient.Request(root,"/v1/ocr",new{image_base64=Convert.ToBase64String(png),formula_only=true},300,canceled);object found;var result=reply.TryGetValue("lines",out found)?found as System.Collections.IList:null;
                    if(result==null||result.Count!=1)continue;var item=result[0] as Dictionary<string,object>;object value,score;
                    if(item==null||!item.TryGetValue("tex",out value)||!item.TryGetValue("formula_confidence",out score)||Convert.ToSingle(score)<.65f)continue;
                    string tex=Convert.ToString(value);if(String.IsNullOrWhiteSpace(tex)||tex.Length>20000)continue;
                    int at=lines.IndexOf(group[0]);foreach(var old in group)lines.Remove(old);
                    lines.Insert(Math.Max(0,at),new OcrLine{Page=group[0].Page,Text=raw,Tex=tex,Kind="formula",Confidence=Convert.ToSingle(score),FormulaConfidence=Convert.ToSingle(score),Corners=new[]{new PointF(region.Left,region.Top),new PointF(region.Right,region.Top),new PointF(region.Right,region.Bottom),new PointF(region.Left,region.Bottom)}});
                }catch(IOException){}catch(System.Net.WebException){}
            }
            return lines;
        }
    }
}
