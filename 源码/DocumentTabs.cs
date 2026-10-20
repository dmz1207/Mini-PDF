using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed partial class MainWindow {
        sealed class DocumentTab {
            internal PdfDocument Document;
            internal TabPage Page;
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
        bool draggingTab;
        bool changingTabs,openingFiles;
        int unnamedNumber;
        System.Windows.Forms.Timer openTimer;
        System.Windows.Forms.Timer tabDragScroll;

        void InitializeDocumentTabs(){
            documentHeader=new Panel{Name="DocumentHeader",Dock=DockStyle.Top,Height=28};
            documentMenu=Bar();documentMenu.Name="DocumentMenu";documentMenu.Dock=DockStyle.Left;documentMenu.AutoSize=false;documentMenu.Width=mainMenuButton.Width+2;documentMenu.Padding=Padding.Empty;documentMenu.LayoutStyle=ToolStripLayoutStyle.HorizontalStackWithOverflow;
            documentTabs=new TabControl{Name="DocumentTabs",Dock=DockStyle.Fill,Height=28,SizeMode=TabSizeMode.Fixed,DrawMode=TabDrawMode.OwnerDrawFixed,ShowToolTips=true,Padding=new Point(6,2)};
            documentHeader.Controls.Add(documentTabs);documentHeader.Controls.Add(documentMenu);
            documentTabs.DrawItem+=DrawDocumentTab;
            documentTabs.MouseDown+=TabMouseDown;documentTabs.MouseMove+=TabMouseMove;documentTabs.MouseUp+=delegate{CancelTabDrag();};documentTabs.MouseCaptureChanged+=delegate{if(!documentTabs.Capture)CancelTabDrag();};
            documentTabs.SelectedIndexChanged+=delegate{if(!changingTabs&&documentTabs.SelectedIndex>=0&&documentTabs.SelectedIndex<documents.Count)ActivateTab(documents[documentTabs.SelectedIndex]);};
        }
        int TabAt(Point point){if(!documentTabs.ClientRectangle.Contains(point))return -1;for(int i=0;i<documents.Count;i++)if(documentTabs.GetTabRect(i).Contains(point))return i;return -1;}
        void TabMouseDown(object sender,MouseEventArgs e){CancelTabDrag();int index=TabAt(e.Location);if(index<0)return;var rect=documentTabs.GetTabRect(index);if(e.Button==MouseButtons.Middle||(e.Button==MouseButtons.Left&&e.X>=rect.Right-CloseTabWidth)){CloseTab(index);return;}if(e.Button==MouseButtons.Left){draggedTab=documents[index];tabDragStart=tabDragPoint=e.Location;documentTabs.Capture=true;}}
        void TabMouseMove(object sender,MouseEventArgs e){if(draggedTab==null)return;tabDragPoint=e.Location;if(!draggingTab){var threshold=new Rectangle(tabDragStart.X-SystemInformation.DragSize.Width/2,tabDragStart.Y-SystemInformation.DragSize.Height/2,SystemInformation.DragSize.Width,SystemInformation.DragSize.Height);if(threshold.Contains(e.Location))return;draggingTab=true;documentTabs.Cursor=Cursors.SizeWE;if(tabDragScroll==null){tabDragScroll=new System.Windows.Forms.Timer{Interval=160};tabDragScroll.Tick+=delegate{ScrollTabDrag();};}tabDragScroll.Start();}int from=documents.IndexOf(draggedTab),target=TabAt(e.Location);if(from<0||target<0||target==from)return;var rect=documentTabs.GetTabRect(target);if((target<from&&e.X<rect.Left+rect.Width/2)||(target>from&&e.X>rect.Left+rect.Width/2))MoveDocumentTab(from,target);}
        void MoveDocumentTab(int from,int target){if(from<0||target<0||from>=documents.Count||target>=documents.Count||from==target)return;var tab=documents[from];changingTabs=true;documentTabs.SuspendLayout();try{documents.RemoveAt(from);documents.Insert(target,tab);documentTabs.TabPages.Remove(tab.Page);documentTabs.TabPages.Insert(target,tab.Page);if(activeTab!=null)documentTabs.SelectedTab=activeTab.Page;}finally{documentTabs.ResumeLayout();changingTabs=false;}documentTabs.Invalidate();}
        void ScrollTabDrag(){if(!draggingTab||draggedTab==null||documents.Count<2||tabDragPoint.Y<0||tabDragPoint.Y>=documentTabs.Height)return;int index=documents.IndexOf(draggedTab);bool overflow=documentTabs.GetTabRect(0).Left<0||documentTabs.GetTabRect(documents.Count-1).Right>documentTabs.ClientSize.Width-20;if(!overflow)return;if(tabDragPoint.X<10)MoveDocumentTab(index,index-1);else if(tabDragPoint.X>=documentTabs.ClientSize.Width-34)MoveDocumentTab(index,index+1);}
        void CancelTabDrag(){draggedTab=null;draggingTab=false;if(tabDragScroll!=null)tabDragScroll.Stop();if(documentTabs!=null&&!documentTabs.IsDisposed){documentTabs.Cursor=Cursors.Default;if(documentTabs.Capture)documentTabs.Capture=false;}}
        int CloseTabWidth {get{return Font.Height+8;}}
        void DrawDocumentTab(object sender,DrawItemEventArgs e){if(e.Index<0||e.Index>=documents.Count)return;var rect=e.Bounds;bool selected=e.Index==documentTabs.SelectedIndex;using(var brush=new SolidBrush(selected?Color.FromArgb(204,229,245):Color.FromArgb(232,237,241)))e.Graphics.FillRectangle(brush,rect);var text=new Rectangle(rect.Left+7,rect.Top,Math.Max(1,rect.Width-CloseTabWidth-9),rect.Height);TextRenderer.DrawText(e.Graphics,documents[e.Index].Page.Text,Font,text,Color.FromArgb(30,50,65),TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);int x=rect.Right-CloseTabWidth/2,y=rect.Top+rect.Height/2;using(var pen=new Pen(Color.FromArgb(65,90,110),1.2f)){e.Graphics.DrawLine(pen,x-4,y-4,x+4,y+4);e.Graphics.DrawLine(pen,x-4,y+4,x+4,y-4);}}
        void UpdateTabStrip(){if(documentTabs==null)return;foreach(var tab in documents){string name=String.IsNullOrEmpty(tab.Document.FilePath)?UiText.Get("未命名 ")+(int)tab.Page.Tag:Path.GetFileName(tab.Document.FilePath);tab.Page.Text=(tab.Document.Dirty?"* ":"")+name;tab.Page.ToolTipText=String.IsNullOrEmpty(tab.Document.FilePath)?name:tab.Document.FilePath;}bool shown=documents.Count>1&&!readingMode&&!fullScreen;var target=shown?documentMenu:toolbar;if(mainMenuButton.Owner!=target){mainMenuButton.HideDropDown();if(mainMenuButton.Owner!=null)mainMenuButton.Owner.Items.Remove(mainMenuButton);target.Items.Insert(0,mainMenuButton);}documentHeader.Visible=shown;documentTabs.ItemSize=new Size(Math.Max(140,Math.Min(260,(ClientSize.Width-documentMenu.Width)/Math.Max(1,Math.Min(4,documents.Count))-8)),Math.Max(24,Font.Height+3));documentHeader.Height=Math.Max(30,documentTabs.ItemSize.Height+4);documentTabs.Invalidate();if(!shown)CancelTabDrag();}
        void RememberActiveTab(){if(activeTab==null||Document==null)return;activeTab.Layout=canvas.LayoutMode;activeTab.Continuous=canvas.Continuous;activeTab.Zoom=canvas.Zoom;activeTab.Fit=canvas.Fit;activeTab.Rotation=canvas.ViewRotation;activeTab.Scroll=new Point(-canvas.AutoScrollPosition.X,-canvas.AutoScrollPosition.Y);activeTab.Tool=readingMode?oldInteraction:canvas.Mode;activeTab.Ink=Ink;activeTab.Width=LineWidth;activeTab.Editor=readingMode||fullScreen?oldEditbar:editbar.Visible;activeTab.Search=query.Text;activeTab.SearchQuery=searchQuery;activeTab.Hit=lastHit;activeTab.PendingImage=pendingImage;}
        void TrackDocument(PdfDocument doc){var tab=new DocumentTab{Document=doc,Page=new TabPage{Tag=++unnamedNumber}};changingTabs=true;try{documents.Add(tab);documentTabs.TabPages.Add(tab.Page);}finally{changingTabs=false;}ActivateTab(tab);}
        void ActivateTab(DocumentTab tab){
            if(activeTab==tab)return;if(slideShow)ToggleFullScreen();RememberActiveTab();activeTab=tab;Document=tab.Document;
            canvas.LayoutMode=tab.Layout;canvas.Continuous=tab.Continuous;canvas.Fit=tab.Fit;canvas.Zoom=tab.Zoom;canvas.ViewRotation=tab.Rotation;canvas.ResetDocumentView();
            Ink=tab.Ink;LineWidth=tab.Width;colorButton.ForeColor=Ink;lineWidthBox.SelectedItem=LineWidth.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);query.Text=tab.Search;searchQuery=tab.SearchQuery;lastHit=tab.Hit;pendingImage=tab.PendingImage;findResult.Text="";if(readingMode||fullScreen){oldInteraction=tab.Tool;oldEditbar=tab.Editor;}
            singleItem.Checked=tab.Layout==PageLayout.Single;doubleItem.Checked=tab.Layout==PageLayout.Double;bookItem.Checked=tab.Layout==PageLayout.Book;gridItem.Checked=tab.Layout==PageLayout.Grid;continuousItem.Checked=tab.Continuous;
            changingTabs=true;try{documentTabs.SelectedIndex=documents.IndexOf(tab);}finally{changingTabs=false;}
            SetTool(readingMode||slideShow?Tool.Read:tab.Tool);if(!readingMode&&!fullScreen){editbar.Visible=tab.Editor;editToggle.Checked=tab.Editor;}UpdateDocument(true);canvas.AutoScrollPosition=tab.Scroll;canvas.Focus();
        }
        void NextTab(int direction){if(documents.Count>1)ActivateTab(documents[(documents.IndexOf(activeTab)+direction+documents.Count)%documents.Count]);}
        bool CanSaveToPath(string path){string full=Path.GetFullPath(path);foreach(var tab in documents)if(tab!=activeTab&&!String.IsNullOrEmpty(tab.Document.FilePath)&&String.Equals(full,tab.Document.FilePath,StringComparison.OrdinalIgnoreCase))return false;return true;}
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
