using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace MiniPdf
{
    internal static partial class SelfTest
    {
        static void PumpUntil(Func<bool> done, int timeout = 20000)
        {
            var watch = Stopwatch.StartNew();
            while (!done())
            {
                Application.DoEvents();
                if (watch.ElapsedMilliseconds > timeout)
                    throw new Exception("Raster task timed out.");
                Thread.Sleep(2);
            }
            Application.DoEvents();
        }
        static RenderFrame WorkerFrame(Control owner, PdfDocument document, int width, int height, int rotation,
                                       Rectangle region)
        {
            RenderFrame result = null;
            Exception failure = null;
            bool done = false;
            bool queued = RenderPool.Request(owner, document.GetRenderSource(), document.Password, 0, width, height,
                                             rotation, region, false, delegate(RenderFrame frame, Exception e) {
                                                 result = frame;
                                                 failure = e;
                                                 done = true;
                                             });
            if (!queued)
                throw new Exception("Raster queue rejected a test task.");
            PumpUntil(() => done);
            if (failure != null)
                throw failure;
            return result;
        }
        static void Performance(string directory)
        {
            string sample = Path.Combine(directory, "baseline.pdf");
            using (var doc = PdfDocument.Open(sample, "")) using (var control = new Form())
            {
                IntPtr handle = control.Handle;
                var input = doc.GetRenderSource();
                using (var lease = input.Retain()) using (var shared = PdfSource.OpenShared(input.Name, input.Length))
                    Assert(input.ReadAll().SequenceEqual(shared.ReadAll()),
                           "shared PDF input preserves exact bytes across views");
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    var region = new Rectangle(80, 120, 400, 240);
                    using (var expected = doc.RenderRegion(0, 1200, 1600, rotation, region)) using (
                        var actual = WorkerFrame(control, doc, 1200, 1600, rotation, region))
                        Assert(SamePixels(expected, actual.Image),
                               "background raster exactly matches synchronous tile at rotation " + rotation);
                }
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    using (var expected = doc.Render(0, 794, 1123, rotation)) using (var actual =
                                                                                         new RenderFrame(794, 1123))
                    {
                        bool done = doc.RenderInto(0, 794, 1123, rotation, new Rectangle(0, 0, 794, 1123),
                                                   actual.Pointer, 794 * 4, 3, 5000);
                        Assert(done && SamePixels(expected, actual.Image),
                               "progressive first-page render matches full-quality output at rotation " + rotation);
                    }
                }
                int completed = 0, heartbeats = 0;
                Exception failure = null;
                var times = new List<double>();
                var watch = Stopwatch.StartNew();
                using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
                {
                    timer.Tick += delegate
                    {
                        heartbeats++;
                    };
                    timer.Start();
                    for (int i = 0; i < 8; i++)
                        Assert(RenderPool.Request(control, doc.GetRenderSource(), doc.Password, 0, 2000, 2800, i % 4,
                                                  new Rectangle(0, 0, 2000, 2800), false,
                                                  delegate(RenderFrame frame, Exception error) {
                                                      if (error != null)
                                                          failure = error;
                                                      if (frame != null)
                                                          frame.Dispose();
                                                      times.Add(watch.Elapsed.TotalMilliseconds);
                                                      completed++;
                                                  }),
                               "parallel render request " + i + " is accepted");
                    PumpUntil(() => completed == 8);
                    timer.Stop();
                }
                Assert(failure == null && heartbeats > 0,
                       "UI message loop remains responsive during queued native rendering");
                Assert(RenderPool.ProcessCount >= 1 && RenderPool.ProcessCount <= RenderPool.WorkerLimit,
                       "raster process count respects the CPU-aware limit");
                log.AppendLine("PARALLEL: 8 pages in " + watch.ElapsedMilliseconds + " ms, " + heartbeats +
                               " UI heartbeats, " + RenderPool.ProcessCount + " workers");
                var original = doc.GetRenderSource();
                var lease2 = original.Retain();
                doc.AddText("SNAPSHOT_CHANGE", 60, 600, 18, "自动", Color.Red);
                var changed = doc.GetRenderSource();
                Assert(original.Name != changed.Name, "editing produces a new immutable rendering snapshot");
                using (var expected = doc.Render(0, 794, 1123)) using (
                    var actual = WorkerFrame(control, doc, 794, 1123, 0, new Rectangle(0, 0, 794, 1123)))
                    Assert(SamePixels(expected, actual.Image),
                           "edited render source matches current document without stale pixels");
                doc.Undo();
                using (var expected = doc.Render(0, 794, 1123)) using (
                    var actual = WorkerFrame(control, doc, 794, 1123, 0, new Rectangle(0, 0, 794, 1123)))
                    Assert(SamePixels(expected, actual.Image),
                           "undo invalidates rendered snapshots and preserves exact output");
                lease2.Dispose();
                bool badDone = false;
                Exception badError = null;
                RenderPool.Request(control, doc.GetRenderSource(), doc.Password, 99999, 794, 1123, 0,
                                   new Rectangle(0, 0, 794, 1123), false, delegate(RenderFrame frame, Exception error) {
                                       if (frame != null)
                                           frame.Dispose();
                                       badError = error;
                                       badDone = true;
                                   });
                PumpUntil(() => badDone);
                Assert(badError != null, "worker rejects invalid page requests without hanging the UI");
                using (var frame = WorkerFrame(control, doc, 794, 1123, 0, new Rectangle(0, 0, 794, 1123)))
                    Assert(frame.Image.Width == 794, "a subsequent request recovers after a worker-side error");
                var workers = (System.Collections.IEnumerable) typeof(RenderPool)
                                  .GetField("workers", System.Reflection.BindingFlags.Static |
                                                           System.Reflection.BindingFlags.NonPublic)
                                  .GetValue(null);
                foreach (var worker in workers)
                {
                    var process = (Process)worker.GetType()
                                      .GetField("Process", System.Reflection.BindingFlags.Instance |
                                                               System.Reflection.BindingFlags.NonPublic)
                                      .GetValue(worker);
                    if (process != null && !process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                        break;
                    }
                }
                using (var frame = WorkerFrame(control, doc, 794, 1123, 0, new Rectangle(0, 0, 794, 1123)))
                    Assert(frame.Image.Height == 1123, "a terminated owned raster process restarts on demand");
            }
            using (var control = new Form())
            {
                IntPtr handle = control.Handle;
                var doc = PdfDocument.Open(sample, "");
                bool done = false;
                Exception error = null;
                RenderPool.Request(control, doc.GetRenderSource(), doc.Password, 0, 794, 1123, 0,
                                   new Rectangle(0, 0, 794, 1123), false,
                                   delegate(RenderFrame frame, Exception failure) {
                                       error = failure;
                                       if (frame != null)
                                           frame.Dispose();
                                       done = true;
                                   });
                doc.Dispose();
                PumpUntil(() => done);
                Assert(error == null, "queued rendering keeps input alive after the document closes");
            }
            using (var form = new MainWindow(null))
            {
                form.LoadForTest(PdfDocument.Open(sample, ""));
                form.Show();
                form.ApplyZoom("100%");
                AwaitRaster(form);
                var canvas = form.CanvasForTest;
                int requests = canvas.RenderRequestsForTest;
                long bytes = canvas.CacheBytesForTest;
                canvas.Rebuild(false);
                AwaitRaster(form);
                Assert(canvas.CachedFramesForTest > 0 && bytes == canvas.CacheBytesForTest &&
                           requests == canvas.RenderRequestsForTest,
                       "unchanged layout reuses exact cached frames without native rerendering");
                form.ApplyZoom("125%");
                AwaitRaster(form);
                form.ApplyZoom("100%");
                AwaitRaster(form);
                Assert(canvas.CacheBytesForTest <= 64L * 1024 * 1024,
                       "returning to an earlier zoom reuses bounded dimension-keyed cache");
                form.ApplyZoom("6400%");
                AwaitRaster(form);
                Assert(canvas.CacheBytesForTest <= 64L * 1024 * 1024 && canvas.CachedFramesForTest > 0,
                       "large zoom renders visible tiles inside the memory budget");
                form.Close();
                Application.DoEvents();
            }
            RenderPool.Shutdown();
            Assert(RenderPool.ProcessCount == 0,
                   "shutdown stops all owned raster processes and releases engine handles");
        }
        internal static int RunPerformance(string directory)
        {
            logPath = Path.Combine(directory, "performance-test.txt");
            try
            {
                Performance(directory);
                log.AppendLine("TOTAL: " + assertions + " performance assertions, 0 failures");
                File.WriteAllText(logPath, log.ToString());
                return 0;
            }
            catch (Exception e)
            {
                log.AppendLine(e.ToString());
                File.WriteAllText(logPath, log.ToString());
                return 1;
            }
        }
    }
}
