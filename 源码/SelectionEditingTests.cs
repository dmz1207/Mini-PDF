using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace MiniPdf {
    internal static partial class SelfTest {
        static TextRange EditingRange(PdfDocument d,string first,string last) {
            int a,b,n;var f=N.FPDFText_FindStart(d.TextPage,first,0,0);try{if(N.FPDFText_FindNext(f)==0)throw new Exception("Missing selection start");a=N.FPDFText_GetSchResultIndex(f);}finally{N.FPDFText_FindClose(f);}
            f=N.FPDFText_FindStart(d.TextPage,last,0,a);try{if(N.FPDFText_FindNext(f)==0)throw new Exception("Missing selection end");b=N.FPDFText_GetSchResultIndex(f);n=N.FPDFText_GetSchCount(f);}finally{N.FPDFText_FindClose(f);}
            return new TextRange{Page=d.Index,Start=a,Count=b+n-a};
        }
        static void SelectedTextCoverage(string folder,bool withUi) {
            string path=Path.Combine(folder,"text-source-selection.pdf");
            using(var d=PdfDocument.Open(path,"")){
                var range=EditingRange(d,"In this paper","selected line.");
                var s=d.SelectTextEdit(new List<TextRange>{range});
                Assert(s.Text.StartsWith("In this paper")&&s.Text.EndsWith("selected line.")&&s.Text.Contains("a priori")&&s.Text.Split('\n').Length==3,"selected text: capture all selected lines and mixed-font fragments without prefix/suffix");
                Assert(s.Indices.Length>=5&&Math.Abs(s.Size*s.SizeScale-10)<.01,"selected text: matrix-scaled one-unit fonts expose the actual ten-point size");
                Assert(!d.ApplyTextEdit(s,EditingOptions(s,s.Text))&&!d.Dirty,"selected text: unchanged full selection is a no-op");
                var o=EditingOptions(s,s.Text.Replace("reaction wheels","control wheels"));d.ApplyTextEdit(s,o);
                Assert(d.AllText().Contains("control wheels")&&!d.AllText().Contains("reaction wheels")&&d.AllText().Contains("PREFIX")&&d.AllText().Contains("SUFFIX"),"selected text: changing the paragraph replaces its words and preserves unselected object prefixes/suffixes");
                Assert(d.AllText().Contains("OTHER COLUMN MUST STAY")&&d.AllText().Contains("UNCHANGED FOOTER"),"selected text: another column and footer stay intact");
                var italic=EditingPick(d,"a priori");Assert(PdfDocument.TextFontName(N.FPDFTextObj_GetFont(d.ObjectAt(italic))).Contains("Italic"),"selected text: unchanged italic fragments retain their own original font");
                d.Save(Path.Combine(folder,"text-edited-selection.pdf"));d.Undo();Assert(d.AllText().Contains("reaction wheels")&&!d.AllText().Contains("control wheels"),"selected text: one undo restores all selected objects and their boundaries");
                d.Redo();Assert(d.AllText().Contains("control wheels"),"selected text: one redo restores the complete paragraph change");
            }
            using(var d=PdfDocument.Open(path,"")){
                var range=EditingRange(d,"In this paper","selected line.");d.ModifySelectedText(new List<TextRange>{range},false,1);string text=d.AllText();
                Assert(!text.Contains("reaction wheels")&&!text.Contains("a priori")&&text.Contains("PREFIX")&&text.Contains("SUFFIX")&&text.Contains("OTHER COLUMN MUST STAY"),"selected text: Delete removes the whole selected range and keeps text outside both boundaries");
                d.Save(Path.Combine(folder,"text-deleted-selection.pdf"));d.Undo();Assert(d.AllText().Contains("reaction wheels")&&d.AllText().Contains("a priori"),"selected text: deletion is one undoable operation");
            }
            using(var d=PdfDocument.Open(path,"")){
                var range=EditingRange(d,"In this paper","selected line.");d.ModifySelectedText(new List<TextRange>{range},true,1.2f);
                Assert(d.AllText().Contains("reaction wheels")&&d.AllText().Contains("a priori"),"selected text: resizing includes every selected fragment");
                var p=EditingPick(d,"a priori");float size;N.FPDFTextObj_GetFontSize(d.ObjectAt(p),out size);N.Matrix m;N.FPDFPageObj_GetMatrix(d.ObjectAt(p),out m);
                Assert(Math.Abs(size*Math.Abs(m.d)-12)<.01,"selected text: a 120 percent resize applies to the italic fragment as well");
                d.Save(Path.Combine(folder,"text-resized-selection.pdf"));
            }
            using(var d=PdfDocument.Open(Path.Combine(folder,"text-source-selection-pages.pdf"),"")){
                var ranges=new List<TextRange>{EditingRange(d,"In this paper","selected line.")};d.OpenPage(1);ranges.Add(EditingRange(d,"In this paper","selected line."));
                var s=d.SelectTextEdit(ranges);Assert(s.Children.Length==2&&s.Text.Contains(PdfDocument.SelectionDivider(1)),"selected text: cross-page editing loads every page with explicit retained separators");
                var o=EditingOptions(s,s.Text.Replace("reaction wheels","control wheels").Replace("\n","\r\n"));d.ApplyTextEdit(s,o);
                Assert(d.AllText().Split(new[]{"control wheels"},StringSplitOptions.None).Length==3,"selected text: the same editor applies changes to both selected pages");
                d.Save(Path.Combine(folder,"text-edited-selection-pages.pdf"));d.Undo();Assert(!d.AllText().Contains("control wheels"),"selected text: one undo restores the complete multi-page edit");
            }

            using(var d=PdfDocument.Open(Path.Combine(folder,"text-source-selection-pages.pdf"),"")){
                var ranges=new List<TextRange>{EditingRange(d,"In this paper","selected line.")};d.OpenPage(1);ranges.Add(EditingRange(d,"In this paper","selected line."));
                var s=d.SelectTextEdit(ranges);var invalid=EditingOptions(s,s.Text.Replace(PdfDocument.SelectionDivider(1),""));
                bool rejected=false;try{d.ApplyTextEdit(s,invalid);}catch(InvalidOperationException){rejected=true;}
                Assert(rejected&&!d.Dirty&&!d.CanUndo,"selected text: removing a page boundary is rejected before any document mutation");
                d.ModifySelectedText(ranges,false,1);Assert(!d.AllText().Contains("reaction wheels")&&d.AllText().Split(new[]{"SUFFIX"},StringSplitOptions.None).Length==3,"selected text: multi-page Delete keeps both unselected page boundaries");
                d.Save(Path.Combine(folder,"text-deleted-selection-pages.pdf"));d.Undo();Assert(d.AllText().Split(new[]{"reaction wheels"},StringSplitOptions.None).Length==3,"selected text: multi-page Delete is one undoable transaction");
                d.ModifySelectedText(ranges,true,1.2f);d.Save(Path.Combine(folder,"text-resized-selection-pages.pdf"));
                for(int page=0;page<2;page++){d.OpenPage(page);var p=EditingPick(d,"a priori");float size;N.FPDFTextObj_GetFontSize(d.ObjectAt(p),out size);N.Matrix m;N.FPDFPageObj_GetMatrix(d.ObjectAt(p),out m);Assert(Math.Abs(size*m.d-12)<.01,"selected text: multi-page resize is persisted on page "+(page+1));}
            }

            using(var d=PdfDocument.Open(Path.Combine(folder,"text-source-variable-spacing.pdf"),"")){
                var s=d.SelectTextEdit(new List<TextRange>{EditingRange(d,"In this paper","selected line.")});
                var o=EditingOptions(s,s.Text.Replace("reaction wheels","control wheels"));d.ApplyTextEdit(s,o);d.Save(Path.Combine(folder,"text-edited-variable-spacing.pdf"));
                Assert(d.AllText().Contains("control wheels")&&d.AllText().Contains("a priori"),"selected text: nonuniform TJ spacing survives a mixed-style paragraph edit");
            }
            using(var d=PdfDocument.Open(path,"")){
                var s=d.SelectTextEdit(new List<TextRange>{EditingRange(d,"In this paper","selected line.")});var o=EditingOptions(s,s.Text);o.LineHeight=2;
                d.ApplyTextEdit(s,o);var part=EditingRange(d,"Known","a priori");double x,y;N.FPDFText_GetCharOrigin(d.TextPage,part.Start,out x,out y);
                Assert(Math.Abs(y-700)<.01,"selected text: spacing-only edits apply to all selected line baselines");
                d.Undo();s=d.SelectTextEdit(new List<TextRange>{EditingRange(d,"In this paper","selected line.")});o=EditingOptions(s,s.Text);o.Wrap=true;o.Width=10;
                Assert(d.ApplyTextEdit(s,o),"selected text: wrapping unchanged selected content still applies the requested layout");d.Save(Path.Combine(folder,"text-wrapped-selection.pdf"));
            }
            if(withUi)using(var d=PdfDocument.Open(Path.Combine(folder,"text-source-selection-pages.pdf"),"")){
                var ranges=new List<TextRange>{EditingRange(d,"In this paper","selected line.")};d.OpenPage(1);ranges.Add(EditingRange(d,"In this paper","selected line."));
                var s=d.SelectTextEdit(ranges);using(var editor=new TextEditWindow(d,s)){editor.Show();editor.SetDraft(s.Text.Replace("reaction wheels","control wheels"));PumpUntil(()=>editor.ReadyForTest);editor.PreviewPageForTest(1);PumpUntil(()=>editor.ReadyForTest);Assert(editor.Draft.Contains("final selected line")&&!d.Dirty,"selected text: the real multi-page editor previews both pages without changing the document");editor.Close();}
            }
            if(withUi)using(var form=new MainWindow(null)){
                form.LoadForTest(PdfDocument.Open(path,""));form.Show();Application.DoEvents();var view=form.CanvasForTest;
                var range=EditingRange(form.Document,"In this paper","selected line.");view.TextStart=range.Start;view.TextCount=range.Count;
                Command(form,Keys.F2);Assert(view.InlineEditing&&view.InlineDraft.Contains("final selected line")&&view.InlineDraft.Contains("a priori"),"inline editor: F2 opens the complete selection in the actual document view");
                view.InlineDraft=view.InlineDraft.Replace("reaction wheels","control wheels");
                using(var shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(folder,"inline-editor.png"));}
                view.EndInlineText();Assert(!form.Document.Dirty&&!form.Document.CanUndo&&form.Document.AllText().Contains("reaction wheels"),"inline editor: Cancel discards the draft without changing the document");
                form.EditSelected(true);view.InlineDraft=view.InlineDraft.Replace("reaction wheels","control wheels");
                Assert(view.ApplyInlineText()&&!view.InlineEditing&&form.Document.AllText().Contains("control wheels"),"inline editor: Apply replaces every selected line in the document");
                form.Document.Undo();Assert(form.Document.AllText().Contains("reaction wheels"),"inline editor: the full edit undoes in one step");
                range=EditingRange(form.Document,"In this paper","selected line.");view.TextStart=range.Start;view.TextCount=range.Count;form.EditSelected(true);view.InlineDraft=view.InlineDraft.Replace("reaction wheels","control wheels");
                bool popup=false;using(var timer=new Timer{Interval=80}){timer.Tick+=delegate{foreach(Form window in Application.OpenForms){var editor=window as TextEditWindow;if(editor==null)continue;popup=editor.Draft.Contains("control wheels")&&editor.Draft.Contains("final selected line");editor.DialogResult=DialogResult.Cancel;timer.Stop();break;}};timer.Start();view.PopInlineText();}
                Assert(popup&&view.InlineEditing&&view.InlineDraft.Contains("control wheels")&&!form.Document.Dirty,"inline editor: switching to a window retains the complete draft; cancel returns to in-document editing");
                view.EndInlineText();form.Close();Application.DoEvents();
            }

            if(withUi)using(var form=new MainWindow(null)){
                form.LoadForTest(PdfDocument.Open(path,""));var first=form.Document;form.LoadForTest(PdfDocument.Open(path,""));form.Show();form.SelectTabForTest(0);Application.DoEvents();
                var view=form.CanvasForTest;var range=EditingRange(first,"In this paper","selected line.");view.TextStart=range.Start;view.TextCount=range.Count;form.EditSelected(true);view.InlineDraft=view.InlineDraft.Replace("reaction wheels","control wheels");
                using(var answer=AnswerSavePrompt(2,"Mini PDF"))form.SelectTabForTest(1);
                Assert(form.Document==first&&form.DocumentTabsForTest.SelectedIndex==0&&view.InlineEditing&&view.InlineDraft.Contains("control wheels"),"inline editor: canceling a tab switch preserves the draft and restores the visible tab");
                using(var answer=AnswerSavePrompt(2,"Mini PDF"))Assert(!form.CloseTabForTest(0)&&form.TabCountForTest==2&&view.InlineEditing,"inline editor: closing a clean document still asks about an unconfirmed draft");
                using(var answer=AnswerSavePrompt(7,"Mini PDF"))Assert(form.CloseTabForTest(0)&&form.TabCountForTest==1&&!view.InlineEditing,"inline editor: explicitly discarding the draft closes only its document tab");
                form.Close();Application.DoEvents();
            }

            if(withUi)using(var form=new MainWindow(null)){
                form.LoadForTest(PdfDocument.Open(path,""));form.Show();Application.DoEvents();var view=form.CanvasForTest;
                var range=EditingRange(form.Document,"In this paper","selected line.");var s=form.Document.SelectTextEdit(new List<TextRange>{range});
                using(var timer=new Timer{Interval=80}){timer.Tick+=delegate{foreach(Form window in Application.OpenForms){var editor=window as TextEditWindow;if(editor==null)continue;editor.SizeForTest(13);editor.DialogResult=DialogResult.Retry;timer.Stop();break;}};timer.Start();form.EditTextInWindow(s);}
                Assert(view.InlineEditing&&view.ApplyInlineText(),"inline editor: separate-window format changes can switch back and apply in the document");
                var p=EditingPick(form.Document,"a priori");float size;N.FPDFTextObj_GetFontSize(form.Document.ObjectAt(p),out size);N.Matrix matrix;N.FPDFPageObj_GetMatrix(form.Document.ObjectAt(p),out matrix);
                Assert(Math.Abs(size*matrix.d-13)<.01,"inline editor: switching back retains the window's effective font-size change");
                form.Document.Undo();form.Close();Application.DoEvents();
            }
            if(withUi)using(var form=new MainWindow(null)){
                form.LoadForTest(PdfDocument.Open(Path.Combine(folder,"text-source-clipped.pdf"),""));form.Show();Application.DoEvents();var view=form.CanvasForTest;view.TextStart=0;view.TextCount=N.FPDFText_CountChars(form.Document.TextPage);
                using(var answer=AnswerSavePrompt(1,UiText.Get("操作未完成")))form.EditSelected(true);
                Assert(!view.InlineEditing&&!form.Document.Dirty&&!form.Document.CanUndo,"selected text: unsupported clipped selection gives an explicit message and edits none of it");form.Close();Application.DoEvents();
            }
            if(withUi)using(var d=PdfDocument.Open(path,"")){
                var s=d.SelectTextEdit(new List<TextRange>{EditingRange(d,"In this paper","selected line.")});
                using(var editor=new TextEditWindow(d,s)){editor.Show();PumpUntil(()=>editor.ReadyForTest);Assert(editor.Selection.Text.Contains("final selected line")&&Math.Abs(editor.DisplaySizeForTest-10)<.01,"selected text: actual editor displays the entire range and effective font size");editor.Close();}
                TextBox input;using(var dialog=Dialogs.CreateAsk("Resize","输入缩放百分比，例如 120 表示放大到 120%：","100",false,out input)){
                    dialog.Show();Application.DoEvents();foreach(Button button in dialog.Controls[0].Controls[2].Controls)Assert(button.Height>=TextRenderer.MeasureText(button.Text,button.Font).Height+button.Padding.Vertical,"dialogs: confirmation/cancel captions fit the real high-DPI buttons");using(var shot=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(folder,"ask-scaled.png"));}dialog.Close();
                }
            }
        }
    }
}
