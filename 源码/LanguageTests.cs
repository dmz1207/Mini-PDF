using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MiniPdf {
    internal static partial class SelfTest {
        static IEnumerable<ToolStripItem> MenuItems(ToolStripItemCollection items){foreach(ToolStripItem item in items){yield return item;var sub=item as ToolStripDropDownItem;if(sub!=null&&sub.HasDropDownItems)foreach(var child in MenuItems(sub.DropDownItems))yield return child;}}
        static void LanguageUi(string directory){
            string external=Path.Combine(directory,"external.pdf");if(!File.Exists(external)){log.AppendLine("SKIP: language UI fixtures missing; run verify_pdf.py fixtures first");return;}
            UiText.English=true;
            using(var form=new MainWindow(null)){
                form.Show();Application.DoEvents();var menu=form.MenuButtonForTest;
                Assert(form.EnglishItemForTest.Checked&&!form.ChineseItemForTest.Checked&&menu.DropDownItems[0].Text=="&File","fresh window opens with English menus selected");
                var languages=form.EnglishItemForTest.Owner.Items;Assert(languages.Count==2&&languages[0]==form.EnglishItemForTest&&languages[1]==form.ChineseItemForTest&&languages[1].Text=="简体中文","language menu lists English first and Simplified Chinese second");
                bool english=true;foreach(var item in MenuItems(menu.DropDownItems))if(item.Text!="简体中文"&&Regex.IsMatch(item.Text,"[\\u4e00-\\u9fff]"))english=false;Assert(english,"every default command and nested menu has an English label");
                var help=(ToolStripMenuItem)menu.DropDownItems[menu.DropDownItems.Count-1];Assert(help.DropDownItems[0].Text=="User guide (English)"&&help.DropDownItems[1].Text=="User guide (Simplified Chinese)","Help lists English guide first and Chinese guide second");
                Assert(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"UserGuide.en.txt"))&&File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"UserGuide.en.txt")).Contains("START AND LANGUAGE"),"English help is included in the actual application payload");
                bool englishHelp=false;using(var timer=new Timer{Interval=60}){timer.Tick+=delegate{foreach(Form window in Application.OpenForms)if(window!=form&&window.Text=="Mini PDF User Guide"){var text=window.Controls[0] as TextBox;englishHelp=text!=null&&text.ReadOnly&&text.Text.Contains("START AND LANGUAGE");timer.Stop();window.Close();break;}};timer.Start();help.DropDownItems[0].PerformClick();}Assert(englishHelp,"English help menu item opens the real embedded English guide");
                bool chineseHelp=false;using(var timer=new Timer{Interval=60}){timer.Tick+=delegate{foreach(Form window in Application.OpenForms)if(window!=form&&window.Text=="Mini PDF 使用说明"){var text=window.Controls[0] as TextBox;chineseHelp=text!=null&&text.ReadOnly&&text.Text.Contains("Ctrl+O")&&text.Text.Contains("保存");timer.Stop();window.Close();break;}};timer.Start();help.DropDownItems[1].PerformClick();}Assert(chineseHelp,"second help menu item opens the real Chinese guide while interface is English");
                string chinesePath=Path.Combine(directory,"菜单 文档.pdf");File.Copy(external,chinesePath,true);form.OpenDocument(chinesePath);var document=form.Document;document.AddText("菜单 原文不变",55,425,12,"自动",Color.Black);form.OpenDocument(Path.Combine(directory,"mixed-sizes.pdf"));form.SelectTabForTest(0);form.Navigate(1);form.ApplyZoom("123.5%");form.SetTool(Tool.Object);form.LineWidth=3;var view=form.CanvasForTest;var position=view.AutoScrollPosition;IntPtr handle=document.Handle;string contents=document.AllText();
                var query=(ToolStripTextBox)typeof(MainWindow).GetField("query",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(form);query.Text="菜单";
                form.ChineseItemForTest.PerformClick();Application.DoEvents();Assert(!UiText.English&&form.ChineseItemForTest.Checked&&menu.DropDownItems[0].Text=="文件(&F)","Chinese language menu item updates the existing window immediately");
                Assert(form.Document==document&&document.Handle==handle&&document.Index==1&&document.Dirty&&document.CanUndo&&Math.Abs(view.Zoom-1.235f)<.00001&&Math.Abs(view.AutoScrollPosition.Y-position.Y)<=1,"language switch preserves document, native handle, page, exact zoom, scroll and undo state");
                Assert(query.Text=="菜单"&&document.FilePath==chinesePath&&document.AllText()==contents,"language switch never translates search text, filenames or PDF contents");
                Assert(Math.Abs(form.LineWidth-3)<.01&&view.Mode==Tool.Object,"language switch preserves editing tool and stroke width");
                bool chineseWidth=false;foreach(ToolStripItem item in form.ToolbarForTest.Items)if(item.Text=="适宽")chineseWidth=true;Assert(chineseWidth,"Chinese toolbar keeps the dedicated fit-width label");
                form.EnglishItemForTest.PerformClick();Application.DoEvents();Assert(UiText.English&&form.EnglishItemForTest.Checked&&menu.DropDownItems[0].Text=="&File","English language item switches back without reopening documents");
                bool englishWidth=false;foreach(ToolStripItem item in form.ToolbarForTest.Items)if(item.Text=="Fit Width")englishWidth=true;Assert(englishWidth&&query.Text=="菜单","English toolbar keeps Fit Width and preserves untranslated user input");
                using(var context=form.BuildContext(new Point(100,100))){bool translated=true;foreach(var item in MenuItems(context.Items))if(Regex.IsMatch(item.Text,"[\\u4e00-\\u9fff]"))translated=false;Assert(translated,"English context menu also localizes nested reading and editing commands");}
                using(var dialog=new TextDialog("Add text","菜单",12,Color.Black,false)){UiText.Apply(dialog);Assert(dialog.Value=="菜单"&&dialog.Face=="自动","English editor keeps user text and resolves Auto to the original font identifier");document.OpenPage(0);document.AddText(dialog.Value,55,380,12,dialog.Face,Color.Black);Assert(document.AllText().Contains("菜单"),"English text dialog font choice inserts real Chinese text correctly");}
                document.OpenPage(1);using(var timer=AnswerSavePrompt(2,"Unsaved changes"))Assert(!form.CloseTabForTest(0)&&form.TabCountForTest==2&&document.Dirty,"English native save prompt can cancel closing without losing changes");
                uint before=N.GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0);for(int i=0;i<12;i++){form.SetLanguageForTest(i%2==0);Application.DoEvents();}form.SetLanguageForTest(true);Assert(N.GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0)<=before+20&&form.Document==document&&document.Dirty,"repeated language switching retains documents and bounds GDI resource use");
                using(var preview=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(preview,new Rectangle(0,0,preview.Width,preview.Height));preview.Save(Path.Combine(directory,"english-ui-preview.png"),ImageFormat.Png);}
                document.Save(Path.Combine(directory,"language-ui.pdf"));form.Close();Application.DoEvents();
            }
            UiText.English=false;
        }
    }
}
