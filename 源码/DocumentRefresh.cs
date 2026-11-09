using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MiniPdf {
    internal struct FileStamp : IEquatable<FileStamp> {
        internal long Length, Written, Created;
        internal bool Exists;
        internal static FileStamp Read(string path) {
            try {
                var file=new FileInfo(path);
                if(file.Exists)return new FileStamp{Exists=true,Length=file.Length,Written=file.LastWriteTimeUtc.Ticks,Created=file.CreationTimeUtc.Ticks};
            }catch(IOException){}catch(UnauthorizedAccessException){}
            return new FileStamp();
        }
        public bool Equals(FileStamp other){return Exists==other.Exists&&Length==other.Length&&Written==other.Written&&Created==other.Created;}
    }

    // Watcher callbacks only signal; all PDFium work stays on the owning UI thread.
    internal sealed class DocumentFileWatch : IDisposable {
        internal readonly string Path;
        internal FileStamp Accepted, Observed;
        internal bool Pending {get;private set;}
        internal string Error="";
        FileSystemWatcher watcher;
        int signals,seen,failures,watcherError;
        long probeAt,quietUntil,retryAt;
        internal static long Now {get{return Stopwatch.GetTimestamp()*1000/Stopwatch.Frequency;}}
        internal DocumentFileWatch(string path,FileStamp stamp){Path=path;Accepted=Observed=stamp;Arm();}
        void Arm(){
            if(watcher!=null)return;
            try {
                watcher=new FileSystemWatcher(System.IO.Path.GetDirectoryName(Path),System.IO.Path.GetFileName(Path)){
                    NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.Size|NotifyFilters.FileName|NotifyFilters.CreationTime};
                watcher.Changed+=Changed;watcher.Created+=Changed;watcher.Deleted+=Changed;
                watcher.Renamed+=delegate{Interlocked.Increment(ref signals);};
                watcher.Error+=delegate{Interlocked.Exchange(ref watcherError,1);Interlocked.Increment(ref signals);};
                watcher.EnableRaisingEvents=true;
            }catch(IOException){ReleaseWatcher();}catch(ArgumentException){ReleaseWatcher();}catch(UnauthorizedAccessException){ReleaseWatcher();}
        }
        void Changed(object sender,FileSystemEventArgs e){Interlocked.Increment(ref signals);}
        internal int Signal {get{return Volatile.Read(ref signals);}}
        internal bool Probe(bool force=false){
            long now=Now;int signal=Signal;
            if(!force&&signal==seen&&now<probeAt)return false;
            if(Interlocked.Exchange(ref watcherError,0)!=0)ReleaseWatcher();
            Arm();var current=FileStamp.Read(Path);bool notified=signal!=seen;seen=signal;
            if(notified||!current.Equals(Observed)){
                Observed=current;quietUntil=now+350;retryAt=0;failures=0;Error="";
                Pending=Pending||notified||!current.Equals(Accepted);
            }
            probeAt=now+(Pending?150:1500);
            return Pending&&current.Exists&&current.Length>0&&now>=quietUntil&&now>=retryAt;
        }
        internal bool ChangedOnDisk(){Probe(true);return Pending||!FileStamp.Read(Path).Equals(Accepted);}
        internal void Accept(FileStamp stamp,int signal){Accepted=Observed=stamp;seen=signal;Pending=false;Error="";failures=0;retryAt=0;probeAt=Now+1500;}
        internal void Failed(Exception error){Error=error.Message;retryAt=Now+Math.Min(10000,500*(1<<Math.Min(5,failures++)));}
        void ReleaseWatcher(){if(watcher!=null){watcher.Dispose();watcher=null;}}
        internal void DisableNotificationsForTest(){if(watcher!=null)watcher.EnableRaisingEvents=false;}
        public void Dispose(){ReleaseWatcher();}
    }

    internal sealed partial class MainWindow {
        System.Windows.Forms.Timer fileRefreshTimer;
        ToolStripButton fileRefreshNotice;
        bool refreshingFiles,closingFiles;
        void InitializeFileRefresh(){
            fileRefreshNotice=new ToolStripButton(UiText.Get("文件已更新 · F5 刷新")){
                Visible=false,ForeColor=Color.FromArgb(151,81,0),DisplayStyle=ToolStripItemDisplayStyle.Text,
                ToolTipText=UiText.Get("原文件已改变；当前编辑会保留。点击重新加载。")};
            fileRefreshNotice.Click+=delegate{ReloadDocument();};toolbar.Items.Add(fileRefreshNotice);
            fileRefreshTimer=new System.Windows.Forms.Timer{Interval=200};fileRefreshTimer.Tick+=delegate{CheckDocumentFiles();};
            Activated+=delegate{if(!closingFiles)CheckDocumentFiles();};
        }
        void SyncDocumentWatch(DocumentTab tab){
            string path=tab.Document.FilePath;
            if(tab.Watch!=null&&(tab.FileVersion!=tab.Document.FileVersion||!String.Equals(path,tab.Watch.Path,StringComparison.OrdinalIgnoreCase))){tab.Watch.Dispose();tab.Watch=null;}
            if(tab.Watch==null&&!String.IsNullOrEmpty(path)){tab.Watch=new DocumentFileWatch(path,tab.Document.DiskStamp);tab.FileVersion=tab.Document.FileVersion;}
            if(fileRefreshTimer!=null){if(documents.Count>0)fileRefreshTimer.Start();else fileRefreshTimer.Stop();}
        }
        void CheckDocumentFiles(){
            if(refreshingFiles||closingFiles||IsDisposed||Disposing||!IsHandleCreated||!Enabled||openingFiles||UseWaitCursor)return;
            // Modal tools may hold the old document; do not replace it while they are open.
            if(OwnedForms.Length>0||canvas.InlineEditing||canvas.Capture||Control.MouseButtons!=MouseButtons.None||draggedTab!=null)return;
            refreshingFiles=true;
            try {
                foreach(var tab in documents.ToArray()){
                    SyncDocumentWatch(tab);if(tab.Watch==null||!tab.Watch.Probe()||tab.Document.Dirty||(tab.Redactions!=null&&tab.Redactions.Regions.Count>0))continue;
                    TryReloadDocument(tab,false);
                }
                UpdateRefreshNotice();
            }finally{refreshingFiles=false;}
        }
        void UpdateRefreshNotice(){
            if(fileRefreshNotice==null)return;
            bool pending=activeTab!=null&&activeTab.Watch!=null&&activeTab.Watch.Pending;
            fileRefreshNotice.Visible=pending;
            if(pending){fileRefreshNotice.Text=UiText.Get("文件已更新 · F5 刷新");fileRefreshNotice.ToolTipText=UiText.Get("原文件已改变；当前编辑会保留。点击重新加载。");}
        }
        void ReloadDocument(){
            if(Document==null||String.IsNullOrEmpty(Document.FilePath)||refreshingFiles||!canvas.ConfirmInlineText())return;
            if((Document.Dirty||HasRedactions)&&MessageBox.Show(this,UiText.Get("重新加载会放弃当前文档未保存的修改。是否继续？"),UiText.Get("重新加载文档"),MessageBoxButtons.OKCancel,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.OK)return;
            refreshingFiles=true;
            try{SyncDocumentWatch(activeTab);TryReloadDocument(activeTab,true);UpdateRefreshNotice();}finally{refreshingFiles=false;}
        }
        bool TryReloadDocument(DocumentTab tab,bool manual){
            var watch=tab.Watch;var old=tab.Document;PdfDocument loaded=null;
            if(watch==null)return false;
            try {
                FileStamp before=FileStamp.Read(watch.Path);int signal=watch.Signal;
                // Writers must finish first. Atomic replacements remain possible and are checked below.
                using(var input=new FileStream(watch.Path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){
                    string password=old.Password;
                    while(true){
                        try{loaded=PdfDocument.Open(watch.Path,password);break;}
                        catch(PdfException error){
                            if(!manual||error.Code!=4)throw;
                            password=Dialogs.Ask(this,UiText.Get("打开受保护的 PDF"),UiText.Get("请输入文档密码："),"",true);
                            if(password==null)return false;
                        }
                    }
                }
                var after=FileStamp.Read(watch.Path);
                if(!before.Equals(after)||signal!=watch.Signal)throw new IOException(UiText.Get("文件仍在写入，将稍后自动刷新。"));
                // Metadata-only changes and delayed self-save events must not clear view or undo state.
                if(!old.Dirty&&old.GetRenderSource().ContentEquals(loaded.GetRenderSource())){if(manual){tab.Redactions=null;UpdateRedactionControls();UpdateTabStrip();canvas.Invalidate();}watch.Accept(after,signal);return true;}
                loaded.OpenPage(Math.Min(old.Index,loaded.Count-1));
                bool current=tab==activeTab,preserve=loaded.Index==old.Index;
                if(current)RememberActiveTab();
                tab.Document=loaded;loaded=null;tab.Document.DiskStamp=after;tab.FileVersion=tab.Document.FileVersion;
                tab.Hit=null;tab.SearchQuery="";
                try {
                    if(current){Document=tab.Document;lastHit=null;UpdateDocument(true,preserve);Report(UiText.Get("已自动刷新 · ")+watch.Path);}
                    else UpdateTabLabels();
                }catch{
                    loaded=tab.Document;tab.Document=old;tab.FileVersion=old.FileVersion;
                    if(current){Document=old;UpdateDocument(true,true);}throw;
                }
                tab.Redactions=null;UpdateRedactionControls();UpdateTabStrip();watch.Accept(after,signal);old.Dispose();return true;
            }catch(Exception error){
                watch.Failed(error);
                if(manual)MessageBox.Show(this,UiText.Get("暂时无法重新加载，已保留当前文档。")+Environment.NewLine+error.Message,UiText.Get("重新加载文档"),MessageBoxButtons.OK,MessageBoxIcon.Information);
                return false;
            }finally{if(loaded!=null)loaded.Dispose();}
        }
        bool ConfirmExternalSave(){
            if(activeTab==null)return true;SyncDocumentWatch(activeTab);
            if(activeTab.Watch==null||!activeTab.Watch.ChangedOnDisk())return true;
            return MessageBox.Show(this,UiText.Get("原文件已在其他程序中改变。仍要用当前文档覆盖它吗？建议取消后另存为。"),UiText.Get("原文件已改变"),MessageBoxButtons.OKCancel,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.OK;
        }
        void DisposeFileRefresh(){
            closingFiles=true;
            if(fileRefreshTimer!=null){fileRefreshTimer.Stop();fileRefreshTimer.Dispose();fileRefreshTimer=null;}
            foreach(var tab in documents)if(tab.Watch!=null){tab.Watch.Dispose();tab.Watch=null;}
        }
        internal void ReloadForTest(){ReloadDocument();}
        internal bool RefreshNoticeForTest {get{return fileRefreshNotice.Available;}}
        internal void StopFileWatcherForTest(){if(activeTab!=null&&activeTab.Watch!=null)activeTab.Watch.DisableNotificationsForTest();}
    }
}
