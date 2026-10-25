using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MiniPdf {
    internal static partial class SelfTest {
        static byte[] OfficePixels(Bitmap image){var bits=image.LockBits(new Rectangle(0,0,image.Width,image.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);try{var bytes=new byte[Math.Abs(bits.Stride)*image.Height];System.Runtime.InteropServices.Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);return bytes;}finally{image.UnlockBits(bits);}}
        static void OfficeFormsCoverage(string directory,bool withUi=true) {
            UiText.English=true;
            WordLayoutCoverage(directory,withUi);
            bool hasCjk;
            using(var fonts=new System.Drawing.Text.InstalledFontCollection())hasCjk=fonts.Families.Any(f=>f.GetName(1033)=="Microsoft YaHei");
            string firstName=hasCjk?"张三 Alice":"Alice",secondName=hasCjk?"李四 Bob":"Bob",noteText=hasCjk?"First line\n第二行":"First line\nSecond line",countryLabel=hasCjk?"中国":"China";
            if(!hasCjk){log.AppendLine("SKIP: newly generated Chinese form appearances require Microsoft YaHei; original Chinese extraction remains tested.");File.WriteAllText(Path.Combine(directory,"forms-cjk-skipped.txt"),"Microsoft YaHei is not installed.");}
            string source=Path.Combine(directory,"office-source.pdf");
            using(var doc=PdfDocument.Open(source,"")) {
                string before=doc.AllText();var page=OfficeLayout.Read(doc,0,true);var table=OfficeExport.Cells(page,true);
                Assert(page.Rows.Any(r=>r.Cells.Any(c=>c.Text.Contains("Apple"))),"Office: geometrical extraction retains table text");
                Assert(page.Rows.Any(r=>r.Cells.Any(c=>c.Text.Contains("可编辑"))),"Office: existing Chinese text exports without adding OCR");
                Assert(table.Any(r=>r.Count>=4&&r[0]=="Apple"&&r[1]=="2"&&r[2]=="12.50"&&r[3]=="=2+2"),"Excel: aligned row values map to four distinct cells");
                Assert(page.Images.Count==1,"Word: native image pixels are extracted");
                OfficeExport.Save(doc,new OfficeExportOptions{Pages=new[]{0,1}},Path.Combine(directory,"office-result.docx"),null);
                OfficeExport.Save(doc,new OfficeExportOptions{Pages=new[]{0,1},Excel=true},Path.Combine(directory,"office-result.xlsx"),null);
                OfficeExport.Save(doc,new OfficeExportOptions{Pages=new[]{0},Images=false},Path.Combine(directory,"office-text-only.docx"),null);
                using(var zip=ZipFile.OpenRead(Path.Combine(directory,"office-result.docx")))Assert(zip.GetEntry("word/document.xml")!=null&&zip.Entries.Count(e=>e.FullName.StartsWith("word/media/"))==2,"Word: valid package includes the source illustration and image-only scan");
                using(var zip=ZipFile.OpenRead(Path.Combine(directory,"office-result.xlsx")))Assert(zip.Entries.Count(e=>e.FullName.StartsWith("xl/worksheets/"))==2,"Excel: selected PDF pages produce individual worksheets");
                Assert(doc.AllText()==before&&!doc.Dirty&&!doc.CanUndo,"Office: conversions leave the source PDF content and undo state untouched");
                Assert(OfficeExport.Numeric("12.50")&&!OfficeExport.Numeric("00123")&&!OfficeExport.Numeric("=2+2")&&!OfficeExport.Numeric("1234567890123456"),"Excel: preserve leading-zero IDs, literal formulas and long identifiers");
                bool rejected=false;try{OfficeExport.Save(doc,new OfficeExportOptions{Pages=new[]{99}},Path.Combine(directory,"office-invalid.docx"),null);}catch(InvalidOperationException){rejected=true;}
                Assert(rejected&&!File.Exists(Path.Combine(directory,"office-invalid.docx")),"Office: invalid page selections do not create partial output");
            }
            if(withUi)using(var doc=PdfDocument.Open(source,"")) {
                string output=Path.Combine(directory,"office-worker.docx");
                using(var progress=new OfficeProgress(doc,new OfficeExportOptions{Pages=new[]{0,1}},output)) {
                    progress.Show();var watch=System.Diagnostics.Stopwatch.StartNew();while(progress.DialogResult==DialogResult.None&&!progress.IsDisposed&&watch.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(20);}
                    Assert(progress.DialogResult==DialogResult.OK&&File.Exists(output),"Office: isolated production worker exports through the actual progress window");progress.Close();
                }
                var cancelOptions=new OfficeExportOptions{Pages=Enumerable.Repeat(0,2000).ToArray(),Images=false};string canceled=Path.Combine(directory,"office-canceled.docx");
                using(var progress=new OfficeProgress(doc,cancelOptions,canceled)) {
                    progress.Show();var watch=System.Diagnostics.Stopwatch.StartNew();while(!File.Exists(cancelOptions.TempPath)&&progress.DialogResult==DialogResult.None&&watch.ElapsedMilliseconds<10000){Application.DoEvents();System.Threading.Thread.Sleep(10);}progress.Close();Application.DoEvents();
                }
                Assert(!File.Exists(cancelOptions.TempPath)&&!File.Exists(canceled),"Office: canceling an active export removes its owned temporary package");
            }
            using(var doc=PdfDocument.Open(Path.Combine(directory,"form.pdf"),"")) {
                var existing=PdfForms.Read(doc);Assert(existing.Fields.Count>0&&existing.Fields.Any(f=>f.Value.Contains("PRESERVED VALUE")),"forms: read existing standard text fields and values");
                using(var image=doc.Render(0,600,800))image.Save(Path.Combine(directory,"form-existing-preview.png"));
            }
            using(var doc=PdfDocument.Open(source,"")) {
                var model=PdfForms.Read(doc);Assert(model.Fields.Count==0,"forms: an ordinary PDF can become a standard form");
                Func<string,FieldKind,int,N.Rect,PdfField> add=delegate(string name,FieldKind kind,int p,N.Rect rect){var f=new PdfField{Name=name,Kind=kind,Changed=true};f.Widgets.Add(new FormWidget{Page=p,Bounds=rect});model.Fields.Add(f);return f;};
                var text=add("Group.Name",FieldKind.Text,0,new N.Rect(45,330,260,355));text.Values=new[]{firstName};text.Default="Default name";text.Tooltip="姓名 / Name";text.Flags=2;text.MaxLength=60;
                var multi=add("Notes",FieldKind.Multiline,0,new N.Rect(45,230,260,320));multi.Values=new[]{noteText};
                var secret=add("Password",FieldKind.Password,0,new N.Rect(280,330,440,355));secret.Values=new[]{"Secret123"};
                var check=add("Agree",FieldKind.Check,0,new N.Rect(280,290,300,310));check.Values=new[]{"Yes"};
                var radio=add("Choice",FieldKind.Radio,0,new N.Rect(280,255,300,275));radio.Widgets[0].On="A";radio.Widgets.Add(new FormWidget{Page=0,Bounds=new N.Rect(320,255,340,275),On="B"});radio.Values=new[]{"B"};
                var combo=add("Country",FieldKind.Combo,0,new N.Rect(45,180,260,205));combo.Choices.Add(new FormChoice{Value="CN",Label=countryLabel});combo.Choices.Add(new FormChoice{Value="US",Label="United States"});combo.Values=new[]{"CN"};
                var list=add("Colors",FieldKind.MultiList,0,new N.Rect(280,155,440,235));foreach(string c in new[]{"Red","Green","Blue"})list.Choices.Add(new FormChoice{Value=c,Label=c});list.Values=new[]{"Red","Blue"};list.Default="Green";
                var readOnly=add("Locked",FieldKind.Text,0,new N.Rect(45,120,260,145));readOnly.Flags=1;readOnly.Values=new[]{"Read only"};
                var excluded=add("Private",FieldKind.Text,0,new N.Rect(45,80,260,105));excluded.Flags=4;excluded.Values=new[]{"Do not export"};
                var editCombo=add("Free choice",FieldKind.EditableCombo,1,new N.Rect(45,710,260,740));editCombo.Choices.Add(new FormChoice{Value="A",Label="A"});editCombo.Values=new[]{"Custom"};
                byte[] built=model.Build();Assert(!doc.Dirty&&!doc.CanUndo&&N.FPDFPage_GetAnnotCount(doc.Page)==0,"forms: preview/build does not modify the original document");
                doc.ApplyForms(model);Assert(doc.Dirty&&doc.CanUndo,"forms: applying the whole design creates one undo action");
                doc.Save(Path.Combine(directory,"forms-created.pdf"));var reopened=PdfForms.Read(doc);
                Assert(reopened.Fields.Count==10&&reopened.Fields.First(f=>f.Name=="Group.Name").Value==firstName,"forms: hierarchical names and Chinese values survive save/reopen");
                Assert(reopened.Fields.First(f=>f.Name=="Choice").Widgets.Count==2&&reopened.Fields.First(f=>f.Name=="Choice").Value=="B","forms: a radio group retains separate widgets and one selected export value");
                Assert(reopened.Fields.First(f=>f.Name=="Country").Choices[0].Label==countryLabel&&reopened.Fields.First(f=>f.Name=="Country").Value=="CN","forms: choice labels and stored export values remain distinct");
                Assert(reopened.Fields.First(f=>f.Name=="Colors").Values.SequenceEqual(new[]{"Red","Blue"}),"forms: multi-selection list saves all selected values");
                string xml=reopened.ExportData();File.WriteAllText(Path.Combine(directory,"forms-data.xfdf"),xml,new UTF8Encoding(false));Assert(xml.Contains(firstName)&&!xml.Contains("Do not export"),"forms: XFDF preserves Unicode and respects NoExport");
                string change="<xfdf xmlns=\"http://ns.adobe.com/xfdf/\"><fields><field name=\"Group\"><field name=\"Name\"><value>"+secondName+"</value></field></field><field name=\"Agree\"><value>Off</value></field><field name=\"Locked\"><value>Changed</value></field><field name=\"Country\"><value>US</value></field></fields></xfdf>";
                int imported=reopened.ImportData(change);Assert(imported==3&&reopened.Fields.First(f=>f.Name=="Group.Name").Value==secondName&&reopened.Fields.First(f=>f.Name=="Locked").Value=="Read only","forms: nested XFDF updates writable fields and preserves read-only values");
                doc.ApplyForms(reopened);doc.Save(Path.Combine(directory,"forms-filled.pdf"));
                OfficeExport.Save(doc,new OfficeExportOptions{Pages=new[]{0,1}},Path.Combine(directory,"forms-export.docx"),null);
                OfficeExport.Save(doc,new OfficeExportOptions{Pages=new[]{0,1},Excel=true},Path.Combine(directory,"forms-export.xlsx"),null);
                var moves=PdfForms.Read(doc);var move=moves.Fields.First(f=>f.Name=="Group.Name");move.Widgets[0].Page=1;move.Widgets[0].Bounds=new N.Rect(45,620,260,650);move.Changed=true;var clone=move.Clone();clone.Id=0;clone.Name="Copy.Name";foreach(var w in clone.Widgets)w.Id=0;moves.Fields.Add(clone);moves.Fields.RemoveAll(f=>f.Name=="Private");doc.ApplyForms(moves);doc.Save(Path.Combine(directory,"forms-managed.pdf"));
                var managed=PdfForms.Read(doc);Assert(managed.Fields.Count==10&&managed.Fields.First(f=>f.Name=="Group.Name").Widgets[0].Page==1&&!managed.Fields.Any(f=>f.Name=="Private"),"forms: move between pages, duplicate and delete preserve a consistent field tree");
                Assert(managed.Fields.First(f=>f.Name=="Group.Name").Id!=managed.Fields.First(f=>f.Name=="Copy.Name").Id,"forms: duplicated fields are independent objects");
                bool rejected=false;string prior=managed.Fields.First(f=>f.Name=="Country").Value;try{managed.ImportData("<xfdf><fields><field name=\"Country\"><value>INVALID</value></field></fields></xfdf>");}catch(InvalidOperationException){rejected=true;}
                Assert(rejected&&managed.Fields.First(f=>f.Name=="Country").Value==prior,"forms: invalid choice imports roll back the entire operation");
                rejected=false;try{managed.ImportData("<!DOCTYPE xfdf [<!ENTITY leak SYSTEM 'file:///not-read'>]><xfdf><fields/></xfdf>");}catch(System.Xml.XmlException){rejected=true;}
                Assert(rejected,"forms: data import never resolves external XML entities");
                doc.Undo();Assert(!PdfForms.Read(doc).Fields.Any(f=>f.Name=="Copy.Name"),"forms: one undo restores all fields and widget placements");doc.Redo();Assert(PdfForms.Read(doc).Fields.Any(f=>f.Name=="Copy.Name"),"forms: redo restores the complete design");
                doc.OpenPage(0);using(var image=doc.Render(0,900,1200))image.Save(Path.Combine(directory,"forms-rendered.png"));
                if(withUi)using(var window=new FormWindow(doc)){window.Show();var wait=System.Diagnostics.Stopwatch.StartNew();while(wait.ElapsedMilliseconds<4000&&!window.ReadyForTest){Application.DoEvents();System.Threading.Thread.Sleep(20);}Assert(window.ReadyForTest,"forms: native designer opens and schedules the real PDF preview");Application.DoEvents();if(!window.LayoutFitsForTest)File.WriteAllText(Path.Combine(directory,"forms-layout-failure.txt"),window.LayoutDiagnosticsForTest);Assert(window.LayoutFitsForTest,"forms: visible property editors keep their height and never overlap at the current display scaling");
                    window.SelectForTest("Agree");window.CheckedForTest(true);Assert(window.ApplyEnabledForTest,"forms: changing a checkbox enables Apply before Update is clicked");window.UpdateForTest();Assert(window.Model.Fields.First(x=>x.Name=="Agree").Value=="Yes","forms: the actual fill controls update the selected field");window.HistoryForTest(false);
                    window.DesignForTest(true);window.SelectForTest("Notes");Application.DoEvents();var board=window.CanvasForTest;var f=window.Model.Fields.First(x=>x.Name=="Notes");var bounds=board.WidgetBoundsForTest(f.Widgets[0]);var center=new Point(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2);float originalLeft=f.Widgets[0].Bounds.left;
                    N.SendMessage(board.Handle,0x201,new IntPtr(1),Position(center));N.SendMessage(board.Handle,0x200,new IntPtr(1),Position(new Point(center.X+20,center.Y+12)));N.SendMessage(board.Handle,0x202,IntPtr.Zero,Position(new Point(center.X+20,center.Y+12)));
                    Assert(f.Widgets[0].Bounds.left>originalLeft+1,"forms: real left-button drag moves the selected widget");
                    bounds=board.WidgetBoundsForTest(f.Widgets[0]);float oldWidth=f.Widgets[0].Bounds.right-f.Widgets[0].Bounds.left;var corner=new Point(bounds.Right-2,bounds.Bottom-2);
                    N.SendMessage(board.Handle,0x201,new IntPtr(1),Position(corner));N.SendMessage(board.Handle,0x200,new IntPtr(1),Position(new Point(corner.X+22,corner.Y+15)));N.SendMessage(board.Handle,0x202,IntPtr.Zero,Position(new Point(corner.X+22,corner.Y+15)));
                    Assert(f.Widgets[0].Bounds.right-f.Widgets[0].Bounds.left>oldWidth+1,"forms: real corner drag resizes a widget");
                    window.HistoryForTest(false);window.HistoryForTest(false);window.SelectForTest("Notes");
                    Assert(Math.Abs(window.Model.Fields.First(x=>x.Name=="Notes").Widgets[0].Bounds.left-originalLeft)<.01,"forms: designer undo restores placement independently of the document");
                    bounds=board.WidgetBoundsForTest(window.Model.Fields.First(x=>x.Name=="Notes").Widgets[0]);center=new Point(bounds.Left+10,bounds.Top+10);N.SendMessage(board.Handle,0x201,new IntPtr(1),Position(center));N.SendMessage(board.Handle,0x200,new IntPtr(1),Position(new Point(center.X+30,center.Y)));board.CancelDrag();N.SendMessage(board.Handle,0x202,IntPtr.Zero,Position(new Point(center.X+30,center.Y)));
                    Assert(Math.Abs(window.Model.Fields.First(x=>x.Name=="Notes").Widgets[0].Bounds.left-originalLeft)<.01,"forms: capture cancellation discards a partial drag");
using(var shot=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(shot,new Rectangle(0,0,window.Width,window.Height));shot.Save(Path.Combine(directory,"forms-window.png"));}window.DiscardForTest();}
            }
            using(var sourceForm=PdfDocument.Open(Path.Combine(directory,"forms-created.pdf"),"")) {
                using(var combined=PdfDocument.MergePages(new[]{new MergePage{Source=sourceForm,Index=0},new MergePage{Source=sourceForm,Index=1}})) {
                    var fields=PdfForms.Read(combined);Assert(fields.Fields.Count==10&&fields.Fields.First(f=>f.Name=="Group.Name").Value==firstName,"forms: merging reconnects field parents and preserves Unicode values");
                    combined.Save(Path.Combine(directory,"forms-merged.pdf"));
                    using(var a=sourceForm.Render(0,600,800))using(var b=combined.Render(0,600,800))Assert(OfficePixels(a).SequenceEqual(OfficePixels(b)),"forms: merged native appearance is pixel-identical to the source");
                }
                using(var target=PdfDocument.Open(Path.Combine(directory,"forms-created.pdf"),"")) {
                    target.DuplicatePage();var fields=PdfForms.Read(target);Assert(fields.Fields.Count==19&&fields.Fields.Select(f=>f.Name).Distinct().Count()==19,"forms: duplicate page creates independent fields with unique names");
                    target.Save(Path.Combine(directory,"forms-duplicated.pdf"));target.Undo();Assert(PdfForms.Read(target).Fields.Count==10,"forms: undo restores field trees after page duplication");
                }
                sourceForm.Extract(new[]{0},Path.Combine(directory,"forms-extracted.pdf"));using(var extracted=PdfDocument.Open(Path.Combine(directory,"forms-extracted.pdf"),""))Assert(PdfForms.Read(extracted).Fields.Count==9,"forms: extraction keeps the selected page's fields and omits unrelated fields");
                using(var target=PdfDocument.Open(source,"")){target.Import(sourceForm,0);Assert(target.Count==4&&PdfForms.Read(target).Fields.Count==10,"forms: inserting a form PDF retains its interactive field tree");target.Save(Path.Combine(directory,"forms-inserted.pdf"));}
            }
            using(var encrypted=PdfDocument.Open(Path.Combine(directory,"forms-encrypted.pdf"),"owner")) {
                var fields=PdfForms.Read(encrypted);bool rejected=false;try{encrypted.ApplyForms(fields);}catch(InvalidOperationException){rejected=true;}
                Assert(rejected&&N.FPDF_GetSecurityHandlerRevision(encrypted.Handle)>=0&&!encrypted.Dirty,"forms: structural editing never silently removes source encryption");
            }
            using(var xfa=PdfDocument.Open(Path.Combine(directory,"forms-xfa.pdf"),"")){bool rejected=false;try{PdfForms.Read(xfa);}catch(InvalidOperationException){rejected=true;}Assert(rejected&&!xfa.Dirty,"forms: unsupported dynamic XFA is reported without modifying the PDF");}
            UiText.English=false;
        }
        static void WordLayoutCoverage(string directory,bool withUi) {
            using(var doc=PdfDocument.Open(Path.Combine(directory,"word-academic-source.pdf"),"")) {
                string original=doc.AllText();string[] publishers={"IEEE","Elsevier","Wiley","Springer Nature"};
                for(int i=0;i<4;i++) {
                    var profile=AcademicLayout.Read(OfficeGeometry.Read(doc,i),doc.PageSize(i));
                    Assert(profile.Publisher==publishers[i],"Academic profiles: recognize publication marks for "+publishers[i]);
                    Assert(profile.Columns==(i!=1),"Academic profiles: geometry selects the actual column count for "+publishers[i]);
                    if(i==2)Assert(profile.Split<270,"Academic profiles: indented right-column blocks cannot push the gutter into prose");
                    if(i==0||i==2)Assert(Math.Abs(profile.Split-300)>20,"Academic profiles: asymmetric gutter is measured for "+publishers[i]+" ("+profile.Split.ToString("F1")+"pt)");
                }
                var directions=new List<bool>();
                for(int i=0;i<2;i++)using(var copy=PdfDocument.FromBytes(doc.GetRenderSource().ReadAll(),""))directions.AddRange(WordLayout.Read(copy,i,false,null,true).MarginNotes.Select(n=>n.Item2));
                Assert(directions.Count==2&&directions.Contains(true)&&directions.Contains(false),"Academic profiles: visible vertical notices keep both directions; hidden OCR cannot create a duplicate image");
                OfficeExport.Save(doc,new OfficeExportOptions{Continuous=true,Pages=new[]{0,1,2,3}},Path.Combine(directory,"word-academic.docx"),null);
                Assert(doc.AllText()==original&&!doc.Dirty,"Academic profiles: mixed templates leave the source unchanged");
            }
            using(var doc=PdfDocument.Open(Path.Combine(directory,"word-reflow-source.pdf"),"")) {
                string before=doc.AllText();
                using(var copy=PdfDocument.FromBytes(doc.GetRenderSource().ReadAll(),"")) {
                    var page=WordLayout.Read(copy,0,false,null,true);
                    string text=String.Join("\n",page.Text.Where(t=>t.Text!=null).Select(t=>t.Text));
                    Assert(text.Contains("CONTAINER-LEFT-00")&&text.Contains("CONTAINER-RIGHT-END"),"Research Word: nested, marked body with a substitute font remains editable");
                    Assert(text.Contains("INLINE PROSE remains editable"),"Research Word: an inline large operator does not rasterize ordinary prose");
                    Assert(!text.Contains("CLIPPED VALUE"),"Research Word: partially clipped text is retained as artwork");
                    Assert(page.Pictures.Any(p=>p.Figure)&&!text.Contains("Container plot label"),"Research Word: nested chart labels remain with the figure");
                }
                OfficeExport.Save(doc,new OfficeExportOptions{Continuous=true,Pages=new[]{0}},Path.Combine(directory,"word-reflow.docx"),null);
                Assert(doc.AllText()==before&&!doc.Dirty&&!doc.CanUndo,"Research Word: extracting nested text never changes the open PDF");
            }
            using(var doc=PdfDocument.Open(Path.Combine(directory,"word-latex-source.pdf"),"")) {
                using(var copy=PdfDocument.FromBytes(doc.GetRenderSource().ReadAll(),"")) {
                    var page=WordLayout.Read(copy,0,false,null,true);
                    Assert(page.Text.Any(t=>t.Text!=null&&t.Text.Contains("RESEARCH-LEFT-00")&&t.Font=="Times New Roman"),"Research Word: Computer Modern Roman body is editable prose rather than a formula image");
                }
                OfficeExport.Save(doc,new OfficeExportOptions{Continuous=true,Pages=new[]{0,1}},Path.Combine(directory,"word-latex.docx"),null);
            }
            using(var doc=PdfDocument.Open(Path.Combine(directory,"word-research-source.pdf"),"")) {
                using(var copy=PdfDocument.FromBytes(doc.GetRenderSource().ReadAll(),"")) {
                    var page=WordLayout.Read(copy,0,true,null,true);
                    Assert(page.Columns,"Research Word: recognize staggered scientific columns before extracting formulas");
                    Assert(page.Text.Any(t=>t.Math!=null&&t.Math.Contains("α")&&t.Math.Contains("<m:sSubSup>")),"Research Word: Greek variables retain native editable subscripts and superscripts");
                    Assert(page.Pictures.Any(p=>p.Block&&!p.Figure&&p.Width>180),"Research Word: unsupported sum and its equation number remain one complete image region");
                    Assert(page.Pictures.Any(p=>p.Figure&&p.Block&&p.Width>200)&&!page.Text.Any(t=>t.Text!=null&&t.Text.Contains("Plot label")),"Research Word: chart labels remain attached to their complete figure");
                    WordLayout.Read(copy,1,false);
                }
                Assert(!WordRegions.Columns(OfficeGeometry.Read(doc,1),doc.PageSize(1)),"Research Word: full-width prose is not mistaken for two columns");
                OfficeExport.Save(doc,new OfficeExportOptions{Continuous=true,Equations=true,Pages=new[]{0,1}},Path.Combine(directory,"word-research.docx"),null);
            }
            using(var doc=PdfDocument.Open(Path.Combine(directory,"word-layout-source.pdf"),"")) {
                string original=doc.AllText();
                var flat=OfficeLayout.Read(doc,0,false);
                Assert(flat.Rows.SelectMany(r=>r.Cells).Any(c=>c.Text.Contains("Matrix-scaled")&&Math.Abs(c.Size-12)<.1),"Word: effective font size includes the PDF text matrix");
                var columns=OfficeLayout.ReadingOrder(OfficeLayout.Read(doc,1,false));
                string ordered=String.Join("\n",columns.SelectMany(r=>r.Cells).Select(c=>c.Text));
                Assert(ordered.IndexOf("LEFT11",StringComparison.Ordinal)<ordered.IndexOf("RIGHT00",StringComparison.Ordinal),"Word: long two-column prose reads down the left column first");
                using(var copy=PdfDocument.FromBytes(doc.GetRenderSource().ReadAll(),"")) {
                    var page=WordLayout.Read(copy,0,true);
                    Assert(page.Text.Any(t=>t.Text!=null&&t.Text.Contains("Matrix-scaled"))&&page.Text.Any(t=>t.Text!=null&&t.Text.Contains("中文正文")),"Word: normal Latin and Chinese body text remain editable");
                    Assert(page.Text.Any(t=>t.Bold&&t.Italic),"Word: body text preserves bold and italic styles");
                    Assert(page.Equations>=2&&page.Text.Any(t=>t.Math!=null&&t.Math.Contains("<m:f>")&&t.Math.Contains("<m:sSubSup>")),"Word: symbol geometry creates an editable fraction with subscript and superscript");
                    Assert(!page.Text.Any(t=>t.Text!=null&&(t.Text.Contains("HIDDEN VALUE")||t.Text.Contains("COVERED VALUE"))),"Word: hidden and visually covered text is not exposed as editable body text");
                    Assert(page.Pictures.Count>0,"Word: ambiguous multi-line math uses a cropped high-resolution image");
                }
                var selection=new[]{0,1,2,3,0};
                OfficeExport.Save(doc,new OfficeExportOptions{Layout=true,Pages=selection},Path.Combine(directory,"word-layout.docx"),null);
                OfficeExport.Save(doc,new OfficeExportOptions{Continuous=true,Equations=true,Pages=new[]{0}},Path.Combine(directory,"word-equations.docx"),null);
                OfficeExport.Save(doc,new OfficeExportOptions{Pages=new[]{1}},Path.Combine(directory,"word-columns.docx"),null);
                OfficeExport.Save(doc,new OfficeExportOptions{Continuous=true,Equations=true,Pages=new[]{0,1,2,3}},Path.Combine(directory,"word-continuous.docx"),null);
                Assert(doc.AllText()==original&&!doc.Dirty&&!doc.CanUndo,"Word: positioned export and repeated pages leave the source and undo state unchanged");
                string output=Path.Combine(directory,"word-preserved.docx");File.WriteAllText(output,"existing file");
                bool rejected=false;try{OfficeExport.Save(doc,new OfficeExportOptions{Layout=true,Pages=new[]{0},MathSettings=new OfficeMathSettings{ScriptRatio=Single.NaN}},output,null);}catch(InvalidOperationException){rejected=true;}
                Assert(rejected&&File.ReadAllText(output)=="existing file"&&!Directory.GetFiles(directory,".minipdf-export-*.tmp").Any(),"Word: invalid rule settings preserve an existing destination and remove temporary output");
                bool collisionsSafe=true;
                foreach(bool positioned in new[]{false,true}) {
                    string occupied=Path.Combine(directory,"word-owned-by-user.tmp");File.WriteAllText(occupied,"user data");
                    try{OfficeExport.Save(doc,new OfficeExportOptions{Layout=positioned,Pages=new[]{0},TempPath=occupied},Path.Combine(directory,"word-collision.docx"),null);}catch(IOException){}
                    collisionsSafe&=File.Exists(occupied)&&File.ReadAllText(occupied)=="user data";
                }
                Assert(collisionsSafe,"Word: an existing temporary-path file is never deleted or overwritten");
                if(withUi) {
                    using(var window=new OfficeWindow(doc,false)) {
                        window.Show();Application.DoEvents();
                        var wait=System.Diagnostics.Stopwatch.StartNew();while(!window.PreviewReadyForTest&&wait.ElapsedMilliseconds<10000){Application.DoEvents();System.Threading.Thread.Sleep(20);}
                        Assert(window.PreviewReadyForTest,"Word: source preview completes in the isolated raster worker");
                        var mode=window.Controls.Find("WordExportMode",true).OfType<ComboBox>().Single();
                        Assert(mode.SelectedIndex==0&&mode.Items[0].ToString().Contains("Preserve layout"),"Word: the bilingual dialog defaults to preserving page layout");
                        using(var shot=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(shot,new Rectangle(0,0,window.Width,window.Height));shot.Save(Path.Combine(directory,"word-export-window.png"));}
                        window.Close();
                    }
                    using(var window=new OfficeMathWindow(new OfficeMathSettings())) {
                        window.Show();Application.DoEvents();
                        using(var shot=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(shot,new Rectangle(0,0,window.Width,window.Height));shot.Save(Path.Combine(directory,"word-equation-settings.png"));}
                        window.Close();
                    }
                    foreach(bool english in new[]{true,false}) {
                        UiText.English=english;
                        using(var progress=new OfficeProgress(doc,new OfficeExportOptions{Pages=new[]{0}},Path.Combine(directory,"layout-only.docx"))) {
                            var label=progress.Controls.Find("ConversionStatus",true).OfType<Label>().Single();
                            var bar=progress.Controls.Find("ConversionProgress",true).OfType<ProgressBar>().Single();
                            label.Text=UiText.Get("正在转换…")+" 1234 / 2000";
                            foreach(float size in new[]{10f,14f,18f})using(var font=new Font("Segoe UI",size)) {
                                progress.Font=font;progress.PerformLayout();label.Parent.PerformLayout();
                                var required=label.GetPreferredSize(new Size(label.Width,0));
                                Assert(label.Height>=required.Height&&label.Bottom<=bar.Top,
                                    "Office progress: complete "+(english?"English":"Chinese")+" status and separate progress bar at "+size+"pt");
                            }
                        }
                    }
                    UiText.English=true;
                    string worker=Path.Combine(directory,"word-layout-worker.docx");
                    using(var progress=new OfficeProgress(doc,new OfficeExportOptions{Continuous=true,Equations=true,Pages=new[]{0},MathSettings=new OfficeMathSettings{SymbolGap=.75f}},worker)) {
                        progress.Show();var watch=System.Diagnostics.Stopwatch.StartNew();
                        while(progress.DialogResult==DialogResult.None&&!progress.IsDisposed&&watch.ElapsedMilliseconds<20000){Application.DoEvents();System.Threading.Thread.Sleep(20);}
                        Assert(progress.DialogResult==DialogResult.OK&&File.Exists(worker),"Word: the isolated worker receives continuous mode and custom offline formula settings");progress.Close();
                    }
                }
            }
        }
        internal static int RunOfficeForms(string directory,bool withUi=true){Directory.CreateDirectory(directory);logPath=Path.Combine(directory,"office-forms-test.txt");log.Clear();assertions=0;try{OfficeFormsCoverage(directory,withUi);log.AppendLine("TOTAL: "+assertions+" Office/form assertions, 0 failures");File.WriteAllText(logPath,log.ToString());return 0;}catch(Exception e){log.AppendLine(e.ToString());File.WriteAllText(logPath,log.ToString());return 1;}}
    }
}
