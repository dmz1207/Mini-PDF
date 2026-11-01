using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using MiniAI.Desktop;

namespace MiniPdf {
    internal static partial class SelfTest {
        static readonly BindingFlags FileTestFlags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void FileEvent(Control target,string name,object argument){typeof(Control).GetMethod(name,FileTestFlags).Invoke(target,new[]{argument});}
        internal static int RunOfflineFiles(string directory){
            Directory.CreateDirectory(directory);logPath=Path.Combine(directory,"offline-files.txt");string previous=Environment.GetEnvironmentVariable("MINIPDF_LAUNCHER");
            try{
                string ai=Environment.GetEnvironmentVariable("MINIPDF_TEST_MINIAI");PdfDocument.Check(!String.IsNullOrEmpty(ai)&&File.Exists(Path.Combine(ai,"MiniAI.exe")),"Set MINIPDF_TEST_MINIAI to a built MiniAI source folder.");
                string home=Path.Combine(directory,"reader");Directory.CreateDirectory(home);Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",Path.Combine(home,"Mini PDF.exe"));MiniAiClient.SelectPackage(Path.Combine(ai,"MiniAI.exe"));
                string photo=Path.Combine(directory,"照片 test.png"),second=Path.Combine(directory,"第二张.png");
                using(var image=new Bitmap(1200,200)){using(var g=Graphics.FromImage(image))using(var font=new Font("Arial",30)){g.Clear(Color.White);g.DrawString("The control system remains stable. 12345",font,Brushes.Black,18,60);}image.Save(photo,ImageFormat.Png);image.Save(second,ImageFormat.Png);}
                string pdf=Path.Combine(directory,"扫描.pdf");using(var document=PdfDocument.New()){document.AddImage(photo,new N.Rect(30,600,565,750));document.Save(pdf);}
                using(var form=new MainWindow(null)){
                    form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-24000,-24000);form.Show();form.OpenDocument(pdf);var original=form.Document;byte[] before=original.GetRenderSource().ReadAll();
                    using(var panel=new OfflineWindow(form,false,"")){
                        panel.ShowInTaskbar=false;panel.StartPosition=FormStartPosition.Manual;panel.Location=new Point(-24000,-24000);panel.Show(form);Application.DoEvents();
                        var source=panel.Controls.Find("OcrFileSource",true)[0];var text=panel.Controls.Find("OcrText",true)[0];
                        Assert(new[]{"OpenOcrFile","CaptureOcr","CaptureOcrTranslate","PasteOcrImage","UseCurrentPdf"}.All(name=>panel.Controls.Find(name,true).Single().Visible),"file, screenshot, paste and current-PDF actions are present");
                        var drop=new DataObject(DataFormats.FileDrop,new[]{photo});var enter=new DragEventArgs(drop,0,0,0,DragDropEffects.Copy,DragDropEffects.None);FileEvent(text,"OnDragEnter",enter);Assert(text.AllowDrop&&enter.Effect==DragDropEffects.Copy,"the OCR text area accepts dragged photos");
                        FileEvent(text,"OnDragDrop",new DragEventArgs(drop,0,0,0,DragDropEffects.Copy,DragDropEffects.Copy));Assert(panel.BusyForTest,"dropping a photo starts the isolated OCR worker");PumpOffline(delegate{return !panel.BusyForTest;});Assert(panel.RecognizedForTest.Contains("control system")&&panel.RecognizedForTest.Contains("12345"),"dragged Unicode photo path is recognized by the real model: "+panel.StatusForTest);
                        Assert(source.Text.Contains("照片 test")&&!panel.ApplyEnabledForTest,"photo source is shown and cannot be added to the current PDF");panel.ApplyForTest();Assert(original.GetRenderSource().ReadAll().SequenceEqual(before),"even direct apply cannot put external image text onto the PDF");
                        panel.ImportForTest(photo,second);PumpOffline(delegate{return !panel.BusyForTest;});Assert(panel.RecognizedForTest.Split(new[]{"12345"},StringSplitOptions.None).Length==3,"both photos in a multi-file drop are recognized");
                        using(var image=Image.FromFile(photo))Clipboard.SetImage(image);((Button)panel.Controls.Find("PasteOcrImage",true)[0]).PerformClick();PumpOffline(delegate{return !panel.BusyForTest;});Assert(panel.RecognizedForTest.Contains("control system")&&source.Text==UiText.Get("剪贴板图片")&&!panel.ApplyEnabledForTest,"pasted image follows OCR without modifying the PDF");
                        string note=Path.Combine(directory,"原文.txt"),words="The system remains stable. 中文原文";File.WriteAllText(note,words,new UTF8Encoding(true));panel.ImportForTest(photo);panel.ImportForTest(note);PumpOffline(delegate{return !panel.BusyForTest;});Application.DoEvents();Assert(panel.RecognizedForTest==words&&source.Text=="原文.txt","loading text cancels pending image OCR and retains all text");
                        string gbk=Path.Combine(directory,"旧文本.txt");File.WriteAllText(gbk,"中文测试",Encoding.GetEncoding(936));panel.ImportForTest(gbk);Assert(panel.RecognizedForTest=="中文测试","legacy GBK text is decoded without garbling");
                        bool rejected=false;try{panel.ImportForTest(photo,note);}catch(InvalidOperationException){rejected=true;}Assert(rejected&&panel.RecognizedForTest=="中文测试","mixed unsupported drops preserve the current input");
                        panel.ImportForTest(pdf);Assert(form.Document.FilePath==pdf&&source.Text=="扫描.pdf","a PDF drop switches the OCR source to the opened PDF");panel.OcrForTest();PumpOffline(delegate{return !panel.BusyForTest;});Assert(panel.RecognizedForTest.Contains("12345")&&panel.ApplyEnabledForTest,"direct current-page OCR remains available with searchable-layer action");
                        CaptureFileChecks(panel,photo,directory);
                        panel.ClientSize=new Size(720,520);Application.DoEvents();Assert(panel.Controls.Find("OpenOcrFile",true)[0].Visible&&text.Height>100,"file actions and OCR output remain usable in a compact window");var sourceBox=panel.Controls.Find("TranslationSource",true)[0];var resultBox=panel.Controls.Find("TranslationResult",true)[0];Assert(sourceBox.Height>=70&&resultBox.Height>=70&&resultBox.Parent.Parent.ClientRectangle.Contains(resultBox.Parent.Bounds),"both original and translated text remain inside the resized panel");using(var picture=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(picture,new Rectangle(Point.Empty,panel.Size));picture.Save(Path.Combine(directory,"offline-file-panel.png"));}
                    }
                    form.Close();
                }
                using(var image=new Bitmap(80,40)){using(var rotated=MiniAiWorker.NormalizePhoto(image,6))Assert(rotated.Size==new Size(40,80)&&image.Size==new Size(80,40),"phone rotation preserves geometry without mutating the source container");}
                File.AppendAllText(logPath,"\r\nAssertions: "+assertions);return 0;
            }catch(Exception error){File.AppendAllText(logPath,"\r\n"+error);return 1;}
            finally{Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",previous);}
        }
        static void CaptureFileChecks(OfflineWindow panel,string photo,string directory){
            using(var fixture=new Form{StartPosition=FormStartPosition.Manual,Location=new Point(30,80),ClientSize=new Size(1200,220),ShowInTaskbar=false,TopMost=true})using(var picture=new PictureBox{Dock=DockStyle.Fill,Image=Image.FromFile(photo),SizeMode=PictureBoxSizeMode.Normal}){
                fixture.Controls.Add(picture);fixture.Show();fixture.BringToFront();Application.DoEvents();System.Threading.Thread.Sleep(300);Application.DoEvents();Rectangle target=picture.RectangleToScreen(new Rectangle(0,0,1200,200));
                bool used=false;using(var timer=new Timer{Interval=180}){
                    timer.Tick+=delegate{var overlay=Application.OpenForms.OfType<CaptureOverlay>().FirstOrDefault();if(overlay==null)return;timer.Stop();used=true;using(var captured=(Bitmap)((Bitmap)typeof(CaptureOverlay).GetField("screen",FileTestFlags).GetValue(overlay)).Clone(overlay.RectangleToClient(target),PixelFormat.Format32bppArgb))captured.Save(Path.Combine(directory,"screen-input.png"));Assert(overlay.Cursor.Handle!=Cursors.Cross.Handle,"the live screen selector uses a custom high-contrast cursor");using(var cursor=new Bitmap(64,64)){using(var g=Graphics.FromImage(cursor)){g.Clear(Color.Gray);overlay.Cursor.Draw(g,new Rectangle(8,8,48,48));}cursor.Save(Path.Combine(directory,"capture-cursor.png"));Assert(Enumerable.Range(0,cursor.Width).Any(x=>Enumerable.Range(0,cursor.Height).Any(y=>{var c=cursor.GetPixel(x,y);return c.G>220&&c.R<100&&c.B<160;})),"cursor pixels are bright green against the dimmed background");}Point a=overlay.PointToClient(target.Location),b=overlay.PointToClient(new Point(target.Right,target.Bottom));typeof(CaptureOverlay).GetMethod("OnMouseDown",FileTestFlags).Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,a.X,a.Y,0)});typeof(CaptureOverlay).GetMethod("OnMouseMove",FileTestFlags).Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,0,b.X,b.Y,0)});typeof(CaptureOverlay).GetMethod("OnMouseUp",FileTestFlags).Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,b.X,b.Y,0)});};timer.Start();((Button)panel.Controls.Find("CaptureOcrTranslate",true)[0]).PerformClick();
                }
                Assert(used&&panel.Visible,"screenshot translation restores the OCR panel after selecting a region");PumpOffline(delegate{return !panel.BusyForTest;});Assert(panel.RecognizedForTest.Contains("control system")&&panel.TranslationForTest.Contains("稳定"),"real screen pixels pass through OCR and Hy translation: "+panel.StatusForTest+"; OCR="+panel.RecognizedForTest+"; "+panel.TranslationForTest);
                string previous=panel.RecognizedForTest;using(var timer=new Timer{Interval=100}){timer.Tick+=delegate{var overlay=Application.OpenForms.OfType<CaptureOverlay>().FirstOrDefault();if(overlay==null)return;timer.Stop();overlay.DialogResult=DialogResult.Cancel;};timer.Start();((Button)panel.Controls.Find("CaptureOcr",true)[0]).PerformClick();}Assert(panel.Visible&&panel.RecognizedForTest==previous,"canceling the capture restores the panel and keeps recognized text");fixture.Close();picture.Image.Dispose();
            }
        }
    }
}
