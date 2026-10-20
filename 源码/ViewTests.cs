using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniPdf {
    internal static partial class SelfTest {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string className,string title);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
        [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr window,int id);
        static void Command(MainWindow form,Keys key){var method=typeof(MainWindow).GetMethod("ProcessCmdKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);method.Invoke(form,new object[]{new Message(),key});Application.DoEvents();}
        static Timer AnswerSavePrompt(int answer){var timer=new Timer{Interval=50};timer.Tick+=delegate{IntPtr dialog=IntPtr.Zero;while((dialog=FindWindowEx(IntPtr.Zero,dialog,"#32770","未保存的修改"))!=IntPtr.Zero){uint id;GetWindowThreadProcessId(dialog,out id);if(id==(uint)System.Diagnostics.Process.GetCurrentProcess().Id){timer.Stop();N.SendMessage(GetDlgItem(dialog,answer),0xf5,IntPtr.Zero,IntPtr.Zero);break;}}};timer.Start();return timer;}
        static void TabsAndFit(string directory){
            string external=Path.Combine(directory,"external.pdf"),mixed=Path.Combine(directory,"mixed-sizes.pdf");
            using(var form=new MainWindow(null)){
                form.Show();form.OpenDocument(external);Application.DoEvents();var view=form.CanvasForTest;var first=form.Document;
                Assert(form.TabCountForTest==1&&!form.TabStripVisibleForTest,"single document keeps tab row hidden to save vertical space");
                first.AddText("UNSAVED_TAB_ONE",55,420,12,"自动",Color.Black);form.Navigate(1);form.SetLayout(PageLayout.Double);view.Continuous=false;form.RotateView(1);form.ApplyZoom("123.45%");view.AutoScrollPosition=new Point(60,120);form.SetTool(Tool.Rectangle);form.LineWidth=3;var scroll=view.AutoScrollPosition;
                form.OpenDocument(mixed);Application.DoEvents();var second=form.Document;
                Assert(form.TabCountForTest==2&&form.TabStripVisibleForTest&&second!=first,"opening another PDF creates a native tab in the same window");
                Assert(view.LayoutRectangles.Length==5&&second.SelectedText(0,200).Contains("MIXED SIZE PAGE 1")&&!second.SelectedText(0,200).Contains("EXTERNAL PAGE"),"canvas displays only selected document pages, never a combination of PDF tabs");
                Assert(!second.Dirty&&view.LayoutMode==PageLayout.Single&&view.ViewRotation==0,"new tab has independent clean document and view");
                Command(form,Keys.Control|Keys.Shift|Keys.Tab);
                Assert(form.Document==first&&first.Index==1&&first.Dirty&&first.CanUndo,"tab switching preserves page and unsaved undo history");
                Assert(view.LayoutRectangles.Length==3&&first.SelectedText(0,200).Contains("EXTERNAL PAGE 2"),"selecting previous tab replaces canvas with only that PDF");
                Assert(view.LayoutMode==PageLayout.Double&&!view.Continuous&&view.ViewRotation==1&&view.Fit==0&&Math.Abs(view.Zoom-1.2345f)<.00001,"tab switching preserves layout, rotation and exact zoom");
                Assert(Math.Abs(view.AutoScrollPosition.Y-scroll.Y)<=1&&view.Mode==Tool.Rectangle&&Math.Abs(form.LineWidth-3)<.01,"tab switching restores scroll, editing tool and stroke width");
                first.Undo();Assert(!first.Dirty&&!first.AllText().Contains("UNSAVED_TAB_ONE"),"undo remains attached to its original tab");
                Command(form,Keys.Control|Keys.Tab);Assert(form.Document==second&&!second.Dirty,"Ctrl+Tab activates next document without affecting it");
                form.OpenDocument(external);Assert(form.TabCountForTest==2&&form.Document==first,"opening an already open file activates its existing tab");
                form.SelectTabForTest(1);Command(form,Keys.Control|Keys.D7);
                Assert(view.LayoutMode==PageLayout.Double&&view.Fit==1,"Ctrl+7 selects automatic double-page width");
                var pair=view.LayoutRectangles;Assert(pair[0].Left<=1&&Math.Abs(pair[1].Right-view.ClientSize.Width)<=2,"double-page fit fills usable width without outer gray strips");
                form.Navigate(2);pair=view.LayoutRectangles;Assert(pair[2].Left<=1&&Math.Abs(pair[3].Right-view.ClientSize.Width)<=2,"mixed-size facing pages fit by their actual combined width");
                form.Navigate(4);Assert(view.PageRectangle.Left<=1&&Math.Abs(view.PageRectangle.Right-view.ClientSize.Width)<=2,"odd final page fits and centers without a phantom empty page");
                Command(form,Keys.Control|Keys.D6);Assert(view.LayoutMode==PageLayout.Single&&view.Fit==2,"Ctrl+6 selects automatic whole single page");
                form.Navigate(0);Command(form,Keys.Control|Keys.D2);Application.DoEvents();
                Assert(view.PageRectangle.Left<=1&&Math.Abs(view.PageRectangle.Right-view.ClientSize.Width)<=1,"single-page fit ignores wider pages elsewhere in the document");
                Assert(view.PointToScreen(Point.Empty).X==view.Parent.PointToScreen(Point.Empty).X,"collapsed sidebar reserves no horizontal strip");
                Assert(view.NarrowVerticalBarForTest&&view.ScrollBarThickness<SystemInformation.VerticalScrollBarWidth,"document uses a narrow native Windows scrollbar");
                foreach(int width in new[]{1001,980,921,1120}){form.ClientSize=new Size(width,660);Application.DoEvents();Assert(view.PageRectangle.Left<=1&&Math.Abs(view.PageRectangle.Right-view.ClientSize.Width)<=1,"fit width follows resize to "+width+" pixels without duplicate scrollbar reserve");}
                form.ToggleSidebar();Application.DoEvents();Assert(Math.Abs(view.PageRectangle.Right-view.ClientSize.Width)<=1,"fit width uses entire area beside expanded sidebar");form.ToggleSidebar();
                form.ApplyZoom("100%");form.ClientSize=new Size(1060,700);Application.DoEvents();Assert(view.Fit==0&&view.Zoom==1,"manual 100 percent remains physically accurate after resize");
                form.SetTool(Tool.Ink);form.ToggleReadingMode();Command(form,Keys.Control|Keys.Shift|Keys.Tab);Assert(form.ReadingModeForTest&&!form.TabStripVisibleForTest,"reading mode hides tab row while retaining native window caption");form.ToggleReadingMode();Assert(view.Mode==Tool.Rectangle&&form.TabStripVisibleForTest,"leaving reading mode restores current tab editing state");
                form.SelectTabForTest(1);Assert(view.Mode==Tool.Ink,"switching tabs in reading mode preserves each tab tool");
                uint gdi=N.GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0);for(int i=0;i<24;i++){form.SelectTabForTest(i%2);Application.DoEvents();}Assert(N.GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0)<=gdi+15,"repeated tab switching releases old render resources");
                form.SetLayout(PageLayout.Double);using(var preview=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(preview,new Rectangle(0,0,preview.Width,preview.Height));preview.Save(Path.Combine(directory,"tabs-preview.png"),ImageFormat.Png);}
                first.AddText("CANCEL_CLOSE",55,400,12,"自动",Color.Black);form.SelectTabForTest(0);using(var timer=AnswerSavePrompt(2))Assert(!form.CloseTabForTest(0)&&form.TabCountForTest==2&&first.Dirty,"canceling native save prompt keeps tab and unsaved data");
                form.SelectTabForTest(1);using(var timer=AnswerSavePrompt(2)){form.Close();Application.DoEvents();}Assert(form.Visible&&form.TabCountForTest==2&&first.Dirty,"window close checks unsaved documents in inactive tabs");
                using(var timer=AnswerSavePrompt(7))Assert(form.CloseTabForTest(0)&&form.TabCountForTest==1&&form.Document==second&&!second.Dirty,"discard confirmation closes only the selected modified tab");
                Command(form,Keys.Control|Keys.F4);Assert(form.TabCountForTest==0&&form.Document==null&&!form.TabStripVisibleForTest,"closing final tab restores empty reader without disposing menus");form.OpenDocument(external);Assert(form.TabCountForTest==1&&form.Document.Count==3,"reader can reopen a document after all tabs close");form.Close();Application.DoEvents();
            }
            using(var form=new MainWindow(null)){form.LoadForTest(PdfDocument.Open(Path.Combine(directory,"large-200.pdf"),""));form.Show();Application.DoEvents();var view=form.CanvasForTest;form.ApplyZoom("100%");N.SendMessage(view.Handle,0x115,new IntPtr(7),view.VerticalBarForTest);Application.DoEvents();Assert(-view.AutoScrollPosition.Y>65535&&form.Document.Index==199,"native scrollbar reaches final page beyond 16-bit scroll range");N.SendMessage(view.Handle,0x115,new IntPtr(6),view.VerticalBarForTest);N.SendMessage(view.Handle,0x20a,new IntPtr(-120*65536),IntPtr.Zero);Application.DoEvents();Assert(view.AutoScrollPosition.Y<0&&view.AutoScrollPosition.Y>-200,"mouse wheel scrolls normally with narrow native scrollbar");form.Close();Application.DoEvents();}
        }
    }
}
