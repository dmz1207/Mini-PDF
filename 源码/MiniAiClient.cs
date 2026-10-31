using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MiniPdf {
    internal static class MiniAiClient {
        internal static string Home {get{string launcher=Environment.GetEnvironmentVariable("MINIPDF_LAUNCHER");return String.IsNullOrEmpty(launcher)?Path.GetDirectoryName(Application.ExecutablePath):Path.GetDirectoryName(launcher);}}
        internal static string PackRoot {
            get{string settings=Path.Combine(Home,"MiniPDF.MiniAI.txt");if(File.Exists(settings)){string path=File.ReadAllText(settings).Trim();if(Directory.Exists(path))return Path.GetFullPath(path);}string beside=Path.Combine(Home,"MiniAI");if(Directory.Exists(beside))return beside;string parentDirectory=Path.GetDirectoryName(Home);if(String.IsNullOrEmpty(parentDirectory))return beside;string parent=Path.Combine(parentDirectory,"MiniAI");return Directory.Exists(parent)?parent:beside;}
        }
        internal static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=32};}
        static Dictionary<string,object> Connection(string root){var data=Serializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"state","connection.json"),Encoding.UTF8));Uri url;PdfDocument.Check(data.ContainsKey("url")&&Uri.TryCreate(Convert.ToString(data["url"]),UriKind.Absolute,out url)&&url.Scheme=="http"&&url.Host=="127.0.0.1"&&url.AbsolutePath=="/"&&String.IsNullOrEmpty(url.UserInfo),"Invalid MiniAI local address.");return data;}
        internal static bool Available {get{return File.Exists(Path.Combine(PackRoot,"MiniAI.exe"))&&File.Exists(Path.Combine(PackRoot,"models","translation","hy-mt2.gguf"))&&File.Exists(Path.Combine(PackRoot,"models","ocr","rec.onnx"));}}
        internal static Dictionary<string,object> Request(string root,string path,object body,int seconds=300,Func<bool> canceled=null) {
            for(int attempt=0;;attempt++)try{return RequestOnce(root,path,body,seconds,canceled);}catch(WebException error){var reply=error.Response as HttpWebResponse;if(reply==null||reply.StatusCode!=(HttpStatusCode)429||attempt>=20)throw;reply.Dispose();if(canceled!=null&&canceled())throw new OperationCanceledException();Thread.Sleep(75);}
        }
        static Dictionary<string,object> RequestOnce(string root,string path,object body,int seconds,Func<bool> canceled) {
            var state=Connection(root);var request=(HttpWebRequest)WebRequest.Create(Convert.ToString(state["url"])+path);request.Proxy=null;request.KeepAlive=false;request.Timeout=request.ReadWriteTimeout=seconds*1000;request.Headers["Authorization"]="Bearer "+Convert.ToString(state["token"]);
            if(body!=null){byte[] bytes=Encoding.UTF8.GetBytes(Serializer().Serialize(body));request.Method="POST";request.ContentType="application/json; charset=utf-8";request.ContentLength=bytes.Length;using(var stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);}
            using(var timer=canceled==null?null:new System.Threading.Timer(delegate{if(canceled())request.Abort();},null,50,50))
            try{if(canceled!=null&&canceled())throw new OperationCanceledException();using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))return Serializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());}
            catch(WebException error){if(canceled!=null&&canceled())throw new OperationCanceledException();var response=error.Response;if(response is HttpWebResponse&&((HttpWebResponse)response).StatusCode==(HttpStatusCode)429)throw;if(response!=null)using(response)using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)){var bodyText=reader.ReadToEnd();if(bodyText.Length<8192)throw new IOException(bodyText,error);}throw;}
        }
        internal static readonly string[] LanguageCodes={"zh","en","ja","ko","fr","de","es","ru","pt","it","zh-Hant"};
        internal static readonly string[] LanguageLabels={"中文","English","日本語","한국어","Français","Deutsch","Español","Русский","Português","Italiano","繁體中文"};
        internal static string AutoTarget(string text){int chinese=0,letters=0;foreach(char c in text??""){if(c>=0x4e00&&c<=0x9fff)chinese++;else if(Char.IsLetter(c))letters++;}return chinese>0&&chinese>letters/3?"en":"zh";}
        internal static List<OcrLine> Recognize(string root,byte[] image,int page=0,Func<bool> canceled=null) {
            var response=Request(root,"/v1/ocr",new{image_base64=Convert.ToBase64String(image)},300,canceled);var items=response["lines"] as System.Collections.IList;
            PdfDocument.Check(items!=null&&items.Count<=5000,"Invalid MiniAI OCR response.");var result=new List<OcrLine>();
            foreach(Dictionary<string,object> item in items){var line=new OcrLine{Page=page,Text=Convert.ToString(item["text"]),Confidence=Convert.ToSingle(item["confidence"]),Corners=new PointF[4]};var corners=(System.Collections.IList)item["box"];PdfDocument.Check(corners.Count==4&&line.Text.Length<=10000,"Invalid OCR result.");for(int i=0;i<4;i++){var pair=(System.Collections.IList)corners[i];float x=Convert.ToSingle(pair[0]),y=Convert.ToSingle(pair[1]);PdfDocument.Check(!Single.IsNaN(x)&&!Single.IsNaN(y)&&x>=-1&&x<=2&&y>=-1&&y<=2,"Invalid OCR coordinates.");line.Corners[i]=new PointF(Math.Max(0,Math.Min(1,x)),Math.Max(0,Math.Min(1,y)));}result.Add(line);}return result;
        }
        internal static string TranslateText(string root,string text,string target,Func<bool> canceled=null) {
            var output=new StringBuilder();foreach(string part in MiniAiWorker.Parts(text)){if(canceled!=null&&canceled())throw new OperationCanceledException();var reply=Request(root,"/v1/translate",new{text=part,target=target},600,canceled);PdfDocument.Check(!Convert.ToBoolean(reply["truncated"]),UiText.Get("译文超过输出限制，请缩小选区。"));if(output.Length>0)output.AppendLine().AppendLine();output.Append(Convert.ToString(reply["text"]));}return output.ToString();
        }
        internal static void Ensure(string root) {
            PdfDocument.Check(File.Exists(Path.Combine(root,"MiniAI.exe")),UiText.Get("请先加载 MiniAI 离线功能包。"));
            try{Request(root,"/health",null,2);return;}catch(IOException){}catch(WebException){}catch(ArgumentException){}
            string state=Path.Combine(root,"state");Directory.CreateDirectory(state);FileStream lease=null;
            for(int i=0;i<100&&lease==null;i++){try{lease=new FileStream(Path.Combine(state,"start.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){Thread.Sleep(100);}}
            PdfDocument.Check(lease!=null,"Another MiniAI instance is still starting.");
            using(lease) {
                try{Request(root,"/health",null,2);return;}catch(IOException){}catch(WebException){}catch(ArgumentException){}
                using(var process=Process.Start(new ProcessStartInfo(Path.Combine(root,"MiniAI.exe"),"--headless --port 0"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=root})) {
                    for(int i=0;i<100;i++){Thread.Sleep(100);if(process.HasExited)break;try{Request(root,"/health",null,2);return;}catch(IOException){}catch(WebException){}catch(ArgumentException){}}
                    throw new IOException(UiText.Get("MiniAI 本地服务未能启动，请检查功能包。"));
                }
            }
        }
    }
    internal sealed class OcrLine {
        internal int Page;
        internal string Text;
        internal float Confidence;
        internal PointF[] Corners;
        internal void Write(BinaryWriter writer){writer.Write(Page);writer.Write(Text);writer.Write(Confidence);foreach(var p in Corners){writer.Write(p.X);writer.Write(p.Y);}}
        internal static OcrLine Read(BinaryReader reader){var line=new OcrLine{Page=reader.ReadInt32(),Text=reader.ReadString(),Confidence=reader.ReadSingle(),Corners=new PointF[4]};PdfDocument.Check(line.Text.Length<=10000,"Invalid OCR text length.");for(int i=0;i<4;i++)line.Corners[i]=new PointF(reader.ReadSingle(),reader.ReadSingle());return line;}
    }
    internal static class MiniAiWorker {
        internal static void Ocr(PdfDocument document,BinaryReader reader,BinaryWriter writer) {
            string root=reader.ReadString();bool skipText=reader.ReadBoolean();int count=reader.ReadInt32();PdfDocument.Check(count>0&&count<=2000,"Invalid OCR page count.");var pages=new int[count];for(int i=0;i<count;i++){pages[i]=reader.ReadInt32();PdfDocument.Check(pages[i]>=0&&pages[i]<document.Count,"Invalid OCR page.");}MiniAiClient.Ensure(root);
            for(int n=0;n<pages.Length;n++) {
                int page=pages[n];using(var text=new PdfTextPage(document.Handle,page))if(skipText&&text.Count>0){writer.Write(3);writer.Write(page);writer.Flush();continue;}
                writer.Write(0);writer.Write(n+1);writer.Write(pages.Length);writer.Write(page);writer.Flush();var size=document.PageSize(page);float scale=Math.Min(300/72f,4096/Math.Max(size.Width,size.Height));
                using(var image=document.Render(page,Math.Max(1,(int)Math.Ceiling(size.Width*scale)),Math.Max(1,(int)Math.Ceiling(size.Height*scale))))using(var data=new MemoryStream()) {
                    image.Save(data,System.Drawing.Imaging.ImageFormat.Png);var response=MiniAiClient.Request(root,"/v1/ocr",new{image_base64=Convert.ToBase64String(data.ToArray())});var lines=response["lines"] as System.Collections.IList;PdfDocument.Check(lines!=null&&lines.Count<=5000,"Invalid MiniAI OCR response.");writer.Write(4);writer.Write(lines.Count);
                    foreach(Dictionary<string,object> item in lines){var line=new OcrLine{Page=page,Text=Convert.ToString(item["text"]),Confidence=Convert.ToSingle(item["confidence"]),Corners=new PointF[4]};var corners=(System.Collections.IList)item["box"];PdfDocument.Check(corners.Count==4,"Invalid OCR coordinates.");for(int i=0;i<4;i++){var pair=(System.Collections.IList)corners[i];line.Corners[i]=new PointF(Convert.ToSingle(pair[0]),Convert.ToSingle(pair[1]));}line.Write(writer);}writer.Flush();
                }
            }
            writer.Write(1);writer.Flush();
        }
        internal static void Translate(BinaryReader reader,BinaryWriter writer) {
            string root=reader.ReadString(),target=reader.ReadString(),text=reader.ReadString();PdfDocument.Check(text.Length>0&&text.Length<=100000,"Translation input exceeds 100000 characters.");MiniAiClient.Ensure(root);var blocks=Parts(text);
            for(int i=0;i<blocks.Count;i++){writer.Write(0);writer.Write(i+1);writer.Write(blocks.Count);writer.Flush();var result=MiniAiClient.Request(root,"/v1/translate",new{text=blocks[i],target=target},600);PdfDocument.Check(!Convert.ToBoolean(result["truncated"]),"The translation reached its output limit. Please translate a shorter selection.");writer.Write(4);writer.Write(Convert.ToString(result["text"]));writer.Flush();}
            writer.Write(1);writer.Flush();
        }
        internal static List<string> Parts(string text) {
            text=System.Text.RegularExpressions.Regex.Replace(text,@"(?<=\p{L})-\r?\n(?=\p{L})","");var result=new List<string>();
            foreach(string paragraph in System.Text.RegularExpressions.Regex.Split(text,@"\r?\n\s*\r?\n")) {
                string value=System.Text.RegularExpressions.Regex.Replace(paragraph,@"\r?\n"," ").Trim();
                while(value.Length>900){int cut=900;for(int i=900;i>=450;i--)if(Char.IsWhiteSpace(value[i])||"。！？.!?;；".IndexOf(value[i])>=0){cut=i+1;break;}if(Char.IsHighSurrogate(value[cut-1]))cut--;result.Add(value.Substring(0,cut).Trim());value=value.Substring(cut).Trim();}
                if(value.Length>0)result.Add(value);
            }
            return result;
        }
    }
}
