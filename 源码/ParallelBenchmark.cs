using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using MiniPdf;
class ParallelBench
{
    static void Pump(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            Application.DoEvents();
            if (watch.ElapsedMilliseconds > 30000)
                throw new Exception("Benchmark timeout.");
            Thread.Sleep(1);
        }
    }
    static double Run(Control control, PdfDocument document, bool parallel, int count)
    {
        var watch = Stopwatch.StartNew();
        if (!parallel)
        {
            for (int i = 0; i < count; i++)
                using (var image = document.Render(0, 2048, 2896)) {}
        }
        else
        {
            int completed = 0;
            Exception error = null;
            for (int i = 0; i < count; i++)
            {
                if (!RenderPool.Request(control, document.GetRenderSource(), document.Password, 0, 2048, 2896, 0,
                                        new Rectangle(0, 0, 2048, 2896), false,
                                        delegate(RenderFrame frame, Exception failure) {
                                            if (frame != null)
                                                frame.Dispose();
                                            if (failure != null)
                                                error = failure;
                                            completed++;
                                        }))
                    throw new Exception("Queue rejected request.");
            }
            Pump(() => completed == count);
            if (error != null)
                throw error;
        }
        return watch.Elapsed.TotalMilliseconds;
    }
    [STAThread]
    static int Main(string[] args)
    {
        N.SetDefaultDllDirectories(0xa00);
        N.LoadLibraryEx(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdfium.dll"), IntPtr.Zero, 0x900);
        N.FPDF_InitLibrary();
        try
        {
            using (var control = new Form()) using (var document =
                                                        PdfDocument.Open(Path.Combine(args[0], "bench.pdf"), ""))
            {
                IntPtr window = control.Handle;
                Run(control, document, false, 2);
                Run(control, document, true, 2);
                var serial = new List<double>();
                var parallel = new List<double>();
                for (int i = 0; i < 3; i++)
                {
                    serial.Add(Run(control, document, false, 8));
                    parallel.Add(Run(control, document, true, 8));
                }
                serial.Sort();
                parallel.Sort();
                Console.WriteLine("serial_8_pages_median_ms=" +
                                  serial[1].ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                Console.WriteLine("parallel_8_pages_median_ms=" +
                                  parallel[1].ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                Console.WriteLine("workers=" + RenderPool.ProcessCount);
                Console.WriteLine("logical_cpus=" + Environment.ProcessorCount);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
        finally
        {
            RenderPool.Shutdown();
            N.FPDF_DestroyLibrary();
        }
        return 0;
    }
}
