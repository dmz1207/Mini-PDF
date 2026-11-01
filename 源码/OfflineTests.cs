using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniPdf {
    internal static partial class SelfTest {
        internal static int RunPortablePackage(string directory) {
            Directory.CreateDirectory(directory);logPath=Path.Combine(directory,"portable-package-tests.txt");
            string previous=Environment.GetEnvironmentVariable("MINIPDF_LAUNCHER"),active=null;
            Action<string> stop=delegate(string package){try{var c=MiniAiClient.Serializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(package,"state","connection.json")));MiniAiClient.Request(package,"/v1/shutdown",new{stop=true},5);try{using(var p=System.Diagnostics.Process.GetProcessById(Convert.ToInt32(c["pid"])))p.WaitForExit(10000);}catch(ArgumentException){}}catch(IOException){}catch(System.Net.WebException){}};
            try {
                string project=Directory.GetParent(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName).FullName;
                string isolated=Path.Combine(directory,"portable-"+Guid.NewGuid().ToString("N"));string home=Path.Combine(isolated,"reader"),external=Path.Combine(isolated,"external software");Directory.CreateDirectory(home);Directory.CreateDirectory(external);
                File.Copy(Path.Combine(project,"Mini PDF.exe"),Path.Combine(home,"Mini PDF.exe"),true);File.Copy(Path.Combine(project,"MiniAI-Portable.exe"),Path.Combine(home,"MiniAI-Portable.exe"),true);
                Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",Path.Combine(home,"Mini PDF.exe"));active=Path.Combine(home,"MiniAI");
                Assert(!Directory.Exists(active)&&MiniAiClient.Available,"unextracted EXE enables the actual reader UI");
                using(var form=new MainWindow(null))using(var dialog=new OfflineWindow(form,true,"The control system remains stable.")) {
                    form.StartPosition=dialog.StartPosition=FormStartPosition.Manual;form.Location=dialog.Location=new Point(-20000,-20000);form.ShowInTaskbar=dialog.ShowInTaskbar=false;
                    form.Show();dialog.Show(form);dialog.TranslateForTest();Assert(dialog.BusyForTest,"translation UI starts before model files have been extracted");PumpOffline(delegate{return !dialog.BusyForTest;});
                    if(!dialog.IsDisposed&&!dialog.TranslationForTest.Contains("稳定"))using(var snapshot=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(snapshot,new Rectangle(Point.Empty,dialog.Size));snapshot.Save(Path.Combine(directory,"portable-translation-failure.png"));}
                    Assert(dialog.TranslationForTest.Contains("稳定"),"first-use translation UI prepares the EXE and completes real inference: busy="+dialog.BusyForTest+"; disposed="+dialog.IsDisposed+"; status="+dialog.StatusForTest+"; text="+dialog.TranslationForTest);
                }
                stop(active);active=null;
                string selected=Path.Combine(external,"Mini OCR.exe");File.Copy(Path.Combine(project,"MiniAI-Portable.exe"),selected,true);MiniAiClient.SelectPackage(selected);active=Path.Combine(external,"MiniAI");
                Assert(MiniAiClient.PackRoot==active&&!Directory.Exists(active)&&MiniAiClient.Available,"file selection accepts a renamed external portable EXE without a cache folder");
                bool rejected=false;try{MiniAiClient.SelectPackage(Path.Combine(Environment.SystemDirectory,"cmd.exe"));}catch(InvalidOperationException){rejected=true;}
                Assert(rejected&&MiniAiClient.PackRoot==active,"unrelated EXE is rejected without replacing the selected package");
                string image=Path.Combine(directory,"photo.png");using(var bitmap=new Bitmap(1400,240)){using(var g=Graphics.FromImage(bitmap))using(var font=new Font("Arial",44)){g.Clear(Color.White);g.DrawString("CONTROL SYSTEM 12345",font,Brushes.Black,30,60);}bitmap.Save(image,ImageFormat.Png);}
                using(var form=new MainWindow(null)) {
                    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-24000,-24000);form.ShowInTaskbar=false;form.Show();var document=PdfDocument.New();document.AddImage(image,new N.Rect(30,600,565,770));form.LoadForTest(document);
                    using(var dialog=new OfflineWindow(form,false,"")){dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-24000,-24000);dialog.ShowInTaskbar=false;dialog.Show(form);dialog.OcrForTest();Assert(dialog.BusyForTest,"OCR UI starts from an external EXE before extraction");PumpOffline(delegate{return !dialog.BusyForTest;});string read=String.Join(" ",dialog.OcrLinesForTest.Select(l=>l.Text));Assert(read.IndexOf("CONTROL",StringComparison.OrdinalIgnoreCase)>=0&&read.Contains("12345"),"first-use OCR UI prepares the chosen EXE and reads the scan: "+dialog.StatusForTest+"; text="+read);}
                    MiniAiClient.SelectPackage(Path.Combine(active,"MiniAI.exe"));Assert(MiniAiClient.PackRoot==active&&MiniAiClient.Available,"existing extracted MiniAI.exe remains supported");
                    File.WriteAllText(Path.Combine(home,"MiniPDF.MiniAI.txt"),active);Assert(MiniAiClient.PackRoot==active&&MiniAiClient.Available,"existing folder settings remain compatible");
                }
                PumpOffline(delegate{return !Directory.Exists(active);},30000);Assert(!Directory.Exists(active),"the final reader exit releases the portable service and cleans its runtime folder");
                File.AppendAllText(logPath,"\r\nAssertions: "+assertions);return 0;
            }catch(Exception error){File.AppendAllText(logPath,"\r\n"+error);return 1;}
            finally{if(active!=null)stop(active);Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",previous);}
        }
        static bool SameOcrPixels(Bitmap a,Bitmap b) {
            if(a.Size!=b.Size)return false;var area=new Rectangle(Point.Empty,a.Size);var x=a.LockBits(area,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var y=b.LockBits(area,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{var one=new byte[Math.Abs(x.Stride)*a.Height];var two=new byte[Math.Abs(y.Stride)*b.Height];Marshal.Copy(x.Scan0,one,0,one.Length);Marshal.Copy(y.Scan0,two,0,two.Length);return one.SequenceEqual(two);}finally{a.UnlockBits(x);b.UnlockBits(y);}
        }
        internal static int RunOffline(string directory) {
            Directory.CreateDirectory(directory);logPath=Path.Combine(directory,"offline-tests.txt");
            try {
                string root=Directory.GetParent(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName).FullName;
                string readerHome=Path.Combine(directory,"installed-reader");Directory.CreateDirectory(readerHome);
                Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",Path.Combine(readerHome,"Mini PDF.exe"));
                MiniAiClient.SelectPackage(Path.Combine(root,"MiniAI","MiniAI.exe"));
                Environment.SetEnvironmentVariable("MINIAI_RESULTS_DIR",Path.Combine(directory,"results"));
                var parts=MiniAiWorker.Parts(new String('a',1900));Assert(parts.Sum(p=>p.Length)==1900&&parts.All(p=>p.Length<=900),"translation segmentation retains long inputs");
                Assert(MiniAiWorker.Parts("con-\r\ntrol systems")[0]=="control systems","translation joins PDF line-end hyphenation");
                string image=Path.Combine(root,"work","ai-tests","ocr-bilingual.png");Assert(File.Exists(image),"bilingual OCR fixture is available");
                using(var form=new MainWindow(null)) {
                    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-24000,-24000);form.ShowInTaskbar=false;form.Show();var document=PdfDocument.New();document.AddImage(image,new N.Rect(30,560,565,770));form.LoadForTest(document);
                    using(var before=document.Render(0,720,1020))using(var dialog=new OfflineWindow(form,false,"")) {
                        dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-24000,-24000);dialog.ShowInTaskbar=false;dialog.Show(form);dialog.OcrForTest();var watch=System.Diagnostics.Stopwatch.StartNew();while(dialog.BusyForTest&&watch.ElapsedMilliseconds<60000){Application.DoEvents();System.Threading.Thread.Sleep(10);}Application.DoEvents();
                        Assert(!dialog.BusyForTest,"PDF OCR finishes in an isolated worker without blocking UI input");Assert(dialog.OcrLinesForTest.Count>=5,"OCR returns all bilingual scan lines: "+dialog.StatusForTest);
                        var health=MiniAiClient.Request(Path.Combine(root,"MiniAI"),"/health",null);Assert(Convert.ToInt32(health["api_version"])==1,"the PDF toolbar automatically starts MiniAI and the shared service survives its PDF worker exiting");
                        Assert(dialog.OcrLinesForTest.Any(l=>l.Text.Contains("control system"))&&dialog.OcrLinesForTest.Any(l=>l.Text.Contains("中英文翻译")),"standalone OCR recognizes Chinese and English rendered from a scanned PDF");
                        var lines=dialog.OcrLinesForTest.ToList();dialog.ApplyForTest();Assert(document.Dirty&&document.SelectedText(0,100000).Contains("control system"),"OCR adds a selectable and searchable text layer");
                        using(var after=document.Render(0,720,1020))Assert(SameOcrPixels(before,after),"invisible OCR text leaves every rendered page pixel unchanged");
                        int characters=N.FPDFText_CountChars(document.TextPage);document.AddOcrLayer(lines);Assert(N.FPDFText_CountChars(document.TextPage)==characters,"rerunning OCR replaces the Mini PDF layer without duplicating text");
                        document.Undo();Assert(document.SelectedText(0,100000).Contains("中英文翻译"),"OCR layer replacement supports undo");
                        string output=Path.Combine(directory,"searchable-bilingual.pdf");document.Save(output);using(var reopened=PdfDocument.Open(output,"")){Assert(reopened.SelectedText(0,100000).Contains("control system")&&reopened.SelectedText(0,100000).Contains("中英文翻译"),"OCR text survives save and reopen with Unicode intact");Assert(SearchText.Page(reopened,0,"control system",false,false).Count==1,"the new search engine finds the saved OCR text");}
                        using(var snapshot=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(snapshot,new Rectangle(0,0,dialog.Width,dialog.Height));snapshot.Save(Path.Combine(directory,"offline-window.png"),ImageFormat.Png);}dialog.Close();
                    }
                    using(var translation=new OfflineWindow(form,true,"The control system remains stable.")) {
                        translation.StartPosition=FormStartPosition.Manual;translation.Location=new Point(-24000,-24000);translation.ShowInTaskbar=false;translation.Show(form);translation.TranslateForTest();var watch=System.Diagnostics.Stopwatch.StartNew();while(translation.BusyForTest&&watch.ElapsedMilliseconds<180000){Application.DoEvents();System.Threading.Thread.Sleep(10);}Application.DoEvents();
                        Assert(!translation.BusyForTest&&translation.TranslationForTest.Any(c=>c>=0x4e00&&c<=0x9fff),"selected PDF text is translated through the real background UI worker: "+translation.StatusForTest);
                        using(var snapshot=new Bitmap(translation.Width,translation.Height)){translation.DrawToBitmap(snapshot,new Rectangle(0,0,translation.Width,translation.Height));snapshot.Save(Path.Combine(directory,"translation-window.png"),ImageFormat.Png);}translation.Close();
                    }
                    using(var combined=new OfflineWindow(form,false,"")) {
                        combined.Show(form);combined.OcrTranslateForTest();var watch=System.Diagnostics.Stopwatch.StartNew();while(combined.BusyForTest&&watch.ElapsedMilliseconds<180000){Application.DoEvents();System.Threading.Thread.Sleep(10);}Application.DoEvents();Assert(!combined.BusyForTest&&combined.OcrLinesForTest.Count>=5&&combined.TranslationForTest.Length>20,"one action recognizes a scanned PDF page then translates the result: "+combined.StatusForTest);combined.Close();
                    }
                }
                QuickTranslationChecks(directory,root,image);
                OcrWordChecks(directory,root,image);
                File.AppendAllText(logPath,"\r\nAssertions: "+assertions);return 0;
            }catch(Exception error){File.AppendAllText(logPath,"\r\n"+error);return 1;}
        }
        static void PumpOffline(Func<bool> ready,int timeout=180000){var watch=System.Diagnostics.Stopwatch.StartNew();while(!ready()&&watch.ElapsedMilliseconds<timeout){Application.DoEvents();System.Threading.Thread.Sleep(10);}Application.DoEvents();}
        static void QuickTranslationChecks(string directory,string root,string fixture) {
            string launcher=Environment.GetEnvironmentVariable("MINIPDF_LAUNCHER");string empty=Path.Combine(directory,"without-ai","reader");Directory.CreateDirectory(empty);Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",Path.Combine(empty,"Mini PDF.exe"));
            using(var absent=new MainWindow(null))Assert(!absent.QuickTranslationEnabledForTest,"selection and capture commands are disabled without an AI add-on");Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",launcher);
            var selection=MiniAI.Desktop.CaptureOverlay.Selection(new Point(190,130),new Point(-20,20),new Size(200,160));Assert(selection==new Rectangle(0,20,190,110),"screen selection accepts reverse drags and clips to desktop bounds");
            using(var image=new Bitmap(fixture))using(var crop=MiniAI.Desktop.CaptureOverlay.Crop(image,new Rectangle(50,50,1200,180)))Assert(crop.Width==1200&&crop.Height==180,"screen OCR receives only the selected crop");
            using(var window=new MainWindow(null)) {
                window.StartPosition=FormStartPosition.Manual;window.Location=new Point(-24000,-24000);window.ShowInTaskbar=false;window.Show();var document=PdfDocument.New();document.AddText("The control system remains stable.",60,760,14,"Arial",Color.Black);window.LoadForTest(document);Application.DoEvents();Assert(window.QuickTranslationEnabledForTest,"installed MiniAI lights up selection and screen translation");
                var canvas=window.CanvasForTest;var textPage=document.SelectionPage(0);var shape=textPage.Rectangles(0,textPage.Count).First();Rectangle area=canvas.DeviceRect(shape);var left=new Point(area.Left+1,area.Top+area.Height/2);var right=new Point(area.Right-1,left.Y);
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(PdfCanvas).GetMethod("OnMouseDown",flags).Invoke(canvas,new object[]{new MouseEventArgs(MouseButtons.Left,1,left.X,left.Y,0)});
                typeof(PdfCanvas).GetMethod("OnMouseMove",flags).Invoke(canvas,new object[]{new MouseEventArgs(MouseButtons.Left,0,right.X,right.Y,0)});
                typeof(PdfCanvas).GetMethod("OnMouseUp",flags).Invoke(canvas,new object[]{new MouseEventArgs(MouseButtons.Left,1,right.X,right.Y,0)});
                var bubble=window.TranslationBubbleForTest;Assert(bubble!=null,"real text drag and mouse release open the translation bubble");bubble.PinForTest();PumpOffline(delegate{return !bubble.BusyForTest;});Assert(bubble.ResultForTest.Contains("稳定"),"selection bubble translates the actual selected text into Chinese: "+bubble.ResultForTest);
                Assert(!bubble.Region.IsVisible(0,0)&&bubble.FormBorderStyle==FormBorderStyle.None&&bubble.Width<700,"translation uses a compact rounded bubble without a window caption");
                bubble.Translate(new String('a',1900));bubble.Translate("The system remains stable.");PumpOffline(delegate{return !bubble.BusyForTest;});Assert(bubble.ResultForTest.Contains("稳定")&&bubble.ResultForTest.Length<100,"a new selection cancels stale work and only its translation is shown");
                bubble.TargetForTest(3);PumpOffline(delegate{return !bubble.BusyForTest;});Assert(bubble.ResultForTest.Contains("穩定"),"the installed translation model supports Traditional Chinese: "+bubble.ResultForTest);
                using(var picture=new Bitmap(bubble.Width,bubble.Height)){bubble.DrawToBitmap(picture,new Rectangle(Point.Empty,bubble.Size));picture.Save(Path.Combine(directory,"translation-bubble.png"));}
                canvas.ClearSelection();Application.DoEvents();Assert(bubble.IsDisposed,"clearing the selection cancels and dismisses its bubble");
                using(var scan=new MiniAI.Desktop.TranslationBubble(MainWindow.TranslationBackend(),new Rectangle(100,100,10,10),true)) {
                    scan.Show(window);scan.PinForTest();using(var original=new Bitmap(fixture))scan.Recognize(MiniAI.Desktop.CaptureOverlay.Crop(original,new Rectangle(50,50,1200,160)));PumpOffline(delegate{return !scan.BusyForTest;});Assert(scan.SourceForTest.Contains("control system")&&scan.ResultForTest.Contains("稳定"),"a captured image region is recognized and translated in the compact bubble: "+scan.SourceForTest+" -> "+scan.ResultForTest);
                    using(var picture=new Bitmap(scan.Width,scan.Height)){scan.DrawToBitmap(picture,new Rectangle(Point.Empty,scan.Size));picture.Save(Path.Combine(directory,"screenshot-bubble.png"));}scan.Close();
                }
            }
        }
        static string WordXml(string path){using(var zip=ZipFile.OpenRead(path))using(var reader=new StreamReader(zip.GetEntry("word/document.xml").Open()))return reader.ReadToEnd();}
        static void OcrWordChecks(string directory,string root,string fixture) {
            using(var document=PdfDocument.New()) {
                document.AddImage(fixture,new N.Rect(30,560,565,770));document.InsertBlank();document.AddText("NATIVE_TEXT_MUST_REMAIN_EDITABLE",60,760,14,"Arial",Color.Black);document.OpenPage(0);byte[] original=document.GetRenderSource().ReadAll();bool dirty=document.Dirty;int epoch=document.RenderEpoch;
                Assert(WordOcr.Needed(document,0)&&!WordOcr.Needed(document,1),"Word OCR distinguishes an image-only scan from native editable text");
                string continuous=Path.Combine(directory,"ocr-continuous.docx");var settings=new OfficeExportOptions{Pages=new[]{0,1},Continuous=true,Ocr=true,Images=true,MiniAiRoot=Path.Combine(root,"MiniAI")};
                using(var progress=new OfficeProgress(document,settings,continuous)){progress.Show();PumpOffline(delegate{return progress.DialogResult!=DialogResult.None||progress.IsDisposed;});Assert(progress.DialogResult==DialogResult.OK&&File.Exists(continuous),"the real isolated Word worker connects OCR to continuous editable export");progress.Close();}
                string text=WordXml(continuous);Assert(text.Contains("control system")&&text.Contains("中英文翻译")&&text.Contains("NATIVE_TEXT_MUST_REMAIN_EDITABLE"),"mixed scan/native Word export contains editable Chinese and English text");Assert(!text.Contains("w:txbxContent")&&text.Contains("wordprocessingDrawing"),"OCR continuous text uses ordinary Word paragraphs and retains uncertain artwork");
                string positioned=Path.Combine(directory,"ocr-layout.docx");OfficeExport.Save(document,new OfficeExportOptions{Pages=new[]{0},Layout=true,Ocr=true,MiniAiRoot=Path.Combine(root,"MiniAI")},positioned,null);text=WordXml(positioned);Assert(text.Contains("control system")&&text.Contains("w:txbxContent"),"OCR layout export places editable prose at recognized page positions");
                Assert(document.GetRenderSource().ReadAll().SequenceEqual(original)&&document.Dirty==dirty&&document.RenderEpoch==epoch,"OCR-to-Word leaves the PDF bytes, dirty state and undo/render epoch unchanged");
                string cancel=Path.Combine(directory,"ocr-cancel.docx");File.WriteAllText(cancel,"EXISTING_OUTPUT");var canceled=new OfficeExportOptions{Pages=Enumerable.Repeat(0,100).ToArray(),Continuous=true,Ocr=true,MiniAiRoot=Path.Combine(root,"MiniAI")};using(var progress=new OfficeProgress(document,canceled,cancel)){progress.Show();PumpOffline(delegate{return File.Exists(canceled.TempPath);},5000);progress.Close();Application.DoEvents();}Assert(File.ReadAllText(cancel)=="EXISTING_OUTPUT"&&!File.Exists(canceled.TempPath),"canceling OCR export protects an existing output and removes its temporary copy");
            }
        }
    }
}
