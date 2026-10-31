using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MiniPdf {
    internal static partial class SelfTest {
        static void SearchCoverage(string directory) {
            Assert(SearchText.Match("Alpha alpha ALPHA","alpha",false,false).Count==3,"search defaults to case-insensitive matches");
            Assert(SearchText.Match("Alpha alpha ALPHA","alpha",true,false).Count==1,"match-case search preserves distinctions");
            Assert(SearchText.Match("control controller control","control",false,true).Count==2,"whole-word search excludes substrings");
            Assert(SearchText.Match("closed-loop con-\r\ntrol","control",false,false).Count==1,"search joins line-end hyphenation");
            Assert(SearchText.Match("closed-loop","closedloop",false,false).Count==0,"ordinary hyphens are preserved");
            Assert(SearchText.Match("machine\r\nlearning","machine learning",false,false).Count==1,"search matches phrases across line breaks");
            Assert(SearchText.Match("机 器\r\n学 习","机器学习",false,false).Count==1,"search ignores artificial CJK spacing");
            Assert(SearchText.Match("ｆｉｎａｌ ﬃ","final ffi",false,false).Count==1,"search normalizes full-width characters and ligatures");
            Assert(SearchText.Match("affinity","ffi",false,false)[0].Count==3,"ordinary search spans map back to source characters");
            Assert(SearchText.Match("aﬃnity","ffi",false,false)[0].Count==1,"ligature matches retain the original source range");
            Assert(SearchText.Match("x\u00ady","xy",false,false)[0].Count==3,"search includes invisible soft-hyphen positions in highlight ranges");
            Assert(SearchText.Match("aa","a",false,false).Count==2,"search reports every repeated match");
            Assert(SearchText.Match("text"," \r\n ",false,false).Count==0,"whitespace-only queries produce no matches");
            using(var document=PdfDocument.New()) {
                document.AddText("Alpha control alpha",50,750,16,"Helvetica",Color.Black);document.InsertBlank();document.AddText("CONTROL alpha",50,730,16,"Helvetica",Color.Black);
                var hits=SearchText.Page(document,0,"alpha",false,false);Assert(hits.Count==2&&hits.All(h=>h.Rectangles.Count>0&&h.Count==5),"native search returns all hits with page rectangles");
                Assert(SearchText.Page(document,1,"control",false,false).Count==1,"search covers another page");
                document.Save(Path.Combine(directory,"search-fixture.pdf"));
            }
            using(var form=new MainWindow(null)) {
                form.Show();form.LoadForTest(PdfDocument.Open(Path.Combine(directory,"search-fixture.pdf"),""));form.SearchForTest("alpha");WaitSearch(form);
                Assert(form.SearchResultsForTest.Items.Count==3,"background search lists all occurrences across both pages");
                Assert(form.SearchResultsForTest.Items[2].Text=="2"&&form.SearchResultsForTest.Items[2].SubItems[1].Text.Contains("alpha"),"results include page numbers and readable context");
                form.SearchStepForTest(1);form.SearchStepForTest(1);Assert(form.Document.Index==1,"next result navigates to its page");form.SearchStepForTest(1);Assert(form.SearchIndexForTest==0,"next result wraps to the first match");form.SearchStepForTest(-1);Assert(form.SearchIndexForTest==2&&form.Document.Index==1,"previous result wraps backwards");
                form.SearchForTest("missing");WaitSearch(form);Assert(form.SearchResultsForTest.Items.Count==0&&!String.IsNullOrEmpty(form.SearchStatusForTest),"empty result sets clear stale results and report completion");
                form.SearchForTest("alpha");form.SearchForTest("control");WaitSearch(form);Assert(form.SearchResultsForTest.Items.Count==2,"superseded searches cannot append stale results");
                form.SearchResultsForTest.Items[0].Selected=true;Application.DoEvents();Assert(form.Document.Index==0,"selecting a result navigates directly");
                form.Run(delegate{form.Document.AddText("control",50,650,16,"Helvetica",Color.Black);});WaitSearch(form);Assert(form.SearchResultsForTest.Items.Count==3,"editing invalidates search results and reruns the active query");
                form.SearchForTest("alpha");form.CloseFindForTest();Application.DoEvents();Assert(form.SearchResultsForTest.Items.Count==0,"closing find cancels the background search");form.Document.Save(Path.Combine(directory,"search-edited.pdf"));
            }
        }
        static void WaitSearch(MainWindow form){var watch=System.Diagnostics.Stopwatch.StartNew();while(!form.SearchCompleteForTest&&watch.ElapsedMilliseconds<20000){Application.DoEvents();System.Threading.Thread.Sleep(10);}Application.DoEvents();Assert(form.SearchCompleteForTest,"background search completes without blocking the UI");}
        internal static int RunSearch(string directory){Directory.CreateDirectory(directory);logPath=Path.Combine(directory,"search-tests.txt");try{SearchCoverage(directory);File.AppendAllText(logPath,"\r\nAssertions: "+assertions);return 0;}catch(Exception error){File.AppendAllText(logPath,"\r\n"+error);return 1;}}
    }
}
