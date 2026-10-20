using System;
using System.IO;
using System.Windows.Forms;

namespace MiniPdf {
    internal static class Program {
        [STAThread] static int Main(string[] args) {
            bool initialized=false;
            try {
                N.SetDefaultDllDirectories(0x00000a00);
                string library=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pdfium.dll");
                if(N.LoadLibraryEx(library,IntPtr.Zero,0x00000900)==IntPtr.Zero)throw new InvalidOperationException("无法载入 pdfium.dll。请保持它与程序在同一文件夹，并使用 64 位 Windows 10 或 11。");
                N.FPDF_InitLibrary();initialized=true;N.SetProcessDPIAware();
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                using(var main=new MainWindow(args.Length>0?args[0]:null))Application.Run(main);
                return 0;
            } catch(Exception e) {
                MessageBox.Show(e.Message,"Mini PDF",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
            } finally {if(initialized)N.FPDF_DestroyLibrary();}
        }
    }
}
