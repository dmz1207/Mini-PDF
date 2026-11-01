using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MiniPdf {
    internal static class MiniAiClient {
        static readonly object packageGate=new object();
        static string cachedSelection,cachedRoot,cachedExecutable;
        static DateTime cachedWrite;
        static string Selection {get{string settings=Path.Combine(Home,"MiniPDF.MiniAI.txt");return File.Exists(settings)?File.ReadAllText(settings).Trim():"";}}
        static bool HasPortablePayload(string path) {
            try{using(var stream=File.OpenRead(path)){if(stream.ReadByte()!='M'||stream.ReadByte()!='Z')return false;using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))return zip.GetEntry("MiniAI/package-checksums.json")!=null&&zip.GetEntry("MiniAI/MiniAI.exe")!=null&&zip.GetEntry("MiniAI/models/translation/hy-mt2.gguf")!=null&&zip.GetEntry("MiniAI/models/ocr/rec.onnx")!=null;}}
            catch(IOException){return false;}catch(InvalidDataException){return false;}catch(UnauthorizedAccessException){return false;}
        }
        static bool ResolveSelection(string path,out string root,out string executable) {
            root=executable=null;if(String.IsNullOrWhiteSpace(path))return false;
            if(Directory.Exists(path)){root=Path.GetFullPath(path);return true;}
            if(!File.Exists(path))return false;path=Path.GetFullPath(path);DateTime write=File.GetLastWriteTimeUtc(path);
            lock(packageGate){if(path==cachedSelection&&write==cachedWrite){root=cachedRoot;executable=cachedExecutable;return true;}
                bool portable=HasPortablePayload(path);string folder=Path.GetDirectoryName(path);
                if(!portable&&(!String.Equals(Path.GetFileName(path),"MiniAI.exe",StringComparison.OrdinalIgnoreCase)||!Installed(folder)))return false;
                root=portable?Path.Combine(folder,"MiniAI"):folder;executable=portable?path:null;
                cachedSelection=path;cachedWrite=write;cachedRoot=root;cachedExecutable=executable;return true;}
        }
        static bool Installed(string root){return File.Exists(Path.Combine(root,"MiniAI.exe"))&&File.Exists(Path.Combine(root,"models","translation","hy-mt2.gguf"))&&File.Exists(Path.Combine(root,"models","ocr","rec.onnx"));}
        internal static void SelectPackage(string path){string root,executable;PdfDocument.Check(ResolveSelection(path,out root,out executable)&&(executable!=null||Installed(root)),UiText.Get("请选择完整的 MiniAI-Portable.exe，或已解包目录中的 MiniAI.exe。"));File.WriteAllText(Path.Combine(Home,"MiniPDF.MiniAI.txt"),Path.GetFullPath(path),new UTF8Encoding(false));}
        internal static string Home {get{string launcher=Environment.GetEnvironmentVariable("MINIPDF_LAUNCHER");return String.IsNullOrEmpty(launcher)?Path.GetDirectoryName(Application.ExecutablePath):Path.GetDirectoryName(launcher);}}
        internal static string PackRoot {
            get{string root,executable;if(ResolveSelection(Selection,out root,out executable))return root;string beside=Path.Combine(Home,"MiniAI");if(Directory.Exists(beside)||File.Exists(Path.Combine(Home,"MiniAI-Portable.exe")))return beside;string parentDirectory=Path.GetDirectoryName(Home);if(String.IsNullOrEmpty(parentDirectory))return beside;string parent=Path.Combine(parentDirectory,"MiniAI");return Directory.Exists(parent)||File.Exists(Path.Combine(parentDirectory,"MiniAI-Portable.exe"))?parent:beside;}
        }
        internal static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=32};}
        static Dictionary<string,object> Connection(string root){var data=Serializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"state","connection.json"),Encoding.UTF8));Uri url;PdfDocument.Check(data.ContainsKey("url")&&Uri.TryCreate(Convert.ToString(data["url"]),UriKind.Absolute,out url)&&url.Scheme=="http"&&url.Host=="127.0.0.1"&&url.AbsolutePath=="/"&&String.IsNullOrEmpty(url.UserInfo),"Invalid MiniAI local address.");return data;}
        static string PortablePath(string root){string selectedRoot,selectedExecutable;if(ResolveSelection(Selection,out selectedRoot,out selectedExecutable)&&selectedExecutable!=null&&String.Equals(Path.GetFullPath(root),selectedRoot,StringComparison.OrdinalIgnoreCase))return selectedExecutable;string parent=Path.GetDirectoryName(Path.GetFullPath(root));return String.Equals(Path.GetFileName(root.TrimEnd('\\','/')),"MiniAI",StringComparison.OrdinalIgnoreCase)&&!String.IsNullOrEmpty(parent)?Path.Combine(parent,"MiniAI-Portable.exe"):"";}
        internal static bool AvailableAt(string root){return File.Exists(PortablePath(root))||Installed(root);}
        internal static bool Available {get{return AvailableAt(PackRoot);}}
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
            string portable=PortablePath(root),executable=File.Exists(portable)?portable:Path.Combine(root,"MiniAI.exe");
            PdfDocument.Check(File.Exists(executable),UiText.Get("请先加载 MiniAI 离线功能包。"));
            try{Request(root,"/health",null,2);return;}catch(IOException){}catch(WebException){}catch(ArgumentException){}
            string state=Path.Combine(root,"state");Directory.CreateDirectory(state);FileStream lease=null;
            int limit=executable==portable?120000:10000;var leaseWatch=Stopwatch.StartNew();
            while(lease==null&&leaseWatch.ElapsedMilliseconds<limit){try{lease=new FileStream(Path.Combine(state,"start.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){Thread.Sleep(100);}}
            PdfDocument.Check(lease!=null,"Another MiniAI instance is still starting.");
            using(lease) {
                try{Request(root,"/health",null,2);return;}catch(IOException){}catch(WebException){}catch(ArgumentException){}
                using(var process=Process.Start(new ProcessStartInfo(executable,"--headless --port 0"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(executable)})) {
                    var startupWatch=Stopwatch.StartNew();while(startupWatch.ElapsedMilliseconds<limit){Thread.Sleep(100);try{Request(root,"/health",null,2);return;}catch(IOException){}catch(WebException){}catch(ArgumentException){}if(process.HasExited)break;}
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
