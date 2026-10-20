using System;
using System.IO;
using System.Security.Cryptography;

namespace MiniPdf.Boot {
    internal static class PackProgram {
        static int Main(string[] args) {
            try {
                if (args.Length != 2) throw new ArgumentException("需要输入资源和输出路径。");
                byte[] raw = File.ReadAllBytes(args[0]);
                byte[] packed = PayloadCompression.Transform(raw, true);
                byte[] roundTrip = PayloadCompression.Transform(packed, false);
                using (var sha = SHA256.Create()) {
                    if (Convert.ToBase64String(sha.ComputeHash(raw)) != Convert.ToBase64String(sha.ComputeHash(roundTrip)))
                        throw new IOException("压缩资源校验失败。");
                }
                File.WriteAllBytes(args[1], packed);
                Console.WriteLine("LZMS: {0:N0} -> {1:N0} bytes; round-trip verified", raw.Length, packed.Length);
                return 0;
            } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
    }
}
