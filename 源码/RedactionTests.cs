using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MiniPdf {
    internal static partial class SelfTest {
        static Color ShapePixel(PdfDocument doc,float x,float y){using(var image=doc.Render(doc.Index,596,842)){var point=doc.ToDevice(x,y,image.Width,image.Height);return image.GetPixel(Math.Max(0,Math.Min(image.Width-1,point.X)),Math.Max(0,Math.Min(image.Height-1,point.Y)));}}
        static void ShapeCoverage(string directory){
            string path=Path.Combine(directory,"rectangle-filled.pdf");
            using(var doc=PdfDocument.New()){
                doc.AddText("COSMETIC_PRIVATE_REMAINS",50,700,16,"Helvetica",Color.Black);doc.AddRectangle(new N.Rect(40,670,400,730),Color.Red,2,Color.Black);
                Assert(ShapePixel(doc,60,690).R<5&&ShapePixel(doc,60,690).G<5,"fill: solid black rectangle covers its interior");
                var pick=doc.PickObject(new N.Pt(90,707));Assert(pick!=null&&pick.Type==2,"fill: opaque rectangle is selectable above text instead of selecting text underneath");
                var style=doc.ReadShapeStyle(pick);Assert(style.Fill.HasValue&&style.Fill.Value.ToArgb()==Color.Black.ToArgb()&&style.Stroke.ToArgb()==Color.Red.ToArgb()&&style.Border,"fill: fill and border colors are independent");
                style.Fill=Color.FromArgb(30,160,220);doc.ApplyShapeStyle(pick,style);var blue=ShapePixel(doc,60,690);Assert(blue.B>190&&blue.G>130&&blue.R<60,"fill: an existing rectangle accepts a different interior color");doc.Undo();Assert(ShapePixel(doc,60,690).R<5,"fill: changing fill is undoable");doc.Redo();doc.Save(path);
                Assert(doc.AllText().Contains("COSMETIC_PRIVATE_REMAINS"),"fill: ordinary fills are explicitly cosmetic and keep underlying text");
            }
            using(var doc=PdfDocument.Open(path,"")){
                var pick=doc.PickObject(new N.Pt(90,707));var style=doc.ReadShapeStyle(pick);Assert(style.Fill.HasValue&&style.Fill.Value.B==220,"fill: interior color survives save/reopen");style.Fill=null;doc.ApplyShapeStyle(pick,style);Assert(ShapePixel(doc,60,690).R>245,"fill: selecting no fill restores a hollow outline");
            }
            using(var doc=PdfDocument.Open(path,"")){string permanent=Path.Combine(directory,"rectangle-permanent.pdf");RedactionExport.SaveForTest(doc,new[]{new RedactionRegion{Page=0,Bounds=new N.Rect(10,10,20,20)}},300,true,permanent);using(var copy=PdfDocument.Open(permanent,""))Assert(!copy.AllText().Contains("COSMETIC_PRIVATE_REMAINS"),"redaction: searchable export never restores text hidden by an existing opaque Mini PDF rectangle");}
            using(var form=new MainWindow(null)){
                form.LoadForTest(PdfDocument.New());form.Show();Application.DoEvents();form.BlackRectangleForTest();var doc=form.Document;var view=form.CanvasForTest;var from=PagePoint(view,doc,80,620);var to=PagePoint(view,doc,300,500);Mouse(view,from,to);
                Assert(N.FPDFPage_CountObjects(doc.Page)==1&&doc.ReadShapeStyle(doc.PickIndex(0)).Fill.HasValue,"fill: actual mouse drag creates a filled black rectangle");form.SetTool(Tool.Object);view.Selected=doc.PickIndex(0);form.RectangleFillForTest(Color.White);Assert(doc.ReadShapeStyle(doc.PickIndex(0)).Fill.Value.ToArgb()==Color.White.ToArgb(),"fill: toolbar changes the selected rectangle interior");
                using(var answer=AnswerSavePrompt(7))form.Close();Application.DoEvents();
            }
        }
        static RedactionRegion MarkToken(PdfDocument doc,string token){var found=doc.Find(token,0,0,false);if(found==null)throw new Exception("Redaction fixture token missing: "+token);var rect=found.Rectangles[0];rect.left-=1;rect.right+=1;rect.bottom-=1;rect.top+=1;return new RedactionRegion{Page=found.Page,Bounds=rect};}
        static void RedactionCoverage(string directory){
            UiText.English=false;
            ShapeCoverage(directory);string source=Path.Combine(directory,"redaction-source.pdf");
            if(!File.Exists(source))throw new Exception("Run verify_redaction.py fixtures before the redaction suite.");
            RedactionRegion[] regions;
            using(var doc=PdfDocument.Open(source,"")){
                regions=new[]{MarkToken(doc,"PRIVATE_9Z7Q_4836"),MarkToken(doc,"PRIVATE_INLINE_X8R2"),MarkToken(doc,"机密甲乙丙丁"),MarkToken(doc,"OCR_PRIVATE_5K3J"),new RedactionRegion{Page=0,Bounds=new N.Rect(178,28,392,104)},MarkToken(doc,"NESTED_PRIVATE_46BX"),MarkToken(doc,"CROP_PRIVATE_71SD")};
                var json=regions.Select(delegate(RedactionRegion r){return new{page=r.Page,bounds=new[]{r.Bounds.left,r.Bounds.bottom,r.Bounds.right,r.Bounds.top}};}).ToArray();File.WriteAllText(Path.Combine(directory,"redaction-regions.json"),new JavaScriptSerializer().Serialize(json));
                doc.OpenPage(1);byte[] original=doc.GetRenderSource().ReadAll();int epoch=doc.RenderEpoch;
                RedactionExport.SaveForTest(doc,regions,300,false,Path.Combine(directory,"redaction-image.pdf"));Assert(doc.Index==1&&doc.RenderEpoch==epoch&&original.SequenceEqual(doc.GetRenderSource().ReadAll()),"redaction: exporting image copy does not modify source bytes or active page");
                RedactionExport.SaveForTest(doc,regions,300,true,Path.Combine(directory,"redaction-search.pdf"));Assert(original.SequenceEqual(doc.GetRenderSource().ReadAll()),"redaction: searchable copy also keeps source bytes intact");
                using(var image=PdfDocument.Open(Path.Combine(directory,"redaction-image.pdf"),"")){Assert(image.Count==3&&String.IsNullOrWhiteSpace(image.AllText()),"redaction: image copy has three pages and zero underlying text");for(int p=0;p<image.Count;p++){image.OpenPage(p);Assert(N.FPDFPage_CountObjects(image.Page)==1&&N.FPDFPage_GetAnnotCount(image.Page)==0,"redaction: image page contains only one baked image and no annotations "+p);}}
                using(var copy=PdfDocument.Open(Path.Combine(directory,"redaction-search.pdf"),"")){
                    string text=copy.AllText();Assert(text.Contains("PUBLIC")&&text.Contains("KEEP"),"redaction: rebuilt text remains selectable and searchable outside masks");foreach(string token in new[]{"PRIVATE_9Z7Q_4836","PRIVATE_INLINE_X8R2","机密甲乙丙丁","OCR_PRIVATE_5K3J","NESTED_PRIVATE_46BX","CROP_PRIVATE_71SD"})Assert(!text.Contains(token),"redaction: original text token is permanently absent: "+token);
                    Assert(text.Contains("INLINE_LEFT")&&text.Contains("INLINE_RIGHT")&&text.Contains("PUBLIC ROTATED KEEP"),"redaction: text on both sides of an inline mask and rotated text retain word order");
                    File.WriteAllText(Path.Combine(directory,"redaction-native-extracted.txt"),text);Assert(text.Contains("公开测试"),"redaction: rebuilt text preserves CJK Unicode");var found=copy.Find("PUBLIC",0,0,false);Assert(found!=null&&found.Rectangles.Count>0,"redaction: searching rebuilt text returns usable coordinates");
                    copy.OpenPage(0);for(int i=N.FPDFPage_CountObjects(copy.Page)-1;i>=0;i--){var pick=copy.PickIndex(i);if(pick.Type==3)copy.Remove(pick);}Assert(!copy.AllText().Contains("PRIVATE_9Z7Q_4836"),"redaction: deleting the black page image cannot reveal the original secret");
                }
                using(var progress=new RedactionProgress(doc,regions,300,true,Path.Combine(directory,"redaction-worker.pdf"))){var result=progress.ShowDialog();Assert(result==DialogResult.OK,"redaction: actual isolated worker renders, streams, verifies and atomically saves the copy: "+progress.Error);}
                using(var progress=new RedactionProgress(doc,regions,300,true,source)){Assert(progress.ShowDialog()==DialogResult.Abort&&original.SequenceEqual(File.ReadAllBytes(source)),"redaction: backend independently refuses to overwrite the source file");}
                using(var progress=new RedactionProgress(doc,regions,600,false,Path.Combine(directory,"redaction-canceled.pdf")))using(var timer=new Timer{Interval=40}){timer.Tick+=delegate{timer.Stop();progress.DialogResult=DialogResult.Cancel;};timer.Start();progress.ShowDialog();}System.Threading.Thread.Sleep(300);Application.DoEvents();Assert(!File.Exists(Path.Combine(directory,"redaction-canceled.pdf"))&&!Directory.GetFiles(directory,".minipdf-redacted-*.tmp").Any(),"redaction: canceling worker leaves no output or temporary file");
            }
            using(var form=new MainWindow(null)){
                form.LoadForTest(PdfDocument.Open(source,""));form.Show();Application.DoEvents();var doc=form.Document;var view=form.CanvasForTest;form.RedactionToolForTest();int epoch=doc.RenderEpoch;var from=PagePoint(view,doc,30,240);var to=PagePoint(view,doc,280,200);Mouse(view,from,to);
                Assert(form.RedactionCountForTest==1&&!doc.Dirty&&doc.RenderEpoch==epoch,"redaction: mouse marking only creates a pending overlay, not a misleading PDF black rectangle");
                form.Navigate(1);form.AddRedaction(regions[5].Bounds);Assert(form.RedactionCountForTest==2,"redaction: pending regions can span pages");form.UndoRedactionForTest();Assert(form.RedactionCountForTest==1,"redaction: last pending region can be undone independently");
                using(var answer=AnswerSavePrompt(2,"待应用的涂黑标记")){Assert(!form.CloseTabForTest(0),"redaction: canceling close retains pending unsaved redaction marks");}Assert(form.RedactionCountForTest==1&&form.TabCountForTest==1,"redaction: pending markers survive canceled close");
                form.LoadForTest(PdfDocument.New());form.SelectTabForTest(0);Assert(form.RedactionCountForTest==1,"redaction: pending marks remain attached to their original tab");form.ClearRedactionsForTest();Assert(form.RedactionCountForTest==0,"redaction: clearing markers does not change source data");form.SelectTabForTest(1);using(var answer=AnswerSavePrompt(7))form.CloseTabForTest(1);form.Close();Application.DoEvents();
            }
            using(var a4=PdfDocument.Open(Path.Combine(directory,"redaction-a4-source.pdf"),"")){
                var marks=new[]{MarkToken(a4,"A4_PRIVATE_61QX")};RedactionExport.SaveForTest(a4,marks,600,false,Path.Combine(directory,"redaction-a4-600.pdf"));using(var copy=PdfDocument.Open(Path.Combine(directory,"redaction-a4-600.pdf"),""))Assert(copy.Count==1&&String.IsNullOrWhiteSpace(copy.AllText()),"redaction: tiled 600 dpi A4 export preserves requested precision beyond the viewport bitmap limit");
            }
            string live=Path.Combine(directory,"redaction-live.pdf"),changed=Path.Combine(directory,"redaction-live-updated.pdf");File.Copy(source,live,true);
            using(var modified=PdfDocument.Open(source,"")){modified.AddText("EXTERNAL_UPDATE_AFTER_MARK",30,20,8,"Helvetica",Color.Black);modified.Save(changed);}
            using(var form=new MainWindow(null)){
                form.LoadForTest(PdfDocument.Open(live,""));form.Show();Application.DoEvents();form.Navigate(2);form.RotateView(1);form.RedactionToolForTest();var doc=form.Document;var mark=regions[6].Bounds;Mouse(form.CanvasForTest,PagePoint(form.CanvasForTest,doc,mark.left,mark.top),PagePoint(form.CanvasForTest,doc,mark.right,mark.bottom));
                var actual=form.RedactionRegionsForTest[0];Assert(actual.Page==2&&Math.Abs(actual.Bounds.left-mark.left)<3&&Math.Abs(actual.Bounds.top-mark.top)<3,"redaction: mouse marks map correctly through CropBox offsets and rotated viewing");
                File.WriteAllBytes(live,File.ReadAllBytes(changed));RefreshPump(1000);Assert(form.Document==doc&&form.RedactionCountForTest==1,"redaction: external file refresh cannot silently change content beneath pending marks");form.ClearRedactionsForTest();WaitRefresh(delegate{return form.Document!=doc;},"redaction: automatic refresh resumes after markers are cleared");
                form.AddRedaction(new N.Rect(30,50,120,90));form.Run(delegate{form.Document.AddText("EDIT_AFTER_MARK",30,60,8,"Helvetica",Color.Black);});Assert(!form.RedactionMarksValidForTest&&form.RedactionCountForTest==1,"redaction: document edits invalidate old coordinates instead of applying to changed content");form.ClearRedactionsForTest();using(var answer=AnswerSavePrompt(7))form.Close();Application.DoEvents();
            }
            using(var document=PdfDocument.Open(source,""))foreach(Form dialog in new Form[]{new ShapeStyleWindow(new ShapeStyle{Fill=Color.Black}),new RedactionWindow(document,regions,delegate{return true;})})using(dialog){dialog.Show();Application.DoEvents();using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(directory,dialog is ShapeStyleWindow?"rectangle-style-preview.png":"redaction-options-preview.png"));}Assert(dialog.Controls.Count>0,"redaction: native options dialog opens without clipping or initialization failures");dialog.Close();}
        }
        internal static int RunRedaction(string directory){Directory.CreateDirectory(directory);logPath=Path.Combine(directory,"redaction-tests.txt");try{UiText.English=false;RedactionCoverage(directory);log.AppendLine("TOTAL: "+assertions+" redaction assertions, 0 failures");File.WriteAllText(logPath,log.ToString());return 0;}catch(Exception error){log.AppendLine(error.ToString());File.WriteAllText(logPath,log.ToString());return 1;}}
    }
}
