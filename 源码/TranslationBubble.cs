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
        internal static string Read(string address,string token,string text,string target,Func<bool> canceled,Action<string> progress,string mode="fast") {
            Uri uri;if(!Uri.TryCreate(address,UriKind.Absolute,out uri)||uri.Scheme!="http"||uri.Host!="127.0.0.1"||uri.AbsolutePath!="/"||uri.UserInfo.Length!=0)throw new IOException("Invalid local MiniAI address.");
            var json=new JavaScriptSerializer{MaxJsonLength=1024*1024};
            for(int attempt=0;;attempt++) {
                if(canceled())throw new OperationCanceledException();
                var request=(HttpWebRequest)WebRequest.Create(address+"/v1/translate");request.Proxy=null;request.KeepAlive=false;request.ServicePoint.UseNagleAlgorithm=false;request.Timeout=request.ReadWriteTimeout=300000;request.Method="POST";request.ContentType="application/json; charset=utf-8";request.Headers["Authorization"]="Bearer "+token;
                byte[] bytes=Encoding.UTF8.GetBytes(json.Serialize(new{text=text,target=target,mode=mode,stream=true}));request.ContentLength=bytes.Length;
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
        internal static readonly string[] Codes={"zh","en","zh-Hant"};
        internal static readonly string[] Labels={"简体中文","English","繁體中文"};
        internal static string AutoTarget(string text){int chinese=0,letters=0;foreach(char c in text??""){if(c>=0x4e00&&c<=0x9fff)chinese++;else if(Char.IsLetter(c))letters++;}return chinese>0&&chinese>letters/3?"en":"zh";}
    }
    internal sealed class TranslationBubble : Form {
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")]static extern IntPtr GetCapture();
        [DllImport("user32.dll")]static extern bool IsChild(IntPtr parent,IntPtr child);
        [DllImport("user32.dll")]static extern int SetWindowCompositionAttribute(IntPtr window,ref Composition data);
        [StructLayout(LayoutKind.Sequential)]struct Accent {internal int State,Flags,Color,Animation;}
        [StructLayout(LayoutKind.Sequential)]struct Composition {internal int Attribute;internal IntPtr Data;internal int Size;}
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
        bool pinned;
        DateTime outside=DateTime.UtcNow;
        DateTime readableUntil=DateTime.MaxValue;
        string original="";
        internal bool BusyForTest;
        internal string ResultForTest {get{return result.Text;}}
        internal string SourceForTest {get{return source.Text;}}
        internal TranslationBubble(BubbleBackend backend,Rectangle selection,bool image) {
            api=backend;anchor=new Rectangle(Cursor.Position,new Size(1,1));anchor.Inflate(20,20);ocr=image;
            uiFont=new Font("Segoe UI",10);bodyFont=new Font("Segoe UI",11);smallFont=new Font("Segoe UI",8.5f);
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Color.FromArgb(249,251,254);Font=uiFont;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);ClientSize=new Size(520,image?300:215);MinimumSize=new Size(320,165);Opacity=.97;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12,8,12,8),ColumnCount=1,RowCount=4};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,image?72:0));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
            var header=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            target=new ComboBox{Width=116,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat};target.Items.Add(api.Text("中英自动互译"));target.Items.AddRange(DesktopLanguages.Labels);target.SelectedIndex=0;target.SelectedIndexChanged+=delegate{Translate(SelectedSource());};
            quality=new ComboBox{Name="TranslationMode",Width=102,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat};quality.Items.Add(api.Text("极速翻译"));quality.Items.Add(api.Text("高精度翻译"));quality.SelectedIndex=DesktopLanguages.Mode=="quality"?1:0;quality.SelectedIndexChanged+=delegate{DesktopLanguages.Mode=quality.SelectedIndex==1?"quality":"fast";Translate(SelectedSource());};
            pin=SmallButton("Ctrl",delegate{SetPinned(!pinned);});var copy=SmallButton(api.Text("复制"),delegate{if(result.TextLength>0&&!BusyForTest)Clipboard.SetText(result.SelectionLength>0?result.SelectedText:result.Text);});var originalButton=SmallButton(api.Text("原文"),delegate{bool visible=!source.Visible;layout.RowStyles[1].Height=visible?72:0;Height+=visible?72:-72;source.Visible=visible;});var close=SmallButton("×",delegate{Close();});header.Controls.AddRange(new Control[]{target,quality,pin,copy,originalButton,close});layout.Controls.Add(header,0,0);
            source=new TextBox{Name="RecognizedSelection",Multiline=true,Dock=DockStyle.Fill,MinimumSize=new Size(80,48),Font=Font,BorderStyle=BorderStyle.FixedSingle,MaxLength=100000};source.TextChanged+=delegate{ScrollIfNeeded(source);};source.Resize+=delegate{ScrollIfNeeded(source);};source.VisibleChanged+=delegate{ScrollIfNeeded(source);};
            source.MouseUp+=delegate{if(source.SelectionLength>0&&!String.IsNullOrWhiteSpace(source.SelectedText))Translate(source.SelectedText);};source.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.Enter){Translate(SelectedSource());e.SuppressKeyPress=true;}};
            layout.Controls.Add(source,0,1);source.Visible=image;
            result=new TextBox{Name="QuickTranslationResult",Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,Font=bodyFont,BorderStyle=BorderStyle.None,BackColor=BackColor,Margin=new Padding(0,8,0,6)};result.TextChanged+=delegate{ScrollIfNeeded(result);};result.Resize+=delegate{ScrollIfNeeded(result);};layout.Controls.Add(result,0,2);
            hint=new Label{AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(93,109,125),Font=smallFont,Text=api.Text("可选原文再译 · Ctrl 固定 · Esc 关闭")};layout.Controls.Add(hint,0,3);
            leave=new System.Windows.Forms.Timer{Interval=100};leave.Tick+=delegate{bool inside=Bounds.Contains(Cursor.Position)||anchor.Contains(Cursor.Position);if(inside&&GetAsyncKeyState(17)<0)SetPinned(true);IntPtr capture=GetCapture();bool selecting=capture!=IntPtr.Zero&&(capture==Handle||IsChild(Handle,capture));if(BusyForTest||pinned||target.DroppedDown||inside||selecting){outside=DateTime.UtcNow;return;}if(DateTime.UtcNow>=readableUntil&&(DateTime.UtcNow-outside).TotalMilliseconds>1200)Close();};leave.Start();
            Shown+=delegate{Place(selection);Glass();};Resize+=delegate{Round();};
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{var value=base.CreateParams;value.ExStyle|=0x80;return value;}}
        Button SmallButton(string title,Action action){var button=new Button{Text=title,AutoSize=true,MinimumSize=new Size(32,26),FlatStyle=FlatStyle.Flat,Margin=new Padding(3,0,0,0),Padding=Padding.Empty,TabStop=false};button.FlatAppearance.BorderSize=0;button.Click+=delegate{action();};return button;}
        static void ScrollIfNeeded(TextBox box){if(!box.Visible||box.Height<24||box.Width<40)return;int width=Math.Max(20,box.Width-SystemInformation.VerticalScrollBarWidth-8);int height=TextRenderer.MeasureText(box.Text,box.Font,new Size(width,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPadding).Height;var value=height>box.Height-6?ScrollBars.Vertical:ScrollBars.None;if(box.ScrollBars!=value)box.ScrollBars=value;}
        void Place(Rectangle selection){var area=Screen.FromRectangle(selection).WorkingArea;int x=Math.Min(area.Right-Width,Math.Max(area.Left,selection.Right-24)),y=selection.Bottom+8;if(y+Height>area.Bottom)y=Math.Max(area.Top,selection.Top-Height-8);Location=new Point(x,y);Round();}
        void Round(){if(Width<8||Height<8)return;using(var path=new GraphicsPath()){int d=16;path.AddArc(0,0,d,d,180,90);path.AddArc(Width-d,0,d,d,270,90);path.AddArc(Width-d,Height-d,d,d,0,90);path.AddArc(0,Height-d,d,d,90,90);path.CloseFigure();var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
        void Glass(){var accent=new Accent{State=4,Flags=2,Color=unchecked((int)0xe8f8f8f8)};IntPtr pointer=Marshal.AllocHGlobal(Marshal.SizeOf(accent));try{Marshal.StructureToPtr(accent,pointer,false);var data=new Composition{Attribute=19,Data=pointer,Size=Marshal.SizeOf(accent)};SetWindowCompositionAttribute(Handle,ref data);}catch(EntryPointNotFoundException){}finally{Marshal.FreeHGlobal(pointer);}}
        string SelectedSource(){return source.Visible?(source.SelectionLength>0?source.SelectedText:source.Text):original;}
        void SetPinned(bool value){pinned=value;pin.BackColor=pinned?Color.LightBlue:BackColor;}
        void Complete(string value){BusyForTest=false;result.Text=value;readableUntil=DateTime.UtcNow.AddSeconds(Math.Min(20,Math.Max(8,value.Length/18.0)));outside=DateTime.UtcNow;}
        void Partial(string value){int start=result.SelectionStart,length=result.SelectionLength;result.Text=value;result.Select(Math.Min(start,result.TextLength),Math.Min(length,Math.Max(0,result.TextLength-start)));}
        void Post(int serial,Action action){try{if(!IsDisposed&&IsHandleCreated)BeginInvoke(new Action(delegate{if(!IsDisposed&&serial==Volatile.Read(ref generation))action();}));}catch(InvalidOperationException){}}
        internal void Translate(string text) {
            if(String.IsNullOrWhiteSpace(text))return;original=text;if(source.TextLength==0)source.Text=text;int serial=Interlocked.Increment(ref generation);BusyForTest=true;readableUntil=DateTime.MaxValue;result.Text=api.Text("正在翻译…");int selected=target.SelectedIndex;string language=selected==0?DesktopLanguages.AutoTarget(text):DesktopLanguages.Codes[selected-1],mode=quality.SelectedIndex==1?"quality":"fast";
            new Thread(delegate(){try{Thread.Sleep(60);if(serial!=Volatile.Read(ref generation))return;Func<bool> canceled=delegate{return serial!=Volatile.Read(ref generation);};string value=api.Stream!=null?api.Stream(text,language,mode,canceled,delegate(string part){Post(serial,delegate{Partial(part);});}):api.Translate(text,language,canceled);Post(serial,delegate{Complete(value);});}catch(Exception error){Post(serial,delegate{Complete(error.Message);});}}){IsBackground=true,Name="MiniPDF selection translation"}.Start();
        }
        internal void Recognize(Bitmap image) {
            int serial=Interlocked.Increment(ref generation);BusyForTest=true;result.Text=api.Text("正在识别选区…");string mode=quality.SelectedIndex==1?"quality":"fast";byte[] bytes;using(image)using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);bytes=stream.ToArray();}
            new Thread(delegate(){try{string text=api.Recognize(bytes,mode,delegate{return serial!=Volatile.Read(ref generation);});Post(serial,delegate{source.Text=text;if(String.IsNullOrWhiteSpace(text))Complete(api.Text("选区内未识别出文字，请重新框选。"));else Translate(text);});}catch(Exception error){Post(serial,delegate{Complete(error.Message);});}}){IsBackground=true,Name="MiniPDF selection OCR"}.Start();
        }
        internal void PinForTest(){pinned=true;}
        internal void TargetForTest(int value){target.SelectedIndex=value;}
        protected override bool ProcessCmdKey(ref Message msg,Keys keys){if(keys==Keys.Escape){Close();return true;}return base.ProcessCmdKey(ref msg,keys);}
        protected override void Dispose(bool disposing){if(disposing&&!cleaned){cleaned=true;Interlocked.Increment(ref generation);leave.Dispose();base.Dispose(true);bodyFont.Dispose();smallFont.Dispose();uiFont.Dispose();return;}base.Dispose(disposing);}
    }
    internal sealed class AiHotkey : NativeWindow,IDisposable {
        [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
        [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr window,int id);
        readonly Action action;internal bool Registered;
        internal AiHotkey(Action handler){action=handler;CreateHandle(new CreateParams());Registered=RegisterHotKey(Handle,1,0x4001,(uint)Keys.S);}
        protected override void WndProc(ref Message m){if(m.Msg==0x312){action();return;}base.WndProc(ref m);}
        public void Dispose(){if(Handle!=IntPtr.Zero){if(Registered)UnregisterHotKey(Handle,1);DestroyHandle();}}
    }
}
