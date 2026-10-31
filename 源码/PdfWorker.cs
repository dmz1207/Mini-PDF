using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MiniPdf {
    // Text extraction and optional inference use an isolated process, never a second PDFium thread.
    internal sealed class PdfJob : IDisposable {
        readonly Control owner;
        readonly PdfSource source;
        readonly string password;
        readonly object gate=new object();
        Process process;
        ProcessBudget budget;
        NamedPipeServerStream pipe;
        System.Threading.Timer watchdog;
        volatile bool canceled;
        long activity;
        internal PdfJob(Control control,PdfDocument document) {
            owner=control;
            if(document!=null){source=document.GetRenderSource().Retain();password=document.Password;}
        }
        internal void Post(Action action){try{if(!canceled&&!owner.IsDisposed&&owner.IsHandleCreated)owner.BeginInvoke(new Action(delegate{if(!canceled&&!owner.IsDisposed)action();}));}catch(InvalidOperationException){}}
        internal void Start(Action<BinaryWriter> request,Action<BinaryReader> receive,Action<Exception> finished,int idleSeconds) {
            new Thread(delegate() {
                Exception failure=null;
                try {
                    string name="MiniPDF-text-"+Guid.NewGuid().ToString("N");
                    var security=new PipeSecurity();using(var identity=WindowsIdentity.GetCurrent())security.AddAccessRule(new PipeAccessRule(identity.User,PipeAccessRights.FullControl,AccessControlType.Allow));
                    lock(gate){if(canceled)return;pipe=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,16384,16384,security);}
                    var connection=pipe;var ready=connection.BeginWaitForConnection(null,null);
                    try {
                        lock(gate){if(canceled)return;process=Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniPDF.Core.exe"),"--text-worker "+name){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});budget=new ProcessBudget(process);}
                        if(!ready.AsyncWaitHandle.WaitOne(15000))throw new TimeoutException("The text worker did not start.");
                        connection.EndWaitForConnection(ready);
                    }finally{ready.AsyncWaitHandle.Close();}
                    Interlocked.Exchange(ref activity,Stopwatch.GetTimestamp());
                    lock(gate){if(canceled)return;watchdog=new System.Threading.Timer(delegate{if((Stopwatch.GetTimestamp()-Interlocked.Read(ref activity))/(double)Stopwatch.Frequency>idleSeconds)Stop();},null,1000,1000);}
                    using(var writer=new BinaryWriter(connection,Encoding.UTF8,true))using(var reader=new BinaryReader(connection,Encoding.UTF8,true)) {
                        writer.Write(source==null?"":source.Name);writer.Write(source==null?0:source.Length);writer.Write(password??"");request(writer);writer.Flush();
                        receive(new ActivityReader(connection,delegate{Interlocked.Exchange(ref activity,Stopwatch.GetTimestamp());}));
                    }
                }catch(Exception error){failure=error;}
                finally{Stop();if(source!=null)source.Dispose();}
                if(!canceled)Post(delegate{finished(failure);});
            }){IsBackground=true,Name="MiniPDF text task"}.Start();
        }
        void Stop() {
            lock(gate){if(watchdog!=null){watchdog.Dispose();watchdog=null;}if(pipe!=null){pipe.Dispose();pipe=null;}if(process!=null){try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}process.Dispose();process=null;}if(budget!=null){budget.Dispose();budget=null;}}
        }
        public void Dispose(){canceled=true;Stop();}
        sealed class ActivityReader : BinaryReader {
            readonly Action touch;
            internal ActivityReader(Stream stream,Action callback):base(stream,Encoding.UTF8,true){touch=callback;}
            public override int ReadInt32(){int value=base.ReadInt32();touch();return value;}
        }
    }
    internal static class TextWorker {
        internal static int Run(string name) {
            using(var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut)) {
                pipe.Connect(15000);
                using(var reader=new BinaryReader(pipe,Encoding.UTF8,true))using(var writer=new BinaryWriter(pipe,Encoding.UTF8,true))try {
                    string mapping=reader.ReadString();long length=reader.ReadInt64();string password=reader.ReadString(),operation=reader.ReadString();
                    if(operation=="search") {
                        using(var source=PdfSource.OpenShared(mapping,length))using(var document=PdfDocument.FromShared(source.Retain(),password))SearchText.Work(document,reader,writer);
                    } else if(operation=="ocr") {
                        using(var source=PdfSource.OpenShared(mapping,length))using(var document=PdfDocument.FromShared(source.Retain(),password))MiniAiWorker.Ocr(document,reader,writer);
                    } else if(operation=="translate")MiniAiWorker.Translate(reader,writer);
                    else throw new IOException("Unknown text operation.");
                    return 0;
                }catch(Exception error){try{writer.Write(-1);writer.Write(error.Message);writer.Flush();}catch(IOException){}return 1;}
            }
        }
    }
}
