using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MiniPdf {
    internal static partial class SelfTest {
        static byte[] RefreshFixture(string directory,string label,int count=3){
            string path=Path.Combine(directory,"refresh-fixture.pdf");
            using(var doc=PdfDocument.New()){
                for(int i=0;i<count;i++){if(i>0)doc.InsertBlank();doc.AddText(label+" PAGE "+(i+1),50,720,18,"Helvetica",Color.Black);}
                doc.Save(path);
            }
            return File.ReadAllBytes(path);
        }
        static void RefreshPump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();System.Threading.Thread.Sleep(10);}}
        static void WaitRefresh(Func<bool> ready,string message,int timeout=9000){
            var clock=Stopwatch.StartNew();while(!ready()&&clock.ElapsedMilliseconds<timeout)RefreshPump(25);
            Assert(ready(),message);
        }
        static void RefreshCoverage(string directory){
            UiText.English=false;string root=Path.Combine(directory,"file-refresh");Directory.CreateDirectory(root);
            byte[] first=RefreshFixture(root,"WATCH_ALPHA"),second=RefreshFixture(root,"WATCH_BRAVO"),shorter=RefreshFixture(root,"ONE_PAGE",1);
            string path=Path.Combine(root,"自动刷新 with spaces.pdf"),other=Path.Combine(root,"后台文档.pdf");
            File.WriteAllBytes(path,first);File.WriteAllBytes(other,first);
            Exception uiFailure=null;System.Threading.ThreadExceptionEventHandler handler=delegate(object s,System.Threading.ThreadExceptionEventArgs e){uiFailure=e.Exception;};Application.ThreadException+=handler;
            try{using(var form=new MainWindow(null)){
                form.Show();form.OpenDocument(path);Application.DoEvents();var old=form.Document;var view=form.CanvasForTest;
                Assert(!form.RefreshNoticeForTest,"refresh: normal reader has no extra visible notice");
                form.Navigate(1);form.ApplyZoom("135%");form.SetTool(Tool.Hand);view.AutoScrollPosition=new Point(20,view.LayoutRectangles[1].Y+130);Application.DoEvents();var scroll=view.AutoScrollPosition;
                File.WriteAllBytes(path,second);
                WaitRefresh(delegate{return form.Document!=old;},"refresh: in-place external write automatically replaces the open snapshot");
                Assert(form.Document.Index==1&&form.Document.SelectedText(0,300).Contains("WATCH_BRAVO"),"refresh: current page and new text are visible");
                Assert(view.Fit==0&&Math.Abs(view.Zoom-1.35f)<.0001f&&view.Mode==Tool.Hand&&view.Continuous,"refresh: zoom, interaction and layout survive automatic reload");
                Assert(Math.Abs(view.AutoScrollPosition.X-scroll.X)<=2&&Math.Abs(view.AutoScrollPosition.Y-scroll.Y)<=2,"refresh: viewport position stays within two pixels for unchanged page geometry");
                Assert(!form.Document.Dirty&&!form.RefreshNoticeForTest,"refresh: external changes do not mark the reader dirty");
                old=form.Document;string temp=path+".tmp";File.WriteAllBytes(temp,first);File.Replace(temp,path,null);
                WaitRefresh(delegate{return form.Document!=old;},"refresh: atomic file replacement is detected");
                Assert(form.Document.AllText().Contains("WATCH_ALPHA"),"refresh: replacement displays the new source bytes");
                old=form.Document;
                using(var writer=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.ReadWrite)){
                    writer.Write(second,0,second.Length/2);writer.Flush();RefreshPump(1200);
                    Assert(form.Document==old&&form.Document.Count==3,"refresh: partial write and an active writer keep the previous readable document");
                    writer.Write(second,second.Length/2,second.Length-second.Length/2);writer.Flush();
                }
                WaitRefresh(delegate{return form.Document!=old;},"refresh: finishes deferred reload after the writer closes");
                old=form.Document;File.WriteAllText(path,"temporary invalid PDF");RefreshPump(1300);
                Assert(form.Document==old&&form.Document.Count==3&&form.RefreshNoticeForTest,"refresh: failed parsing keeps the old document without a modal error");
                File.WriteAllBytes(path,first);WaitRefresh(delegate{return form.Document!=old;},"refresh: retry recovers when a corrupt intermediate file becomes valid");
                old=form.Document;File.Delete(path);RefreshPump(650);
                Assert(form.Document==old&&form.Document.Count==3,"refresh: deleting a source does not close its readable snapshot");
                File.WriteAllBytes(path,second);WaitRefresh(delegate{return form.Document!=old;},"refresh: deleting then recreating the same path recovers automatically");
                old=form.Document;form.Navigate(2);File.WriteAllBytes(path,shorter);
                WaitRefresh(delegate{return form.Document!=old;},"refresh: page-count reduction is detected");
                Assert(form.Document.Count==1&&form.Document.Index==0&&view.LayoutRectangles.Length==1,"refresh: removed current page clamps to the final valid page and clears old layout");
                File.WriteAllBytes(path,first);WaitRefresh(delegate{return form.Document.Count==3;},"refresh: page-count growth updates the document");
                form.Run(delegate{form.Document.AddText("KEEP_LOCAL_EDIT",50,660,14,"Helvetica",Color.Blue);});old=form.Document;
                File.WriteAllBytes(path,second);RefreshPump(1000);
                Assert(form.Document==old&&old.Dirty&&old.AllText().Contains("KEEP_LOCAL_EDIT")&&form.RefreshNoticeForTest,"refresh: external update preserves unsaved edits and shows a reload notice");
                using(var answer=AnswerSavePrompt(2,"重新加载文档")){Command(form,Keys.F5);}
                Assert(form.Document==old&&old.Dirty,"refresh: canceling F5 preserves the exact document and undo history");
                using(var answer=AnswerSavePrompt(2,"原文件已改变")){Command(form,Keys.Control|Keys.S);}
                Assert(form.Document==old&&old.Dirty&&Convert.ToBase64String(File.ReadAllBytes(path))==Convert.ToBase64String(second),"refresh: canceling overwrite keeps the external version and local draft");
                form.Run(delegate{old.Undo();});WaitRefresh(delegate{return form.Document!=old;},"refresh: undoing local edits allows deferred external refresh");
                form.Run(delegate{form.Document.AddText("DISCARD_ONLY_ON_CONFIRM",50,640,12,"Helvetica",Color.Black);});old=form.Document;
                using(var answer=AnswerSavePrompt(1,"重新加载文档")){Command(form,Keys.F5);}
                Assert(form.Document!=old&&!form.Document.Dirty&&!form.Document.AllText().Contains("DISCARD_ONLY_ON_CONFIRM"),"refresh: confirmed F5 discards edits and reloads the disk version");
                old=form.Document;File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddSeconds(2));RefreshPump(1400);
                Assert(form.Document==old,"refresh: touching metadata does not recreate document or clear view state");
                form.Run(delegate{form.Document.AddText("SAVE_OUR_CHANGES",50,620,12,"Helvetica",Color.Black);});
                Command(form,Keys.Control|Keys.S);old=form.Document;RefreshPump(1500);
                Assert(form.Document==old&&!old.Dirty&&old.CanUndo&&old.AllText().Contains("SAVE_OUR_CHANGES"),"refresh: own save does not trigger a reload or lose undo history");
                File.WriteAllBytes(path,first);WaitRefresh(delegate{return form.Document!=old;},"refresh: the same watcher still sees changes after saving");
                old=form.Document;Assert(first.Length==second.Length,"refresh: same-size update fixture has equal byte length");var stamp=File.GetLastWriteTimeUtc(path);
                File.WriteAllBytes(path,second);File.SetLastWriteTimeUtc(path,stamp);
                WaitRefresh(delegate{return form.Document!=old;},"refresh: watcher detects replacement content with unchanged size and write timestamp");
                old=form.Document;form.StopFileWatcherForTest();File.WriteAllBytes(path,shorter);
                WaitRefresh(delegate{return form.Document!=old;},"refresh: periodic metadata check catches a change when notifications are disabled");
                form.OpenDocument(other);var background=form.Document;form.SelectTabForTest(0);old=form.Document;File.WriteAllBytes(other,second);
                WaitRefresh(delegate{return form.TabDocumentForTest(1)!=background;},"refresh: inactive tabs also refresh");
                Assert(form.Document==old&&form.TabDocumentForTest(1).AllText().Contains("WATCH_BRAVO"),"refresh: background refresh neither steals focus nor replaces the active tab");
                form.SelectTabForTest(1);old=form.Document;
                using(var dialog=new Form{Text="Modal document tool"}){
                    dialog.Show(form);File.WriteAllBytes(other,first);RefreshPump(900);
                    Assert(form.Document==old,"refresh: an owned document tool pins its document until closed");dialog.Close();
                }
                WaitRefresh(delegate{return form.Document!=old;},"refresh: refresh resumes after the owned tool closes");
                form.Navigate(1);var selection=form.Document.SelectTextEdit(new System.Collections.Generic.List<TextRange>{new TextRange{Page=1,Start=0,Count=10}});
                view.BeginInlineText(selection);view.InlineDraft="UNCONFIRMED_DRAFT";old=form.Document;File.WriteAllBytes(other,second);RefreshPump(850);
                Assert(form.Document==old&&view.InlineDraft=="UNCONFIRMED_DRAFT","refresh: unconfirmed inline editor is not lost on external changes");view.EndInlineText();
                WaitRefresh(delegate{return form.Document!=old;},"refresh: refresh resumes after inline editing is canceled");
                form.SearchForTest("WATCH_BRAVO");WaitSearch(form);
                Assert(form.SearchResultsForTest.Items.Count==3,"refresh: search finds the three pages before external modification");old=form.Document;File.WriteAllBytes(other,first);
                WaitRefresh(delegate{return form.Document!=old;},"refresh: reloading invalidates the active search snapshot");WaitSearch(form);
                Assert(form.SearchResultsForTest.Items.Count==0,"refresh: active search is rerun and contains no stale results from the old file");form.CloseFindForTest();
                string retarget=Path.Combine(root,"另存为 after-save.pdf");form.Document.Save(retarget);old=form.Document;RefreshPump(250);File.WriteAllBytes(other,second);RefreshPump(1700);
                Assert(form.Document==old&&old.FilePath==retarget,"refresh: Save As retargets monitoring and stops watching the previous path");File.WriteAllBytes(retarget,second);
                WaitRefresh(delegate{return form.Document!=old;},"refresh: modifications to the Save As destination refresh the same tab");
                form.SetLanguageForTest(true);Assert(UiText.Get("重新加载文档")=="Reload document","refresh: reload controls have English translations");form.SetLanguageForTest(false);
                form.CloseTabForTest(1);File.WriteAllBytes(other,first);RefreshPump(500);
                Assert(form.TabCountForTest==1&&form.Document.Count==1,"refresh: closing a tab removes its watcher without affecting other documents");
                form.Close();Application.DoEvents();File.WriteAllBytes(path,first);RefreshPump(250);
                Assert(form.IsDisposed&&uiFailure==null,"refresh: closing the reader releases watches and safely ignores late notifications");
            }
            using(var form=new MainWindow(null)){
                form.Show();form.LoadForTest(PdfDocument.New());Assert(form.Document.Dirty,"refresh: an untitled document remains editable without a source watcher");
                string saved=Path.Combine(root,"新建首次保存.pdf");form.Document.Save(saved);var old=form.Document;RefreshPump(300);File.WriteAllBytes(saved,first);
                WaitRefresh(delegate{return form.Document!=old;},"refresh: saving an untitled document starts automatic file monitoring");form.Close();Application.DoEvents();
            }}finally{Application.ThreadException-=handler;}
        }
        internal static int RunRefresh(string directory){
            Directory.CreateDirectory(directory);logPath=Path.Combine(directory,"refresh-tests.txt");
            try{RefreshCoverage(directory);log.AppendLine("TOTAL: "+assertions+" refresh assertions, 0 failures");File.WriteAllText(logPath,log.ToString());return 0;}
            catch(Exception error){log.AppendLine(error.ToString());File.WriteAllText(logPath,log.ToString());return 1;}
        }
    }
}
