using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace MiniPdf {
    internal static class WindowsCertificates {
        [StructLayout(LayoutKind.Sequential)]struct Usage {internal uint Count;internal IntPtr Identifiers;}
        [StructLayout(LayoutKind.Sequential)]struct Match {internal uint Type;internal Usage Usage;}
        [StructLayout(LayoutKind.Sequential)]struct Parameters {internal uint Size;internal Match Usage,Policy;internal uint Timeout;internal int CheckFreshness;internal uint Freshness;internal IntPtr Resync,Strong;internal uint StrongFlags;}
        [DllImport("crypt32.dll")]static extern bool CertGetCertificateChain(IntPtr engine,IntPtr cert,IntPtr time,IntPtr store,ref Parameters parameters,uint flags,IntPtr reserved,out IntPtr chain);
        [DllImport("crypt32.dll")]static extern void CertFreeCertificateChain(IntPtr chain);
        internal static bool TrustedOffline(X509Certificate2 cert){var p=new Parameters{Size=(uint)Marshal.SizeOf(typeof(Parameters))};IntPtr native;
            // Cache-only URL retrieval and disabled automatic root updates prevent downloads.
            if(!CertGetCertificateChain(IntPtr.Zero,cert.Handle,IntPtr.Zero,IntPtr.Zero,ref p,0x80000104,IntPtr.Zero,out native))return false;
            try{using(var chain=new X509Chain(native)){foreach(var status in chain.ChainStatus)if(status.Status!=X509ChainStatusFlags.NoError)return false;return chain.ChainElements.Count>0;}}finally{CertFreeCertificateChain(native);}}
    }
}
