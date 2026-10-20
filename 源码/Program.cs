using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MiniPdf {
    internal static class Program {
        [STAThread] static int Main(string[] args) {
            bool initialized=false;SingleInstance instance=null;
            try {
                bool separate=false;var files=new List<string>();foreach(string arg in args){if(arg=="--new-window")separate=true;else files.Add(Path.GetFullPath(arg));}
                if(!separate){instance=new SingleInstance();if(instance.Forward(files.ToArray()))return 0;}
                N.SetDefaultDllDirectories(0x00000a00);
                string library=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pdfium.dll");
                if(N.LoadLibraryEx(library,IntPtr.Zero,0x00000900)==IntPtr.Zero)throw new InvalidOperationException(UiText.Get("无法载入 pdfium.dll。请保持它与程序在同一文件夹，并使用 64 位 Windows 10 或 11。"));
                N.FPDF_InitLibrary();initialized=true;N.SetProcessDPIAware();
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                using(var main=new MainWindow(null)){main.Shown+=delegate{main.OpenDocuments(files.ToArray());};if(instance!=null){IntPtr handle=main.Handle;instance.Listen(delegate(string[] paths){try{if(!main.IsDisposed)main.BeginInvoke(new Action(delegate{main.ReceiveOpenRequest(paths);}));}catch(InvalidOperationException){}});}Application.Run(main);}
                return 0;
            } catch(Exception e) {
                MessageBox.Show(e.Message,"Mini PDF",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
            } finally {if(instance!=null)instance.Dispose();if(initialized)N.FPDF_DestroyLibrary();}
        }
    }
}
