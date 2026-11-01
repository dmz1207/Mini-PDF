using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MiniPdf {
    // The caller supplies a local paper. Neither the paper nor extracted text
    // belongs in the repository or public release artifacts.
    internal static class TranslationPaperTests {
        static int count;static string log;
        static void Check(bool value,string name){if(!value)throw new Exception(name);count++;File.AppendAllText(log,"PASS: "+name+Environment.NewLine);}
        static void Pump(Func<bool> done){var watch=System.Diagnostics.Stopwatch.StartNew();while(!done()&&watch.ElapsedMilliseconds<180000){Application.DoEvents();Thread.Sleep(10);}Application.DoEvents();if(!done())throw new Exception("Paper translation timed out");}
        static int Find(PdfTextPage page,string text){for(int i=0;i<page.Count;i++)if(N.FPDFText_GetUnicode(page.Text,i)==text[0]&&page.ReadForTranslation(i,Math.Min(page.Count-i,text.Length+12)).StartsWith(text,StringComparison.Ordinal))return i;throw new Exception("Paper passage not found: "+text);}
        [STAThread]static int Main(string[] args){Directory.CreateDirectory(args[2]);log=Path.Combine(args[2],"paper-translation-checks.txt");File.WriteAllText(log,"");try{
            string home=Path.Combine(args[2],"reader");Directory.CreateDirectory(home);Environment.SetEnvironmentVariable("MINIPDF_LAUNCHER",Path.Combine(home,"Mini PDF.exe"));File.WriteAllText(Path.Combine(home,"MiniPDF.MiniAI.txt"),Path.Combine(args[1],"MiniAI.exe"));Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);N.FPDF_InitLibrary();
            using(var window=new MainWindow(null)){window.StartPosition=FormStartPosition.Manual;window.Location=new Point(-24000,-24000);window.ClientSize=new Size(1100,850);window.ShowInTaskbar=false;window.Show();var document=PdfDocument.Open(args[0],"");window.LoadForTest(document);Application.DoEvents();var canvas=window.CanvasForTest;var flags=BindingFlags.Instance|BindingFlags.NonPublic;typeof(MainWindow).GetField("autoTranslate",flags).SetValue(window,false);
                for(int n=0;n<3;n++){
                    int pageIndex=n==2?1:0;window.Navigate(pageIndex);canvas.Rebuild(true);Application.DoEvents();var page=document.SelectionPage(pageIndex);string start=n==0?"This paper deals":n==1?"Practically, all":"In this paper, we will use";string stop=n==0?"Index Terms":n==1?"Among all robust": "In this paper, we will take";int first=Find(page,start),end=Find(page,stop);canvas.ClearSelection();canvas.TextStart=first;canvas.TextCount=end-first;string selected=canvas.TranslationSelection();Check(selected.Length>100&&selected.StartsWith(start),"actual paper selection retains the entire selected passage "+(n+1));
                    File.WriteAllText(Path.Combine(args[2],"selected-"+(n+1)+".private.txt"),selected);if(n==0)Check(selected.Contains("three-axis")&&selected.Contains("combined")&&selected.Contains("proposed method"),"academic line wrapping preserves compounds, joined words and the abstract ending");
                    using(var menu=new ContextMenuStrip()){window.QuickTranslationContext(menu,new Point(20,20));Check(menu.Items.Count>=3&&menu.Items[0].Enabled,"manual translation is available with automatic selection translation disabled");menu.Items[0].PerformClick();}
                    var bubble=window.TranslationBubbleForTest;Check(bubble!=null&&bubble.SourceForTest==selected,"reader sends the full paper selection to the bubble "+(n+1));bubble.Location=new Point(-24000,-24000);Pump(delegate{return !bubble.BusyForTest;});Check(bubble.ResultForTest.Length>20&&bubble.ResultForTest.Any(c=>c>=0x4e00&&c<=0x9fff),"real Hy inference returns a readable complete paper translation "+(n+1));Check(bubble.IsPinned&&bubble.Opacity==1,"manual paper translation stays pinned and fully readable");
                    var source=(TextBox)bubble.Controls.Find("RecognizedSelection",true)[0];IntPtr handle=source.Handle;var toggle=(Button)bubble.Controls.Find("ToggleTranslationSource",true)[0];for(int k=0;k<20;k++){toggle.PerformClick();Application.DoEvents();}Check(source.Handle==handle&&!bubble.IsDisposed,"repeated source toggling preserves the native text window handle");bubble.ClientSize=new Size(650,400);Application.DoEvents();var close=bubble.Controls.Find("CloseTranslation",true)[0];Check(close.Visible&&close.Parent.ClientRectangle.Contains(close.Bounds),"close control remains available after resizing a long paper translation");window.CloseTranslation();
                }window.Close();
            }File.AppendAllText(log,"Assertions: "+count);return 0;
        }catch(Exception error){File.AppendAllText(log,error.ToString());return 1;}}
    }
}
