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
        DocumentTab activeTab;
        bool changingTabs,openingFiles;
        int unnamedNumber;
        System.Windows.Forms.Timer openTimer;

        void InitializeDocumentTabs(){
            documentTabs=new TabControl{Name="DocumentTabs",Dock=DockStyle.Top,Height=28,SizeMode=TabSizeMode.Fixed,DrawMode=TabDrawMode.OwnerDrawFixed,ShowToolTips=true,Padding=new Point(6,2)};
            documentTabs.DrawItem+=DrawDocumentTab;
            documentTabs.MouseDown+=delegate(object sender,MouseEventArgs e){for(int i=0;i<documents.Count;i++){var rect=documentTabs.GetTabRect(i);if(rect.Contains(e.Location)&&(e.Button==MouseButtons.Middle||(e.Button==MouseButtons.Left&&e.X>=rect.Right-CloseTabWidth))){CloseTab(i);return;}}};
            documentTabs.SelectedIndexChanged+=delegate{if(!changingTabs&&documentTabs.SelectedIndex>=0&&documentTabs.SelectedIndex<documents.Count)ActivateTab(documents[documentTabs.SelectedIndex]);};
        }
        int CloseTabWidth {get{return Font.Height+8;}}
        void DrawDocumentTab(object sender,DrawItemEventArgs e){if(e.Index<0||e.Index>=documents.Count)return;var rect=e.Bounds;bool selected=e.Index==documentTabs.SelectedIndex;using(var brush=new SolidBrush(selected?Color.FromArgb(204,229,245):Color.FromArgb(232,237,241)))e.Graphics.FillRectangle(brush,rect);var text=new Rectangle(rect.Left+7,rect.Top,Math.Max(1,rect.Width-CloseTabWidth-9),rect.Height);TextRenderer.DrawText(e.Graphics,documents[e.Index].Page.Text,Font,text,Color.FromArgb(30,50,65),TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);int x=rect.Right-CloseTabWidth/2,y=rect.Top+rect.Height/2;using(var pen=new Pen(Color.FromArgb(65,90,110),1.2f)){e.Graphics.DrawLine(pen,x-4,y-4,x+4,y+4);e.Graphics.DrawLine(pen,x-4,y+4,x+4,y-4);}}
        void UpdateTabStrip(){if(documentTabs==null)return;foreach(var tab in documents){string name=String.IsNullOrEmpty(tab.Document.FilePath)?(string)tab.Page.Tag:Path.GetFileName(tab.Document.FilePath);tab.Page.Text=(tab.Document.Dirty?"* ":"")+name;tab.Page.ToolTipText=String.IsNullOrEmpty(tab.Document.FilePath)?name:tab.Document.FilePath;}documentTabs.Visible=documents.Count>1&&!readingMode&&!fullScreen;documentTabs.ItemSize=new Size(Math.Max(140,Math.Min(260,ClientSize.Width/Math.Max(1,Math.Min(4,documents.Count))-8)),Math.Max(22,Font.Height+6));documentTabs.Height=documentTabs.ItemSize.Height+4;documentTabs.Invalidate();}
        void RememberActiveTab(){if(activeTab==null||Document==null)return;activeTab.Layout=canvas.LayoutMode;activeTab.Continuous=canvas.Continuous;activeTab.Zoom=canvas.Zoom;activeTab.Fit=canvas.Fit;activeTab.Rotation=canvas.ViewRotation;activeTab.Scroll=new Point(-canvas.AutoScrollPosition.X,-canvas.AutoScrollPosition.Y);activeTab.Tool=readingMode?oldInteraction:canvas.Mode;activeTab.Ink=Ink;activeTab.Width=LineWidth;activeTab.Editor=readingMode||fullScreen?oldEditbar:editbar.Visible;activeTab.Search=query.Text;activeTab.SearchQuery=searchQuery;activeTab.Hit=lastHit;activeTab.PendingImage=pendingImage;}
        void TrackDocument(PdfDocument doc){var tab=new DocumentTab{Document=doc,Page=new TabPage{Tag="未命名 "+(++unnamedNumber)}};changingTabs=true;try{documents.Add(tab);documentTabs.TabPages.Add(tab.Page);}finally{changingTabs=false;}ActivateTab(tab);}
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
            if(index<0||index>=documents.Count)return false;var tab=documents[index];
            if(tab.Document.Dirty){ActivateTab(tab);if(!ConfirmClose())return false;}
            bool current=tab==activeTab;
            changingTabs=true;try{documents.RemoveAt(index);documentTabs.TabPages.Remove(tab.Page);}finally{changingTabs=false;}
            if(current){activeTab=null;Document=null;canvas.ResetDocumentView();if(documents.Count>0)ActivateTab(documents[Math.Min(index,documents.Count-1)]);else{lastHit=null;UpdateDocument(true);Report("打开 PDF，开始阅读或编辑");}}
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
    }
}
