using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace MiniPdf {
    // One window per portable application directory and Windows user.
    internal sealed class SingleInstance : IDisposable {
        readonly string name;
        readonly Mutex mutex;
        readonly object gate=new object();
        NamedPipeServerStream server;
        volatile bool stopping;
        bool owns;
        internal SingleInstance(){name=OpenRequest.Name(Environment.CurrentDirectory);mutex=new Mutex(false,"Local\\"+name);owns=TryOwn();}
        bool TryOwn(){try{return mutex.WaitOne(0);}catch(AbandonedMutexException){return true;}}
        internal bool Forward(string[] paths){if(owns)return false;for(int attempt=0;attempt<3;attempt++){try{OpenRequest.Send(name,paths,1800);return true;}catch(TimeoutException){}catch(IOException){}if(TryOwn()){owns=true;return false;}}throw new IOException(UiText.Get("已有窗口正在启动或关闭，请稍后重试。"));}
        internal void Listen(Action<string[]> received){var thread=new Thread(delegate(){while(!stopping){try{var security=new PipeSecurity();using(var identity=WindowsIdentity.GetCurrent())security.AddAccessRule(new PipeAccessRule(identity.User,PipeAccessRights.FullControl,AccessControlType.Allow));using(var pipe=new NamedPipeServerStream(name,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security)){lock(gate){if(stopping)return;server=pipe;}pipe.WaitForConnection();using(var reader=new BinaryReader(pipe,Encoding.UTF8)){int length=reader.ReadInt32();if(length<4||length>1024*1024)continue;byte[] bytes=reader.ReadBytes(length);if(bytes.Length!=length)continue;using(var packet=new BinaryReader(new MemoryStream(bytes),Encoding.UTF8)){int count=packet.ReadInt32();if(count<0||count>1024)continue;var paths=new string[count];for(int i=0;i<count;i++){paths[i]=packet.ReadString();if(paths[i].Length>32767)throw new IOException(UiText.Get("文件路径过长。"));}received(paths);}}}}catch(IOException){}catch(ObjectDisposedException){}catch(InvalidOperationException){}finally{lock(gate){server=null;}}}}){IsBackground=true,Name="MiniPDF file requests"};thread.Start();}
        public void Dispose(){stopping=true;lock(gate){if(server!=null)server.Dispose();}if(owns){mutex.ReleaseMutex();owns=false;}mutex.Dispose();}
    }
}
