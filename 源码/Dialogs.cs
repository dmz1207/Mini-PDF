using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed class TextDialog : Form {
        readonly TextBox text;
        readonly NumericUpDown size;
        readonly ComboBox face;
        readonly Button colorButton;
        public string Value {get{return text.Text;}}
        public float TextSize {get{return (float)size.Value;}}
        public string Face {get{return face.Text;}}
        public Color Ink {get;private set;}
        internal void EnterForTest(string value){text.Text=value;}
        public TextDialog(string title,string value,float fontSize,Color ink,bool note,bool ocr=false) {
            Text=title;Font=new Font("Microsoft YaHei UI",9);StartPosition=FormStartPosition.CenterParent;
            MinimizeBox=false;MaximizeBox=false;ShowInTaskbar=false;MinimumSize=new Size(460,300);ClientSize=new Size(540,330);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            Ink=Color.FromArgb(255,ink.R,ink.G,ink.B);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),RowCount=4,ColumnCount=1};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));
            layout.Controls.Add(new Label{Text=note?"便签会作为标准 PDF 批注保存。":ocr?"修改 OCR 识别层，保留隐藏属性；扫描图像上的文字保持原样。":"文字按当前文字块编辑；新文字保留原位置，可选择替换字体。",Dock=DockStyle.Fill,AutoSize=true},0,0);
            var style=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
            style.Controls.Add(new Label{Text="字体",AutoSize=true,Margin=new Padding(0,6,6,0)});
            face=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=160};face.Items.AddRange(new object[]{"自动","Microsoft YaHei","SimSun","Arial","Times-Roman","Courier"});face.SelectedIndex=0;style.Controls.Add(face);
            style.Controls.Add(new Label{Text="字号",AutoSize=true,Margin=new Padding(10,6,6,0)});
            size=new NumericUpDown{Minimum=1,Maximum=300,DecimalPlaces=1,Value=(decimal)Math.Max(1,Math.Min(300,fontSize)),Width=68};style.Controls.Add(size);
            colorButton=new Button{Text="颜色",AutoSize=true,BackColor=Ink,ForeColor=Ink.GetBrightness()<0.5?Color.White:Color.Black,Margin=new Padding(10,0,0,0)};
            colorButton.Click+=delegate{using(var d=new ColorDialog{Color=Ink,FullOpen=true})if(d.ShowDialog(this)==DialogResult.OK){Ink=d.Color;colorButton.BackColor=Ink;colorButton.ForeColor=Ink.GetBrightness()<0.5?Color.White:Color.Black;}};style.Controls.Add(colorButton);
            if(note)style.Visible=false;layout.Controls.Add(style,0,1);
            text=new TextBox{Multiline=true,AcceptsReturn=true,AcceptsTab=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Text=value,Font=new Font("Microsoft YaHei UI",11)};layout.Controls.Add(text,0,2);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,8,0,0)};
            var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel,AutoSize=true};var okay=new Button{Text="确定",AutoSize=true};
            okay.Click+=delegate{if(String.IsNullOrWhiteSpace(Value)){text.Focus();return;}DialogResult=DialogResult.OK;};buttons.Controls.Add(cancel);buttons.Controls.Add(okay);layout.Controls.Add(buttons,0,3);
            Controls.Add(layout);AcceptButton=okay;CancelButton=cancel;Shown+=delegate{text.Focus();text.SelectAll();};
        }
    }
    internal static class Dialogs {
        public static string Ask(IWin32Window owner,string title,string hint,string value,bool password) {
            using(var f=new Form{Text=title,Font=new Font("Microsoft YaHei UI",9),ClientSize=new Size(430,145),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent,AutoScaleDimensions=new SizeF(96,96),AutoScaleMode=AutoScaleMode.Dpi}) {
                var label=new Label{Left=16,Top=15,Width=395,Height=32,Text=hint};var input=new TextBox{Left=16,Top=52,Width=395,Text=value,UseSystemPasswordChar=password};
                var ok=new Button{Left=245,Top=98,Width=78,Text="确定",DialogResult=DialogResult.OK};var cancel=new Button{Left=333,Top=98,Width=78,Text="取消",DialogResult=DialogResult.Cancel};f.Controls.AddRange(new Control[]{label,input,ok,cancel});f.AcceptButton=ok;f.CancelButton=cancel;f.Shown+=delegate{input.Focus();input.SelectAll();};
                return f.ShowDialog(owner)==DialogResult.OK?input.Text:null;
            }
        }
    }
}
