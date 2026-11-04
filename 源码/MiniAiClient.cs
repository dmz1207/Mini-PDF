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
        static string warmRoot;
        static DateTime warmedAt;
        static int readers;
        static readonly Dictionary<string,FileStream> clientLeases=new Dictionary<string,FileStream>(StringComparer.OrdinalIgnoreCase);
        static string cachedSelection,cachedRoot,cachedExecutable;
        static DateTime cachedWrite;
        sealed class PackageProbe {
            internal string Home,Root,Portable;
            internal bool Available;
            internal long Stamp;
        }
        static readonly object probeGate=new object();
        static PackageProbe probe;
        // A short snapshot keeps mouse/menu events off the filesystem. It is
        // keyed by reader location and expires so adding/removing a pack is seen.
        static PackageProbe Probe() {
            string home=Home;long now=Stopwatch.GetTimestamp();
            lock(probeGate){
                if(probe!=null&&probe.Home==home&&now-probe.Stamp<Stopwatch.Frequency/2)return probe;
                string settings=Path.Combine(home,"MiniPDF.MiniAI.txt"),selection="";
                try{if(File.Exists(settings))selection=File.ReadAllText(settings).Trim();}catch(IOException){}catch(UnauthorizedAccessException){}
                string root,executable;
                if(!ResolveSelection(selection,out root,out executable)){
                    root=Path.Combine(home,"MiniAI");string parent=Path.GetDirectoryName(home);
                    if(!Directory.Exists(root)&&!File.Exists(Path.Combine(home,"MiniAI-Portable.exe"))&&!String.IsNullOrEmpty(parent)){
                        string beside=Path.Combine(parent,"MiniAI");if(Directory.Exists(beside)||File.Exists(Path.Combine(parent,"MiniAI-Portable.exe")))root=beside;
                    }
                    executable=Path.Combine(Path.GetDirectoryName(root),"MiniAI-Portable.exe");
                }
                probe=new PackageProbe{Home=home,Root=root,Portable=executable??"",Available=File.Exists(executable??"")||Installed(root),Stamp=now};return probe;
            }
        }
        internal static void InvalidatePackage(){lock(probeGate){probe=null;}}
        static bool HasPortablePayload(string path) {
            try{using(var stream=File.OpenRead(path)){if(stream.ReadByte()!='M'||stream.ReadByte()!='Z')return false;using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))return zip.GetEntry("MiniAI/package-checksums.json")!=null&&zip.GetEntry("MiniAI/MiniAI.exe")!=null&&(zip.GetEntry("MiniAI/models/translation/hy-mt2.gguf")!=null||zip.GetEntry("MiniAI/models/translation/eng-zho/model.bin")!=null&&zip.GetEntry("MiniAI/models/translation/zho-eng/model.bin")!=null)&&zip.GetEntry("MiniAI/models/ocr/rec.onnx")!=null;}}
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
        static bool Installed(string root){return File.Exists(Path.Combine(root,"MiniAI.exe"))&&(File.Exists(Path.Combine(root,"models/translation/hy-mt2.gguf"))||File.Exists(Path.Combine(root,"models/translation/eng-zho/model.bin"))&&File.Exists(Path.Combine(root,"models/translation/zho-eng/model.bin")))&&File.Exists(Path.Combine(root,"models/ocr/rec.onnx"));}
        internal static void SelectPackage(string path){string root,executable;PdfDocument.Check(ResolveSelection(path,out root,out executable)&&(executable!=null||Installed(root)),UiText.Get("请选择完整的 MiniAI-Portable.exe，或已解包目录中的 MiniAI.exe。"));File.WriteAllText(Path.Combine(Home,"MiniPDF.MiniAI.txt"),Path.GetFullPath(path),new UTF8Encoding(false));InvalidatePackage();}
        internal static string Home {get{string launcher=Environment.GetEnvironmentVariable("MINIPDF_LAUNCHER");return String.IsNullOrEmpty(launcher)?Path.GetDirectoryName(Application.ExecutablePath):Path.GetDirectoryName(launcher);}}
        internal static string PackRoot {
            get{return Probe().Root;}
        }
        internal static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=32};}
        static Dictionary<string,object> Connection(string root){var data=Serializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"state","connection.json"),Encoding.UTF8));Uri url;PdfDocument.Check(data.ContainsKey("url")&&Uri.TryCreate(Convert.ToString(data["url"]),UriKind.Absolute,out url)&&url.Scheme=="http"&&url.Host=="127.0.0.1"&&url.AbsolutePath=="/"&&String.IsNullOrEmpty(url.UserInfo),"Invalid MiniAI local address.");return data;}
        static string PortablePath(string root){var found=Probe();if(String.Equals(Path.GetFullPath(root),found.Root,StringComparison.OrdinalIgnoreCase))return found.Portable;string parent=Path.GetDirectoryName(Path.GetFullPath(root));return String.Equals(Path.GetFileName(root.TrimEnd('\\','/')),"MiniAI",StringComparison.OrdinalIgnoreCase)&&!String.IsNullOrEmpty(parent)?Path.Combine(parent,"MiniAI-Portable.exe"):"";}
        internal static bool AvailableAt(string root){return File.Exists(PortablePath(root))||Installed(root);}
        internal static bool Available {get{return Probe().Available;}}
        internal static void AttachReader(){lock(packageGate){readers++;}}
        internal static void ReleaseReader() {
            var roots=new List<string>();lock(packageGate){if(readers>0)readers--;if(readers>0)return;foreach(var item in clientLeases){item.Value.Dispose();roots.Add(item.Key);}clientLeases.Clear();warmRoot=null;}
            foreach(string root in roots)ReleaseService(root);
        }
        static void ReleaseService(string root){try{Request(root,"/v1/client-release",new{closed=true},2);}catch(IOException){}catch(WebException){}catch(InvalidOperationException){}}
        internal static void Prewarm() {
            string root=PackRoot;if(!AvailableAt(root))return;
            lock(packageGate){if(root==warmRoot&&(DateTime.UtcNow-warmedAt).TotalMinutes<2)return;warmRoot=root;warmedAt=DateTime.UtcNow;}
            ThreadPool.QueueUserWorkItem(delegate{try{Ensure(root);bool abandoned;lock(packageGate){abandoned=readers==0;if(!abandoned&&!clientLeases.ContainsKey(root)){string folder=Path.Combine(root,"state/clients");Directory.CreateDirectory(folder);clientLeases[root]=new FileStream(Path.Combine(folder,Process.GetCurrentProcess().Id+"-"+Guid.NewGuid().ToString("N")+".lock"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,1,FileOptions.DeleteOnClose);}}if(abandoned)ReleaseService(root);}catch(IOException){}catch(WebException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}});
        }
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
        internal static readonly string[] LanguageCodes=MiniAI.Desktop.LanguageCatalog.Codes;
        internal static string[] LanguageLabels {get{return MiniAI.Desktop.LanguageCatalog.Labels(UiText.English);}}
        internal static string AutoTarget(string text){int chinese=0,letters=0;foreach(char c in text??""){if(c>=0x4e00&&c<=0x9fff)chinese++;else if(Char.IsLetter(c))letters++;}return chinese>0&&chinese>letters/3?"en":"zh";}
        internal static List<OcrLine> Recognize(string root,byte[] image,int page=0,Func<bool> canceled=null,string mode="quality",bool formulas=false) {
            var response=Request(root,"/v1/ocr",new{image_base64=Convert.ToBase64String(image),mode=mode,formula=formulas},300,canceled);
            var result=ReadOcr(response,page);
            if(formulas)using(var stream=new MemoryStream(image))using(var bitmap=new Bitmap(stream))return FormulaRegions.Refine(bitmap,result,root,canceled);
            return result;
        }
        internal static List<OcrLine> ReadOcr(Dictionary<string,object> response,int page=0){
            var items=response["lines"] as System.Collections.IList;PdfDocument.Check(items!=null&&items.Count<=5000,"Invalid MiniAI OCR response.");var result=new List<OcrLine>();
            foreach(Dictionary<string,object> item in items){object tex,kind,score;var line=new OcrLine{Page=page,Text=Convert.ToString(item["text"]),Confidence=Convert.ToSingle(item["confidence"]),Tex=item.TryGetValue("tex",out tex)?Convert.ToString(tex):"",Kind=item.TryGetValue("kind",out kind)?Convert.ToString(kind):"text",FormulaConfidence=item.TryGetValue("formula_confidence",out score)?Convert.ToSingle(score):0,Corners=new PointF[4]};var corners=(System.Collections.IList)item["box"];PdfDocument.Check(corners.Count==4&&line.Text.Length<=10000&&line.Tex.Length<=20000,"Invalid OCR result.");for(int i=0;i<4;i++){var pair=(System.Collections.IList)corners[i];float x=Convert.ToSingle(pair[0]),y=Convert.ToSingle(pair[1]);PdfDocument.Check(!Single.IsNaN(x)&&!Single.IsNaN(y)&&x>=-1&&x<=2&&y>=-1&&y<=2,"Invalid OCR coordinates.");line.Corners[i]=new PointF(Math.Max(0,Math.Min(1,x)),Math.Max(0,Math.Min(1,y)));}result.Add(line);}return result;
        }
        internal static string TranslateText(string root,string text,string target,Func<bool> canceled=null) {
            var output=new StringBuilder();foreach(string part in MiniAiWorker.Parts(text)){if(canceled!=null&&canceled())throw new OperationCanceledException();var reply=Request(root,"/v1/translate",new{text=part,target=target},600,canceled);PdfDocument.Check(!Convert.ToBoolean(reply["truncated"]),UiText.Get("译文超过输出限制，请缩小选区。"));if(output.Length>0)output.AppendLine().AppendLine();output.Append(Convert.ToString(reply["text"]));}return output.ToString();
        }
        internal static string TranslateStreaming(string root,string text,string target,Func<bool> canceled,Action<string> progress,string mode="fast") {
            Ensure(root);var state=Connection(root);var output=new StringBuilder();
            foreach(var chunk in MiniAI.Desktop.TranslationText.Chunks(text)){string part=chunk.Text;if(canceled())throw new OperationCanceledException();string previous=output.ToString();string value=MiniAI.Desktop.TranslationStream.Read(Convert.ToString(state["url"]),Convert.ToString(state["token"]),part,target,canceled,delegate(string partial){progress(previous+partial);},mode);output.Append(value).Append(chunk.After);}return output.ToString().TrimEnd();
        }
        internal static void KeepResult(string root,string kind,string source,string target,string text) {
            if(String.IsNullOrWhiteSpace(text))return;try{Request(root,"/v1/results",new{kind=kind,source=source,target=target,text=text},10);}catch(IOException error){var web=error.InnerException as WebException;var response=web==null?null:web.Response as HttpWebResponse;if(response==null||response.StatusCode!=HttpStatusCode.NotFound)throw;response.Dispose();}
        }
        internal static void Ensure(string root) {
            string portable=PortablePath(root),installed=Path.Combine(root,"MiniAI.exe");bool usePortable=File.Exists(portable);
            if(usePortable&&Installed(root)){Version a,b;if(Version.TryParse(FileVersionInfo.GetVersionInfo(installed).FileVersion,out a)&&Version.TryParse(FileVersionInfo.GetVersionInfo(portable).FileVersion,out b)&&a>b)usePortable=false;}
            string executable=usePortable?portable:installed;
            PdfDocument.Check(File.Exists(executable),UiText.Get("请先加载 MiniAI 离线功能包。"));
            try{Request(root,"/health",null,2);return;}catch(IOException){}catch(WebException){}catch(ArgumentException){}
            string state=Path.Combine(root,"state");Directory.CreateDirectory(state);FileStream lease=null;
            int limit=executable==portable?120000:10000;var leaseWatch=Stopwatch.StartNew();
            while(lease==null&&leaseWatch.ElapsedMilliseconds<limit){try{lease=new FileStream(Path.Combine(state,"start.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){Thread.Sleep(100);}}
            PdfDocument.Check(lease!=null,"Another MiniAI instance is still starting.");
            using(lease) {
                try{Request(root,"/health",null,2);return;}catch(IOException){}catch(WebException){}catch(ArgumentException){}
                var startup=new ProcessStartInfo(executable,"--headless --port 0"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(executable)};startup.EnvironmentVariables["MINIAI_OWNER_PID"]=Process.GetCurrentProcess().Id.ToString();
                using(var process=Process.Start(startup)) {
                    var startupWatch=Stopwatch.StartNew();while(startupWatch.ElapsedMilliseconds<limit){Thread.Sleep(100);try{Request(root,"/health",null,2);return;}catch(IOException){}catch(WebException){}catch(ArgumentException){}if(process.HasExited)break;}
                    throw new IOException(UiText.Get("MiniAI 本地服务未能启动，请检查功能包。"));
                }
            }
        }
    }
    internal sealed class OcrLine {
        internal int Page;
        internal string Text,Tex="",Kind="text";
        internal float Confidence,FormulaConfidence;
        internal PointF[] Corners;
        internal void Write(BinaryWriter writer){writer.Write(Page);writer.Write(Text);writer.Write(Confidence);foreach(var p in Corners){writer.Write(p.X);writer.Write(p.Y);}}
        internal static OcrLine Read(BinaryReader reader){var line=new OcrLine{Page=reader.ReadInt32(),Text=reader.ReadString(),Confidence=reader.ReadSingle(),Corners=new PointF[4]};PdfDocument.Check(line.Text.Length<=10000,"Invalid OCR text length.");for(int i=0;i<4;i++)line.Corners[i]=new PointF(reader.ReadSingle(),reader.ReadSingle());return line;}
    }
    internal static partial class MiniAiWorker {
        internal static void Ocr(PdfDocument document,BinaryReader reader,BinaryWriter writer) {
            string root=reader.ReadString();bool skipText=reader.ReadBoolean();int count=reader.ReadInt32();PdfDocument.Check(count>0&&count<=2000,"Invalid OCR page count.");var pages=new int[count];for(int i=0;i<count;i++){pages[i]=reader.ReadInt32();PdfDocument.Check(pages[i]>=0&&pages[i]<document.Count,"Invalid OCR page.");}MiniAiClient.Ensure(root);
            for(int n=0;n<pages.Length;n++) {
                int page=pages[n];using(var text=new PdfTextPage(document.Handle,page))if(skipText&&text.Count>0){writer.Write(3);writer.Write(page);writer.Flush();continue;}
                writer.Write(0);writer.Write(n+1);writer.Write(pages.Length);writer.Write(page);writer.Flush();var size=document.PageSize(page);float scale=Math.Min(300/72f,4096/Math.Max(size.Width,size.Height));
                using(var image=document.Render(page,Math.Max(1,(int)Math.Ceiling(size.Width*scale)),Math.Max(1,(int)Math.Ceiling(size.Height*scale))))using(var data=new MemoryStream()) {
                    image.Save(data,System.Drawing.Imaging.ImageFormat.Png);var response=MiniAiClient.Request(root,"/v1/ocr",new{image_base64=Convert.ToBase64String(data.ToArray())});MiniAiClient.KeepResult(root,"ocr","","",Convert.ToString(response["text"]));var lines=response["lines"] as System.Collections.IList;PdfDocument.Check(lines!=null&&lines.Count<=5000,"Invalid MiniAI OCR response.");writer.Write(4);writer.Write(lines.Count);
                    foreach(Dictionary<string,object> item in lines){var line=new OcrLine{Page=page,Text=Convert.ToString(item["text"]),Confidence=Convert.ToSingle(item["confidence"]),Corners=new PointF[4]};var corners=(System.Collections.IList)item["box"];PdfDocument.Check(corners.Count==4,"Invalid OCR coordinates.");for(int i=0;i<4;i++){var pair=(System.Collections.IList)corners[i];line.Corners[i]=new PointF(Convert.ToSingle(pair[0]),Convert.ToSingle(pair[1]));}line.Write(writer);}writer.Flush();
                }
            }
            writer.Write(1);writer.Flush();
        }
        internal static void Translate(BinaryReader reader,BinaryWriter writer) {
            string root=reader.ReadString(),target=reader.ReadString(),mode=reader.ReadString(),text=reader.ReadString();PdfDocument.Check(text.Length>0&&text.Length<=100000,"Translation input exceeds 100000 characters.");MiniAiClient.Ensure(root);var blocks=MiniAI.Desktop.TranslationText.Chunks(text);
            var complete=new StringBuilder();for(int i=0;i<blocks.Count;i++){writer.Write(0);writer.Write(i+1);writer.Write(blocks.Count);writer.Flush();string result=MiniAiClient.TranslateStreaming(root,blocks[i].Text,target,delegate{return false;},delegate(string partial){writer.Write(5);writer.Write(partial);writer.Flush();},mode);writer.Write(4);writer.Write(result+blocks[i].After);writer.Flush();complete.Append(result).Append(blocks[i].After);}MiniAiClient.KeepResult(root,"translation",text,target,complete.ToString().TrimEnd());
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
