using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace MiniPdf.Boot {
    internal static class Launcher {
        const string Prefix=".MiniPDF-runtime-";
        static bool OwnedFolder(string folder,string root){string full=Path.GetFullPath(folder);return full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith(Prefix,StringComparison.Ordinal)&&((File.GetAttributes(full)&FileAttributes.ReparsePoint)==0);}
        static void RemoveFolder(string folder,string root){for(int attempt=0;attempt<12;attempt++){try{if(!Directory.Exists(folder))return;if(!OwnedFolder(folder,root))return;string manifest=Path.Combine(folder,".payload-files");if(!File.Exists(manifest))return;string prefix=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;var owned=new HashSet<string>(StringComparer.OrdinalIgnoreCase){Path.GetFullPath(manifest),Path.Combine(folder,".lease")};foreach(string relative in File.ReadAllLines(manifest)){string file=Path.GetFullPath(Path.Combine(folder,relative));if(Path.IsPathRooted(relative)||!file.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))return;owned.Add(file);}bool userFiles=false;foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))if(!owned.Contains(Path.GetFullPath(file))){userFiles=true;break;}foreach(string file in owned)if(file!=manifest&&File.Exists(file))File.Delete(file);if(userFiles){File.SetAttributes(folder,File.GetAttributes(folder)&~FileAttributes.Hidden);return;}File.Delete(manifest);Directory.Delete(folder,true);return;}catch(IOException){}catch(UnauthorizedAccessException){}System.Threading.Thread.Sleep(100);}}
        static void CleanupStale(string root){foreach(string folder in Directory.GetDirectories(root,Prefix+"*")){try{if(!OwnedFolder(folder,root))continue;string lease=Path.Combine(folder,".lease");using(var f=new FileStream(lease,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){}RemoveFolder(folder,root);}catch(IOException){}catch(UnauthorizedAccessException){}}}
        static string Quote(string value){if(value.Length==0)return "\"\"";var b=new StringBuilder("\"");int slashes=0;foreach(char ch in value){if(ch=='\\'){slashes++;continue;}if(ch=='\"'){b.Append('\\',slashes*2+1).Append(ch);slashes=0;continue;}b.Append('\\',slashes).Append(ch);slashes=0;}b.Append('\\',slashes*2).Append('"');return b.ToString();}
        static void Extract(string directory){byte[] packed;using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("MiniPDF.Payload")){if(resource==null||resource.Length<=0||resource.Length>128*1024*1024)throw new IOException("程序资源损坏。");packed=new byte[(int)resource.Length];int offset=0;while(offset<packed.Length){int read=resource.Read(packed,offset,packed.Length-offset);if(read<=0)throw new EndOfStreamException("程序资源不完整。");offset+=read;}}using(var raw=new MemoryStream(PayloadCompression.Transform(packed,false)))using(var reader=new BinaryReader(raw,Encoding.UTF8)){
            if(reader.ReadInt32()!=0x53554a31)throw new IOException("程序资源损坏，请重新复制完整 EXE。");int count=reader.ReadInt32();if(count<1||count>100)throw new IOException("程序资源数量无效。");
            string prefix=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            var extracted=new List<string>();for(int i=0;i<count;i++){string relative=reader.ReadString();string path=Path.GetFullPath(Path.Combine(directory,relative));if(Path.IsPathRooted(relative)||!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("程序资源路径无效。");int length=reader.ReadInt32();if(length<0||length>128*1024*1024)throw new IOException("程序资源大小无效。");Directory.CreateDirectory(Path.GetDirectoryName(path));extracted.Add(relative);File.WriteAllLines(Path.Combine(directory,".payload-files"),extracted.ToArray());using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var buffer=new byte[65536];int remaining=length;while(remaining>0){int read=reader.Read(buffer,0,Math.Min(remaining,buffer.Length));if(read<=0)throw new EndOfStreamException("程序资源不完整。");output.Write(buffer,0,read);remaining-=read;}}}
        }}
        [STAThread] static int Main(string[] args){string root=AppDomain.CurrentDomain.BaseDirectory,folder=null;FileStream lease=null;try{
            bool separate=false;var paths=new List<string>();foreach(string argument in args){if(argument=="--new-window")separate=true;else if(!argument.StartsWith("--",StringComparison.Ordinal))paths.Add(Path.GetFullPath(argument));}
            if(!separate&&OpenRequest.TryForward(root,paths.ToArray()))return 0;
            CleanupStale(root);folder=Path.Combine(root,Prefix+Process.GetCurrentProcess().Id+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(folder);
            File.SetAttributes(folder,File.GetAttributes(folder)|FileAttributes.Hidden);lease=new FileStream(Path.Combine(folder,".lease"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
            Extract(folder);var arguments=new StringBuilder();foreach(string argument in args){if(arguments.Length>0)arguments.Append(' ');arguments.Append(Quote(argument.StartsWith("--",StringComparison.Ordinal)?argument:Path.GetFullPath(argument)));}
            var start=new ProcessStartInfo(Path.Combine(folder,"MiniPDF.Core.exe"),arguments.ToString()){UseShellExecute=false,WorkingDirectory=root,CreateNoWindow=true};
            // All application-created temporary files stay beside the portable EXE.
            start.EnvironmentVariables["MINIPDF_LAUNCHER"]=Assembly.GetExecutingAssembly().Location;start.EnvironmentVariables["TEMP"]=folder;start.EnvironmentVariables["TMP"]=folder;
            using(var child=Process.Start(start)){child.WaitForExit();return child.ExitCode;}
        }catch(Exception e){MessageBox.Show(e.Message+"\n\n请把程序放在可写入的文件夹中运行。","Mini PDF 无法启动",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
        finally{if(lease!=null)lease.Dispose();if(folder!=null)RemoveFolder(folder,root);}}
    }
}
