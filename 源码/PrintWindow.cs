using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniPdf {
    internal static class FormUi {
        internal static Label Label(string text){return new Label{Text=UiText.Get(text),AutoSize=true,Margin=new Padding(0,7,8,0)};}
        internal static ComboBox Combo(int width,params string[] values){var c=new ComboBox{Width=width,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat};foreach(string s in values)c.Items.Add(UiText.Get(s));if(values.Length>0)c.SelectedIndex=0;return c;}
        internal static NumericUpDown Number(decimal value,decimal min,decimal max,int width,int decimals){return new NumericUpDown{Width=width,Minimum=min,Maximum=max,DecimalPlaces=decimals,Value=value,BorderStyle=BorderStyle.FixedSingle};}
        internal static CheckBox Check(string text,bool value){return new CheckBox{Text=UiText.Get(text),Checked=value,AutoSize=true,Margin=new Padding(0,7,12,0)};}
        internal static Button Button(string text,EventHandler click){var b=new Button{Text=UiText.Get(text),AutoSize=true,FlatStyle=FlatStyle.Flat,BackColor=Color.White,Padding=new Padding(5,2,5,2),Margin=new Padding(0,1,8,1)};b.FlatAppearance.BorderColor=Color.FromArgb(210,217,226);if(click!=null)b.Click+=click;return b;}
        internal static FlowLayoutPanel Row(params Control[] controls){var r=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,Margin=new Padding(0,2,0,5)};r.Controls.AddRange(controls);return r;}
        internal static FlowLayoutPanel Column(){return new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(14)};}
        static void FitControls(Control parent){foreach(Control child in parent.Controls){var combo=child as ComboBox;if(combo!=null){int width=combo.Width;foreach(var value in combo.Items)if(value is string)width=Math.Max(width,TextRenderer.MeasureText((string)value,combo.Font).Width+SystemInformation.VerticalScrollBarWidth+12);combo.Width=Math.Min(720,width);combo.DropDownWidth=Math.Max(combo.Width,Math.Min(900,width));}var number=child as NumericUpDown;if(number!=null)number.Width=Math.Max(number.Width,TextRenderer.MeasureText(number.Maximum.ToString("F"+number.DecimalPlaces),number.Font).Width+SystemInformation.VerticalScrollBarWidth+10);FitControls(child);}}
        internal static void Setup(Form form,string title,Size size){form.Text=UiText.Get(title);form.Font=new Font("Segoe UI",10);form.BackColor=Color.White;form.ClientSize=size;form.MinimumSize=new Size(size.Width+16,size.Height+39);form.StartPosition=FormStartPosition.CenterParent;form.ShowInTaskbar=false;form.MinimizeBox=false;form.AutoScaleDimensions=new SizeF(96,96);form.AutoScaleMode=AutoScaleMode.Dpi;form.Shown+=delegate{FitControls(form);};}
    }
    internal sealed class PrintWindow : Form {
        readonly PdfDocument document;
        readonly PrinterSettings printer=new PrinterSettings();
        PageSettings pageSettings;
        readonly ComboBox printers,range,parity,mode,sizing,orientation,perSheet,duplex,booklet,dpi,papers;
        readonly NumericUpDown copies,percent,margin,overlap;
        readonly TextBox ranges;
        readonly CheckBox asImage,gray,notes,reverse,collate,border,rightBinding,pdfPaper,cutMarks;
        readonly FlowLayoutPanel scaleRow,nupRow,bookRow,posterRow;
        readonly Label info,errors,bookletHelp;
        readonly Panel preview;
        readonly Button printButton;
        readonly Timer delay=new Timer{Interval=240};
        List<PrintSheet> sheets;
        PrintOptions options;
        Bitmap previewImage;
        int selected;
        bool loading=true;
        [DllImport("winspool.drv",CharSet=CharSet.Unicode)]static extern bool OpenPrinter(string name,out IntPtr printer,IntPtr defaults);
        [DllImport("winspool.drv")]static extern bool ClosePrinter(IntPtr printer);
        [DllImport("winspool.drv",CharSet=CharSet.Unicode)]static extern int DocumentProperties(IntPtr window,IntPtr printer,string name,IntPtr output,IntPtr input,int mode);
        [DllImport("kernel32.dll")]static extern IntPtr GlobalLock(IntPtr handle);
        [DllImport("kernel32.dll")]static extern bool GlobalUnlock(IntPtr handle);
        [DllImport("kernel32.dll")]static extern IntPtr GlobalFree(IntPtr handle);
        internal PrintWindow(PdfDocument doc) {
            document=doc;FormUi.Setup(this,"打印",new Size(1050,650));pageSettings=new PageSettings(printer);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));Controls.Add(root);
            var controls=FormUi.Column();root.Controls.Add(controls,0,0);
            printers=FormUi.Combo(340);try{foreach(string name in PrinterSettings.InstalledPrinters)printers.Items.Add(name);printers.SelectedItem=printer.PrinterName;if(printers.SelectedIndex<0&&printers.Items.Count>0)printers.SelectedIndex=0;}catch(Exception e){printers.Items.Add(e.Message);}
            controls.Controls.Add(FormUi.Row(FormUi.Label("打印机"),printers));
            controls.Controls.Add(FormUi.Row(FormUi.Button("打印机属性…",delegate{Properties();}),FormUi.Button("页面设置…",delegate{using(var d=new PageSetupDialog{PrinterSettings=printer,PageSettings=pageSettings,EnableMetric=true})if(d.ShowDialog(this)==DialogResult.OK){ReloadPaper();Changed();}})));
            copies=FormUi.Number(1,1,999,64,0);collate=FormUi.Check("逐份打印",true);controls.Controls.Add(FormUi.Row(FormUi.Label("份数"),copies,collate));
            range=FormUi.Combo(145,"所有页面","当前页面","页码范围");ranges=new TextBox{Width=250,BorderStyle=BorderStyle.FixedSingle,Text="1-"+doc.Count};controls.Controls.Add(FormUi.Row(range,ranges));
            parity=FormUi.Combo(180,"奇数和偶数页","仅奇数页","仅偶数页");reverse=FormUi.Check("逆序打印",false);controls.Controls.Add(FormUi.Row(parity,reverse));
            mode=FormUi.Combo(210,"大小","多页合一","小册子","海报拼接");controls.Controls.Add(FormUi.Row(FormUi.Label("页面处理"),mode));
            sizing=FormUi.Combo(205,"适合可打印区域","实际大小","缩小过大的页面","自定义比例");percent=FormUi.Number(100,1,6400,76,2);scaleRow=FormUi.Row(sizing,percent,FormUi.Label("%"));controls.Controls.Add(scaleRow);
            perSheet=FormUi.Combo(90,"2","4","6","9","16");border=FormUi.Check("绘制页面边框",false);nupRow=FormUi.Row(FormUi.Label("每张纸的页数"),perSheet,border);controls.Controls.Add(nupRow);
            booklet=FormUi.Combo(160,"正反两面","仅正面","仅背面");rightBinding=FormUi.Check("从右侧装订",false);bookRow=FormUi.Row(booklet,rightBinding);controls.Controls.Add(bookRow);
            bookletHelp=new Label{Text=UiText.Get("小册子通常使用横向、短边翻转；手动双面请按打印机进纸方向翻纸。"),MaximumSize=new Size(476,0),AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(0,0,0,8)};controls.Controls.Add(bookletHelp);
            overlap=FormUi.Number(5,0,100,64,1);cutMarks=FormUi.Check("裁切标记",false);posterRow=FormUi.Row(FormUi.Label("重叠 (mm)"),overlap,cutMarks);controls.Controls.Add(posterRow);
            orientation=FormUi.Combo(210,"自动纵向 / 横向","纵向","横向");controls.Controls.Add(FormUi.Row(FormUi.Label("方向"),orientation));
            papers=FormUi.Combo(240);controls.Controls.Add(FormUi.Row(FormUi.Label("纸张"),papers));
            pdfPaper=FormUi.Check("按照 PDF 页面大小选择纸张",false);controls.Controls.Add(FormUi.Row(pdfPaper));
            margin=FormUi.Number(0,0,80,64,1);dpi=FormUi.Combo(88,"600","300","1200");controls.Controls.Add(FormUi.Row(FormUi.Label("附加页边距 (mm)"),margin,FormUi.Label("DPI"),dpi));
            duplex=FormUi.Combo(330,"使用打印机默认双面设置","单面","双面：长边翻转","双面：短边翻转");controls.Controls.Add(FormUi.Row(duplex));
            asImage=FormUi.Check("作为图像打印（兼容模式）",false);controls.Controls.Add(FormUi.Row(asImage));
            gray=FormUi.Check("以灰度（黑白）打印",false);notes=FormUi.Check("包含批注",true);controls.Controls.Add(FormUi.Row(gray,notes));
            errors=new Label{MaximumSize=new Size(475,0),AutoSize=true,ForeColor=Color.Firebrick,Margin=new Padding(0,5,0,8)};controls.Controls.Add(errors);
            var previewArea=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(8)};previewArea.RowStyles.Add(new RowStyle(SizeType.Percent,100));previewArea.RowStyles.Add(new RowStyle(SizeType.Absolute,42));previewArea.RowStyles.Add(new RowStyle(SizeType.Absolute,50));root.Controls.Add(previewArea,1,0);
            preview=new BufferedPanel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(238,241,245)};preview.Paint+=PaintPreview;previewArea.Controls.Add(preview,0,0);
            var nav=FormUi.Row(FormUi.Button("‹",delegate{Navigate(-1);}),FormUi.Button("›",delegate{Navigate(1);}));nav.Dock=DockStyle.Fill;previewArea.Controls.Add(nav,0,1);info=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.TopCenter};previewArea.Controls.Add(info,0,2);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};root.Controls.Add(footer,0,1);root.SetColumnSpan(footer,2);
            footer.Controls.Add(FormUi.Button("取消",delegate{DialogResult=DialogResult.Cancel;}));printButton=FormUi.Button("打印",delegate{RefreshPlan();if(options!=null&&printer.IsValid){printer.Copies=(short)copies.Value;printer.Collate=collate.Checked;DialogResult=DialogResult.OK;}});footer.Controls.Add(printButton);AcceptButton=printButton;CancelButton=(IButtonControl)footer.Controls[0];
            printers.SelectedIndexChanged+=delegate{if(loading)return;try{printer.PrinterName=printers.Text;pageSettings=new PageSettings(printer);ReloadPaper();}catch(Exception e){errors.Text=e.Message;}Changed();};
            papers.SelectedIndexChanged+=delegate{if(!loading&&papers.SelectedItem is PaperSize){pageSettings.PaperSize=(PaperSize)papers.SelectedItem;Changed();}};
            foreach(var c in new[]{range,parity,mode,sizing,orientation,perSheet,duplex,booklet,dpi})c.SelectedIndexChanged+=delegate{Changed();};
            foreach(var c in new[]{asImage,gray,notes,reverse,collate,border,rightBinding,pdfPaper,cutMarks})c.CheckedChanged+=delegate{Changed();};
            foreach(var c in new[]{copies,percent,margin,overlap})c.ValueChanged+=delegate{Changed();};ranges.TextChanged+=delegate{Changed();};
            delay.Tick+=delegate{delay.Stop();RefreshPlan();};ReloadPaper();loading=false;Shown+=delegate{RefreshPlan();};Resize+=delegate{preview.Invalidate();};Changed();
        }
        void ReloadPaper(){bool was=loading;loading=true;try{papers.Items.Clear();if(printer.IsValid){foreach(PaperSize paper in printer.PaperSizes)papers.Items.Add(paper);papers.DisplayMember="PaperName";for(int i=0;i<papers.Items.Count;i++)if(((PaperSize)papers.Items[i]).RawKind==pageSettings.PaperSize.RawKind){papers.SelectedIndex=i;break;}}if(papers.Items.Count==0){papers.Items.Add(new PaperSize("A4",827,1169));papers.DisplayMember="PaperName";papers.SelectedIndex=0;}}finally{loading=was;}}
        void Properties(){if(!printer.IsValid)return;IntPtr handle=IntPtr.Zero,mode=IntPtr.Zero,pointer=IntPtr.Zero;try{if(!OpenPrinter(printer.PrinterName,out handle,IntPtr.Zero))throw new InvalidOperationException(UiText.Get("无法打开打印机属性。"));mode=printer.GetHdevmode(pageSettings);pointer=GlobalLock(mode);if(pointer==IntPtr.Zero)throw new InvalidOperationException(UiText.Get("无法打开打印机属性。"));if(DocumentProperties(Handle,handle,printer.PrinterName,pointer,pointer,14)==1){printer.SetHdevmode(mode);pageSettings.SetHdevmode(mode);ReloadPaper();Changed();}}catch(Exception e){errors.Text=e.Message;}finally{if(pointer!=IntPtr.Zero)GlobalUnlock(mode);if(mode!=IntPtr.Zero)GlobalFree(mode);if(handle!=IntPtr.Zero)ClosePrinter(handle);}}
        void Changed(){if(loading)return;dpi.Enabled=asImage.Checked;ranges.Enabled=range.SelectedIndex==2;nupRow.Visible=mode.SelectedIndex==1;bookRow.Visible=bookletHelp.Visible=mode.SelectedIndex==2;posterRow.Visible=mode.SelectedIndex==3;scaleRow.Visible=mode.SelectedIndex==0||mode.SelectedIndex==3;sizing.Visible=mode.SelectedIndex!=3;percent.Enabled=mode.SelectedIndex==3||sizing.SelectedIndex==3;pdfPaper.Enabled=mode.SelectedIndex==0;delay.Stop();delay.Start();}
        void RefreshPlan(){delay.Stop();try{options=new PrintOptions{Pages=PrintEngine.SelectPages(document.Count,document.Index,range.SelectedIndex,ranges.Text,parity.SelectedIndex,reverse.Checked),Mode=(PrintMode)mode.SelectedIndex,Sizing=(PrintScale)sizing.SelectedIndex,Scale=(float)percent.Value,PerSheet=int.Parse(perSheet.Text),Orientation=orientation.SelectedIndex,Gray=gray.Checked,Annotations=notes.Checked,Border=border.Checked,MarginMm=(float)margin.Value,OverlapMm=(float)overlap.Value,CutMarks=cutMarks.Checked,BookletSides=booklet.SelectedIndex,RightBinding=rightBinding.Checked,DuplexMode=duplex.SelectedIndex,Dpi=int.Parse(dpi.Text),PdfPaper=pdfPaper.Checked&&mode.SelectedIndex==0,AsImage=asImage.Checked};
                if(options.DuplexMode>=2&&printer.IsValid&&!printer.CanDuplex)throw new InvalidOperationException(UiText.Get("所选打印机不支持自动双面打印。"));
                sheets=PrintEngine.Plan(document,options,pageSettings);selected=Math.Min(selected,sheets.Count-1);errors.Text=printer.IsValid?"":UiText.Get("没有可用的打印机。仍可查看排版预览。");printButton.Enabled=printer.IsValid;RenderPreview();
            }catch(Exception e){options=null;sheets=null;errors.Text=e.Message;printButton.Enabled=false;info.Text="";if(previewImage!=null){previewImage.Dispose();previewImage=null;}preview.Invalidate();}}
        void Navigate(int delta){if(sheets==null)return;selected=Math.Max(0,Math.Min(sheets.Count-1,selected+delta));RenderPreview();}
        void RenderPreview(){if(previewImage!=null){previewImage.Dispose();previewImage=null;}var sheet=sheets[selected];float ratio=Math.Min(900f/sheet.Size.Width,1100f/sheet.Size.Height);previewImage=new Bitmap(Math.Max(1,(int)(sheet.Size.Width*ratio)),Math.Max(1,(int)(sheet.Size.Height*ratio)));using(var g=Graphics.FromImage(previewImage)){g.Clear(Color.White);g.ScaleTransform(ratio,ratio);PrintEngine.Draw(document,options,sheet,g,Math.Max(36,(int)(ratio*100)));using(var pen=new Pen(Color.FromArgb(210,217,225),1/ratio){DashStyle=DashStyle.Dash})g.DrawRectangle(pen,sheet.Area.X,sheet.Area.Y,sheet.Area.Width,sheet.Area.Height);}
            var list=new List<string>();foreach(var item in sheet.Items)list.Add((item.Page+1).ToString());info.Text=String.Format(UiText.Get("纸面 {0} / {1} · 页码 {2}\n{3:0.#} × {4:0.#} mm"),selected+1,sheets.Count,String.Join(", ",list),sheet.Size.Width*25.4/100,sheet.Size.Height*25.4/100);preview.Invalidate();}
        void PaintPreview(object sender,PaintEventArgs e){if(previewImage==null)return;float scale=Math.Min((preview.ClientSize.Width-32f)/previewImage.Width,(preview.ClientSize.Height-24f)/previewImage.Height);if(scale<=0)return;int w=(int)(previewImage.Width*scale),h=(int)(previewImage.Height*scale);var r=new Rectangle((preview.Width-w)/2,(preview.Height-h)/2,w,h);e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.FillRectangle(Brushes.LightGray,r.X+3,r.Y+3,r.Width,r.Height);e.Graphics.DrawImage(previewImage,r);}
        internal PrintDocument CreateJob(){return PrintEngine.CreateJob(document,options,printer,pageSettings);}
        internal void RefreshForTest(){RefreshPlan();}
        internal bool HasPreviewForTest {get{return previewImage!=null&&sheets!=null;}}
        protected override void Dispose(bool disposing){if(disposing){delay.Dispose();if(previewImage!=null)previewImage.Dispose();}base.Dispose(disposing);}
        sealed class BufferedPanel : Panel {internal BufferedPanel(){DoubleBuffered=true;}}
    }
}
