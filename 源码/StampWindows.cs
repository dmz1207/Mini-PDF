using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed class WatermarkWindow : Form {
        readonly PdfDocument document;
        readonly TextBox text,ranges;
        readonly ComboBox range,position;
        readonly NumericUpDown size,angle,opacity,width;
        readonly CheckBox tile,behind,replace;
        readonly PictureBox preview;
        readonly Label error;
        readonly Timer delay=new Timer{Interval=300};
        Color color=Color.Gray;
        Bitmap sourceImage;
        internal WatermarkOptions Options;
        internal WatermarkWindow(PdfDocument doc){document=doc;FormUi.Setup(this,"水印",new Size(850,580));var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));Controls.Add(root);var left=FormUi.Column();root.Controls.Add(left,0,0);
            text=new TextBox{Width=365,BorderStyle=BorderStyle.FixedSingle,Text="CONFIDENTIAL",MaxLength=200};left.Controls.Add(FormUi.Row(text));left.Controls.Add(FormUi.Row(FormUi.Button("图片水印…",Import),FormUi.Button("使用文字",delegate{if(sourceImage!=null){sourceImage.Dispose();sourceImage=null;}Changed();})));
            size=FormUi.Number(48,4,300,66,0);angle=FormUi.Number(45,-180,180,66,0);left.Controls.Add(FormUi.Row(FormUi.Label("字号"),size,FormUi.Label("旋转角度"),angle));
            opacity=FormUi.Number(20,1,100,66,0);width=FormUi.Number(40,1,100,66,0);left.Controls.Add(FormUi.Row(FormUi.Label("不透明度 %"),opacity,FormUi.Button("颜色…",delegate{using(var d=new ColorDialog{Color=color})if(d.ShowDialog(this)==DialogResult.OK){color=d.Color;Changed();}})));left.Controls.Add(FormUi.Row(FormUi.Label("图片宽度 %"),width));
            position=FormUi.Combo(150,"居中","顶部","底部");tile=FormUi.Check("平铺",false);left.Controls.Add(FormUi.Row(position,tile));behind=FormUi.Check("置于页面内容下方",false);replace=FormUi.Check("替换现有 Mini PDF 水印",true);left.Controls.Add(FormUi.Row(behind));left.Controls.Add(FormUi.Row(replace));
            range=FormUi.Combo(120,"所有页面","当前页面","页码范围");ranges=new TextBox{Width=230,BorderStyle=BorderStyle.FixedSingle,Text="1-"+doc.Count};left.Controls.Add(FormUi.Row(range,ranges));left.Controls.Add(new Label{Text=UiText.Get("文字支持 {page} 和 {pages} 页码占位符。\n预览显示当前页面；应用后可撤销。"),AutoSize=true,MaximumSize=new Size(370,0),Margin=new Padding(0,8,0,12)});
            error=new Label{AutoSize=true,MaximumSize=new Size(370,0),ForeColor=Color.Firebrick};left.Controls.Add(error);var footer=FormUi.Row(FormUi.Button("应用",delegate{try{Options=Read();DialogResult=DialogResult.OK;}catch(Exception e){error.Text=e.Message;}}),FormUi.Button("取消",delegate{DialogResult=DialogResult.Cancel;}));left.Controls.Add(footer);AcceptButton=(IButtonControl)footer.Controls[0];CancelButton=(IButtonControl)footer.Controls[1];
            preview=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(237,240,244),Margin=new Padding(12)};root.Controls.Add(preview,1,0);foreach(var c in new[]{range,position})c.SelectedIndexChanged+=delegate{Changed();};foreach(var c in new[]{tile,behind,replace})c.CheckedChanged+=delegate{Changed();};foreach(var c in new[]{size,angle,opacity,width})c.ValueChanged+=delegate{Changed();};text.TextChanged+=delegate{Changed();};ranges.TextChanged+=delegate{Changed();};delay.Tick+=delegate{delay.Stop();UpdatePreview();};Shown+=delegate{UpdatePreview();};Changed();
        }
        void Import(object sender,EventArgs e){using(var d=new OpenFileDialog{Filter=UiText.Get("图片 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"),RestoreDirectory=true})if(d.ShowDialog(this)==DialogResult.OK)try{using(var image=Image.FromFile(d.FileName)){PdfDocument.Check(image.Width<=6000&&image.Height<=6000&&(long)image.Width*image.Height<=12000000,UiText.Get("水印图片请缩小至 1200 万像素以内。"));if(sourceImage!=null)sourceImage.Dispose();sourceImage=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(sourceImage))g.DrawImageUnscaled(image,0,0);}Changed();}catch(Exception ex){error.Text=ex.Message;}}
        WatermarkOptions Read(){var o=new WatermarkOptions{Pages=PrintEngine.SelectPages(document.Count,document.Index,range.SelectedIndex,ranges.Text,0,false),Text=text.Text,Image=sourceImage,Size=(float)size.Value,Angle=(float)angle.Value,Opacity=(float)opacity.Value,ImageWidth=(float)width.Value,Position=position.SelectedIndex,Tile=tile.Checked,Behind=behind.Checked,Replace=replace.Checked,Color=color};PdfDocument.Check(sourceImage!=null||!String.IsNullOrWhiteSpace(text.Text),UiText.Get("请输入水印文字或选择图片。"));return o;}
        void Changed(){ranges.Enabled=range.SelectedIndex==2;text.Enabled=size.Enabled=sourceImage==null;width.Enabled=sourceImage!=null;position.Enabled=!tile.Checked;delay.Stop();delay.Start();}
        void UpdatePreview(){try{var o=Read();using(var copy=document.CopyPage(document.Index)){bool selected=Array.IndexOf(o.Pages,document.Index)>=0;if(selected){o.Pages=new[]{0};o.Text=o.Text.Replace("{page}",(document.Index+1).ToString()).Replace("{pages}",document.Count.ToString());copy.Watermark(o);}var s=copy.PageSize(0);float scale=Math.Min(700/s.Width,900/s.Height);var image=copy.Render(0,Math.Max(1,(int)(s.Width*scale)),Math.Max(1,(int)(s.Height*scale)));var old=preview.Image;preview.Image=image;if(old!=null)old.Dispose();error.Text=selected?"":UiText.Get("当前页面不在水印范围内。");}}catch(Exception e){error.Text=e.Message;}}
        protected override void Dispose(bool disposing){if(disposing){delay.Dispose();if(sourceImage!=null)sourceImage.Dispose();if(preview.Image!=null)preview.Image.Dispose();}base.Dispose(disposing);}
    }
    internal sealed class CertificateWindow : Form {
        readonly ComboBox certificates;
        readonly TextBox reason,location;
        readonly Label info;
        readonly List<X509Certificate2> owned=new List<X509Certificate2>();
        internal X509Certificate2 Certificate {get{return certificates.SelectedItem as X509Certificate2;}}
        internal string Reason {get{return reason.Text;}}
        internal string SigningLocation {get{return location.Text;}}
        internal CertificateWindow(){FormUi.Setup(this,"证书数字签名",new Size(660,390));var controls=FormUi.Column();Controls.Add(controls);certificates=FormUi.Combo(600);certificates.Format+=delegate(object s,ListControlConvertEventArgs e){var cert=e.ListItem as X509Certificate2;if(cert!=null)e.Value=cert.GetNameInfo(X509NameType.SimpleName,false)+" · "+cert.NotAfter.ToString("yyyy-MM-dd");};certificates.FormattingEnabled=true;controls.Controls.Add(FormUi.Row(certificates));
            controls.Controls.Add(FormUi.Row(FormUi.Button("载入 PFX / P12…",Import),FormUi.Button("查看证书…",delegate{if(Certificate!=null)X509Certificate2UI.DisplayCertificate(Certificate,Handle);})));
            reason=new TextBox{Width=440,BorderStyle=BorderStyle.FixedSingle,MaxLength=200};location=new TextBox{Width=440,BorderStyle=BorderStyle.FixedSingle,MaxLength=200};controls.Controls.Add(FormUi.Row(FormUi.Label("签署原因"),reason));controls.Controls.Add(FormUi.Row(FormUi.Label("地点"),location));
            controls.Controls.Add(new Label{Text=UiText.Get("使用 SHA-256 / CMS 签署并另存为副本。\n可先放置可见签名；证书签名本身不增加页面图案。\n不包含可信时间戳、长期验证或云端签名服务。"),MaximumSize=new Size(600,0),AutoSize=true,Margin=new Padding(0,10,0,12)});
            info=new Label{MaximumSize=new Size(600,0),AutoSize=true,ForeColor=Color.Firebrick};controls.Controls.Add(info);var footer=FormUi.Row(FormUi.Button("签署并另存为…",delegate{try{PdfSignatures.CheckCertificate(Certificate);DialogResult=DialogResult.OK;}catch(Exception e){info.Text=e.Message;}}),FormUi.Button("取消",delegate{DialogResult=DialogResult.Cancel;}));controls.Controls.Add(footer);AcceptButton=(IButtonControl)footer.Controls[0];CancelButton=(IButtonControl)footer.Controls[1];
            foreach(var where in new[]{StoreLocation.CurrentUser,StoreLocation.LocalMachine})try{using(var store=new X509Store(StoreName.My,where)){store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);foreach(var cert in store.Certificates){if(cert.HasPrivateKey){owned.Add(cert);certificates.Items.Add(cert);}else cert.Dispose();}}}catch{}
            if(certificates.Items.Count>0)certificates.SelectedIndex=0;else info.Text=UiText.Get("未找到含私钥的个人证书，可载入 PFX / P12 文件。");
        }
        void Import(object sender,EventArgs e){using(var d=new OpenFileDialog{Filter="PKCS #12 (*.pfx;*.p12)|*.pfx;*.p12",RestoreDirectory=true})if(d.ShowDialog(this)==DialogResult.OK){string password=Dialogs.Ask(this,UiText.Get("证书密码"),UiText.Get("输入 PFX / P12 密码（允许为空）："),"",true);if(password==null)return;try{var cert=new X509Certificate2(System.IO.File.ReadAllBytes(d.FileName),password,(X509KeyStorageFlags)32);try{PdfSignatures.CheckCertificate(cert);}catch{cert.Dispose();throw;}owned.Add(cert);certificates.Items.Add(cert);certificates.SelectedItem=cert;info.Text="";}catch(Exception ex){info.Text=ex.Message;}}}
        protected override void Dispose(bool disposing){if(disposing)foreach(var cert in owned)cert.Dispose();base.Dispose(disposing);}
    }
}
