using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace MiniPdf.Boot {
    // Windows 10/11 include Cabinet's LZMS implementation; no extra library is shipped.
    internal static class PayloadCompression {
        const int Limit = 128 * 1024 * 1024;
        const uint Lzms = 5;
        [DllImport("cabinet.dll", SetLastError=true)]
        static extern bool CreateCompressor(uint algorithm, IntPtr allocation, out IntPtr handle);
        [DllImport("cabinet.dll", SetLastError=true)]
        static extern bool Compress(IntPtr handle, byte[] input, UIntPtr inputSize, byte[] output, UIntPtr outputSize, out UIntPtr needed);
        [DllImport("cabinet.dll", SetLastError=true)]
        static extern bool SetCompressorInformation(IntPtr handle, int informationClass, ref uint value, UIntPtr size);
        [DllImport("cabinet.dll", SetLastError=true)]
        static extern bool CloseCompressor(IntPtr handle);
        [DllImport("cabinet.dll", SetLastError=true)]
        static extern bool CreateDecompressor(uint algorithm, IntPtr allocation, out IntPtr handle);
        [DllImport("cabinet.dll", SetLastError=true)]
        static extern bool Decompress(IntPtr handle, byte[] input, UIntPtr inputSize, byte[] output, UIntPtr outputSize, out UIntPtr needed);
        [DllImport("cabinet.dll", SetLastError=true)]
        static extern bool CloseDecompressor(IntPtr handle);

        internal static byte[] Transform(byte[] input, bool encode) {
            if (input == null || input.Length == 0 || input.Length > Limit)
                throw new IOException("程序资源大小无效。");
            IntPtr handle;
            if (!(encode ? CreateCompressor(Lzms, IntPtr.Zero, out handle) : CreateDecompressor(Lzms, IntPtr.Zero, out handle)))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try {
                if (encode) {
                    uint blockSize = 8 * 1024 * 1024;
                    if (!SetCompressorInformation(handle, 1, ref blockSize, new UIntPtr(4)))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                UIntPtr needed;
                UIntPtr inputSize = new UIntPtr((uint)input.Length);
                bool sized = encode ? Compress(handle, input, inputSize, null, UIntPtr.Zero, out needed)
                                    : Decompress(handle, input, inputSize, null, UIntPtr.Zero, out needed);
                int error = Marshal.GetLastWin32Error();
                if (!sized && error != 122) throw new Win32Exception(error, "程序资源解包失败。");
                ulong size = needed.ToUInt64();
                if (size == 0 || size > Limit) throw new IOException("程序资源大小无效。");
                byte[] output = new byte[(int)size];
                bool done = encode ? Compress(handle, input, inputSize, output, needed, out needed)
                                   : Decompress(handle, input, inputSize, output, needed, out needed);
                if (!done) throw new Win32Exception(Marshal.GetLastWin32Error(), "程序资源解包失败。");
                if (needed.ToUInt64() != (ulong)output.Length) Array.Resize(ref output, checked((int)needed.ToUInt64()));
                return output;
            } finally {
                if (encode) CloseCompressor(handle); else CloseDecompressor(handle);
            }
        }
    }
}
