using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed class SignatureWindow : Form {
        readonly SignaturePad pad=new SignaturePad();
        readonly ComboBox kind;
        readonly TextBox name;
        readonly CheckBox removeWhite;
        readonly NumericUpDown widthMm;
        readonly Label dimensions,error;
        readonly Button place;
        Bitmap imported,imagePreview;
        internal Bitmap Result;
        internal float WidthMm {get{return (float)widthMm.Value;}}
        internal SignatureWindow(float pageWidthMm=210){pad.PageWidthMm=Math.Max(1,pageWidthMm);FormUi.Setup(this,"放置可见签名",new Size(760,480));MinimumSize=new Size(660,500);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(12)};for(int i=0;i<7;i++)layout.RowStyles.Add(new RowStyle(i==2?SizeType.Percent:SizeType.AutoSize,i==2?100:0));Controls.Add(layout);
            kind=FormUi.Combo(180,"绘制签名","输入签名","图片签名");kind.Name="signatureKind";kind.Font=Font;
            var top=FormUi.Row(FormUi.Label("签名方式"),kind,FormUi.Button("导入图片…",Import),FormUi.Button("清除",delegate{pad.Clear();name.Clear();ReleaseImage();UpdatePreview();}));top.WrapContents=true;top.Dock=DockStyle.Fill;layout.Controls.Add(top,0,0);
            name=new TextBox{Name="signatureName",Dock=DockStyle.Fill,MaxLength=100,Font=Font,BorderStyle=BorderStyle.FixedSingle};var input=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,Margin=new Padding(0,2,0,8)};input.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));input.Controls.Add(FormUi.Label("姓名（输入后立即预览）"),0,0);input.Controls.Add(name,1,0);layout.Controls.Add(input,0,1);
            pad.Dock=DockStyle.Fill;pad.MinimumSize=new Size(200,150);pad.Margin=new Padding(0,2,0,8);layout.Controls.Add(pad,0,2);
            widthMm=FormUi.Number(45,1,300,80,1);widthMm.Name="signatureWidth";dimensions=FormUi.Label("");removeWhite=FormUi.Check("移除图片白底",true);removeWhite.Name="removeWhite";var sizing=FormUi.Row(FormUi.Label("页面上的宽度 (mm)"),widthMm,dimensions,removeWhite);sizing.WrapContents=true;sizing.Dock=DockStyle.Fill;layout.Controls.Add(sizing,0,3);
            var hint=FormUi.Label("单击页面放置，或拖框设定大小；放置后拖动角点缩放。\n可见签名是图片；数字签名请使用证书菜单。");hint.MaximumSize=new Size(1100,0);layout.Controls.Add(hint,0,4);
            error=new Label{AutoSize=true,ForeColor=Color.Firebrick,Margin=new Padding(0,3,0,5)};layout.Controls.Add(error,0,5);
            place=FormUi.Button("放置签名",delegate{try{using(var image=Build()){if(Result!=null)Result.Dispose();Result=new Bitmap(image);DialogResult=DialogResult.OK;}}catch(Exception ex){error.Text=ex.Message;}});place.Name="placeSignature";var footer=FormUi.Row(place,FormUi.Button("取消",delegate{DialogResult=DialogResult.Cancel;}));footer.WrapContents=true;layout.Controls.Add(footer,0,6);AcceptButton=place;CancelButton=(IButtonControl)footer.Controls[1];
            kind.SelectedIndexChanged+=delegate{pad.Kind=kind.SelectedIndex;UpdatePreview();};name.TextChanged+=delegate{pad.Value=name.Text;if(name.Text.Length>0)kind.SelectedIndex=1;UpdatePreview();};widthMm.ValueChanged+=delegate{UpdatePreview();};removeWhite.CheckedChanged+=delegate{RefreshImage();UpdatePreview();};pad.ContentChanged+=delegate{UpdatePreview();};UpdatePreview();
        }
        void Import(object sender,EventArgs e){using(var d=new OpenFileDialog{Filter=UiText.Get("图片 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"),RestoreDirectory=true})if(d.ShowDialog(this)==DialogResult.OK)try{using(var image=Image.FromFile(d.FileName))LoadImage(image);}catch(Exception ex){error.Text=ex.Message;}}
        internal void LoadImage(Image image){PdfDocument.Check(image.Width>0&&image.Height>0&&image.Width<=14000&&image.Height<=14000&&(long)image.Width*image.Height<=32000000,UiText.Get("图片过大，请先缩小至 3200 万像素以内。"));var copy=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(copy))g.DrawImage(image,new Rectangle(0,0,copy.Width,copy.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel);ReleaseImage();imported=copy;RefreshImage();kind.SelectedIndex=2;UpdatePreview();}
        void ReleaseImage(){pad.Image=null;if(imagePreview!=null){imagePreview.Dispose();imagePreview=null;}if(imported!=null){imported.Dispose();imported=null;}}
        void RefreshImage(){pad.Image=null;if(imagePreview!=null){imagePreview.Dispose();imagePreview=null;}if(imported!=null)try{using(var copy=new Bitmap(imported))imagePreview=Crop(copy,removeWhite.Checked);}catch(Exception ex){error.Text=ex.Message;}pad.Image=imagePreview;}
        internal Bitmap Build(){using(var full=pad.Export(Math.Min(4096,Math.Max(256,(int)Math.Ceiling(WidthMm*300/25.4f)))))return Crop(full);}
        void UpdatePreview(){pad.DesiredWidthMm=WidthMm;pad.Kind=kind.SelectedIndex;pad.Value=name.Text;removeWhite.Enabled=kind.SelectedIndex==2;pad.Invalidate();try{using(var image=Build()){dimensions.Text=String.Format(UiText.Get("高 {0:0.#} mm · 保持比例"),WidthMm*image.Height/image.Width);error.Text="";place.Enabled=true;}}catch{dimensions.Text="";place.Enabled=false;error.Text=kind.SelectedIndex==1?UiText.Get("请输入姓名。"):kind.SelectedIndex==2?UiText.Get("请导入含有签名的图片。"):UiText.Get("请在预览区域绘制，或直接输入姓名。");}}
        internal static Bitmap Crop(Bitmap image,bool removeWhite=false){int l=image.Width,r=-1,t=image.Height,b=-1;var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);try{var row=new byte[Math.Abs(data.Stride)];for(int y=0;y<image.Height;y++){IntPtr address=IntPtr.Add(data.Scan0,y*data.Stride);Marshal.Copy(address,row,0,row.Length);for(int x=0;x<image.Width;x++){int k=x*4;if(removeWhite&&row[k]>=245&&row[k+1]>=245&&row[k+2]>=245)row[k+3]=0;if(row[k+3]>8){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}}if(removeWhite)Marshal.Copy(row,0,address,row.Length);}}finally{image.UnlockBits(data);}PdfDocument.Check(r>=l,UiText.Get("请先绘制、输入或导入签名。"));l=Math.Max(0,l-3);t=Math.Max(0,t-3);r=Math.Min(image.Width-1,r+3);b=Math.Min(image.Height-1,b+3);return image.Clone(new Rectangle(l,t,r-l+1,b-t+1),PixelFormat.Format32bppArgb);}
        protected override void Dispose(bool disposing){if(disposing){ReleaseImage();if(Result!=null)Result.Dispose();}base.Dispose(disposing);}
        internal sealed class SignaturePad : Control {
            readonly List<List<PointF>> strokes=new List<List<PointF>>();List<PointF> active;
            internal event EventHandler ContentChanged;
            internal int Kind;
            internal string Value="";
            internal Image Image;
            internal float PageWidthMm=210,DesiredWidthMm=45;
            internal SignaturePad(){DoubleBuffered=true;BackColor=Color.White;Cursor=Cursors.Cross;SetStyle(ControlStyles.ResizeRedraw,true);}
            internal void Clear(){strokes.Clear();Invalidate();Changed();}
            void Changed(){if(ContentChanged!=null)ContentChanged(this,EventArgs.Empty);}
            internal RectangleF FitImage(Size size){float scale=Math.Min(Math.Max(1,(Width-24f)*(Kind==0?1:Math.Min(1,DesiredWidthMm/PageWidthMm)))/size.Width,Math.Max(1,Height-24f)/size.Height);return new RectangleF((Width-size.Width*scale)/2,(Height-size.Height*scale)/2,size.Width*scale,size.Height*scale);}
            PointF Position(Point p){var r=FitImage(new Size(640,200));return new PointF(Math.Max(0,Math.Min(640,(p.X-r.X)*640/r.Width)),Math.Max(0,Math.Min(200,(p.Y-r.Y)*200/r.Height)));}
            protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||Kind!=0)return;active=new List<PointF>{Position(e.Location)};strokes.Add(active);Capture=true;}
            protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(active==null||!Capture)return;if(active.Count<20000)active.Add(Position(e.Location));Invalidate();}
            protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(active!=null&&e.Button==MouseButtons.Left){active.Add(Position(e.Location));active=null;Capture=false;Invalidate();Changed();}}
            protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&active!=null){active=null;Changed();}}
            internal Bitmap Export(int targetWidth=0){if(Kind==2){PdfDocument.Check(Image!=null,UiText.Get("请导入含有签名的图片。"));return new Bitmap(Image);}if(Kind==1){PdfDocument.Check(!String.IsNullOrWhiteSpace(Value),UiText.Get("请输入姓名。"));bool cjk=false;foreach(char c in Value)if(c>0x2e80)cjk=true;using(var font=new Font(cjk?"Microsoft YaHei":"Segoe Script",60,FontStyle.Regular,GraphicsUnit.Pixel))using(var format=new StringFormat(StringFormat.GenericTypographic){FormatFlags=StringFormatFlags.NoWrap})using(var measure=new Bitmap(1,1)){SizeF size;using(var g=Graphics.FromImage(measure))size=g.MeasureString(Value,font,12000,format);float scale=targetWidth>0?(Math.Max(32,targetWidth)-24)/Math.Max(1,size.Width):1;var result=new Bitmap(Math.Max(32,(int)Math.Ceiling(size.Width*scale)+24),Math.Max(32,(int)Math.Ceiling(size.Height*scale)+24),PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(result))using(var brush=new SolidBrush(Color.FromArgb(24,50,80))){g.Clear(Color.Transparent);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;g.TranslateTransform(12,12);g.ScaleTransform(scale,scale);g.DrawString(Value,font,brush,PointF.Empty,format);}return result;}}
                int width=targetWidth>0?targetWidth:640;var output=new Bitmap(width,Math.Max(1,(int)Math.Round(width*200f/640)),PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(output))using(var pen=new Pen(Color.FromArgb(24,50,80),2.8f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){g.Clear(Color.Transparent);g.ScaleTransform(output.Width/640f,output.Height/200f);g.SmoothingMode=SmoothingMode.AntiAlias;foreach(var stroke in strokes)if(stroke.Count>1)g.DrawLines(pen,stroke.ToArray());else if(stroke.Count==1)g.FillEllipse(Brushes.Black,stroke[0].X-1,stroke[0].Y-1,2,2);}return output;}
            protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;try{using(var image=Export(Math.Max(512,ClientSize.Width-24))){var r=FitImage(image.Size);e.Graphics.DrawImage(image,r,new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel);}}catch{}using(var pen=new Pen(Color.FromArgb(212,220,229)))e.Graphics.DrawRectangle(pen,0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));}
        }
    }
}
