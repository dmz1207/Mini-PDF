using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace MiniPdf
{
    // Immutable, pagefile-backed input shared by readers without pinning a large GC array.
    internal sealed class PdfSource : IDisposable
    {
        internal readonly string Name;
        internal readonly long Length;
        readonly MemoryMappedFile mapping;
        readonly MemoryMappedViewAccessor view;
        int references = 1;
        internal IntPtr Pointer
        {
            get {
                return view.SafeMemoryMappedViewHandle.DangerousGetHandle();
            }
        }
        PdfSource(string name, long length, MemoryMappedFile file)
        {
            Name = name;
            Length = length;
            mapping = file;
            try
            {
                view = file.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
        internal static PdfSource OpenShared(string name, long length)
        {
            if (length <= 0)
                throw new IOException("Invalid PDF input length.");
            return new PdfSource(name, length, MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read));
        }
        internal static PdfSource FromFile(string path)
        {
            using (var input =
                       new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                                      65536, FileOptions.SequentialScan)) return Copy(input, input.Length);
        }
        internal static PdfSource FromBytes(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            using (var input = new MemoryStream(data, false)) return Copy(input, data.LongLength);
        }
        static PdfSource Copy(Stream input, long length)
        {
            if (length <= 0)
                throw new IOException(UiText.Get("文件为空。"));
            string name = "Local\\MiniPDF-input-" + Guid.NewGuid().ToString("N");
            var map = MemoryMappedFile.CreateNew(name, length, MemoryMappedFileAccess.ReadWrite);
            try
            {
                using (var output = map.CreateViewStream(0, length, MemoryMappedFileAccess.Write))
                {
                    input.CopyTo(output, 65536);
                    if (output.Position != length)
                        throw new IOException("PDF input changed while reading.");
                }
                return new PdfSource(name, length, map);
            }
            catch
            {
                map.Dispose();
                throw;
            }
        }
        internal PdfSource Retain()
        {
            int count;
            do
            {
                count = Volatile.Read(ref references);
                if (count == 0)
                    throw new ObjectDisposedException("PdfSource");
            } while (Interlocked.CompareExchange(ref references, count + 1, count) != count);
            return this;
        }
        internal byte[] ReadAll()
        {
            if (Length > int.MaxValue)
                throw new IOException("PDF input is too large to serialize.");
            byte[] bytes = new byte[(int)Length];
            view.ReadArray(0, bytes, 0, bytes.Length);
            return bytes;
        }
        public void Dispose()
        {
            if (Interlocked.Decrement(ref references) == 0)
            {
                view.Dispose();
                mapping.Dispose();
            }
        }
    }
}
