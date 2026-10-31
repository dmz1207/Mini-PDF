using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed partial class MainWindow {
        TabControl sidebarTabs;
        TabPage searchTab;
        ListView searchResults;
        ToolStripButton matchCase,wholeWords;
        readonly List<SearchHit> searchHits=new List<SearchHit>();
        System.Windows.Forms.Timer searchDelay;
        PdfJob searchJob;
        int searchGeneration,searchEpoch=-1,searchIdentity=-1,searchIndex=-1,searchFromPage;
        bool updatingSearch,searchComplete;
        void InitializeSearch(TabControl tabs) {
            shortcuts[Keys.Shift|Keys.F3]=FindPrevious;
            sidebarTabs=tabs;searchTab=new TabPage(UiText.Get("搜索结果")){Padding=Padding.Empty};tabs.TabPages.Add(searchTab);
            searchResults=new ListView{Name="SearchResults",Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,BorderStyle=BorderStyle.None,HeaderStyle=ColumnHeaderStyle.Nonclickable,ShowItemToolTips=true};
            searchResults.Columns.Add(UiText.Get("页"),44);searchResults.Columns.Add(UiText.Get("上下文"),220);searchTab.Controls.Add(searchResults);
            searchResults.SelectedIndexChanged+=delegate{if(!updatingSearch&&searchResults.SelectedIndices.Count>0)SelectSearchHit(searchResults.SelectedIndices[0],false);};
            searchResults.Resize+=delegate{if(searchResults.Columns.Count>1)searchResults.Columns[1].Width=Math.Max(120,searchResults.ClientSize.Width-46);};
            matchCase=Button(findbar,UiText.Get("区分大小写"),delegate{QueueSearch();},UiText.Get("区分大小写"),null);matchCase.CheckOnClick=true;
            wholeWords=Button(findbar,UiText.Get("全词匹配"),delegate{QueueSearch();},UiText.Get("全词匹配"),null);wholeWords.CheckOnClick=true;
            searchDelay=new System.Windows.Forms.Timer{Interval=300};searchDelay.Tick+=delegate{searchDelay.Stop();StartSearch();};query.TextChanged+=delegate{if(!changingTabs)QueueSearch();};
        }
        void QueueSearch() {
            if(searchDelay==null)return;ResetSearch();if(Document!=null&&!String.IsNullOrWhiteSpace(query.Text))searchDelay.Start();
        }
        void ResetSearch() {
            if(searchDelay!=null)searchDelay.Stop();searchGeneration++;if(searchJob!=null){searchJob.Dispose();searchJob=null;}
            searchHits.Clear();searchIndex=-1;searchComplete=false;lastHit=null;searchQuery="";
            if(searchResults!=null)searchResults.Items.Clear();if(findResult!=null)findResult.Text="";
            if(canvas!=null){canvas.Markers.Clear();canvas.Invalidate();}
        }
        void RefreshSearchDocument() {
            int identity=Document==null?-1:Document.Identity,epoch=Document==null?-1:Document.RenderEpoch;
            if(identity==searchIdentity&&epoch==searchEpoch)return;searchIdentity=identity;searchEpoch=epoch;ResetSearch();
            if(Document!=null&&findbar.Visible&&!String.IsNullOrWhiteSpace(query.Text))searchDelay.Start();
        }
        void ShowFind() {
            findbar.Visible=true;if(split.Panel1Collapsed){split.Panel1Collapsed=false;UpdateSidebarControls();canvas.Rebuild(false);}
            sidebarTabs.SelectedTab=searchTab;if(!String.IsNullOrWhiteSpace(query.Text)&&searchHits.Count==0&&searchJob==null)QueueSearch();query.Focus();query.SelectAll();
        }
        void CloseFind(){ResetSearch();findbar.Visible=false;if(sidebarTabs.SelectedTab==searchTab)sidebarTabs.SelectedIndex=0;canvas.Focus();}
        void StartSearch() {
            if(Document==null||String.IsNullOrWhiteSpace(query.Text))return;ResetSearch();string text=query.Text.Trim();if(text.Length>2048){findResult.Text=UiText.Get("搜索内容过长。");return;}
            searchQuery=text;searchIdentity=Document.Identity;searchEpoch=Document.RenderEpoch;searchFromPage=Document.Index;
            int generation=searchGeneration;bool sensitive=matchCase.Checked,words=wholeWords.Checked;
            try {
                var job=new PdfJob(this,Document);searchJob=job;findResult.Text=UiText.Get("正在搜索…");
                job.Start(delegate(BinaryWriter writer){writer.Write("search");writer.Write(text);writer.Write(sensitive);writer.Write(words);},delegate(BinaryReader reader) {
                    while(true) {
                        int code=reader.ReadInt32();if(code<0)throw new IOException(reader.ReadString());
                        if(code==1){bool limited=reader.ReadBoolean();int empty=reader.ReadInt32();job.Post(delegate{if(generation!=searchGeneration)return;searchComplete=true;findResult.Text=SearchSummary(limited,empty);if(searchIndex<0&&searchHits.Count>0){int first=searchHits.FindIndex(delegate(SearchHit hit){return hit.Page>=searchFromPage;});SelectSearchHit(first<0?0:first,true);}});return;}
                        int done=reader.ReadInt32(),total=reader.ReadInt32(),count=reader.ReadInt32();PdfDocument.Check(code==0&&count>=0&&count<=SearchText.MaximumHits,"Invalid search response.");
                        var batch=new List<SearchHit>();for(int i=0;i<count;i++){var hit=new SearchHit{Page=reader.ReadInt32(),Start=reader.ReadInt32(),Count=reader.ReadInt32(),Snippet=reader.ReadString(),Rectangles=new List<N.Rect>()};int rectangles=reader.ReadInt32();PdfDocument.Check(rectangles>=0&&rectangles<=256,"Invalid search rectangles.");for(int j=0;j<rectangles;j++)hit.Rectangles.Add(new N.Rect(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()));batch.Add(hit);}
                        job.Post(delegate{if(generation!=searchGeneration)return;searchResults.BeginUpdate();try{foreach(var hit in batch){searchHits.Add(hit);var item=new ListViewItem((hit.Page+1).ToString()){ToolTipText=hit.Snippet};item.SubItems.Add(hit.Snippet);searchResults.Items.Add(item);}}finally{searchResults.EndUpdate();}findResult.Text=UiText.Get("正在搜索…")+" "+done+" / "+total+" · "+searchHits.Count;});
                    }
                },delegate(Exception error){if(generation!=searchGeneration)return;searchJob=null;if(error!=null){searchComplete=true;findResult.Text=UiText.Get("搜索未完成，已保留找到的结果。")+" "+error.Message;}},30);
            }catch(Exception error){searchJob=null;findResult.Text=error.Message;}
        }
        string SearchSummary(bool limited,int empty) {
            if(limited)return String.Format(UiText.Get("已显示前 {0} 处，请缩小搜索范围。"),searchHits.Count);
            if(searchHits.Count==0)return UiText.Get(empty>0?"没有找到；无文字层的页面请先运行 OCR。":"没有找到匹配内容。");
            return String.Format(UiText.Get("共 {0} 处"),searchHits.Count);
        }
        void FindNext(){MoveSearch(1);}
        void FindPrevious(){MoveSearch(-1);}
        void MoveSearch(int direction) {
            if(Document==null)return;if(String.IsNullOrWhiteSpace(query.Text)){ShowFind();return;}
            if(searchQuery!=query.Text.Trim()||searchIdentity!=Document.Identity||searchEpoch!=Document.RenderEpoch){searchDelay.Stop();StartSearch();return;}
            if(searchHits.Count==0){if(searchJob==null&&!searchComplete)StartSearch();return;}
            int index=searchIndex<0?(direction>0?0:searchHits.Count-1):(searchIndex+direction+searchHits.Count)%searchHits.Count;SelectSearchHit(index,true);
        }
        void SelectSearchHit(int index,bool updateList) {
            if(index<0||index>=searchHits.Count||Document==null||Document.Identity!=searchIdentity||Document.RenderEpoch!=searchEpoch)return;
            var hit=searchHits[index];Navigate(hit.Page);if(Document.Index!=hit.Page)return;searchIndex=index;lastHit=hit;canvas.ShowMarkers(hit.Rectangles);
            if(updateList){updatingSearch=true;try{searchResults.Items[index].Selected=true;searchResults.Items[index].EnsureVisible();}finally{updatingSearch=false;}}
            findResult.Text=(index+1)+" / "+searchHits.Count+" · "+UiText.Get("页")+" "+(hit.Page+1);
        }
        void DisposeSearch(){ResetSearch();if(searchDelay!=null){searchDelay.Dispose();searchDelay=null;}}
        internal void SearchForTest(string text){ShowFind();query.Text=text;searchDelay.Stop();StartSearch();}
        internal bool SearchCompleteForTest {get{return searchComplete;}}
        internal ListView SearchResultsForTest {get{return searchResults;}}
        internal void SearchStepForTest(int direction){MoveSearch(direction);}
        internal int SearchIndexForTest {get{return searchIndex;}}
        internal string SearchStatusForTest {get{return findResult.Text;}}
        internal void CloseFindForTest(){CloseFind();}
    }
}
