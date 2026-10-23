using System;
using System.IO;
using System.Windows.Forms;

namespace MiniPdf.Tests {
    internal static class TestProgram {
        [STAThread] static int Main(string[] args) {
            bool initialized = false;
            string directory = args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "work", "tests");
            Directory.CreateDirectory(directory);
            try {
                N.SetDefaultDllDirectories(0x00000a00);
                string library = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdfium.dll");
                if (N.LoadLibraryEx(library, IntPtr.Zero, 0x00000900) == IntPtr.Zero)
                    throw new IOException("测试运行目录缺少 pdfium.dll。");
                N.FPDF_InitLibrary(); initialized = true;
                N.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if(args.Length>1&&args[1]=="--office-forms")return SelfTest.RunOfficeForms(directory);
                if(args.Length>1&&args[1]=="--office-forms-core")return SelfTest.RunOfficeForms(directory,false);
                if(args.Length>1&&args[1]=="--text-edit-core")return SelfTest.RunTextEditing(directory,false);
                if(args.Length>1&&args[1]=="--text-edit-only")return SelfTest.RunTextEditing(directory);
                if(args.Length>1&&args[1]=="--selection-only")return SelfTest.RunSelection(directory);
                if(args.Length>1&&args[1]=="--performance-only")return SelfTest.RunPerformance(directory);
                if(args.Length>1&&args[1]=="--merge-only")return SelfTest.RunMergeDetails(directory);
                if(args.Length>1&&args[1]=="--signature-fixes")return SelfTest.RunSignatureFixes(directory);
                if(args.Length>1&&args[1]=="--features-only")return SelfTest.RunFeatures(directory);
                return args.Length>1&&args[1]=="--header-only"?SelfTest.RunHeader(directory):SelfTest.Run(directory);
            } catch (Exception e) {
                File.WriteAllText(Path.Combine(directory, "fatal.txt"), e.ToString());
                return 1;
            } finally { RenderPool.Shutdown(); if (initialized) N.FPDF_DestroyLibrary(); }
        }
    }
}
