using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using MiniAI.Desktop;

namespace MiniPdf {
    internal static partial class SelfTest {
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateHardLink(string name,string existing,IntPtr security);
        static readonly BindingFlags CaptureFlags=BindingFlags.Instance|BindingFlags.NonPublic;
        static Dictionary<string,object> CaptureLine(string text,string tex=""){return new Dictionary<string,object>{{"text",text},{"kind",tex.Length>0?"formula":"text"},{"tex",tex}};}
        static CaptureContent CaptureResponse(params Dictionary<string,object>[] lines){return CaptureContent.Parse(new Dictionary<string,object>{{"formula",true},{"lines",lines}});}
        static ToolStripItem CaptureItem(MainWindow form,string name){return (ToolStripItem)typeof(MainWindow).GetField(name,CaptureFlags).GetValue(form);}
        static CaptureRecognitionWindow CaptureWindow(MainWindow form){return (CaptureRecognitionWindow)typeof(MainWindow).GetField("captureOcrWindow",CaptureFlags).GetValue(form);}
        internal static int RunCaptureOcr(string directory) {
            Directory.CreateDirectory(directory);logPath=Path.Combine(directory,"capture-tests.txt");string prior=Environment.GetEnvironmentVariable("MINIPDF_LAUNCHER");
            string pack=Environment.GetEnvironmentVariable("MINIPDF_TEST_PORTABLE");string home=Path.Combine(directory,"capture-reader"),empty=Path.Combine(directory,"capture-empty","reader");
            try {
                var text=CaptureResponse(CaptureLine("The system remains stable."));Assert(text.Parts.Count==1&&text.Selected==0&&!text.Parts[0].Formula&&text.Parts[0].Value=="The system remains stable.","ordinary OCR remains editable text without a formula wrapper");
                var formula=CaptureResponse(CaptureLine("x2+y2=z2",@"x^{2}+y^{2}=z^{2}"));Assert(formula.Parts.Count==2&&formula.Selected==1&&formula.Parts[1].Formula,"a single formula opens its editable code directly");
                var mixed=CaptureResponse(CaptureLine("The system is stable."),CaptureLine("x2",@"x^2"),CaptureLine("y2",@"y^2"));Assert(mixed.Selected==0&&mixed.Parts.Count==3&&mixed.Parts[0].Value.Contains("The system")&&mixed.Parts[0].Value.Contains("$$")&&mixed.Parts[2].Value=="y^2","mixed captures retain all prose and separate equations in reading order");
                foreach(string ordinary in new[]{"2026-11-04","(27)","Fig. 2","100 Hz"})Assert(CaptureResponse(CaptureLine(ordinary,@"x^2")).Parts.Count==1,"automatic mode keeps an ambiguous date, label or measurement as text: "+ordinary);
                var fallback=CaptureContent.Parse(new Dictionary<string,object>{{"formula",false},{"lines",new[]{CaptureLine("Retained ordinary text")}}});Assert(!fallback.FormulaAvailable&&fallback.Parts[0].Value=="Retained ordinary text","packages without equation support retain ordinary OCR");
                bool rejected=false;try{CaptureContent.Parse(new Dictionary<string,object>{{"lines","invalid"}});}catch(InvalidOperationException){rejected=true;}Assert(rejected,"malformed OCR responses cannot become editable results");
                Directory.CreateDirectory(empty);Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",Path.Combine(empty,"Mini PDF.exe"));
                using(var doc=PdfDocument.New())using(var options=new OfficeWindow(doc,false)){options.ShowInTaskbar=false;options.StartPosition=FormStartPosition.Manual;options.Location=new Point(-24000,-24000);options.Show();Application.DoEvents();Assert(!options.Controls.Find("WordFormulaMode",true).Single().Visible,"Word hides AI equation choices and defaults to images when no package exists");options.Close();}
                using(var window=new MainWindow(null)){window.ShowInTaskbar=false;window.StartPosition=FormStartPosition.Manual;window.Location=new Point(-24000,-24000);window.Show();Application.DoEvents();Assert(!CaptureItem(window,"captureOcrButton").Available&&!CaptureItem(window,"captureOcrItem").Available,"the toolbar and menu hide capture OCR when no AI package exists");using(var menu=new ContextMenuStrip()){window.QuickTranslationContext(menu,Point.Empty);Assert(!menu.Items.Cast<ToolStripItem>().Any(i=>i.Text==UiText.Get("截取识别（文字 / 公式）")),"the context menu also hides the feature without an AI package");}typeof(MainWindow).GetMethod("CaptureRecognition",CaptureFlags).Invoke(window,null);Assert(CaptureWindow(window)==null,"a hidden capture shortcut does not open an unavailable feature");window.Close();}
                PdfDocument.Check(!String.IsNullOrEmpty(pack)&&File.Exists(pack),"Set MINIPDF_TEST_PORTABLE to the complete MiniAI-Portable.exe.");Directory.CreateDirectory(home);string linked=Path.Combine(home,"MiniAI-Portable.exe");if(!File.Exists(linked)&&!CreateHardLink(linked,pack,IntPtr.Zero))File.Copy(pack,linked);
                File.WriteAllText(Path.Combine(home,"MiniPDF.MiniAI.txt"),"");MiniAiClient.InvalidatePackage();Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",Path.Combine(home,"Mini PDF.exe"));
                Assert(MiniAiClient.Available&&MiniAiClient.PackRoot==Path.Combine(home,"MiniAI"),"an unextracted AI EXE beside the reader is detected without loading a model");
                string service=Environment.GetEnvironmentVariable("MINIPDF_TEST_SERVICE_ROOT");if(!String.IsNullOrEmpty(service))MiniAiClient.SelectPackage(Path.Combine(service,"MiniAI.exe"));
                using(var window=new MainWindow(null)){
                    window.ShowInTaskbar=false;window.StartPosition=FormStartPosition.Manual;window.Location=new Point(30,30);window.ClientSize=new Size(1040,Math.Min(680,Screen.PrimaryScreen.WorkingArea.Height-100));
                    var document=PdfDocument.New();document.AddText("The control system remains stable.",45,742,20,"Arial",Color.Black);document.AddText("x² + y² = z²",45,620,30,"Arial",Color.Black);document.Save(Path.Combine(directory,"capture-fixture.pdf"));window.LoadForTest(document);byte[] original=document.GetRenderSource().ReadAll();bool dirty=document.Dirty;window.Show();window.CanvasForTest.Fit=2;window.CanvasForTest.Rebuild(false);AwaitRaster(window);
                    Assert(CaptureItem(window,"captureOcrButton").Available&&CaptureItem(window,"captureOcrItem").Available,"placing the complete AI EXE beside the reader lights up capture OCR automatically");using(var menu=new ContextMenuStrip()){window.QuickTranslationContext(menu,Point.Empty);Assert(menu.Items.Cast<ToolStripItem>().Any(i=>i.Text==UiText.Get("截取识别（文字 / 公式）")),"the available capture action appears in the context menu");}
                    var probeWatch=System.Diagnostics.Stopwatch.StartNew();for(int n=0;n<1000;n++)if(!MiniAiClient.Available)throw new Exception("Missing cached package");probeWatch.Stop();Assert(probeWatch.ElapsedMilliseconds<200,"1000 warm package probes complete without repeated filesystem reads: "+probeWatch.ElapsedMilliseconds+"ms");
                    CapturePdfRegion(window,new N.Rect(30,726,560,770),directory,"text");var result=CaptureWindow(window);PumpOffline(delegate{return result!=null&&!result.BusyForTest;});Assert(result.TextForTest.Contains("control system")&&!result.TextForTest.Contains("$$"),"real PDF screen pixels are recognized as ordinary text: "+result.TextForTest+" / "+result.Controls.Find("CaptureStatus",true).Single().Text);
                    var editor=(TextBox)result.Controls.Find("CaptureResult",true).Single();editor.Select(editor.Text.IndexOf("control",StringComparison.Ordinal),7);result.CopyResult();Assert(Clipboard.GetText()=="control","copying a selection copies exactly the selected OCR text");editor.Text="Manually corrected text";result.ModeForTest(CaptureMode.Formula);result.ModeForTest(CaptureMode.Auto);PumpOffline(delegate{return !result.BusyForTest;});Thread.Sleep(200);Application.DoEvents();Assert(result.TextForTest=="Manually corrected text","mode switches preserve corrections and reject stale recognition replies");
                    using(var timer=new System.Windows.Forms.Timer{Interval=150}){timer.Tick+=delegate{var overlay=Application.OpenForms.OfType<CaptureOverlay>().FirstOrDefault();if(overlay==null)return;timer.Stop();overlay.DialogResult=DialogResult.Cancel;};timer.Start();CaptureItem(window,"captureOcrButton").PerformClick();}Assert(result.Visible&&result.TextForTest=="Manually corrected text","canceling a new capture restores the previous result and its edits");
                    CapturePdfRegion(window,new N.Rect(30,602,430,666),directory,"formula");PumpOffline(delegate{return !result.BusyForTest;});var list=(ComboBox)result.Controls.Find("CaptureParts",true).Single();Assert(list.Items.Count>=2&&list.SelectedIndex==1&&result.TextForTest.Contains("^{2}"),"a cropped PDF formula is automatically recognized as editable TeX: "+result.TextForTest+" / "+result.Controls.Find("CaptureStatus",true).Single().Text);result.CopyResult();Assert(Clipboard.GetText()==result.TextForTest,"the default formula copy format is LaTeX");
                    var recognizedFormat=(ComboBox)result.Controls.Find("CaptureFormat",true).Single();recognizedFormat.SelectedIndex=1;XNamespace generated="http://www.w3.org/1998/Math/MathML";string recognized=result.TextForTest;
                    try{string recognizedXml=CaptureMathClipboard(result);Assert(XElement.Parse(recognizedXml).Descendants(generated+"msup").Count()==3,"supported actual formula output copies with all three exponent groups in MathML");}
                    catch(NotSupportedException){Assert(result.TextForTest==recognized,"unsupported OCR commands remain editable TeX rather than silently exporting a different formula");}
                    Assert(XElement.Parse(FormulaMarkup.Convert(@"\scriptstyle x^{2}+y^{2}=z^{2}")).Descendants(generated+"msup").Count()==3,"scriptstyle and every exponent remain available in MathType conversion after manual review");recognizedFormat.SelectedIndex=0;
                    editor.Text=@"\mathcal{L}V_{i,1}=x^2";list.SelectedIndex=0;list.SelectedIndex=1;Assert(editor.Text==@"\mathcal{L}V_{i,1}=x^2","switching between the overview and formula retains manual corrections");var format=(ComboBox)result.Controls.Find("CaptureFormat",true).Single();format.SelectedIndex=1;XNamespace math="http://www.w3.org/1998/Math/MathML";var markup=XElement.Parse(CaptureMathClipboard(result));Assert(markup.Descendants(math+"msub").Any()&&markup.Descendants(math+"mi").Any(x=>x.Value=="V"),"MathType copy exports editable MathML from the corrected code");
                    result.ClientSize=new Size(640,480);Application.DoEvents();var copy=result.Controls.Find("CopyCapture",true).Single();Assert(copy.Visible&&copy.Parent.ClientRectangle.Contains(copy.Bounds)&&editor.Height>80,"copy controls and editable output fit in a compact result window");using(var shot=new Bitmap(result.Width,result.Height)){result.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(directory,"capture-workspace.png"));}
                    CapturePdfRegion(window,new N.Rect(30,590,560,770),directory,"mixed");PumpOffline(delegate{return !result.BusyForTest;});Assert(list.SelectedIndex==0&&result.TextForTest.Contains("control system")&&result.TextForTest.Contains("$$"),"a mixed PDF capture retains both its paragraph and formula");
                    result.ModeForTest(CaptureMode.Text);PumpOffline(delegate{return !result.BusyForTest;});Assert(!result.Controls.Find("CaptureFormat",true).Single().Visible&&!result.TextForTest.Contains("$$"),"text-only override uses normal OCR without equation code");
                    Assert(document.GetRenderSource().ReadAll().SequenceEqual(original)&&document.Dirty==dirty,"capture recognition never changes the PDF or adds an OCR layer");
                    result.ModeForTest(CaptureMode.Formula);result.Close();Application.DoEvents();Assert(result.IsDisposed,"closing during recognition cancels the result window safely");
                    CaptureWordExport(directory,MiniAiClient.PackRoot);
                    window.Close();Application.DoEvents();
                }
                File.AppendAllText(logPath,"\r\nAssertions: "+assertions);return 0;
            }catch(Exception error){File.AppendAllText(logPath,"\r\n"+error);return 1;}finally{Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",prior);}
        }
        static string CaptureMathClipboard(CaptureRecognitionWindow window){string value="";for(int n=0;n<5;n++){window.CopyResult();Application.DoEvents();Thread.Sleep(20);value=Clipboard.GetText();if(value.TrimStart().StartsWith("<math",StringComparison.Ordinal))break;}return value;}
        static void CaptureWordExport(string directory,string root){
            string fixture=Environment.GetEnvironmentVariable("MINIPDF_TEST_FORMULA_IMAGE");PdfDocument.Check(File.Exists(fixture),"Set MINIPDF_TEST_FORMULA_IMAGE to a rendered mathematical fixture.");
            MiniAiClient.Ensure(root);
            using(var document=PdfDocument.New()){
                document.AddImage(fixture,new N.Rect(45,480,550,700));document.Save(Path.Combine(directory,"word-ai-equation.pdf"));byte[] before=document.GetRenderSource().ReadAll();
                using(var options=new OfficeWindow(document,false)){
                    options.ShowInTaskbar=false;options.StartPosition=FormStartPosition.Manual;options.Location=new Point(-24000,-24000);options.Show();Application.DoEvents();
                    var select=options.Controls.Find("WordFormulaMode",true).OfType<ComboBox>().Single();Assert(select.Visible&&select.Items.Count==3&&select.SelectedIndex==0,"Word exposes image, editable-equation and TeX-text modes when the AI package is present");select.SelectedIndex=1;Application.DoEvents();Assert(select.SelectedIndex==1,"Word formula mode remains selectable in the preserve-layout mode");select.SelectedIndex=2;var layout=options.Controls.Find("WordExportMode",true).OfType<ComboBox>().Single();Assert(layout.SelectedIndex==1&&!layout.Enabled,"TeX source mode selects continuous paragraphs instead of squeezing code into positioned boxes");options.Close();
                }
                foreach(int mode in new[]{0,1,2,3}){
                    bool editable=mode>0,flow=mode>=2,tex=mode==3;string output=Path.Combine(directory,"word-ai-"+mode+".docx");
                    using(var progress=new OfficeProgress(document,new OfficeExportOptions{Pages=new[]{0},Ocr=true,AiEquations=editable,Equations=editable,TexEquations=tex,Layout=!flow,Continuous=flow,MiniAiRoot=root},output)){
                        progress.ShowInTaskbar=false;progress.StartPosition=FormStartPosition.Manual;progress.Location=new Point(-24000,-24000);progress.Show();PumpOffline(delegate{return progress.DialogResult!=DialogResult.None||progress.IsDisposed;},120000);Assert(progress.DialogResult==DialogResult.OK&&File.Exists(output),"the isolated Word export worker completes equation mode "+mode);progress.Close();
                    }
                    using(var zip=System.IO.Compression.ZipFile.OpenRead(output))using(var reader=new StreamReader(zip.GetEntry("word/document.xml").Open())){
                        var xml=XElement.Parse(reader.ReadToEnd());XNamespace math="http://schemas.openxmlformats.org/officeDocument/2006/math";int count=xml.Descendants(math+"oMath").Count();
                        Assert(tex?count==0&&xml.Value.Contains(@"\frac"):editable?count>0:count==0,"real scanned PDF export retains the selected equation representation: "+mode);
                        if(editable)Assert(tex?System.Text.RegularExpressions.Regex.Matches(xml.Value,@"\\frac").Count==2:xml.Descendants(math+"f").Count()==2,"both fractions of the scanned expression are retained together, rather than converting a numerator fragment: "+mode);
                    }
                }
                Assert(document.GetRenderSource().ReadAll().SequenceEqual(before)&&!document.Dirty,"AI formula export leaves the scanned source PDF unchanged");
            }
            using(var native=PdfDocument.Open(Path.Combine(directory,"word-research-source.pdf"),"")){
                byte[] before=native.GetRenderSource().ReadAll();
                foreach(bool flow in new[]{false,true}){
                    string output=Path.Combine(directory,"word-native-ai-"+flow+".docx");OfficeExport.Save(native,new OfficeExportOptions{Pages=new[]{0},AiEquations=true,Equations=flow,Layout=!flow,Continuous=flow,MiniAiRoot=root},output,null);
                    using(var zip=System.IO.Compression.ZipFile.OpenRead(output))using(var reader=new StreamReader(zip.GetEntry("word/document.xml").Open())){XNamespace math="http://schemas.openxmlformats.org/officeDocument/2006/math";var xml=XElement.Parse(reader.ReadToEnd());Assert(xml.Descendants(math+"oMath").Any(),"native scientific PDF equations become editable in "+(flow?"continuous":"positioned")+" Word output");}
                }
                Assert(native.GetRenderSource().ReadAll().SequenceEqual(before),"native formula conversion leaves the open PDF snapshot unchanged");
            }
            using(var inline=PdfDocument.Open(Path.Combine(directory,"word-inline-source.pdf"),"")){
                string output=Path.Combine(directory,"word-inline-tex.docx");OfficeExport.Save(inline,new OfficeExportOptions{Pages=new[]{0},AiEquations=true,TexEquations=true,MiniAiRoot=root},output,null);
                using(var zip=System.IO.Compression.ZipFile.OpenRead(output))using(var reader=new StreamReader(zip.GetEntry("word/document.xml").Open())){var xml=XElement.Parse(reader.ReadToEnd());XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main",m="http://schemas.openxmlformats.org/officeDocument/2006/math";Assert(!xml.Descendants(m+"oMath").Any()&&xml.Descendants(w+"p").Any(p=>p.Value.Contains("The state")&&p.Value.Contains(@"\alpha")&&p.Value.Contains("remains bounded")),"a real inline PDF equation exports as TeX between its surrounding words in one flowing paragraph");}
            }
        }
        static void CapturePdfRegion(MainWindow window,N.Rect box,string directory,string name){
            if(name!="text"){
                // The first case exercises the actual desktop overlay. Later
                // model cases use PDF pixels so active desktop apps cannot
                // overwrite a mathematical test crop while inference runs.
                var size=window.Document.PageSize(0);int w=(int)Math.Ceiling(size.Width*2),h=(int)Math.Ceiling(size.Height*2);Point p=window.Document.ToDevice(box.left,box.top,w,h,0),q=window.Document.ToDevice(box.right,box.bottom,w,h,0);var area=Rectangle.FromLTRB(Math.Min(p.X,q.X),Math.Min(p.Y,q.Y),Math.Max(p.X,q.X),Math.Max(p.Y,q.Y));
                using(var crop=window.Document.RenderRegion(0,w,h,0,area)){crop.Save(Path.Combine(directory,"capture-"+name+".png"));CaptureWindow(window).ReplaceImage(crop,MiniAiClient.PackRoot);}
                Assert(CaptureWindow(window)!=null,"rendered PDF pixels enter the same recognition window: "+name);return;
            }
            var canvas=window.CanvasForTest;Point a=PagePoint(canvas,window.Document,box.left,box.top),b=PagePoint(canvas,window.Document,box.right,box.bottom);Rectangle target=canvas.RectangleToScreen(Rectangle.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)));bool used=false,topmost=window.TopMost;
            window.TopMost=true;window.Activate();window.Refresh();Application.DoEvents();
            try {
            using(var timer=new System.Windows.Forms.Timer{Interval=200}){timer.Tick+=delegate{var overlay=Application.OpenForms.OfType<CaptureOverlay>().FirstOrDefault();if(overlay==null)return;timer.Stop();used=true;Point from=overlay.PointToClient(target.Location),to=overlay.PointToClient(new Point(target.Right,target.Bottom));var screen=(Bitmap)typeof(CaptureOverlay).GetField("screen",CaptureFlags).GetValue(overlay);using(var crop=CaptureOverlay.Crop(screen,overlay.RectangleToClient(target)))crop.Save(Path.Combine(directory,"capture-"+name+".png"));typeof(CaptureOverlay).GetMethod("OnMouseDown",CaptureFlags).Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,from.X,from.Y,0)});typeof(CaptureOverlay).GetMethod("OnMouseUp",CaptureFlags).Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,to.X,to.Y,0)});};timer.Start();CaptureItem(window,"captureOcrButton").PerformClick();}
            }finally{window.TopMost=topmost;}
            Assert(used&&CaptureWindow(window)!=null,"the native capture overlay returns the selected PDF region: "+name);
        }
    }
}
