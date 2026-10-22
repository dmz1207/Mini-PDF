using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MiniPdf
{
    // Separate processes keep each PDFium instance single threaded. Read-only
    // snapshots and raster buffers are shared, never mutable PDFium handles.
    internal sealed class RenderFrame : IDisposable
    {
        readonly MemoryMappedFile mapping;
        readonly MemoryMappedViewAccessor view;
        internal readonly string Name;
        internal readonly Bitmap Image;
        internal IntPtr Pointer
        {
            get {
                return view.SafeMemoryMappedViewHandle.DangerousGetHandle();
            }
        }
        internal RenderFrame(int width, int height)
        {
            Name = "Local\\MiniPDF-raster-" + Guid.NewGuid().ToString("N");
            mapping = MemoryMappedFile.CreateNew(Name, checked((long)width * height * 4));
            try
            {
                view = mapping.CreateViewAccessor();
                Image = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, Pointer);
            }
            catch
            {
                if (view != null)
                    view.Dispose();
                mapping.Dispose();
                throw;
            }
        }
        public void Dispose()
        {
            Image.Dispose();
            view.Dispose();
            mapping.Dispose();
        }
    }
    internal static class RenderPool
    {
        sealed class Job
        {
            internal Control Owner;
            internal PdfSource Source;
            internal string Password;
            internal int Page, Width, Height, Rotation;
            internal Rectangle Region;
            internal volatile bool Canceled;
            internal bool Prefetch;
            internal Action<RenderFrame, Exception> Complete;
            internal RenderFrame Result;
        }
        sealed class NativeRasterFailure : IOException
        {
            internal NativeRasterFailure(Exception cause) : base("PDFium raster worker stopped.", cause) { }
        }
        sealed class Worker
        {
            internal Job Active;
            internal Thread Thread;
            internal Process Process;
            internal NamedPipeServerStream Pipe;
            ProcessBudget budget;
            BinaryReader reader;
            BinaryWriter writer;
            readonly object connection = new object();
            bool systemBackend;
            void Connect(bool system)
            {
                NamedPipeServerStream pipe;
                IAsyncResult connected;
                lock (connection)
                {
                    if (Process != null && !Process.HasExited && Pipe != null && Pipe.IsConnected && systemBackend == system)
                        return;
                    Stop();
                    string name = "MiniPDF-render-" + Guid.NewGuid().ToString("N");
                    var security = new PipeSecurity();
                    using (var identity = WindowsIdentity.GetCurrent()) security.AddAccessRule(
                        new PipeAccessRule(identity.User, PipeAccessRights.FullControl, AccessControlType.Allow));
                    pipe = Pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                                                            PipeOptions.Asynchronous, 4096, 4096, security);
                    var start = new ProcessStartInfo(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MiniPDF.Core.exe"),
                        (system ? "--system-render-worker " : "--render-worker ") + name + " " + System.Diagnostics.Process.GetCurrentProcess().Id) {
                        UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = Environment.CurrentDirectory
                    };
                    systemBackend = system;
                    Process = System.Diagnostics.Process.Start(start);
                    try { budget = new ProcessBudget(Process); }
                    catch { Stop(); throw; }
                    connected = pipe.BeginWaitForConnection(null, null);
                }
                try
                {
                    if (!connected.AsyncWaitHandle.WaitOne(10000))
                        throw new IOException("PDF raster worker did not start.");
                    pipe.EndWaitForConnection(connected);
                }
                finally
                {
                    connected.AsyncWaitHandle.Close();
                }
                lock (connection)
                {
                    if (Pipe != pipe)
                        throw new IOException("PDF raster worker stopped.");
                    reader = new BinaryReader(pipe, Encoding.UTF8, true);
                    writer = new BinaryWriter(pipe, Encoding.UTF8, true);
                }
            }
            internal RenderFrame Render(Job job, bool system)
            {
                Connect(system);
                if (job.Canceled)
                    return null;
                var elapsed = Stopwatch.StartNew();
                var process = Process;
                using (var watchdog = new System.Threading.Timer(delegate(object ignored) {
                    if (job.Canceled || elapsed.ElapsedMilliseconds > (system ? 45000 : 30000))
                    {
                        lock (connection)
                        {
                            if (Process == process)
                                Stop();
                        }
                    }
                }, null, 200, 200))
                {
                    var frame = new RenderFrame(job.Region.Width, job.Region.Height);
                    bool reported = false;
                    try
                    {
                        writer.Write(job.Source.Name);
                        writer.Write(job.Source.Length);
                        writer.Write(job.Password);
                        writer.Write(job.Page);
                        writer.Write(job.Width);
                        writer.Write(job.Height);
                        writer.Write(job.Rotation);
                        writer.Write(job.Region.X);
                        writer.Write(job.Region.Y);
                        writer.Write(job.Region.Width);
                        writer.Write(job.Region.Height);
                        writer.Write(frame.Name);
                        writer.Flush();
                        string error = reader.ReadString();
                        if (error.Length != 0)
                        { reported = true; throw new IOException(error); }
                        return frame;
                    }
                    catch (Exception error)
                    {
                        Stop();
                        frame.Dispose();
                        if (!reported && !job.Canceled) throw new NativeRasterFailure(error);
                        throw;
                    }
                }
            }
            internal void Stop()
            {
                lock (connection)
                {
                    if (Pipe != null)
                    {
                        Pipe.Dispose();
                        Pipe = null;
                    }
                    reader = null;
                    writer = null;
                    if (Process != null)
                    {
                        try
                        {
                            if (!Process.HasExited)
                            {
                                Process.Kill();
                                Process.WaitForExit(1500);
                            }
                        }
                        catch (InvalidOperationException)
                        {
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                        }
                        Process.Dispose();
                        Process = null;
                    }
                    if (budget != null) { budget.Dispose(); budget = null; }
                }
            }
            internal void Loop()
            {
                while (true)
                {
                    Job job = null;
                    lock (gate)
                    {
                        while (!stopping && queue.Count == 0)
                        {
                            if (!Monitor.Wait(gate, 30000))
                                break;
                        }
                        if (stopping)
                            break;
                        if (queue.Count > 0)
                        {
                            job = queue[0];
                            queue.RemoveAt(0);
                            Active = job;
                        }
                    }
                    if (job == null)
                    {
                        Stop();
                        continue;
                    }
                    RenderFrame frame = null;
                    Exception failure = null;
                    try
                    {
                        if (!job.Canceled)
                        {
                            bool system = UsesWindows(job.Source, job.Page);
                            try { frame = Render(job, system); }
                            catch (NativeRasterFailure)
                            {
                                if (system || job.Canceled) throw;
                                frame = Render(job, true);
                                lock (gate)
                                {
                                    if (systemPages.Count >= 256) systemPages.Clear();
                                    systemPages.Add(job.Source.Name + ":" + job.Page);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        failure = e;
                    }
                    finally
                    {
                        job.Source.Dispose();
                        lock (gate)
                        {
                            Active = null;
                        }
                    }
                    if (job.Canceled || stopping || job.Owner.IsDisposed || !job.Owner.IsHandleCreated)
                    {
                        if (frame != null)
                            frame.Dispose();
                        continue;
                    }
                    lock (gate)
                    {
                        if (job.Canceled || stopping)
                        {
                            if (frame != null)
                                frame.Dispose();
                            continue;
                        }
                        job.Result = frame;
                        delivered.Add(job);
                    }
                    var completedError = failure;
                    try
                    {
                        job.Owner.BeginInvoke(new Action(delegate {
                            RenderFrame result = TakeResult(job);
                            if (job.Canceled || job.Owner.IsDisposed)
                            {
                                if (result != null)
                                    result.Dispose();
                                return;
                            }
                            job.Complete(result, completedError);
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                        var result = TakeResult(job);
                        if (result != null)
                            result.Dispose();
                    }
                }
                Stop();
            }
        }
        static readonly HashSet<Job> delivered = new HashSet<Job>();
        static RenderFrame TakeResult(Job job)
        {
            lock (gate)
            {
                delivered.Remove(job);
                var result = job.Result;
                job.Result = null;
                return result;
            }
        }
        static void ClearDeliveries(Control owner)
        {
            var remove = new List<Job>();
            foreach (var job in delivered)
                if (owner == null || job.Owner == owner)
                {
                    job.Canceled = true;
                    if (job.Result != null)
                    {
                        job.Result.Dispose();
                        job.Result = null;
                    }
                    remove.Add(job);
                }
            foreach (var job in remove)
                delivered.Remove(job);
        }
        static readonly HashSet<string> systemPages = new HashSet<string>();
        internal static bool HasCompatibilitySource(PdfSource source)
        {
            if(source==null) return false;
            lock(gate)
            {
                string prefix=source.Name+":";
                foreach(string key in systemPages) if(key.StartsWith(prefix,StringComparison.Ordinal)) return true;
                return false;
            }
        }
        internal static bool UsesWindows(PdfSource source, int page)
        {
            lock (gate) return systemPages.Contains(source.Name + ":" + page);
        }
        static readonly object gate = new object();
        static readonly List<Job> queue = new List<Job>();
        static readonly List<Worker> workers = new List<Worker>();
        static volatile bool stopping;
        internal static int WorkerLimit
        {
            get {
                return Math.Min(2, Math.Max(1, Environment.ProcessorCount - 1));
            }
        }
        internal static int ProcessCount
        {
            get {
                lock (gate)
                {
                    int n = 0;
                    foreach (var w in workers)
                        if (w.Process != null)
                            n++;
                    return n;
                }
            }
        }
        internal static bool Request(Control owner, PdfSource source, string password, int page, int width, int height,
                                     int rotation, Rectangle region, bool prefetch,
                                     Action<RenderFrame, Exception> complete)
        {
            lock (gate)
            {
                if (stopping || queue.Count >= 64)
                    return false;
                var job = new Job { Owner = owner,       Source = source.Retain(), Password = password, Page = page,
                                    Width = width,       Height = height,          Rotation = rotation, Region = region,
                                    Prefetch = prefetch, Complete = complete };
                int position = queue.Count;
                if (!prefetch)
                {
                    position = queue.FindIndex(j => j.Prefetch);
                    if (position < 0)
                        position = queue.Count;
                }
                queue.Insert(position, job);
                int desired =
                    Math.Min(WorkerLimit, Math.Max(1, queue.Count + workers.FindAll(w => w.Active != null).Count));
                while (workers.Count < desired)
                {
                    var worker = new Worker();
                    worker.Thread = new Thread(worker.Loop) { IsBackground = true, Name = "MiniPDF raster IPC" };
                    workers.Add(worker);
                    worker.Thread.Start();
                }
                Monitor.PulseAll(gate);
                return true;
            }
        }
        internal static void Cancel(Control owner, bool running)
        {
            lock (gate)
            {
                for (int i = queue.Count - 1; i >= 0; i--)
                    if (queue[i].Owner == owner)
                    {
                        queue[i].Source.Dispose();
                        queue.RemoveAt(i);
                    }
                ClearDeliveries(owner);
                if (running)
                    foreach (var w in workers)
                        if (w.Active != null && w.Active.Owner == owner)
                            w.Active.Canceled = true;
            }
        }
        internal static void Shutdown()
        {
            Worker[] copy;
            lock (gate)
            {
                stopping = true;
                ClearDeliveries(null);
                foreach (var job in queue)
                    job.Source.Dispose();
                queue.Clear();
                copy = workers.ToArray();
                foreach (var w in copy)
                    if (w.Active != null)
                        w.Active.Canceled = true;
                Monitor.PulseAll(gate);
            }
            foreach (var w in copy)
                w.Stop();
            foreach (var w in copy)
                w.Thread.Join(2000);
            lock (gate)
            {
                workers.Clear();
                systemPages.Clear();
                stopping = false;
            }
        }
    }
    internal static class RasterWorker
    {
        internal static int Run(string name, int parent, bool systemBackend)
        {
            Process supervisor;
            try
            {
                supervisor = Process.GetProcessById(parent);
            }
            catch (ArgumentException)
            {
                return 1;
            }
            new Thread(delegate() { using (supervisor) { supervisor.WaitForExit(); Environment.Exit(0); } }) {
                IsBackground = true, Name = "MiniPDF parent lifetime"
            }
                .Start();
            using (var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut))
            {
                try
                {
                    pipe.Connect(10000);
                    using (var reader = new BinaryReader(pipe, Encoding.UTF8, true)) using (
                        var writer = new BinaryWriter(pipe, Encoding.UTF8, true))
                    {
                        PdfDocument document = null;
                        WindowsRaster systemDocument = null;
                        string sourceName = null;
                        try
                        {
                            while (pipe.IsConnected)
                            {
                                string source = reader.ReadString();
                                long length = reader.ReadInt64();
                                string password = reader.ReadString();
                                int index = reader.ReadInt32(), width = reader.ReadInt32(), height = reader.ReadInt32(),
                                    rotation = reader.ReadInt32();
                                var region = new Rectangle(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                                                           reader.ReadInt32());
                                string output = reader.ReadString();
                                string error = "";
                                try
                                {
                                    if (width < 1 || height < 1 || region.X < 0 || region.Y < 0 || region.Width < 1 ||
                                        region.Height < 1 || region.Width > 14000 || region.Height > 14000 ||
                                        (long)region.Width * region.Height > 24000000 ||
                                        (long)region.X + region.Width > width ||
                                        (long)region.Y + region.Height > height)
                                        throw new IOException("Invalid raster bounds.");
                                    if (source != sourceName)
                                    {
                                        if (document != null)
                                        {
                                            document.Dispose();
                                            document = null;
                                        }
                                        if (systemDocument != null) { systemDocument.Dispose(); systemDocument = null; }
                                        if (systemBackend)
                                        {
                                            using (var shared = PdfSource.OpenShared(source, length))
                                                systemDocument = WindowsRaster.Open(shared, password);
                                        }
                                        else document = PdfDocument.FromShared(PdfSource.OpenShared(source, length), password);
                                        sourceName = source;
                                    }
                                    if (index < 0 || index >= (systemBackend ? systemDocument.Count : document.Count))
                                        throw new IOException("Invalid raster page.");
                                    if (!systemBackend) document.OpenPage(index);
                                    using (var map = MemoryMappedFile.OpenExisting(
                                               output,
                                               MemoryMappedFileRights.ReadWrite)) using (var view =
                                                                                             map.CreateViewAccessor(
                                                                                                 0, (long)region.Width *
                                                                                                        region.Height *
                                                                                                        4))
                                    {
                                        IntPtr pixels = view.SafeMemoryMappedViewHandle.DangerousGetHandle();
                                        if (systemBackend) systemDocument.RenderInto(index, width, height, rotation, region, pixels);
                                        else document.RenderInto(index, width, height, rotation, region, pixels, region.Width * 4);
                                    }
                                }
                                catch (Exception e)
                                {
                                    error = e.Message;
                                }
                                writer.Write(error);
                                writer.Flush();
                            }
                        }
                        finally
                        {
                            if (document != null)
                                document.Dispose();
                            if (systemDocument != null) systemDocument.Dispose();
                        }
                    }
                    return 0;
                }
                catch (IOException)
                {
                    return 0;
                }
                catch (TimeoutException)
                {
                    return 1;
                }
            }
        }
    }
}
