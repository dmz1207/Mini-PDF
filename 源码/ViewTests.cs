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
        static void Command(MainWindow form,Keys key){var method=typeof(MainWindow).GetMethod("ProcessCmdKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);method.Invoke(form,new object[]{new Message(),key});if(key!=Keys.F10)Application.DoEvents();}
        static Timer AnswerSavePrompt(int answer,string title="未保存的修改"){var timer=new Timer{Interval=50};timer.Tick+=delegate{IntPtr dialog=IntPtr.Zero;while((dialog=FindWindowEx(IntPtr.Zero,dialog,"#32770",title))!=IntPtr.Zero){uint id;GetWindowThreadProcessId(dialog,out id);if(id==(uint)System.Diagnostics.Process.GetCurrentProcess().Id){timer.Stop();N.SendMessage(GetDlgItem(dialog,answer),0xf5,IntPtr.Zero,IntPtr.Zero);break;}}};timer.Start();return timer;}
        static void TabMouse(TabControl tabs,int message,Point point,int buttons){N.SendMessage(tabs.Handle,(uint)message,new IntPtr(buttons),Position(point));if(message==0x202||message==0x208)Application.DoEvents();}
        static void HoverTabEdge(MainWindow form,PdfDocument document,int target){var timer=(Timer)typeof(MainWindow).GetField("tabDragScroll",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(form);if(timer==null||!timer.Enabled)throw new Exception("Edge drag timer did not start");var tick=typeof(Timer).GetMethod("OnTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);for(int step=0;step<form.TabCountForTest+1&&form.TabDocumentForTest(target)!=document;step++)tick.Invoke(timer,new object[]{EventArgs.Empty});}
        static void DragTab(MainWindow form,int from,int to){var tabs=form.DocumentTabsForTest;var start=tabs.GetTabRect(from);var end=tabs.GetTabRect(to);TabMouse(tabs,0x201,new Point(start.Left+24,start.Top+start.Height/2),1);TabMouse(tabs,0x200,new Point(end.Left+(to>from?end.Width*4/5:end.Width/5),end.Top+end.Height/2),1);TabMouse(tabs,0x202,new Point(end.Left+end.Width/2,end.Top+end.Height/2),0);}
        static void HeaderDragging(string directory){
            if(!File.Exists(Path.Combine(directory,"external.pdf"))||!File.Exists(Path.Combine(directory,"mixed-sizes.pdf"))){log.AppendLine("SKIP: tab header fixtures missing; run verify_pdf.py fixtures first");return;}
            using(var form=new MainWindow(null)){
                form.Show();form.OpenDocument(Path.Combine(directory,"external.pdf"));Application.DoEvents();var first=form.Document;var view=form.CanvasForTest;var menu=form.MenuButtonForTest;
                Assert(menu.Owner==form.ToolbarForTest&&menu.Width>=34&&menu.Height>=26&&menu.Image.Width>=24,"single-document menu has enlarged icon and click target without a tab row");
                Assert(form.ToolbarForTest.Font.Size>=10&&form.ToolbarForTest.ImageScalingSize.Width>=18&&menu.DropDown.Font.Size>=10,"toolbar text, common icons and menu text are larger");
                first.AddText("KEEP_UNSAVED_WHILE_DRAGGING",55,430,12,"自动",Color.Black);form.Navigate(1);form.ApplyZoom("157.75%");view.AutoScrollPosition=new Point(30,-view.AutoScrollPosition.Y+60);var savedScroll=view.AutoScrollPosition;
                form.OpenDocument(Path.Combine(directory,"mixed-sizes.pdf"));var second=form.Document;form.OpenDocument(Path.Combine(directory,"baseline.pdf"));var third=form.Document;Application.DoEvents();var tabs=form.DocumentTabsForTest;
                Assert(tabs.PointToScreen(Point.Empty).Y<form.ToolbarForTest.PointToScreen(Point.Empty).Y&&menu.Owner.Parent==tabs.Parent,"document tabs stay above reading toolbar with menu beside them");
                Assert(menu.Owner.PointToScreen(Point.Empty).X<tabs.PointToScreen(Point.Empty).X&&Math.Abs(menu.Owner.PointToScreen(Point.Empty).Y-tabs.PointToScreen(Point.Empty).Y)<=2,"enlarged menu sits at left of top document tab row");
                Assert(tabs.Parent.Height<=Math.Max(36,form.Font.Height+8)&&form.ToolbarForTest.Height<=Math.Max(36,form.Font.Height+8),"multi-document header uses two compact rows ("+tabs.Parent.Height+" / "+form.ToolbarForTest.Height+")");
                Command(form,Keys.F10);Assert(menu.DropDown.Visible,"F10 opens the menu in the document header");form.CloseMainMenuForTest();
                DragTab(form,0,2);
                Assert(form.TabDocumentForTest(0)==second&&form.TabDocumentForTest(1)==third&&form.TabDocumentForTest(2)==first,"real mouse drag moves first tab after the third tab");
                Assert(form.Document==first&&first.Dirty&&first.CanUndo&&first.Index==1&&Math.Abs(view.Zoom-1.5775f)<.00001&&Math.Abs(view.AutoScrollPosition.Y-savedScroll.Y)<=1,"dragging preserves selected PDF, unsaved undo history, page, exact zoom and scroll");
                Assert(!form.DraggingTabForTest&&!tabs.Capture,"releasing tab drag clears mouse capture");
                DragTab(form,2,0);Assert(form.TabDocumentForTest(0)==first&&form.TabDocumentForTest(1)==second&&form.TabDocumentForTest(2)==third,"real mouse drag also reorders tabs to the left");
                Command(form,Keys.Control|Keys.Tab);bool next=form.Document==second;Command(form,Keys.Control|Keys.Tab);bool nextAgain=form.Document==third;Command(form,Keys.Control|Keys.Shift|Keys.Tab);Assert(next&&nextAgain&&form.Document==second,"keyboard tab traversal follows the dragged order");
                var rect=tabs.GetTabRect(1);var point=new Point(rect.Left+24,rect.Top+rect.Height/2);TabMouse(tabs,0x201,point,1);TabMouse(tabs,0x200,new Point(point.X+1,point.Y),1);TabMouse(tabs,0x202,point,0);Assert(form.TabDocumentForTest(0)==first&&form.TabDocumentForTest(1)==second&&form.TabCountForTest==3,"ordinary click and minor pointer movement do not reorder or close tabs");
                TabMouse(tabs,0x201,point,1);TabMouse(tabs,0x200,new Point(point.X+24,point.Y),1);Command(form,Keys.Escape);Assert(!form.DraggingTabForTest&&!tabs.Capture,"Escape stops an active tab drag");
                TabMouse(tabs,0x201,point,1);TabMouse(tabs,0x200,new Point(point.X+24,point.Y),1);tabs.Capture=false;Application.DoEvents();Assert(!form.DraggingTabForTest,"capture loss safely cancels a tab drag");
                TabMouse(tabs,0x201,point,1);TabMouse(tabs,0x200,new Point(-40,tabs.Height+30),1);TabMouse(tabs,0x202,new Point(-40,tabs.Height+30),0);Assert(form.TabCountForTest==3&&form.TabDocumentForTest(1)==second&&!form.DraggingTabForTest,"releasing outside tab row keeps documents intact");
                form.ToggleReadingMode();Assert(form.ReadingModeForTest&&!form.TabStripVisibleForTest,"reading mode hides the combined menu and tab header");form.ToggleReadingMode();Assert(form.TabStripVisibleForTest&&menu.Owner.Parent==tabs.Parent,"leaving reading mode restores top tabs and enlarged menu");
                using(var preview=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(preview,new Rectangle(0,0,preview.Width,preview.Height));preview.Save(Path.Combine(directory,"draggable-tabs-preview.png"),ImageFormat.Png);}
                rect=tabs.GetTabRect(2);point=new Point(rect.Right-(form.Font.Height+8)/2,rect.Top+rect.Height/2);TabMouse(tabs,0x201,point,1);TabMouse(tabs,0x202,point,0);Assert(form.TabCountForTest==2&&form.TabDocumentForTest(0)==first&&form.TabDocumentForTest(1)==second,"tab close button still closes correct document after reordering");
                rect=tabs.GetTabRect(1);point=new Point(rect.Left+24,rect.Top+rect.Height/2);TabMouse(tabs,0x207,point,16);TabMouse(tabs,0x208,point,0);Assert(form.TabCountForTest==1&&form.Document==first&&menu.Owner==form.ToolbarForTest&&!form.TabStripVisibleForTest,"middle-click close returns enlarged menu to compact single-document toolbar");
                uint gdi=N.GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0);for(int i=0;i<8;i++){form.OpenDocument(Path.Combine(directory,"mixed-sizes.pdf"));form.OpenMainMenuForTest();form.CloseMainMenuForTest();form.CloseTabForTest(1);Application.DoEvents();}Assert(N.GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0)<=gdi+20&&menu.Owner==form.ToolbarForTest,"moving menu between headers repeatedly retains menu lifetime and bounds GDI resources");
                var normalSize=form.ClientSize;form.ClientSize=new Size(680,500);Application.DoEvents();Assert(menu.Available&&!menu.IsOnOverflow&&form.ToolbarForTest.Height<=Math.Max(36,form.Font.Height+8),"larger menu remains accessible at minimum window width without another toolbar row");form.ClientSize=normalSize;
                byte[] external=File.ReadAllBytes(Path.Combine(directory,"external.pdf"));for(int i=0;i<9;i++)form.LoadForTest(PdfDocument.FromBytes(external,""));form.SelectTabForTest(0);Application.DoEvents();
                rect=tabs.GetTabRect(0);point=new Point(rect.Left+24,rect.Top+rect.Height/2);TabMouse(tabs,0x201,point,1);var right=new Point(tabs.ClientSize.Width-3,point.Y);TabMouse(tabs,0x200,right,1);HoverTabEdge(form,first,9);TabMouse(tabs,0x202,right,0);Assert(form.TabDocumentForTest(9)==first&&form.TabCountForTest==10&&form.Document==first,"drag near right edge scrolls overflowing tabs and can move first tab to the end");
                rect=tabs.GetTabRect(9);point=new Point(rect.Left+24,rect.Top+rect.Height/2);TabMouse(tabs,0x201,point,1);var left=new Point(1,point.Y);TabMouse(tabs,0x200,left,1);HoverTabEdge(form,first,0);TabMouse(tabs,0x202,left,0);Assert(form.TabDocumentForTest(0)==first&&form.Document==first&&first.Dirty,"drag near left edge scrolls back and preserves the unsaved document");
                for(int i=form.TabCountForTest-1;i>0;i--)form.CloseTabForTest(i);first.Undo();Assert(!first.Dirty&&!first.AllText().Contains("KEEP_UNSAVED_WHILE_DRAGGING"),"original undo history remains usable after repeated drag and overflow operations");form.Close();Application.DoEvents();
            }
        }
        internal static int RunHeader(string directory){logPath=Path.Combine(directory,"header-test.txt");try{UiText.English=false;HeaderDragging(directory);log.AppendLine("TOTAL: "+assertions+" header assertions, 0 failures");File.WriteAllText(logPath,log.ToString());return 0;}catch(Exception e){log.AppendLine(e.ToString());File.WriteAllText(logPath,log.ToString());return 1;}}
        static void TabsAndFit(string directory){
            string external=Path.Combine(directory,"external.pdf"),mixed=Path.Combine(directory,"mixed-sizes.pdf");
            if(!File.Exists(external)||!File.Exists(mixed)||!File.Exists(Path.Combine(directory,"large-200.pdf"))){log.AppendLine("SKIP: tab and fit fixtures missing; run verify_pdf.py fixtures first");return;}
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
                var toolbar=(ToolStrip)typeof(MainWindow).GetField("toolbar",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(form);ToolStripButton fitWidth=null;foreach(ToolStripItem item in toolbar.Items)if(item.AccessibleName=="适宽")fitWidth=item as ToolStripButton;form.ApplyZoom("87%");if(fitWidth!=null)fitWidth.PerformClick();Application.DoEvents();Assert(fitWidth!=null&&fitWidth.Available&&fitWidth.Overflow==ToolStripItemOverflow.Never&&view.Fit==1&&Math.Abs(view.PageRectangle.Right-view.ClientSize.Width)<=1,"dedicated fit-width toolbar button remains available and performs Ctrl+2 action");
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
