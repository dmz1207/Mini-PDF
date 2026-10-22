using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniPdf
{
    // This parser accepts only PDFium's fresh, unencrypted classic-xref output.
    // Offsets come from xref entries; PDF stream contents are never searched for objects.
    internal sealed class PdfSyntax {
        static readonly Encoding Bytes = Encoding.GetEncoding(28591);
        readonly Dictionary<int,int> generations = new Dictionary<int,int>();
        readonly string text;
        readonly Dictionary<int,int> offsets=new Dictionary<int,int>();
        internal readonly Dictionary<string,string> Trailer;
        internal readonly int Root,RootGeneration,Size,StartXref;
        internal PdfSyntax(byte[] data){text=Bytes.GetString(data);int marker=text.LastIndexOf("startxref",StringComparison.Ordinal);Check(marker>=0);int at=marker+9;StartXref=Int32.Parse(Atom(ref at),CultureInfo.InvariantCulture);at=StartXref;Check(Atom(ref at)=="xref");
            while(true){string first=Atom(ref at);if(first=="trailer")break;int start=Int32.Parse(first,CultureInfo.InvariantCulture),count=Int32.Parse(Atom(ref at),CultureInfo.InvariantCulture);Check(start>=0&&count>=0&&count<=1000000);for(int i=0;i<count;i++){int offset=Int32.Parse(Atom(ref at),CultureInfo.InvariantCulture);int generation=Int32.Parse(Atom(ref at),CultureInfo.InvariantCulture);if(Atom(ref at)=="n"){offsets[start+i]=offset;generations[start+i]=generation;}}}
            Trailer=ParseDictionary(Read(ref at));Check(!Trailer.ContainsKey("Encrypt"));Size=Int32.Parse(Trailer["Size"],CultureInfo.InvariantCulture);int[] root=Reference(Trailer["Root"]);Root=root[0];RootGeneration=root[1];Check(Size>0&&Size<10000000);}
        static void Check(bool ok){PdfDocument.Check(ok,UiText.Get("PDF 结构不支持安全签名，请重新导出 PDF 后再试。"));}
        void Space(ref int p){while(p<text.Length){if(Char.IsWhiteSpace(text[p])||text[p]=='\0'){p++;continue;}if(text[p]=='%'){while(p<text.Length&&text[p]!='\r'&&text[p]!='\n')p++;continue;}break;}}
        static bool Delimiter(char c){return Char.IsWhiteSpace(c)||"/[]<>()%\0".IndexOf(c)>=0;}
        string Atom(ref int p){Space(ref p);int start=p;if(p<text.Length&&text[p]=='/')p++;while(p<text.Length&&!Delimiter(text[p]))p++;Check(p>start);return text.Substring(start,p-start);}
        string Read(ref int p, int level=0){Check(level<64);Space(ref p);int start=p;Check(p<text.Length);char c=text[p++];if(c=='<'){if(p<text.Length&&text[p]=='<'){p++;while(true){Space(ref p);Check(p+1<text.Length);if(text[p]=='>'&&text[p+1]=='>'){p+=2;break;}Read(ref p,level+1);}}else{while(p<text.Length&&text[p]!='>')p++;Check(p<text.Length);p++;}}
            else if(c=='['){while(true){Space(ref p);Check(p<text.Length);if(text[p]==']'){p++;break;}Read(ref p,level+1);}}
            else if(c=='('){int depth=1;while(depth>0&&p<text.Length){char ch=text[p++];if(ch=='\\'){if(p<text.Length)p++;}else if(ch=='(')depth++;else if(ch==')')depth--;}Check(depth==0);}
            else{p=start;Atom(ref p);int firstEnd=p;if(Char.IsDigit(c)){try{string second=Atom(ref p);int n;if(!Int32.TryParse(second,out n)||Atom(ref p)!="R")p=firstEnd;}catch{p=firstEnd;}}}return text.Substring(start,p-start);}
        Dictionary<string,string> ParseDictionary(string value){var parser=new PdfSyntax(value);int p=2;var dict=new Dictionary<string,string>();Check(value.StartsWith("<<"));while(true){parser.Space(ref p);Check(p+1<value.Length);if(value[p]=='>'&&value[p+1]=='>')break;string name=parser.Atom(ref p);Check(name.StartsWith("/"));name=Regex.Replace(name.Substring(1),"#([0-9A-Fa-f]{2})",delegate(Match m){return ((char)Convert.ToByte(m.Groups[1].Value,16)).ToString();});Check(!dict.ContainsKey(name));dict[name]=parser.Read(ref p);}return dict;}
        PdfSyntax(string fragment){text=fragment;}
        static int[] Reference(string value){var match=Regex.Match(value,@"^\s*(\d+)\s+(\d+)\s+R\s*$");Check(match.Success);return new[]{Int32.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture),Int32.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture)};}
        internal string Resolve(string value){if(value.TrimStart().StartsWith("<<")||value.TrimStart().StartsWith("["))return value;int[] reference=Reference(value);return Object(reference[0],reference[1]);}
        string Object(int id,int generation){int at;Check(offsets.TryGetValue(id,out at));Check(Atom(ref at)==id.ToString(CultureInfo.InvariantCulture)&&Atom(ref at)==generation.ToString(CultureInfo.InvariantCulture)&&Atom(ref at)=="obj");return Read(ref at);}
        internal Dictionary<string,string> ObjectDictionary(int id){return ParseDictionary(Object(id,generations[id]));}
        internal Dictionary<string,string> DictionaryValue(string value){return ParseDictionary(Resolve(value));}
        internal static string Dictionary(Dictionary<string,string> dictionary){var b=new StringBuilder("<<");foreach(var entry in dictionary){string key=Regex.Replace(entry.Key,@"[^a-zA-Z0-9_]",delegate(Match m){return "#"+((int)m.Value[0]).ToString("X2",CultureInfo.InvariantCulture);});b.Append(" /").Append(key).Append(' ').Append(entry.Value);}return b.Append(" >>").ToString();}
        internal bool ContainsObject(int id) { return offsets.ContainsKey(id); }
        internal bool IsStreamReference(string value)
        {
            var visited=new HashSet<int>();
            int id;
            while(TryReference(value,out id) && visited.Count<64 && visited.Add(id))
            {
                int at;
                if(!offsets.TryGetValue(id,out at)) return false;
                if(Atom(ref at)!=id.ToString(CultureInfo.InvariantCulture) ||
                   Atom(ref at)!=generations[id].ToString(CultureInfo.InvariantCulture) || Atom(ref at)!="obj") return false;
                value=Read(ref at);
                Space(ref at);
                if(value.StartsWith("<<") && text.IndexOf("stream",at,StringComparison.Ordinal)==at) return true;
            }
            return false;
        }
        internal IEnumerable<int> ObjectNumbers { get { return offsets.Keys; } }
        internal string ObjectValue(int id) { return Object(id, generations[id]); }
        internal Dictionary<string,string> ParseValueDictionary(string value) { return ParseDictionary(value); }
        internal string ObjectReference(int id) { return id + " " + generations[id] + " R"; }
        internal static bool TryReference(string value, out int id)
        {
            var match = Regex.Match(value ?? "", @"^\s*(\d+)\s+\d+\s+R\s*$");
            id = 0;
            return match.Success && Int32.TryParse(match.Groups[1].Value, out id);
        }
        internal List<string> ArrayValues(string value)
        {
            value = Resolve(value).Trim();
            Check(value.StartsWith("[") && value.EndsWith("]"));
            var parser = new PdfSyntax(value);
            var result = new List<string>();
            int at = 1;
            while (true)
            {
                parser.Space(ref at);
                Check(at < value.Length);
                if (value[at] == ']') break;
                result.Add(parser.Read(ref at));
            }
            return result;
        }
        internal byte[] Append(byte[] original, SortedDictionary<int,string> changes)
        {
            using (var output = new MemoryStream())
            {
                output.Write(original,0,original.Length);
                Action<string> write = delegate(string value) { byte[] bytes=Bytes.GetBytes(value); output.Write(bytes,0,bytes.Length); };
                write("\n");
                var positions = new SortedDictionary<int,long>();
                foreach (var item in changes)
                {
                    positions[item.Key]=output.Position;
                    write(item.Key+" "+generations[item.Key]+" obj\n"+item.Value+"\nendobj\n");
                }
                long xref=output.Position;
                write("xref\n");
                foreach (var item in positions)
                    write(item.Key+" 1\n"+item.Value.ToString("0000000000",CultureInfo.InvariantCulture)+" "+generations[item.Key].ToString("00000",CultureInfo.InvariantCulture)+" n \n");
                var trailer = new Dictionary<string,string>(Trailer);
                trailer["Prev"]=StartXref.ToString(CultureInfo.InvariantCulture);
                trailer.Remove("XRefStm");
                write("trailer\n"+Dictionary(trailer)+"\nstartxref\n"+xref.ToString(CultureInfo.InvariantCulture)+"\n%%EOF\n");
                return output.ToArray();
            }
        }
    }
}
