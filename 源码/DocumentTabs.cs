using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed partial class MainWindow {
        sealed class DocumentTab {
            internal PdfDocument Document;
            internal TabPage Page;
            internal int Number;
            internal PageLayout Layout=PageLayout.Single;
            internal bool Continuous=true,Editor;
            internal float Zoom=1,Width=1.6f;
            internal int Fit=2,Rotation;
            internal Point Scroll;
            internal Tool Tool=Tool.Read;
            internal Color Ink=Color.FromArgb(24,117,152);
            internal string Search="",SearchQuery="",PendingImage;
            internal SearchHit Hit;
        }
        readonly List<DocumentTab> documents=new List<DocumentTab>();
        readonly Queue<string[]> openRequests=new Queue<string[]>();
        TabControl documentTabs;
        Panel documentHeader;
        ToolStrip documentMenu;
        DocumentTab activeTab;
        DocumentTab draggedTab;
        Point tabDragStart,tabDragPoint;
        bool draggingTab,movingTab;
        int tabGrabOffset;
        DocumentTab[] tabDragOrder;
        bool changingTabs,openingFiles;
        int unnamedNumber;
        System.Windows.Forms.Timer openTimer;
        System.Windows.Forms.Timer tabDragScroll;

        void InitializeDocumentTabs(){
            documentHeader=new TitlePanel{Name="DocumentHeader",Dock=DockStyle.Top,Height=32,BackColor=Color.White};
            documentHeader.Paint+=delegate(object sender,PaintEventArgs e){if(documents.Count==0)TextRenderer.DrawText(e.Graphics,"Mini PDF",Font,new Rectangle(documentMenu.Width+6,0,180,documentHeader.Height),SystemColors.ControlText,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);};
            documentMenu=Bar();documentMenu.Name="DocumentMenu";documentMenu.Dock=DockStyle.Left;documentMenu.AutoSize=false;documentMenu.Width=mainMenuButton.Width+2;documentMenu.Padding=Padding.Empty;documentMenu.LayoutStyle=ToolStripLayoutStyle.HorizontalStackWithOverflow;
            documentTabs=new TitleTabs{Name="DocumentTabs",Dock=DockStyle.Fill,Height=28,SizeMode=TabSizeMode.Fixed,DrawMode=TabDrawMode.OwnerDrawFixed,ShowToolTips=true,Padding=new Point(6,2)};
            documentHeader.Controls.Add(documentTabs);documentHeader.Controls.Add(documentMenu);InitializeCaptionButtons();
            documentTabs.DrawItem+=DrawDocumentTab;((TitleTabs)documentTabs).PaintOverlay=PaintDraggedTab;
            documentTabs.MouseDown+=TabMouseDown;documentTabs.MouseMove+=TabMouseMove;documentTabs.MouseUp+=delegate{CancelTabDrag();};documentTabs.MouseCaptureChanged+=delegate{if(!documentTabs.Capture&&!movingTab)CancelTabDrag();};
            documentTabs.SelectedIndexChanged+=delegate{if(!changingTabs&&documentTabs.SelectedIndex>=0&&documentTabs.SelectedIndex<documents.Count)ActivateTab(documents[documentTabs.SelectedIndex]);};
        }
        Rectangle DocumentTabRectangle(int index){return ((TitleTabs)documentTabs).ItemBounds(index);}
        int TabAt(Point point){if(!documentTabs.ClientRectangle.Contains(point))return -1;for(int i=0;i<documents.Count;i++)if(DocumentTabRectangle(i).Contains(point))return i;return -1;}
        void TabMouseDown(object sender,MouseEventArgs e){
            CancelTabDrag();int index=TabAt(e.Location);if(index<0)return;var rect=DocumentTabRectangle(index);
            if(e.Button==MouseButtons.Middle||(e.Button==MouseButtons.Left&&e.X>=rect.Right-CloseTabWidth)){CloseTab(index);return;}
            if(e.Button==MouseButtons.Left){ActivateTab(documents[index]);draggedTab=documents[index];tabDragOrder=documents.ToArray();tabGrabOffset=e.X-rect.Left;tabDragStart=tabDragPoint=e.Location;documentTabs.Capture=true;}
        }
        void TabMouseMove(object sender,MouseEventArgs e){
            if(draggedTab==null)return;tabDragPoint=e.Location;
            if(!draggingTab){var threshold=new Rectangle(tabDragStart.X-SystemInformation.DragSize.Width/2,tabDragStart.Y-SystemInformation.DragSize.Height/2,SystemInformation.DragSize.Width,SystemInformation.DragSize.Height);if(threshold.Contains(e.Location))return;draggingTab=true;documentTabs.Cursor=Cursors.SizeWE;if(tabDragScroll==null){tabDragScroll=new System.Windows.Forms.Timer{Interval=140};tabDragScroll.Tick+=delegate{ScrollTabDrag();};}tabDragScroll.Start();}
            if(e.Y<-12||e.Y>documentTabs.Height+12){documentTabs.Invalidate();return;}
            int from=documents.IndexOf(draggedTab),target=-1;
            for(int i=0;i<documents.Count;i++){var r=DocumentTabRectangle(i);if(e.X>=r.Left&&e.X<r.Right){target=i;break;}}
            if(target<0&&e.X<DocumentTabRectangle(0).Left)target=0;
            if(target<0&&e.X>=DocumentTabRectangle(documents.Count-1).Right)target=documents.Count-1;
            if(from>=0&&target>=0&&target!=from){var rect=DocumentTabRectangle(target);if((target<from&&e.X<rect.Left+rect.Width/2)||(target>from&&e.X>rect.Left+rect.Width/2))MoveDocumentTab(from,target);}
            documentTabs.Invalidate();
        }
        void MoveDocumentTab(int from,int target){
            if(from<0||target<0||from>=documents.Count||target>=documents.Count||from==target)return;
            // Keep native tab slots alive while dragging: removing a TabPage releases mouse capture.
            var slots=new TabPage[documents.Count];for(int i=0;i<slots.Length;i++)slots[i]=documentTabs.TabPages[i];
            var tab=documents[from];movingTab=changingTabs=true;try{
                documents.RemoveAt(from);documents.Insert(target,tab);
                for(int i=0;i<slots.Length;i++){documents[i].Page=slots[i];slots[i].Tag=documents[i].Number;}
                if(activeTab!=null)documentTabs.SelectedIndex=documents.IndexOf(activeTab);UpdateTabLabels();
            }finally{changingTabs=movingTab=false;}
            if(draggedTab!=null&&!documentTabs.Capture)documentTabs.Capture=true;documentTabs.Invalidate();
        }
        void ScrollTabDrag(){if(!draggingTab||draggedTab==null||documents.Count<2||tabDragPoint.Y<-documentTabs.Height||tabDragPoint.Y>=documentTabs.Height*2)return;int index=documents.IndexOf(draggedTab);bool overflow=DocumentTabRectangle(0).Left<0||DocumentTabRectangle(documents.Count-1).Right>documentTabs.ClientSize.Width-20;if(!overflow)return;if(tabDragPoint.X<18)MoveDocumentTab(index,index-1);else if(tabDragPoint.X>=documentTabs.ClientSize.Width-34)MoveDocumentTab(index,index+1);}
        void RestoreTabDrag(){if(tabDragOrder!=null){var order=tabDragOrder;for(int i=0;i<order.Length;i++)MoveDocumentTab(documents.IndexOf(order[i]),i);}CancelTabDrag();}
        void CancelTabDrag(){draggedTab=null;draggingTab=false;tabDragOrder=null;if(tabDragScroll!=null)tabDragScroll.Stop();if(documentTabs!=null&&!documentTabs.IsDisposed){documentTabs.Cursor=Cursors.Default;if(documentTabs.Capture)documentTabs.Capture=false;documentTabs.Invalidate();}}
        int CloseTabWidth {get{return Font.Height+8;}}
        static GraphicsPath RoundedTab(Rectangle rect){int radius=Math.Min(8,rect.Height/3),diameter=radius*2;var path=new GraphicsPath();path.AddLine(rect.Left,rect.Bottom,rect.Left,rect.Top+radius);path.AddArc(rect.Left,rect.Top,diameter,diameter,180,90);path.AddLine(rect.Left+radius,rect.Top,rect.Right-radius,rect.Top);path.AddArc(rect.Right-diameter,rect.Top,diameter,diameter,270,90);path.AddLine(rect.Right,rect.Top+radius,rect.Right,rect.Bottom);path.CloseFigure();return path;}
        void PaintDocumentTab(Graphics graphics,DocumentTab tab,Rectangle bounds,bool selected,bool floating){
            var rect=new Rectangle(bounds.Left+1,bounds.Top+1,Math.Max(1,bounds.Width-2),Math.Max(1,bounds.Height-1));graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var shape=RoundedTab(rect))using(var brush=new SolidBrush(selected?Color.FromArgb(62,150,217):Color.FromArgb(156,202,235)))graphics.FillPath(brush,shape);
            var text=new Rectangle(rect.Left+9,rect.Top,Math.Max(1,rect.Width-CloseTabWidth-12),rect.Height);TextRenderer.DrawText(graphics,tab.Page.Text,Font,text,Color.FromArgb(17,40,60),TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
            int x=rect.Right-CloseTabWidth/2,y=rect.Top+rect.Height/2;using(var pen=new Pen(Color.FromArgb(42,82,113),1.4f)){graphics.DrawLine(pen,x-4,y-4,x+4,y+4);graphics.DrawLine(pen,x-4,y+4,x+4,y-4);}
            if(floating)using(var pen=new Pen(Color.FromArgb(26,105,165),1))using(var shape=RoundedTab(rect))graphics.DrawPath(pen,shape);
        }
        void DrawDocumentTab(object sender,DrawItemEventArgs e){if(e.Index<0||e.Index>=documents.Count)return;if(draggingTab&&documents[e.Index]==draggedTab){using(var brush=new SolidBrush(Color.FromArgb(234,243,250)))e.Graphics.FillRectangle(brush,e.Bounds);return;}PaintDocumentTab(e.Graphics,documents[e.Index],e.Bounds,e.Index==documentTabs.SelectedIndex,false);}
        void PaintDraggedTab(Graphics graphics){if(!draggingTab||draggedTab==null)return;int index=documents.IndexOf(draggedTab);if(index<0)return;var rect=DocumentTabRectangle(index);rect.X=Math.Max(0,Math.Min(tabDragPoint.X-tabGrabOffset,documentTabs.ClientSize.Width-rect.Width));PaintDocumentTab(graphics,draggedTab,rect,true,true);}
        void UpdateTabLabels(){foreach(var tab in documents){string name=String.IsNullOrEmpty(tab.Document.FilePath)?UiText.Get("未命名 ")+tab.Number:Path.GetFileName(tab.Document.FilePath);tab.Page.Text=(tab.Document.Dirty?"* ":"")+name;tab.Page.ToolTipText=String.IsNullOrEmpty(tab.Document.FilePath)?name:tab.Document.FilePath;}}
        void UpdateTabStrip(){if(documentTabs==null)return;UpdateTabLabels();bool shown=!readingMode&&!fullScreen;var target=shown?documentMenu:toolbar;if(mainMenuButton.Owner!=target){mainMenuButton.HideDropDown();if(mainMenuButton.Owner!=null)mainMenuButton.Owner.Items.Remove(mainMenuButton);target.Items.Insert(0,mainMenuButton);}documentHeader.Visible=shown;documentTabs.Visible=shown&&documents.Count>0;documentTabs.ItemSize=new Size(Math.Max(140,Math.Min(260,(ClientSize.Width-documentMenu.Width-captionReserve-28)/Math.Max(1,Math.Min(4,documents.Count))-4)),Math.Max(24,Font.Height+3));documentHeader.Height=Math.Max(32,documentTabs.ItemSize.Height+5);RefreshWindowFrame();documentTabs.Invalidate();if(!shown)CancelTabDrag();}
        void RememberActiveTab(){if(activeTab==null||Document==null)return;activeTab.Layout=canvas.LayoutMode;activeTab.Continuous=canvas.Continuous;activeTab.Zoom=canvas.Zoom;activeTab.Fit=canvas.Fit;activeTab.Rotation=canvas.ViewRotation;activeTab.Scroll=new Point(-canvas.AutoScrollPosition.X,-canvas.AutoScrollPosition.Y);activeTab.Tool=readingMode?oldInteraction:canvas.Mode;activeTab.Ink=Ink;activeTab.Width=LineWidth;activeTab.Editor=readingMode||fullScreen?oldEditbar:editbar.Visible;activeTab.Search=query.Text;activeTab.SearchQuery=searchQuery;activeTab.Hit=lastHit;activeTab.PendingImage=pendingImage;}
        void TrackDocument(PdfDocument doc){var tab=new DocumentTab{Document=doc,Number=++unnamedNumber,Page=new TabPage{Tag=unnamedNumber}};changingTabs=true;try{documents.Add(tab);documentTabs.TabPages.Add(tab.Page);}finally{changingTabs=false;}ActivateTab(tab);}
        void ActivateTab(DocumentTab tab){
            if(activeTab==tab)return;if(slideShow)ToggleFullScreen();RememberActiveTab();activeTab=tab;Document=tab.Document;
            canvas.LayoutMode=tab.Layout;canvas.Continuous=tab.Continuous;canvas.Fit=tab.Fit;canvas.Zoom=tab.Zoom;canvas.ViewRotation=tab.Rotation;canvas.ResetDocumentView();
            Ink=tab.Ink;LineWidth=tab.Width;colorButton.ForeColor=Ink;lineWidthBox.SelectedItem=LineWidth.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);query.Text=tab.Search;searchQuery=tab.SearchQuery;lastHit=tab.Hit;pendingImage=tab.PendingImage;findResult.Text="";if(readingMode||fullScreen){oldInteraction=tab.Tool;oldEditbar=tab.Editor;}
            singleItem.Checked=tab.Layout==PageLayout.Single;doubleItem.Checked=tab.Layout==PageLayout.Double;bookItem.Checked=tab.Layout==PageLayout.Book;gridItem.Checked=tab.Layout==PageLayout.Grid;continuousItem.Checked=tab.Continuous;
            changingTabs=true;try{documentTabs.SelectedIndex=documents.IndexOf(tab);}finally{changingTabs=false;}
            SetTool(readingMode||slideShow?Tool.Read:tab.Tool);if(!readingMode&&!fullScreen){editbar.Visible=tab.Editor;editToggle.Checked=tab.Editor;}UpdateDocument(true);canvas.AutoScrollPosition=tab.Scroll;canvas.Focus();
        }
        void NextTab(int direction){if(documents.Count>1)ActivateTab(documents[(documents.IndexOf(activeTab)+direction+documents.Count)%documents.Count]);}
        internal bool CanSaveToPath(string path,bool includeCurrent=false){string full=Path.GetFullPath(path);foreach(var tab in documents)if((includeCurrent||tab!=activeTab)&&!String.IsNullOrEmpty(tab.Document.FilePath)&&String.Equals(full,tab.Document.FilePath,StringComparison.OrdinalIgnoreCase))return false;return true;}
        bool CloseTab(int index){
            CancelTabDrag();
            if(index<0||index>=documents.Count)return false;var tab=documents[index];
            if(tab.Document.Dirty){ActivateTab(tab);if(!ConfirmClose())return false;}
            bool current=tab==activeTab;
            changingTabs=true;try{documents.RemoveAt(index);documentTabs.TabPages.Remove(tab.Page);}finally{changingTabs=false;}
            if(current){activeTab=null;Document=null;canvas.ResetDocumentView();if(documents.Count>0)ActivateTab(documents[Math.Min(index,documents.Count-1)]);else{lastHit=null;UpdateDocument(true);Report(UiText.Get("打开 PDF，开始阅读或编辑"));}}
            tab.Document.Dispose();tab.Page.Dispose();UpdateTabStrip();return true;
        }
        bool ConfirmAllTabs(){foreach(var tab in documents.ToArray())if(tab.Document.Dirty){ActivateTab(tab);if(!ConfirmClose())return false;}return true;}
        internal void OpenDocuments(string[] paths){if(paths==null)return;openingFiles=true;try{foreach(string path in paths)OpenDocument(path);}finally{openingFiles=false;}}
        internal void ReceiveOpenRequest(string[] paths){openRequests.Enqueue(paths);if(openTimer==null){openTimer=new System.Windows.Forms.Timer{Interval=120};openTimer.Tick+=delegate{if(IsDisposed||!Enabled||openingFiles||openRequests.Count==0)return;openTimer.Stop();if(WindowState==FormWindowState.Minimized)N.ShowWindow(Handle,9);Activate();N.SetForegroundWindow(Handle);OpenDocuments(openRequests.Dequeue());if(openRequests.Count>0)openTimer.Start();};}openTimer.Start();}
        void NewWindow(){string executable=Environment.GetEnvironmentVariable("MINIPDF_LAUNCHER");if(String.IsNullOrEmpty(executable)||!File.Exists(executable))executable=Application.ExecutablePath;Process.Start(new ProcessStartInfo(executable,"--new-window"){UseShellExecute=false,WorkingDirectory=Environment.CurrentDirectory});}
        protected override void OnResize(EventArgs e){base.OnResize(e);UpdateTabStrip();}
        internal int TabCountForTest {get{return documents.Count;}}
        internal bool TabStripVisibleForTest {get{return documentTabs.Visible;}}
        internal void SelectTabForTest(int index){documentTabs.SelectedIndex=index;}
        internal bool CloseTabForTest(int index){return CloseTab(index);}
        internal TabControl DocumentTabsForTest {get{return documentTabs;}}
        internal ToolStripDropDownButton MenuButtonForTest {get{return mainMenuButton;}}
        internal ToolStrip ToolbarForTest {get{return toolbar;}}
        internal bool DraggingTabForTest {get{return draggedTab!=null;}}
        internal PdfDocument TabDocumentForTest(int index){return documents[index].Document;}
    }
}
