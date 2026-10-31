using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MiniAI.Desktop {
    internal sealed class BubbleBackend {
        internal Func<string,string,Func<bool>,string> Translate;
        internal Func<byte[],Func<bool>,string> Recognize;
        internal Func<string,string> Text;
    }
    internal static class DesktopLanguages {
        internal static readonly string[] Codes={"zh","en","ja","ko","fr","de","es","ru","pt","it","zh-Hant"};
        internal static readonly string[] Labels={"中文","English","日本語","한국어","Français","Deutsch","Español","Русский","Português","Italiano","繁體中文"};
        internal static string AutoTarget(string text){int chinese=0,letters=0;foreach(char c in text??""){if(c>=0x4e00&&c<=0x9fff)chinese++;else if(Char.IsLetter(c))letters++;}return chinese>0&&chinese>letters/3?"en":"zh";}
    }
    internal sealed class TranslationBubble : Form {
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")]static extern int SetWindowCompositionAttribute(IntPtr window,ref Composition data);
        [StructLayout(LayoutKind.Sequential)]struct Accent {internal int State,Flags,Color,Animation;}
        [StructLayout(LayoutKind.Sequential)]struct Composition {internal int Attribute;internal IntPtr Data;internal int Size;}
        readonly TextBox result,source;
        readonly ComboBox target;
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
        string original="";
        internal bool BusyForTest;
        internal string ResultForTest {get{return result.Text;}}
        internal string SourceForTest {get{return source.Text;}}
        internal TranslationBubble(BubbleBackend backend,Rectangle selection,bool image) {
            api=backend;anchor=new Rectangle(Cursor.Position,new Size(1,1));anchor.Inflate(20,20);ocr=image;
            uiFont=new Font("Segoe UI",10);bodyFont=new Font("Segoe UI",11);smallFont=new Font("Segoe UI",8.5f);
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Color.FromArgb(249,251,254);Font=uiFont;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);ClientSize=new Size(420,image?290:205);MinimumSize=new Size(320,165);Opacity=.97;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12,8,12,8),ColumnCount=1,RowCount=image?4:3};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));if(image)layout.RowStyles.Add(new RowStyle(SizeType.Absolute,72));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
            var header=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            target=new ComboBox{Width=132,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat};target.Items.Add(api.Text("中英自动互译"));target.Items.AddRange(DesktopLanguages.Labels);target.SelectedIndex=0;target.SelectedIndexChanged+=delegate{Translate(SelectedSource());};
            pin=SmallButton("Ctrl",delegate{pinned=!pinned;pin.BackColor=pinned?Color.LightBlue:BackColor;});var copy=SmallButton(api.Text("复制"),delegate{if(result.TextLength>0&&!BusyForTest)Clipboard.SetText(result.Text);});var close=SmallButton("×",delegate{Close();});header.Controls.AddRange(new Control[]{target,pin,copy,close});layout.Controls.Add(header,0,0);
            source=new TextBox{Name="RecognizedSelection",Multiline=true,Dock=DockStyle.Fill,Font=Font,BorderStyle=BorderStyle.FixedSingle,MaxLength=100000};source.TextChanged+=delegate{ScrollIfNeeded(source);};source.Resize+=delegate{ScrollIfNeeded(source);};
            source.MouseUp+=delegate{if(source.SelectionLength>0&&!String.IsNullOrWhiteSpace(source.SelectedText))Translate(source.SelectedText);};source.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.Enter){Translate(SelectedSource());e.SuppressKeyPress=true;}};
            if(image)layout.Controls.Add(source,0,1);else{source.Visible=false;Controls.Add(source);}
            result=new TextBox{Name="QuickTranslationResult",Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,Font=bodyFont,BorderStyle=BorderStyle.None,BackColor=BackColor,Margin=new Padding(0,8,0,6)};result.TextChanged+=delegate{ScrollIfNeeded(result);};result.Resize+=delegate{ScrollIfNeeded(result);};layout.Controls.Add(result,0,image?2:1);
            hint=new Label{AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(93,109,125),Font=smallFont,Text=api.Text(image?"可选中识别文字再译 · Ctrl+Enter 重译 · Ctrl 固定":"本机翻译 · 移开鼠标收起 · 按住 Ctrl 固定")};layout.Controls.Add(hint,0,image?3:2);
            leave=new System.Windows.Forms.Timer{Interval=100};leave.Tick+=delegate{bool control=GetAsyncKeyState(17)<0;if(pinned||control||target.DroppedDown||Bounds.Contains(Cursor.Position)||anchor.Contains(Cursor.Position)){outside=DateTime.UtcNow;return;}if((DateTime.UtcNow-outside).TotalMilliseconds>900)Close();};leave.Start();
            Shown+=delegate{Place(selection);Glass();};Resize+=delegate{Round();};
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{var value=base.CreateParams;value.ExStyle|=0x80;return value;}}
        Button SmallButton(string title,Action action){var button=new Button{Text=title,AutoSize=true,MinimumSize=new Size(32,26),FlatStyle=FlatStyle.Flat,Margin=new Padding(3,0,0,0),Padding=Padding.Empty,TabStop=false};button.FlatAppearance.BorderSize=0;button.Click+=delegate{action();};return button;}
        static void ScrollIfNeeded(TextBox box){int width=Math.Max(20,box.Width-SystemInformation.VerticalScrollBarWidth-8);int height=TextRenderer.MeasureText(box.Text,box.Font,new Size(width,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPadding).Height;var value=height>box.Height-6?ScrollBars.Vertical:ScrollBars.None;if(box.ScrollBars!=value)box.ScrollBars=value;}
        void Place(Rectangle selection){var area=Screen.FromRectangle(selection).WorkingArea;int x=Math.Min(area.Right-Width,Math.Max(area.Left,selection.Right-24)),y=selection.Bottom+8;if(y+Height>area.Bottom)y=Math.Max(area.Top,selection.Top-Height-8);Location=new Point(x,y);Round();}
        void Round(){if(Width<8||Height<8)return;using(var path=new GraphicsPath()){int d=16;path.AddArc(0,0,d,d,180,90);path.AddArc(Width-d,0,d,d,270,90);path.AddArc(Width-d,Height-d,d,d,0,90);path.AddArc(0,Height-d,d,d,90,90);path.CloseFigure();var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
        void Glass(){var accent=new Accent{State=4,Flags=2,Color=unchecked((int)0xe8f8f8f8)};IntPtr pointer=Marshal.AllocHGlobal(Marshal.SizeOf(accent));try{Marshal.StructureToPtr(accent,pointer,false);var data=new Composition{Attribute=19,Data=pointer,Size=Marshal.SizeOf(accent)};SetWindowCompositionAttribute(Handle,ref data);}catch(EntryPointNotFoundException){}finally{Marshal.FreeHGlobal(pointer);}}
        string SelectedSource(){return ocr?(source.SelectionLength>0?source.SelectedText:source.Text):original;}
        void Post(int serial,Action action){try{if(!IsDisposed&&IsHandleCreated)BeginInvoke(new Action(delegate{if(!IsDisposed&&serial==Volatile.Read(ref generation))action();}));}catch(InvalidOperationException){}}
        internal void Translate(string text) {
            if(String.IsNullOrWhiteSpace(text))return;original=text;int serial=Interlocked.Increment(ref generation);BusyForTest=true;result.Text=api.Text("正在翻译…");int selected=target.SelectedIndex;string language=selected==0?DesktopLanguages.AutoTarget(text):DesktopLanguages.Codes[selected-1];
            new Thread(delegate(){try{Thread.Sleep(120);if(serial!=Volatile.Read(ref generation))return;string value=api.Translate(text,language,delegate{return serial!=Volatile.Read(ref generation);});Post(serial,delegate{BusyForTest=false;result.Text=value;outside=DateTime.UtcNow;});}catch(Exception error){Post(serial,delegate{BusyForTest=false;result.Text=error.Message;});}}){IsBackground=true,Name="MiniPDF selection translation"}.Start();
        }
        internal void Recognize(Bitmap image) {
            int serial=Interlocked.Increment(ref generation);BusyForTest=true;result.Text=api.Text("正在识别选区…");byte[] bytes;using(image)using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);bytes=stream.ToArray();}
            new Thread(delegate(){try{string text=api.Recognize(bytes,delegate{return serial!=Volatile.Read(ref generation);});Post(serial,delegate{source.Text=text;BusyForTest=false;if(String.IsNullOrWhiteSpace(text))result.Text=api.Text("选区内未识别出文字，请重新框选。");else Translate(text);});}catch(Exception error){Post(serial,delegate{BusyForTest=false;result.Text=error.Message;});}}){IsBackground=true,Name="MiniPDF selection OCR"}.Start();
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
