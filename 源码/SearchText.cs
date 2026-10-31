using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MiniPdf {
    internal static class SearchText {
        internal const int MaximumHits=20000;
        internal sealed class NormalText {
            internal string Text,Raw;
            internal readonly List<int> Starts=new List<int>(),Ends=new List<int>();
        }
        static bool Cjk(char c){return c>=0x2e80&&c<=0x9fff||c>=0xf900&&c<=0xfaff;}
        static bool Letter(char c){return c<128&&Char.IsLetter(c);}
        internal static NormalText Normalize(string raw,bool matchCase) {
            var result=new NormalText{Raw=raw??""};var text=new StringBuilder();raw=result.Raw;
            for(int i=0;i<raw.Length;) {
                int start=i;string element=StringInfo.GetNextTextElement(raw,i);i+=element.Length;
                char c=element[0];
                if(c=='\u00ad'||c=='\u200b'||c=='\ufeff')continue;
                if((c=='-'||c=='\u2010')&&start>0&&Letter(raw[start-1])) {
                    int next=i;bool newline=false;while(next<raw.Length&&Char.IsWhiteSpace(raw[next])){newline|=raw[next]=='\r'||raw[next]=='\n';next++;}
                    if(newline&&next<raw.Length&&Letter(raw[next])){i=next;continue;}
                }
                if(Char.IsWhiteSpace(c)) {
                    while(i<raw.Length&&Char.IsWhiteSpace(raw[i]))i++;
                    if(start>0&&i<raw.Length&&Cjk(raw[start-1])&&Cjk(raw[i]))continue;
                    if(text.Length==0||text[text.Length-1]==' ')continue;
                    element=" ";
                } else {try{element=element.Normalize(NormalizationForm.FormKC);}catch(ArgumentException){}if(!matchCase)element=element.ToUpperInvariant();}
                foreach(char value in element){text.Append(value);result.Starts.Add(start);result.Ends.Add(i);}
            }
            result.Text=text.ToString();return result;
        }
        static bool Word(char c){return Char.IsLetterOrDigit(c)||c=='_';}
        internal static List<SearchHit> Match(string raw,string query,bool matchCase,bool wholeWords) {
            var result=new List<SearchHit>();var page=Normalize(raw,matchCase);string needle=Normalize((query??"").Trim(),matchCase).Text.Trim();if(needle.Length==0)return result;
            int position=0;
            while(position<=page.Text.Length-needle.Length&&result.Count<MaximumHits) {
                int found=page.Text.IndexOf(needle,position,StringComparison.Ordinal);if(found<0)break;position=found+1;
                if(wholeWords&&(found>0&&Word(page.Text[found-1])||found+needle.Length<page.Text.Length&&Word(page.Text[found+needle.Length])))continue;
                int start=page.Starts[found],end=page.Ends[found+needle.Length-1],before=Math.Max(0,start-34),after=Math.Min(raw.Length,end+66);
                result.Add(new SearchHit{Start=start,Count=end-start,Snippet=(before>0?"…":"")+raw.Substring(before,after-before).Replace('\r',' ').Replace('\n',' ').Replace('\t',' ')+(after<raw.Length?"…":"")});
            }
            return result;
        }
        internal static List<SearchHit> Page(PdfDocument document,int index,string query,bool matchCase,bool wholeWords) {
            using(var page=new PdfTextPage(document.Handle,index)) {
                PdfDocument.Check(page.Count<=1000000,"This page exceeds the search text limit.");
                var raw=new StringBuilder();var characterMap=new List<int>();
                for(int i=0;i<page.Count;i++) {
                    uint scalar=N.FPDFText_GetUnicode(page.Text,i);string value=scalar>0&&scalar<=0x10ffff&&!(scalar>=0xd800&&scalar<=0xdfff)?Char.ConvertFromUtf32((int)scalar):"\u200b";
                    raw.Append(value);foreach(char c in value)characterMap.Add(i);
                }
                var hits=Match(raw.ToString(),query,matchCase,wholeWords);
                foreach(var hit in hits) {
                    int start=characterMap[hit.Start],end=characterMap[hit.Start+hit.Count-1]+1;hit.Page=index;hit.Start=start;hit.Count=end-start;hit.Rectangles=new List<N.Rect>();
                    int count=N.FPDFText_CountRects(page.Text,start,hit.Count);
                    for(int r=0;r<Math.Min(count,256);r++){double left,top,right,bottom;N.FPDFText_GetRect(page.Text,r,out left,out top,out right,out bottom);hit.Rectangles.Add(new N.Rect((float)left,(float)bottom,(float)right,(float)top));}
                }
                return hits;
            }
        }
        internal static void Work(PdfDocument document,BinaryReader reader,BinaryWriter writer) {
            string query=reader.ReadString();bool matchCase=reader.ReadBoolean(),wholeWords=reader.ReadBoolean();int total=0,empty=0;
            PdfDocument.Check(query.Length>0&&query.Length<=2048,"Invalid search query.");
            for(int page=0;page<document.Count;page++) {
                var hits=Page(document,page,query,matchCase,wholeWords);if(hits.Count>MaximumHits-total)hits.RemoveRange(MaximumHits-total,hits.Count-(MaximumHits-total));
                writer.Write(0);writer.Write(page+1);writer.Write(document.Count);writer.Write(hits.Count);
                foreach(var hit in hits){writer.Write(hit.Page);writer.Write(hit.Start);writer.Write(hit.Count);writer.Write(hit.Snippet);writer.Write(hit.Rectangles.Count);foreach(var r in hit.Rectangles){writer.Write(r.left);writer.Write(r.bottom);writer.Write(r.right);writer.Write(r.top);}}
                writer.Flush();total+=hits.Count;
                if(total>=MaximumHits)break;
                using(var text=new PdfTextPage(document.Handle,page))if(text.Count==0)empty++;
            }
            writer.Write(1);writer.Write(total>=MaximumHits);writer.Write(empty);writer.Flush();
        }
    }
}
