using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MiniAI.Desktop {
    internal static class TranslationStream {
        internal static string Read(string address,string token,string text,string target,Func<bool> canceled,Action<string> progress,string mode="fast",string engine="hy") {
            Uri uri;if(!Uri.TryCreate(address,UriKind.Absolute,out uri)||uri.Scheme!="http"||uri.Host!="127.0.0.1"||uri.AbsolutePath!="/"||uri.UserInfo.Length!=0)throw new IOException("Invalid local MiniAI address.");
            var json=new JavaScriptSerializer{MaxJsonLength=1024*1024};
            for(int attempt=0;;attempt++) {
                if(canceled())throw new OperationCanceledException();
                var request=(HttpWebRequest)WebRequest.Create(address+"/v1/translate");request.Proxy=null;request.KeepAlive=false;request.ServicePoint.UseNagleAlgorithm=false;request.Timeout=request.ReadWriteTimeout=300000;request.Method="POST";request.ContentType="application/json; charset=utf-8";request.Headers["Authorization"]="Bearer "+token;
                byte[] bytes=Encoding.UTF8.GetBytes(json.Serialize(new{text=text,target=target,mode=mode,engine=engine,stream=true}));request.ContentLength=bytes.Length;
                using(var timer=new System.Threading.Timer(delegate{if(canceled())request.Abort();},null,40,40))try {
                    using(var stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);
                    using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)) {
                        string line;while((line=reader.ReadLine())!=null) {
                            if(canceled())throw new OperationCanceledException();if(line.Length>1024*1024)throw new IOException("MiniAI response exceeds the limit.");var value=json.Deserialize<Dictionary<string,object>>(line);object type;value.TryGetValue("type",out type);
                            if(Convert.ToString(type)=="partial"){if(progress!=null)progress(Convert.ToString(value["text"]));continue;}
                            if(Convert.ToString(type)=="error")throw new IOException(Convert.ToString(value["error"]));
                            var result=Convert.ToString(type)=="done"?(Dictionary<string,object>)value["result"]:value;
                            if(result.ContainsKey("error"))throw new IOException(Convert.ToString(result["error"]));
                            if(result.ContainsKey("truncated")&&Convert.ToBoolean(result["truncated"]))throw new IOException("Select a shorter passage; the output limit was reached.");
                            if(result.ContainsKey("text"))return Convert.ToString(result["text"]);
                        }
                        throw new IOException("MiniAI disconnected before completing the translation.");
                    }
                }catch(WebException error) {
                    if(canceled())throw new OperationCanceledException();var response=error.Response as HttpWebResponse;
                    if(response!=null&&response.StatusCode==(HttpStatusCode)429&&attempt<40){response.Dispose();Thread.Sleep(50);continue;}
                    if(response!=null)using(response)using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)){string message=reader.ReadToEnd();if(message.Length<8192)throw new IOException(message,error);}throw;
                }
            }
        }
    }
    internal sealed class BubbleBackend {
        internal Func<string,string,Func<bool>,string> Translate;
        internal Func<string,string,string,Func<bool>,Action<string>,string> Stream;
        internal Func<byte[],string,Func<bool>,string> Recognize;
        internal Func<string,string> Text;
    }
    internal static class DesktopLanguages {
        internal static string Mode="fast";
        internal static string Engine="hy";
        internal static readonly string[] Codes=LanguageCatalog.Codes;
        internal static readonly string[] Labels=LanguageCatalog.ChineseLabels;
        internal static string AutoTarget(string text){int chinese=0,letters=0;foreach(char c in text??""){if(c>=0x4e00&&c<=0x9fff)chinese++;else if(Char.IsLetter(c))letters++;}return chinese>0&&chinese>letters/3?"en":"zh";}
    }
    internal sealed class TranslationBubble : Form {
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")]static extern IntPtr GetCapture();
        [DllImport("user32.dll")]static extern bool IsChild(IntPtr parent,IntPtr child);
        [DllImport("user32.dll")]static extern bool ReleaseCapture();
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr a,IntPtr b);
        readonly TextBox result,source;
        readonly ComboBox target,quality;
        readonly Label hint;
        readonly Button pin;
        readonly System.Windows.Forms.Timer leave;
        readonly BubbleBackend api;
        readonly Rectangle anchor;
        readonly bool ocr;
        readonly Font uiFont,bodyFont,smallFont;
        bool cleaned;
        int generation;
        bool pinned,userSized;
        DateTime outside=DateTime.UtcNow;
        DateTime readableUntil=DateTime.MaxValue;
        string original="";
        internal bool BusyForTest;
        internal string ResultForTest {get{return result.Text;}}
        internal string SourceForTest {get{return source.Text;}}
        internal bool IsPinned {get{return pinned;}}
        internal TranslationBubble(BubbleBackend backend,Rectangle selection,bool image,bool keep=false) {
            api=backend;pinned=keep;anchor=new Rectangle(Cursor.Position,new Size(1,1));anchor.Inflate(20,20);ocr=image;
            uiFont=new Font("Segoe UI",10);bodyFont=new Font("Segoe UI",11);smallFont=new Font("Segoe UI",8.5f);
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Color.FromArgb(249,251,254);Font=uiFont;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);ClientSize=new Size(520,image?300:215);MinimumSize=new Size(320,165);Opacity=1;Padding=new Padding(7);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12,8,12,8),ColumnCount=1,RowCount=4};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,image?72:0));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
            var header=new FlowLayoutPanel{Name="TranslationHeader",AutoSize=true,Dock=DockStyle.Fill,WrapContents=true,Margin=Padding.Empty,Cursor=Cursors.SizeAll};header.MouseDown+=DragHeader;
            target=new ComboBox{Width=116,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Default};target.Items.Add(api.Text("中英自动互译"));target.Items.AddRange(LanguageCatalog.Labels(api.Text("复制")=="Copy"));target.DropDownWidth=220;target.MaxDropDownItems=16;target.SelectedIndex=0;target.SelectedIndexChanged+=delegate{Translate(SelectedSource(),true);};
            quality=new ComboBox{Name="TranslationMode",Width=102,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Default};quality.Items.Add(api.Text("极速翻译"));quality.Items.Add(api.Text("高精度翻译"));quality.SelectedIndex=DesktopLanguages.Mode=="quality"?1:0;quality.SelectedIndexChanged+=delegate{DesktopLanguages.Mode=quality.SelectedIndex==1?"quality":"fast";Translate(SelectedSource(),true);};
            pin=SmallButton("Ctrl",delegate{SetPinned(!pinned);});var copy=SmallButton(api.Text("复制"),delegate{if(result.TextLength>0&&!BusyForTest)Clipboard.SetText(result.SelectionLength>0?result.SelectedText:result.Text);});var originalButton=SmallButton(api.Text("原文"),delegate{SuspendLayout();bool visible=!source.Visible;source.Visible=visible;layout.RowStyles[1].Height=visible?90:0;Height+=visible?90:-90;ResumeLayout(true);});var close=SmallButton("×",delegate{Close();});var move=new Label{Name="TranslationDragHandle",Text="⋮⋮",AutoSize=true,Cursor=Cursors.SizeAll,Margin=new Padding(0,3,4,0)};move.MouseDown+=DragHeader;close.Name="CloseTranslation";header.Controls.AddRange(new Control[]{move,target,quality,pin,copy,originalButton,close});layout.Controls.Add(header,0,0);
            originalButton.Name="ToggleTranslationSource";source=new TextBox{Name="RecognizedSelection",Multiline=true,Dock=DockStyle.Fill,MinimumSize=new Size(80,48),Font=Font,BorderStyle=BorderStyle.FixedSingle,MaxLength=100000,ScrollBars=ScrollBars.Vertical,WordWrap=true};
            source.MouseUp+=delegate{if(source.SelectionLength>0&&!String.IsNullOrWhiteSpace(source.SelectedText))Translate(source.SelectedText,true);};source.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.Enter){Translate(SelectedSource(),true);e.SuppressKeyPress=true;}};
            layout.Controls.Add(source,0,1);source.Visible=image;
            result=new TextBox{Name="QuickTranslationResult",Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,Font=bodyFont,BorderStyle=BorderStyle.None,BackColor=BackColor,Margin=new Padding(0,8,0,6),ForeColor=Color.FromArgb(25,32,40),ScrollBars=ScrollBars.Vertical,WordWrap=true};layout.Controls.Add(result,0,2);
            hint=new Label{AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(93,109,125),Font=smallFont,Text=api.Text("可选原文再译 · Ctrl 固定 · Esc 关闭")};var bottom=new TableLayoutPanel{ColumnCount=2,Dock=DockStyle.Fill,AutoSize=true,Margin=Padding.Empty};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,22));bottom.Controls.Add(hint,0,0);var grip=new Label{Text="◢",Dock=DockStyle.Fill,TextAlign=ContentAlignment.BottomRight,Cursor=Cursors.SizeNWSE,ForeColor=Color.FromArgb(111,126,142),AccessibleName=api.Text("拖动调整大小")};grip.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){userSized=true;SetPinned(true);ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)17,IntPtr.Zero);}};bottom.Controls.Add(grip,1,0);layout.Controls.Add(bottom,0,3);
            hint.Cursor=Cursors.SizeAll;hint.MouseDown+=DragHeader;layout.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Y<header.Bottom+layout.Padding.Top)DragHeader(sender,e);};
            leave=new System.Windows.Forms.Timer{Interval=100};leave.Tick+=delegate{bool inside=Bounds.Contains(Cursor.Position)||anchor.Contains(Cursor.Position);if(inside&&GetAsyncKeyState(17)<0)SetPinned(true);IntPtr capture=GetCapture();bool selecting=capture!=IntPtr.Zero&&(capture==Handle||IsChild(Handle,capture));if(BusyForTest||pinned||target.DroppedDown||quality.DroppedDown||inside||selecting){outside=DateTime.UtcNow;return;}if(DateTime.UtcNow>=readableUntil&&(DateTime.UtcNow-outside).TotalMilliseconds>1200)Close();};leave.Start();
            Shown+=delegate{Place(selection);SetPinned(pinned);};Resize+=delegate{Round();};ResizeBegin+=delegate{userSized=true;SetPinned(true);};
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        void DragHeader(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;SetPinned(true);ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);}
        protected override CreateParams CreateParams {get{var value=base.CreateParams;value.ExStyle|=0x80;return value;}}
        Button SmallButton(string title,Action action){var button=new Button{Text=title,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new Size(32,26),FlatStyle=FlatStyle.Flat,Margin=new Padding(3,0,0,0),Padding=Padding.Empty,TabStop=false,Cursor=Cursors.Default};button.FlatAppearance.BorderSize=0;button.Click+=delegate{action();};return button;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Color.FromArgb(190,204,222)))e.Graphics.DrawRectangle(pen,0,0,Math.Max(0,ClientSize.Width-1),Math.Max(0,ClientSize.Height-1));}
        void Place(Rectangle selection){var area=Screen.FromRectangle(selection).WorkingArea;int x=Math.Min(area.Right-Width,Math.Max(area.Left,selection.Right-24)),y=selection.Bottom+8;if(y+Height>area.Bottom)y=Math.Max(area.Top,selection.Top-Height-8);Location=new Point(x,y);Round();}
        void Round(){if(Width<8||Height<8)return;using(var path=new GraphicsPath()){int d=16;path.AddArc(0,0,d,d,180,90);path.AddArc(Width-d,0,d,d,270,90);path.AddArc(Width-d,Height-d,d,d,0,90);path.AddArc(0,Height-d,d,d,90,90);path.CloseFigure();var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
        protected override void WndProc(ref Message message){base.WndProc(ref message);if(message.Msg!=0x84||WindowState!=FormWindowState.Normal)return;long value=message.LParam.ToInt64();Point p=PointToClient(new Point(unchecked((short)value),unchecked((short)(value>>16))));if(!ClientRectangle.Contains(p))return;int edge=Padding.Left;bool left=p.X<edge,right=p.X>=ClientSize.Width-edge,top=p.Y<edge,bottom=p.Y>=ClientSize.Height-edge;if(left||right||top||bottom)message.Result=(IntPtr)(top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:11);}
        void FitResult(){if(userSized||!Visible)return;int width=Math.Max(120,result.ClientSize.Width-24);int lines=TextRenderer.MeasureText(result.Text,result.Font,new Size(width,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPadding).Height;var area=Screen.FromControl(this).WorkingArea;int chrome=Height-result.Height;Height=Math.Min((int)(area.Height*.7),Math.Max(Height,chrome+lines+20));if(Bottom>area.Bottom)Top=Math.Max(area.Top,area.Bottom-Height);}
        string SelectedSource(){return source.Visible?(source.SelectionLength>0?source.SelectedText:source.Text):original;}
        void SetPinned(bool value){pinned=value;pin.BackColor=pinned?Color.LightBlue:BackColor;}
        void Complete(string value){BusyForTest=false;result.Text=value;result.Select(0,0);result.ScrollToCaret();FitResult();readableUntil=DateTime.UtcNow.AddSeconds(Math.Min(60,Math.Max(15,value.Length/12.0)));outside=DateTime.UtcNow;}
        void Partial(string value){int start=result.SelectionStart,length=result.SelectionLength;result.Text=value;result.Select(Math.Min(start,result.TextLength),Math.Min(length,Math.Max(0,result.TextLength-start)));}
        void Post(int serial,Action action){try{if(!IsDisposed&&IsHandleCreated)BeginInvoke(new Action(delegate{if(!IsDisposed&&serial==Volatile.Read(ref generation))action();}));}catch(InvalidOperationException){}}
        internal void Translate(string text,bool keepSource=false) {
            if(String.IsNullOrWhiteSpace(text))return;text=TranslationText.Normalize(text);original=text;if(!keepSource)source.Text=text;int serial=Interlocked.Increment(ref generation);BusyForTest=true;readableUntil=DateTime.MaxValue;result.Text=api.Text("正在翻译…");int selected=target.SelectedIndex;string language=selected==0?DesktopLanguages.AutoTarget(text):DesktopLanguages.Codes[selected-1],mode=quality.SelectedIndex==1?"quality":"fast";
            result.RightToLeft=LanguageCatalog.IsRightToLeft(language)?RightToLeft.Yes:RightToLeft.No;
            new Thread(delegate(){try{Thread.Sleep(60);if(serial!=Volatile.Read(ref generation))return;Func<bool> canceled=delegate{return serial!=Volatile.Read(ref generation);};string value=api.Stream!=null?api.Stream(text,language,mode,canceled,delegate(string part){Post(serial,delegate{Partial(part);});}):api.Translate(text,language,canceled);Post(serial,delegate{Complete(value);});}catch(Exception error){Post(serial,delegate{Complete(error.Message);});}}){IsBackground=true,Name="MiniPDF selection translation"}.Start();
        }
        internal void Recognize(Bitmap image) {
            int serial=Interlocked.Increment(ref generation);BusyForTest=true;result.Text=api.Text("正在识别选区…");string mode=quality.SelectedIndex==1?"quality":"fast";byte[] bytes;using(image)using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);bytes=stream.ToArray();}
            new Thread(delegate(){try{string text=api.Recognize(bytes,mode,delegate{return serial!=Volatile.Read(ref generation);});Post(serial,delegate{source.Text=text;if(String.IsNullOrWhiteSpace(text))Complete(api.Text("选区内未识别出文字，请重新框选。"));else Translate(text);});}catch(Exception error){Post(serial,delegate{Complete(error.Message);});}}){IsBackground=true,Name="MiniPDF selection OCR"}.Start();
        }
        internal void PinForTest(){SetPinned(true);}
        internal void KeepOpen(){SetPinned(true);}
        internal void TargetForTest(int value){target.SelectedIndex=value;}
        protected override bool ProcessCmdKey(ref Message msg,Keys keys){if(keys==Keys.Escape){Close();return true;}return base.ProcessCmdKey(ref msg,keys);}
        protected override void Dispose(bool disposing){if(disposing&&!cleaned){cleaned=true;Interlocked.Increment(ref generation);leave.Dispose();base.Dispose(true);bodyFont.Dispose();smallFont.Dispose();uiFont.Dispose();return;}base.Dispose(disposing);}
    }
    internal sealed class AiHotkey : NativeWindow,IDisposable {
        [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
        [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr window,int id);
        readonly Action action,ocrAction;internal bool Registered,FormulaRegistered;
        internal AiHotkey(Action handler,Action formula=null){action=handler;ocrAction=formula;CreateHandle(new CreateParams());Registered=RegisterHotKey(Handle,1,0x4001,(uint)Keys.S);if(formula!=null)FormulaRegistered=RegisterHotKey(Handle,2,0x4005,(uint)Keys.S);}
        protected override void WndProc(ref Message m){if(m.Msg==0x312){if(m.WParam.ToInt32()==2&&ocrAction!=null)ocrAction();else action();return;}base.WndProc(ref m);}
        public void Dispose(){if(Handle!=IntPtr.Zero){if(Registered)UnregisterHotKey(Handle,1);if(FormulaRegistered)UnregisterHotKey(Handle,2);DestroyHandle();}}
    }
}
