using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MiniPdf
{
    internal static partial class SelfTest
    {
        static Pick EditingPick(PdfDocument document, string text)
        {
            for (int i = 0; i < N.FPDFPage_CountObjects(document.Page); i++)
            {
                var pick = document.PickIndex(i);
                if (pick.Type == 1 && pick.Text.TrimEnd() == text.TrimEnd()) return pick;
            }
            throw new Exception("Missing text fixture: " + text + " / " + document.AllText());
        }
        static TextEditOptions EditingOptions(TextEditSelection selection, string text)
        {
            return new TextEditOptions { Text = text, Size = selection.Size, Color = selection.Color,
                Width = selection.Width, LineHeight = selection.LineHeight };
        }
        static List<PointF> EditingOrigins(PdfDocument document, string text)
        {
            var pick = EditingPick(document, text); var obj = document.ObjectAt(pick);
            var result = new List<PointF>();
            for (int i = 0; i < N.FPDFText_CountChars(document.TextPage); i++)
                if (N.FPDFText_GetTextObject(document.TextPage, i) == obj)
                {
                    double x, y; if (N.FPDFText_GetCharOrigin(document.TextPage, i, out x, out y) != 0)
                        result.Add(new PointF((float)x, (float)y));
                }
            return result;
        }
        static void TextEditingCoverage(string directory, bool withUi = true)
        {
            UiText.English = false;
            bool hasCjk;
            using (var fonts = new System.Drawing.Text.InstalledFontCollection())
                hasCjk = fonts.Families.Any(f => f.GetName(1033) == "Microsoft YaHei");
            if (!hasCjk) { log.AppendLine("SKIP: system-font Chinese cases require Microsoft YaHei"); File.WriteAllText(Path.Combine(directory,"system-cjk-skipped.txt"),"Microsoft YaHei is not installed."); }
            string fixture = Path.Combine(directory, "text-source-basic.pdf");
            if (!File.Exists(fixture)) { log.AppendLine("SKIP: text fixtures missing; run verify_text_edit.py fixtures first"); return; }
            string[] names = { "basic", "embedded", "chinese", "clipped", "rotated", "hidden", "marked", "page-rotation", "stroke" };
            string[] beforeText = { "TRACKED ABC 123 ABC", "Embedded original 1234", "原始中文编辑测试文字和高亮", "Clipped original abc123",
                "Rotated original 123", "Hidden original 123", "Embedded original 1234", "TRACKED ABC 123 ABC", "Stroke original 123" };
            string[] afterText = { "TRACKED ABC 321 ABC", "Embedded revised 4321", "原始中文测试文字和编辑", "Clipped revised abc321",
                "Rotated revised 321", "Hidden revised 321", "Embedded revised 4321", "TRACKED ABC 321 ABC", "Stroke revised 321" };
            for (int caseIndex = 0; caseIndex < names.Length; caseIndex++)
            using (var document = PdfDocument.Open(Path.Combine(directory, "text-source-" + names[caseIndex] + ".pdf"), ""))
            {
                string name = names[caseIndex], original = document.AllText();
                var pick = EditingPick(document, beforeText[caseIndex]);
                var selected = document.SelectTextEdit(pick, 0); var options = EditingOptions(selected, afterText[caseIndex]);
                var obj = document.ObjectAt(pick); var font = N.FPDFTextObj_GetFont(obj); var origins = EditingOrigins(document, beforeText[caseIndex]);
                N.Matrix matrix; N.FPDFPageObj_GetMatrix(obj, out matrix);
                int marks = N.FPDFPageObj_CountMarks(obj), mode = N.FPDFTextObj_GetTextRenderMode(obj);
                Assert(!document.ApplyTextEdit(selected, EditingOptions(selected, selected.Text)) && !document.Dirty && !document.CanUndo,
                    name + ": accepting unchanged original text creates no revision");
                Assert(document.ApplyTextEdit(selected, options), name + ": original font edit applies");
                var updated = EditingPick(document, afterText[caseIndex]); var changed = document.ObjectAt(updated);
                Assert(obj == changed && font == N.FPDFTextObj_GetFont(changed), name + ": retains the original text object and font handle");
                N.Matrix after; N.FPDFPageObj_GetMatrix(changed, out after);
                Assert(Math.Abs(matrix.a - after.a) < .001 && Math.Abs(matrix.b - after.b) < .001 && Math.Abs(matrix.c - after.c) < .001 &&
                    Math.Abs(matrix.d - after.d) < .001 && Math.Abs(matrix.e - after.e) < .001 && Math.Abs(matrix.f - after.f) < .001,
                    name + ": retains text transformation and baseline");
                Assert(marks == N.FPDFPageObj_CountMarks(changed) && mode == N.FPDFTextObj_GetTextRenderMode(changed),
                    name + ": preserves marks and rendering mode");
                if (name == "basic" || name == "page-rotation")
                {
                    var positions = EditingOrigins(document, afterText[caseIndex]);
                    Assert(origins.Count == positions.Count && origins.Zip(positions, (a, b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)).Max() < .015,
                        name + ": same-width digit correction preserves all original tracking positions");
                }
                if (name == "clipped") Assert(N.FPDFClipPath_CountPaths(N.FPDFPageObj_GetClipPath(changed)) > 0,
                    "clipped: clipping path remains attached after editing");
                string output = Path.Combine(directory, "text-edited-" + name + ".pdf"); document.Save(output);
                using (var reopened = PdfDocument.Open(output, "")) Assert(reopened.AllText().Contains(afterText[caseIndex]) && !reopened.AllText().Contains(beforeText[caseIndex]),
                    name + ": edited text survives save/reopen and old text is removed");
                document.Undo(); Assert(document.AllText() == original && !document.CanUndo,
                    name + ": one undo restores complete original content");
                document.Redo(); Assert(document.AllText().Contains(afterText[caseIndex]), name + ": redo restores the edit");
            }
            using (var document = PdfDocument.Open(fixture, ""))
            {
                var original = document.AllText(); var pick = EditingPick(document, "Fragment");
                var line = document.SelectTextEdit(pick, 1);
                Assert(line.Indices.Length == 2 && line.Text.TrimEnd() == "Fragment line", "line: joins adjacent fragments without crossing the column gutter (" + line.Indices.Length + ": " + line.Text.Replace(" ", "_") + ")");
                document.ApplyTextEdit(line, EditingOptions(line, "Joined line"));
                Assert(document.AllText().Contains("Joined line") && document.AllText().Contains("OTHER COLUMN"), "line: replaces the selected line and preserves neighboring column");
                document.Save(Path.Combine(directory, "text-edited-line.pdf")); document.Undo();
                Assert(document.AllText() == original, "line: whole multi-object edit is one undo step");
                var paragraph = document.SelectTextEdit(EditingPick(document, "Paragraph line two ABCDEFGHIJ"), 2);
                Assert(paragraph.Indices.Length == 3 && paragraph.Text.Contains("line one") && paragraph.Text.Contains("line three") && !paragraph.Text.Contains("FORMULA"),
                    "paragraph: includes matching adjacent lines and stops at a changed line gap");
                var settings = EditingOptions(paragraph, paragraph.Text.Replace("line two", "revised two"));
                document.ApplyTextEdit(paragraph, settings);
                Assert(document.AllText().Contains("revised two") && document.AllText().Contains("FORMULA KEEP"), "paragraph: updates selected paragraph and retains the next block");
                document.Save(Path.Combine(directory,"text-edited-paragraph.pdf"));
            }
            using (var document = PdfDocument.Open(fixture, ""))
            {
                var pick = EditingPick(document, "Original serif text 1234"); var selection = document.SelectTextEdit(pick, 0);
                string before = document.AllText(); var options = EditingOptions(selection, "WRAPPED TEXT CONTENT WRAPPED TEXT CONTENT");
                options.Wrap = true; options.Width = 130;
                document.ApplyTextEdit(selection, options); Assert(document.AllText().Contains("WRAPPED") && N.FPDFPage_CountObjects(document.Page) > 9,
                    "wrap: creates real selectable multiline PDF text");
                document.Save(Path.Combine(directory,"text-edited-wrap.pdf")); document.Undo();
                Assert(document.AllText() == before && !document.CanUndo, "wrap: undo restores every original object in one step");
                selection = document.SelectTextEdit(EditingPick(document, "Original serif text 1234"), 0);
                options = EditingOptions(selection, "W"); options.Wrap = true; options.Width = 1;
                bool rejected = false; try { document.ApplyTextEdit(selection, options); } catch (InvalidOperationException) { rejected = true; }
                Assert(rejected && document.AllText() == before && !document.CanUndo, "wrap: impossible width rolls back without losing original text");
            }
            using (var document = PdfDocument.Open(Path.Combine(directory,"text-source-chinese.pdf"), ""))
            {
                string before = document.AllText(); var selection = document.SelectTextEdit(EditingPick(document,"原始中文编辑测试文字和高亮"),0);
                bool rejected = false; try { document.ApplyTextEdit(selection,EditingOptions(selection,"原始中文新增字符")); } catch(InvalidOperationException){ rejected=true; }
                Assert(rejected && document.AllText() == before && !document.Dirty && !document.CanUndo, "font: unconfirmed new characters leave the original untouched");
                if (hasCjk)
                {
                    var replacement = EditingOptions(selection,"原始中文新增字符"); replacement.Face = "自动";
                    document.ApplyTextEdit(selection,replacement); document.Save(Path.Combine(directory,"text-edited-system-font.pdf"));
                    Assert(document.AllText().Contains("原始中文新增字符"),"font: explicit system-font replacement accepts supported new Chinese characters");
                }
            }
            using (var document = PdfDocument.Open(fixture, ""))
            {
                var selection = document.SelectTextEdit(EditingPick(document,"Paragraph line two ABCDEFGHIJ"),2);
                var options = EditingOptions(selection,selection.Text); options.LineHeight = 2;
                Assert(document.ApplyTextEdit(selection,options),"paragraph: changing only line spacing creates a real edit");
                var a=EditingOrigins(document,"Paragraph line one 0123456789")[0];var b=EditingOrigins(document,"Paragraph line two ABCDEFGHIJ")[0];
                Assert(Math.Abs(a.Y-b.Y-24)<.02,"paragraph: requested line spacing controls the actual baseline distance");
                document.Save(Path.Combine(directory,"text-edited-spacing.pdf"));
            }
            using (var document = PdfDocument.New())
            {
                document.AddText("Tiny text 1234",45,700,.75f,"自动",Color.Black);
                var selection=document.SelectTextEdit(EditingPick(document,"Tiny text 1234"),0);
                document.ApplyTextEdit(selection,EditingOptions(selection,"Tiny text 4321"));
                Assert(document.AllText().Contains("Tiny text 4321"),"small text: sub-point text edits without a numeric control exception");
                if (withUi) using(var editor=new TextEditWindow(document,EditingPick(document,"Tiny text 4321")))
                    Assert(Math.Abs(editor.Options.Size-.75)<.001,"small text: native editor retains the original fractional font size");
            }
            if (hasCjk) using(var document=PdfDocument.Open(Path.Combine(directory,"text-source-hidden.pdf"),""))
            {
                var selection=document.SelectTextEdit(EditingPick(document,"Hidden original 123"),0);
                var options=EditingOptions(selection,"隐藏文字更新");options.Face="自动";
                document.ApplyTextEdit(selection,options);var hidden=EditingPick(document,"隐藏文字更新");
                Assert(hidden.IsOcrText && N.FPDFTextObj_GetTextRenderMode(document.ObjectAt(hidden))==3,
                    "hidden: explicit system font replacement retains invisible render mode");
                document.Save(Path.Combine(directory,"text-edited-hidden-font.pdf"));
            }
            using(var document=PdfDocument.Open(Path.Combine(directory,"text-source-stroke.pdf"),""))
            {
                var selection=document.SelectTextEdit(EditingPick(document,"Stroke original 123"),0);
                Assert(selection.Color.R>190 && selection.Color.B<40,"stroke: editor shows the visible stroke color instead of unused fill color");
            }
            using(var document=PdfDocument.Open(Path.Combine(directory,"text-source-dual-color.pdf"),""))
            {
                var pick=EditingPick(document,"Stroke original 123");var selection=document.SelectTextEdit(pick,0);
                uint r,g,b,a,oldStrokeAlpha,oldFillAlpha;N.FPDFPageObj_GetStrokeColor(document.ObjectAt(pick),out r,out g,out b,out oldStrokeAlpha);
                N.FPDFPageObj_GetFillColor(document.ObjectAt(pick),out r,out g,out b,out oldFillAlpha);
                var options=EditingOptions(selection,selection.Text);options.Color=Color.Blue;options.ChangeColor=true;document.ApplyTextEdit(selection,options);
                N.FPDFPageObj_GetStrokeColor(document.ObjectAt(EditingPick(document,"Stroke original 123")),out r,out g,out b,out a);
                Assert(r==0 && b==255 && a==oldStrokeAlpha,"color: explicit color selection updates stroke and preserves stroke opacity");
                N.FPDFPageObj_GetFillColor(document.ObjectAt(EditingPick(document,"Stroke original 123")),out r,out g,out b,out a);
                Assert(r==0 && b==255 && a==oldFillAlpha,"color: fill opacity is preserved independently of stroke opacity");
            }
            using(var document=PdfDocument.Open(Path.Combine(directory,"text-source-actual-text.pdf"),""))
            {
                string before=document.AllText();bool rejected=false;
                try{var selection=document.SelectTextEdit(document.PickIndex(0),0);document.ApplyTextEdit(selection,EditingOptions(selection,selection.Text+" A"));}
                catch(InvalidOperationException){rejected=true;}
                Assert(rejected && document.AllText()==before && !document.Dirty && !document.CanUndo,
                    "ActualText: unsupported alternate-text metadata is rejected without changing the PDF");
            }
            foreach (string unsupported in new[] { "שלום", "नमस्ते", "e" + (char)0x301 })
                using (var document = PdfDocument.Open(Path.Combine(directory,"text-source-basic.pdf"), ""))
                {
                    var selection = document.SelectTextEdit(EditingPick(document,"Original serif text 1234"),0);
                    var options = EditingOptions(selection, unsupported); options.Face = "自动";
                    string before = document.AllText(); bool rejected = false;
                    try { document.ApplyTextEdit(selection, options); } catch (InvalidOperationException) { rejected = true; }
                    Assert(rejected && document.AllText() == before && !document.Dirty && !document.CanUndo,
                           "complex shaping: unsupported script or combining sequence is rejected before modifying PDF");
                }
            string malformedFont = Path.Combine(directory, "font-source-SimFang-variant.pdf");
            if (File.Exists(malformedFont)) using (var document = PdfDocument.Open(malformedFont, ""))
            {
                string before = document.AllText();
                var pick = EditingPick(document, "全"); var selection = document.SelectTextEdit(pick, 0);
                bool rejected = false; try { document.ApplyTextEdit(selection, EditingOptions(selection, selection.Text + " ")); }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected && document.AllText() == before && !document.CanUndo,
                       "public font: reject re-encoding that would lose unrelated text after save and restore original");
            }
            if (!withUi) { UiText.English=false; return; }
            UiText.English=true;
            using(var document=PdfDocument.Open(fixture,""))
            using(var editor=new TextEditWindow(document,EditingPick(document,"Original serif text 1234")))
            {
                editor.Show();Application.DoEvents();PumpUntil(()=>editor.ReadyForTest);
                Assert(editor.LayoutFitsForTest,"editor: both footer buttons fit inside the native layout at current DPI");
                Assert(editor.Options.Face=="保留原字体" && editor.Selection.Indices.Length==1,"editor: English UI defaults to keeping original font and one object");
                editor.TextForTest("Original revised text 4321");PumpUntil(()=>editor.ReadyForTest);
                Assert(!document.Dirty && document.AllText().Contains("Original serif text 1234"),"editor: actual background-rendered preview does not modify the open document");
                using(var capture=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(capture,new Rectangle(0,0,capture.Width,capture.Height));capture.Save(Path.Combine(directory,"text-editor-preview.png"),ImageFormat.Png);}
                editor.Close();Assert(!document.Dirty && !document.CanUndo,"editor: closing discards preview changes without an undo entry");
            }
            using(var form=new MainWindow(null))
            {
                form.LoadForTest(PdfDocument.Open(fixture,""));form.Show();Application.DoEvents();form.CanvasForTest.Selected=EditingPick(form.Document,"Original serif text 1234");
                bool entered=false;
                using(var timer=new Timer{Interval=80})
                {
                    timer.Tick+=delegate{
                        TextEditWindow editor=null;foreach(Form window in Application.OpenForms)if(window is TextEditWindow)editor=(TextEditWindow)window;
                        if(editor==null)return;if(!entered){entered=true;editor.TextForTest("Original revised text 4321");return;}
                        if(editor.ReadyForTest){timer.Stop();editor.ApplyForTest();}
                    };timer.Start();form.EditSelected();
                }
                Assert(form.Document.Dirty && form.Document.AllText().Contains("Original revised text 4321"),"editor: real main-window edit command applies the previewed replacement");
                form.Document.Undo();Assert(form.Document.AllText().Contains("Original serif text 1234"),"editor: command edit is one undoable action");
                form.Close();Application.DoEvents();
            }
            UiText.English=false;
        }
        internal static int RunTextEditing(string directory, bool withUi = true)
        {
            logPath=Path.Combine(directory,"text-edit-test.txt");
            try{TextEditingCoverage(directory,withUi);log.AppendLine("TOTAL: "+assertions+" text-edit assertions, 0 failures");File.WriteAllText(logPath,log.ToString());return 0;}
            catch(Exception error){log.AppendLine(error.ToString());File.WriteAllText(logPath,log.ToString());return 1;}
        }
    }
}
