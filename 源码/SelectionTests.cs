using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace MiniPdf
{
    internal static partial class SelfTest
    {
        static Point SelectionPoint(MainWindow form, int index, int character)
        {
            var text = form.Document.SelectionPage(index);
            N.Rect box;
            Assert(N.FPDFText_GetLooseCharBox(text.Text, character, out box) != 0, "selection fixture exposes character bounds");
            var rectangle = form.CanvasForTest.LayoutRectangles[index];
            rectangle.Offset(form.CanvasForTest.AutoScrollPosition);
            var point = text.ToDevice((box.left + box.right) / 2, (box.bottom + box.top) / 2,
                                      rectangle.Width, rectangle.Height, form.CanvasForTest.ViewRotation);
            point.Offset(rectangle.Location);
            return point;
        }
        static int LastCharacter(PdfDocument document, int index)
        {
            var text = document.SelectionPage(index);
            int result = text.Count - 1;
            while (result > 0 && N.FPDFText_GetUnicode(text.Text, result) <= 32) result--;
            return result;
        }
        static void ImportedFontCoverage(string directory)
        {
            foreach (string name in new[] { "SimFang-variant", "XiaoBiaoSong", "issue18117", "issue20489" })
            {
                string path = Path.Combine(directory, "font-source-" + name + ".pdf");
                if (!File.Exists(path)) continue;
                using (var source = PdfDocument.Open(path, ""))
                {
                    var pages = new System.Collections.Generic.List<MergePage> {
                        new MergePage { Source=source,Index=0 },new MergePage { Source=source,Index=source.Count-1 } };
                    using (var merged = PdfDocument.MergePages(pages))
                    {
                        merged.AddText("ADDED_AFTER_MERGE", 30, 30, 12, "自动", Color.Black);
                        string output = Path.Combine(directory,"font-merged-"+name+".pdf"); merged.Save(output);
                        using (var check=PdfDocument.Open(output,"")) Assert(check.Count==2&&check.SelectedText(0,100000).Contains("ADDED_AFTER_MERGE"),
                            "public font case " + name + " merges, adds text and saves without a native trap");
                    }
                    using (var duplicate = PdfDocument.Open(path,""))
                    {
                        duplicate.DuplicatePage(); duplicate.AddText("ADDED_AFTER_DUPLICATE",30,30,12,"自动",Color.Black);
                        string output=Path.Combine(directory,"font-duplicated-"+name+".pdf"); duplicate.Save(output);
                        using (var check=PdfDocument.Open(output,"")) { check.OpenPage(1); Assert(check.Count==source.Count+1&&check.SelectedText(0,100000).Contains("ADDED_AFTER_DUPLICATE"),
                            "public font case "+name+" duplicates, adds text and saves without a native trap"); }
                    }
                    using (var inserted=PdfDocument.New())
                    {
                        inserted.Import(source,1); inserted.AddText("ADDED_AFTER_INSERT",30,30,12,"自动",Color.Black);
                        string output=Path.Combine(directory,"font-inserted-"+name+".pdf"); inserted.Save(output);
                        using(var check=PdfDocument.Open(output,"")){check.OpenPage(1);Assert(check.Count==source.Count+1&&check.SelectedText(0,100000).Contains("ADDED_AFTER_INSERT"),
                            "public font case "+name+" inserts, adds text and saves without a native trap");}
                    }
                }
            }
        }
        static void PublicWorkerCoverage(string directory)
        {
            string path = Path.Combine(directory, "public-invalid-jpx.pdf");
            if (!File.Exists(path)) return;
            using (var owner = new Form())
            using (var broken = PdfDocument.Open(path, ""))
            {
                owner.Show(); Application.DoEvents();
                Assert(!broken.CanRenderQuickly(0), "public malformed JPX stays off the UI-thread quick path");
                bool finished = false; Exception failure = null; bool recovered = false;
                RenderPool.Request(owner, broken.GetRenderSource(), broken.Password, 0, 600, 413, 0,
                    new Rectangle(0, 0, 600, 413), false, delegate(RenderFrame frame, Exception error) {
                        if (frame != null)
                        {
                            var colors = new System.Collections.Generic.HashSet<int>();
                            for (int y = 20; y < frame.Image.Height; y += 30)
                                for (int x = 20; x < frame.Image.Width; x += 30) colors.Add(frame.Image.GetPixel(x,y).ToArgb());
                            recovered = colors.Count > 50;
                            frame.Image.Save(Path.Combine(directory,"jpx-system-recovery.png"),ImageFormat.Png);
                            frame.Dispose();
                        }
                        failure = error; finished = true;
                    });
                PumpUntil(() => finished);
                Assert(failure == null && recovered && RenderPool.UsesWindows(broken.GetRenderSource(),0),
                    "Windows compatibility rendering recovers the malformed JPX image after PDFium failure");
                using(var original=WorkerFrame(owner,broken,640,440,0,new Rectangle(0,0,640,440)))
                for(int rotation=0;rotation<4;rotation++)
                {
                    int width=rotation%2==0?640:440, height=rotation%2==0?440:640;
                    using(var expected=(Bitmap)original.Image.Clone())
                    {
                        if(rotation!=0) expected.RotateFlip(rotation==1?RotateFlipType.Rotate90FlipNone:
                            rotation==2?RotateFlipType.Rotate180FlipNone:RotateFlipType.Rotate270FlipNone);
                        using(var full=WorkerFrame(owner,broken,width,height,rotation,new Rectangle(0,0,width,height)))
                            Assert(RasterDifference(expected,full.Image)<4,"Windows compatibility rendering preserves whole-page rotation "+rotation);
                        var crop=new Rectangle(35,40,120,100);
                        using(var wanted=expected.Clone(crop,PixelFormat.Format32bppArgb))
                        using(var tile=WorkerFrame(owner,broken,width,height,rotation,crop))
                            Assert(RasterDifference(wanted,tile.Image)<4,"Windows compatibility tile coordinates preserve rotation "+rotation);
                    }
                }
                using (var valid = PdfDocument.Open(Path.Combine(directory, "selection-pages.pdf"), ""))
                using (var frame = WorkerFrame(owner, valid, 400, 300, 0, new Rectangle(0, 0, 400, 300)))
                    Assert(frame.Image.Width == 400, "normal PDF renders after the malformed JPX worker failure");
                owner.Close();
            }
            using(var form=new MainWindow(null))
            {
                form.LoadForTest(PdfDocument.Open(path,"")); form.Show(); Application.DoEvents(); AwaitRaster(form);
                Assert(form.Document.CompatibilityPreview && !form.Document.CanEdit && !form.Document.CanPrint,
                    "uncertain compatibility colors are explicitly read-only and cannot enter native printing");
                using(var preview=new Bitmap(form.Width,form.Height))
                { form.DrawToBitmap(preview,new Rectangle(0,0,preview.Width,preview.Height)); preview.Save(Path.Combine(directory,"compatibility-preview.png"),ImageFormat.Png); }
                form.Close();
            }
        }
        static void SelectionCoverage(string directory)
        {
            PublicLayerCoverage(directory);
            ImportedFontCoverage(directory);
            foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
            using (var font = new Font("Segoe UI", 10 * scale))
            foreach (var size in new[] { new SizeF(400,600), new SizeF(600,400), new SizeF(100,1800) })
            {
                var row = new Rectangle(0, 30, (int)(180 * scale), (int)(128 * scale));
                Rectangle picture, number;
                MainWindow.ThumbnailLayout(row, size, font, scale, out picture, out number);
                Assert(picture.Bottom + 2 < number.Top && row.Contains(picture) && row.Contains(number),
                       "thumbnail and page number have separate bounds at scale " + scale + " for " + size);
            }
            if (!File.Exists(Path.Combine(directory, "selection-pages.pdf")) ||
                !File.Exists(Path.Combine(directory, "ocr-selection.pdf")))
            {
                log.AppendLine("SKIP: selection fixtures missing; run selection_fixtures.py for mouse/OCR coverage.");
                return;
            }
            PublicWorkerCoverage(directory);
            using (var form = new MainWindow(null))
            {
                form.LoadForTest(PdfDocument.Open(Path.Combine(directory, "selection-pages.pdf"), ""));
                form.Show(); Application.DoEvents(); form.ApplyZoom("50%"); form.SetTool(Tool.Read);
                var view = form.CanvasForTest;
                Point start = SelectionPoint(form, 0, 0), finish = SelectionPoint(form, 1, LastCharacter(form.Document, 1));
                Mouse(view, start, finish);
                string copied = view.CopiedText();
                Assert(copied.Contains("PAGE 1 SELECTION START") && copied.Contains("PAGE 2 SELECTION END"),
                       "actual mouse drag copies continuous text across two pages");
                Assert(view.TextRanges().Select(r => r.Page).SequenceEqual(new[] { 0, 1 }),
                       "cross-page selection has correctly ordered page ranges");
                AwaitRaster(form);
                using (var image = new Bitmap(form.Width, form.Height))
                { form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height)); image.Save(Path.Combine(directory, "cross-page-selection.png"), ImageFormat.Png); }
                form.ActivatePageFromCanvas(1,true);
                Assert(view.CopiedText() == copied, "scroll-driven active-page updates preserve the complete selection");
                Mouse(view, finish, start);
                Assert(view.CopiedText() == copied, "reverse cross-page drag copies in document order");
                form.HighlightSelection(Color.FromArgb(255, 220, 56));
                int active = form.Document.Index;
                for (int i = 0; i < 2; i++)
                {
                    form.Document.OpenPage(i);
                    Assert(N.FPDFPage_GetAnnotCount(form.Document.Page) == 1, "cross-page highlight marks selected page " + i);
                }
                form.Document.Save(Path.Combine(directory, "cross-page-highlight.pdf"));
                form.Document.Undo();
                for (int i = 0; i < 2; i++)
                {
                    form.Document.OpenPage(i);
                    Assert(N.FPDFPage_GetAnnotCount(form.Document.Page) == 0, "one undo removes cross-page highlight from page " + i);
                }
                form.Document.OpenPage(active);
                form.SetLayout(PageLayout.Double); form.ApplyZoom("50%"); Application.DoEvents();
                start = SelectionPoint(form, 0, 0); finish = SelectionPoint(form, 1, LastCharacter(form.Document, 1));
                Mouse(view, start, finish);
                Assert(view.CopiedText().Contains("PAGE 2 SELECTION END"), "facing-page layout supports crossing the gutter while selecting");
                form.SetLayout(PageLayout.Single); form.ApplyZoom("130%"); form.CanvasForTest.AutoScrollPosition = Point.Empty;
                start = SelectionPoint(form, 0, 0); finish = new Point(start.X, view.ClientSize.Height + 12);
                N.SendMessage(view.Handle, 0x201, new IntPtr(1), Position(start));
                N.SendMessage(view.Handle, 0x200, new IntPtr(1), Position(finish));
                int beforeScroll = view.AutoScrollPosition.Y;
                var tick = typeof(PdfCanvas).GetMethod("SelectionScrollTick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                for (int i = 0; i < 20; i++) tick.Invoke(view, null);
                Assert(view.AutoScrollPosition.Y < beforeScroll && view.TextRanges().Count > 1, "held selection scrolls the viewport and extends across newly visible pages");
                N.SendMessage(view.Handle, 0x202, IntPtr.Zero, Position(finish));
                Assert(view.CopiedText().Contains("PAGE 2 SELECTION"), "selection survives mouse release after automatic scrolling");
                using (var answer = AnswerSavePrompt(7, UiText.Get("未保存的修改"))) form.Close();
            }
            string publicScan = Path.Combine(directory, "public-ocr.pdf");
            if (File.Exists(publicScan)) using (var form = new MainWindow(null))
            {
                form.LoadForTest(PdfDocument.Open(publicScan, "")); form.Show(); Application.DoEvents();
                form.SetTool(Tool.Read); form.CanvasForTest.Fit=2; form.CanvasForTest.Rebuild(true); Application.DoEvents();
                Mouse(form.CanvasForTest, SelectionPoint(form, 0, 0), SelectionPoint(form, 0, LastCharacter(form.Document, 0)));
                Assert(form.CanvasForTest.CopiedText().Contains("Lorem ipsum"), "public PDF.js scan issue5747 selects and copies its hidden OCR layer");
                Assert(!form.Document.Dirty, "selecting public OCR text leaves the scanned image and document unchanged");
                form.Close();
            }
            using (var form = new MainWindow(null))
            {
                form.LoadForTest(PdfDocument.Open(Path.Combine(directory, "ocr-selection.pdf"), ""));
                form.Show(); Application.DoEvents(); form.SetTool(Tool.Read); form.ApplyZoom("80%");
                for (int page = 0; page < form.Document.Count; page++)
                {
                    form.Navigate(page); form.CanvasForTest.ClearSelection(); Application.DoEvents();
                    var text = form.Document.SelectionPage(page);
                    Assert(!form.Document.CanRenderQuickly(page), "scan images stay off the UI-thread quick raster path in variant " + page);
                    Assert(text.Read(0, text.Count).Contains("扫描文字选择"), "OCR variant " + page + " retains its Chinese text layer");
                    Point from = SelectionPoint(form, page, 0), to = SelectionPoint(form, page, LastCharacter(form.Document, page));
                    var rectangle = form.CanvasForTest.LayoutRectangles[page]; rectangle.Offset(form.CanvasForTest.AutoScrollPosition);
                    from.X = rectangle.Left + 3;
                    Mouse(form.CanvasForTest, from, to);
                    Assert(form.CanvasForTest.CopiedText().Contains("扫描文字选择") && form.CanvasForTest.CopiedText().Contains("OCR TEXT SELECTION"),
                           "actual margin-to-text drag selects hidden Chinese OCR variant " + page);
                    Assert(form.CanvasForTest.TextRanges().Count == 1 && !form.Document.Dirty,
                           "OCR selection alone preserves document and image in variant " + page);
                }
                form.Navigate(0); if(!form.SidebarShownForTest)form.ToggleSidebar(); AwaitRaster(form);
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(directory,"ocr-sidebar-preview.png"),ImageFormat.Png);}
                form.Close();
            }
        }
        internal static int RunSelection(string directory)
        {
            logPath = Path.Combine(directory, "selection-test.txt");
            try { SelectionCoverage(directory); log.AppendLine("TOTAL: " + assertions + " selection assertions, 0 failures"); File.WriteAllText(logPath, log.ToString()); return 0; }
            catch (Exception error) { log.AppendLine(error.ToString()); File.WriteAllText(logPath, log.ToString()); return 1; }
        }
    }
}
