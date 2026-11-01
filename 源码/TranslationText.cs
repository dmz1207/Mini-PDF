using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MiniAI.Desktop {
    internal static class TranslationText {
        internal sealed class Chunk {internal string Text,After;}
        static readonly Lazy<HashSet<string>> words=new Lazy<HashSet<string>>(delegate {
            var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("MiniAI.EnglishWords");if(resource==null)return result;
            using(resource)using(var compressed=new GZipStream(resource,CompressionMode.Decompress))using(var reader=new StreamReader(compressed)){string line;while((line=reader.ReadLine())!=null)result.Add(line);}return result;
        });
        internal static string Normalize(string text,string context=null) {
            text=(text??"").Replace("\ufb00","ff").Replace("\ufb01","fi").Replace("\ufb02","fl").Replace("\ufb03","ffi").Replace("\ufb04","ffl").Replace("\u2011","-");
            var known=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(Match match in Regex.Matches(context??text,@"[A-Za-z]+(?:[-\u2010][A-Za-z]+)*"))known.Add(match.Value.Replace('\u2010','-'));
            text=Regex.Replace(text,@"([A-Za-z]+)[-\u2010\u00ad][ \t]*\r?\n[ \t]*([A-Za-z]+)",delegate(Match match){string left=match.Groups[1].Value,right=match.Groups[2].Value,joined=left+right,compound=left+"-"+right;if(known.Contains(compound))return compound;if(known.Contains(joined))return joined;if(words.Value.Contains(compound))return compound;if(words.Value.Contains(joined))return joined;return compound;});
            text=text.Replace("\u00ad","");var paragraphs=Regex.Split(text.Trim(),@"\r?\n[ \t]*\r?\n");
            for(int i=0;i<paragraphs.Length;i++){string value=Regex.Replace(paragraphs[i],@"(?<=[\u4e00-\u9fff])\r?\n(?=[\u4e00-\u9fff])","");paragraphs[i]=Regex.Replace(value,@"[\s]+"," ").Trim();}
            return String.Join("\n\n",paragraphs).Trim();
        }
        internal static List<Chunk> Chunks(string text){return Chunks(text,8000);}
        internal static List<Chunk> Chunks(string text,int maximum) {
            maximum=Math.Max(256,maximum);var result=new List<Chunk>();foreach(string paragraph in Regex.Split(Normalize(text),@"\n\n")){string value=paragraph;while(value.Length>maximum){int cut=value.LastIndexOf(' ',maximum-1,maximum/2);if(cut<0)cut=maximum;if(Char.IsHighSurrogate(value[cut-1]))cut--;result.Add(new Chunk{Text=value.Substring(0,cut).Trim(),After=" "});value=value.Substring(cut).Trim();}if(value.Length>0)result.Add(new Chunk{Text=value,After="\n\n"});}return result;
        }
    }
}
