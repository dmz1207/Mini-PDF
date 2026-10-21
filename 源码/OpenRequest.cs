using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace MiniPdf
{
    internal static class OpenRequest
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetNamedPipeServerProcessId(IntPtr pipe, out uint processId);
        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(uint processId);
        internal static string Name(string directory)
        {
            using (var identity = WindowsIdentity.GetCurrent()) using (var sha = SHA256.Create())
            {
                byte[] key = sha.ComputeHash(Encoding.UTF8.GetBytes(
                    Path.GetFullPath(directory).TrimEnd('\\').ToUpperInvariant() + "|" + identity.User.Value));
                return "MiniPDF-" + BitConverter.ToString(key, 0, 16).Replace("-", "");
            }
        }
        internal static void Send(string name, string[] paths, int timeout)
        {
            if (paths.Length > 1024)
                throw new IOException("At most 1024 files can be opened at once.");
            using (var pipe = new NamedPipeClientStream(".", name, PipeDirection.Out))
            {
                pipe.Connect(timeout);
                uint process;
                if (GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out process))
                    AllowSetForegroundWindow(process);
                using (var packet = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(packet, Encoding.UTF8, true))
                    {
                        writer.Write(paths.Length);
                        foreach (string path in paths)
                        {
                            if (path.Length > 32767)
                                throw new IOException("File path is too long.");
                            writer.Write(path);
                        }
                    }
                    if (packet.Length > 1024 * 1024)
                        throw new IOException("Too many file paths.");
                    using (var writer = new BinaryWriter(pipe, Encoding.UTF8))
                    {
                        writer.Write((int)packet.Length);
                        writer.Write(packet.ToArray());
                        writer.Flush();
                    }
                }
            }
        }
        internal static bool TryForward(string directory, string[] paths)
        {
            string name = Name(directory);
            try
            {
                using (var mutex = Mutex.OpenExisting("Local\\" + name))
                {
                    bool acquired;
                    try
                    {
                        acquired = mutex.WaitOne(0);
                    }
                    catch (AbandonedMutexException)
                    {
                        acquired = true;
                    }
                    if (acquired)
                    {
                        mutex.ReleaseMutex();
                        return false;
                    }
                }
                Send(name, paths, 1800);
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
