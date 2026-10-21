using System;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using MiniPdf;
class Bench
{
    static string root;
    static string Hash(Bitmap b)
    {
        var bits =
            b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] data = new byte[b.Width * b.Height * 4];
            for (int y = 0; y < b.Height; y++)
                Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), data, y * b.Width * 4, b.Width * 4);
            using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(data)).Replace("-", "");
        }
        finally
        {
            b.UnlockBits(bits);
        }
    }
    static void Measure(string name, Action a, int n)
    {
        a();
        var ms = new List<double>();
        long before = GC.GetTotalMemory(true);
        for (int i = 0; i < n; i++)
        {
            var sw = Stopwatch.StartNew();
            a();
            sw.Stop();
            ms.Add(sw.Elapsed.TotalMilliseconds);
        }
        ms.Sort();
        Console.WriteLine(
            name + "_median_ms=" + ms[n / 2].ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        Console.WriteLine(name + "_managed_bytes=" + (GC.GetTotalMemory(false) - before));
    }
    static void UiBenchmark()
    {
        using (var form = new MainWindow(null))
        {
            form.LoadForTest(PdfDocument.Open(Path.Combine(root, "bench.pdf"), ""));
            form.Show();
            var view = form.CanvasForTest;
            view.Continuous = false;
            form.ApplyZoom("75%");
            var pending =
                view.GetType().GetProperty("PendingRendersForTest", System.Reflection.BindingFlags.Instance |
                                                                        System.Reflection.BindingFlags.NonPublic);
            Action wait = delegate
            {
                var clock = Stopwatch.StartNew();
                do
                {
                    form.Refresh();
                    Application.DoEvents();
                    if (clock.ElapsedMilliseconds >= 400 && (pending == null || (int)pending.GetValue(view, null) == 0))
                        break;
                    System.Threading.Thread.Sleep(2);
                } while (clock.ElapsedMilliseconds < 15000);
            };
            wait();
            Measure("cached_relayout", delegate {
                view.Rebuild(false);
                form.Refresh();
                Application.DoEvents();
                if (pending != null)
                {
                    var clock = Stopwatch.StartNew();
                    while ((int)pending.GetValue(view, null) > 0 && clock.ElapsedMilliseconds < 15000)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(2);
                    }
                }
            }, 7);
            form.Close();
            Application.DoEvents();
        }
    }
    [STAThread]
    static int Main(string[] args)
    {
        root = args[0];
        N.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        N.SetDefaultDllDirectories(0xa00);
        N.LoadLibraryEx(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdfium.dll"), IntPtr.Zero, 0x900);
        N.FPDF_InitLibrary();
        try
        {
            using (var doc = PdfDocument.Open(Path.Combine(root, "bench.pdf"), ""))
            {
                Measure("render_794x1123", delegate { using (var b = doc.Render(0, 794, 1123)) {} }, 7);
                Measure("render_2048x2896", delegate { using (var b = doc.Render(0, 2048, 2896)) {} }, 7);
                Measure("render_tile",
                        delegate { using (
                            var b = doc.RenderRegion(0, 6000, 8485, 0, new Rectangle(768, 1536, 768, 768))) {} },
                        7);
                Measure("serialize", delegate { doc.Serialize(false); }, 5);
                var pages = new List<MergePage>();
                for (int i = 0; i < doc.Count; i++)
                    pages.Add(new MergePage { Source = doc, Index = i, Name = "bench.pdf" });
                Measure("merge_12_pages",
                        delegate { using (var merged = PdfDocument.MergePages(pages)) merged.Serialize(false); }, 7);
                using (var merged = PdfDocument.MergePages(pages))
                    Console.WriteLine("merged_size_bytes=" + merged.Serialize(false).Length);
                for (int rot = 0; rot < 4; rot++)
                {
                    using (var b = doc.Render(0, 794, 1123, rot)) Console.WriteLine("pixels_" + rot + "=" + Hash(b));
                    using (var b = doc.RenderRegion(0, 2048, 2896, rot, new Rectangle(277, 721, 768, 768)))
                        Console.WriteLine("tile_pixels_" + rot + "=" + Hash(b));
                }
            }
            Measure("open_large",
                    delegate { using (var doc = PdfDocument.Open(Path.Combine(root, "bench-large.pdf"), "")) {} }, 7);
            using (var doc = PdfDocument.Open(Path.Combine(root, "bench-large.pdf"), ""))
            {
                Measure("metadata_1000x10", delegate {
                    for (int j = 0; j < 10; j++)
                        for (int i = 0; i < doc.Count; i++)
                            doc.PageSize(i);
                }, 7);
            }
            UiBenchmark();
            return 0;
        }
        finally
        {
            var pool = typeof(PdfDocument).Assembly.GetType("MiniPdf.RenderPool");
            if (pool != null)
                pool.GetMethod("Shutdown",
                               System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, null);
            N.FPDF_DestroyLibrary();
        }
    }
}
