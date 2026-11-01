using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MiniAI.Desktop {
    internal sealed class ImageRegion {internal string Text,Translation;internal RectangleF Bounds;internal int Rows=1;}
    internal sealed class ImageServices {
        internal Func<byte[],string,Func<bool>,List<ImageRegion>> Ocr;
        internal Func<string,string,string,Func<bool>,Action<string>,string> Translate;
        internal BubbleBackend Bubble;
        internal Func<string,string> Text;
    }
    internal static class ImageTranslationLayout {
        internal static List<ImageRegion> Group(List<ImageRegion> lines) {
            var result=new List<ImageRegion>();
            foreach(var line in lines) {
                if(String.IsNullOrWhiteSpace(line.Text)||line.Bounds.Width<=0||line.Bounds.Height<=0)continue;
                var previous=result.Count==0?null:result[result.Count-1];float gap=previous==null?1:line.Bounds.Top-previous.Bounds.Bottom;
                float overlap=previous==null?0:Math.Max(0,Math.Min(previous.Bounds.Right,line.Bounds.Right)-Math.Max(previous.Bounds.Left,line.Bounds.Left));
                if(previous!=null&&gap>=-line.Bounds.Height*.3f&&gap<=line.Bounds.Height*1.35f&&overlap>=Math.Min(previous.Bounds.Width,line.Bounds.Width)*.6f&&Math.Abs(previous.Bounds.Left-line.Bounds.Left)<=line.Bounds.Height*2.5f) {
                    previous.Text=previous.Text.TrimEnd()+(previous.Text.EndsWith("-")?"":" ")+line.Text;previous.Bounds=RectangleF.Union(previous.Bounds,line.Bounds);previous.Rows++;
                } else result.Add(new ImageRegion{Text=line.Text,Bounds=line.Bounds});
            }
            return result;
        }
        static Rectangle Pixel(RectangleF value,Size size){int x=(int)Math.Floor(value.X*size.Width),y=(int)Math.Floor(value.Y*size.Height),right=(int)Math.Ceiling(value.Right*size.Width),bottom=(int)Math.Ceiling(value.Bottom*size.Height);return Rectangle.Intersect(new Rectangle(0,0,size.Width,size.Height),Rectangle.FromLTRB(x,y,right,bottom));}
        static Color Background(Bitmap image,Rectangle area) {
            var bins=new Dictionary<int,List<Color>>();int step=Math.Max(1,(area.Width+area.Height)/150);
            for(int i=0;i<2*(area.Width+area.Height);i+=step) {
                int x,y;if(i<area.Width){x=area.Left+i;y=Math.Max(0,area.Top-2);}else if(i<area.Width+area.Height){x=Math.Min(image.Width-1,area.Right+1);y=area.Top+i-area.Width;}else if(i<2*area.Width+area.Height){x=area.Right-1-(i-area.Width-area.Height);y=Math.Min(image.Height-1,area.Bottom+1);}else{x=Math.Max(0,area.Left-2);y=area.Bottom-1-(i-2*area.Width-area.Height);}
                var c=image.GetPixel(Math.Max(0,Math.Min(image.Width-1,x)),Math.Max(0,Math.Min(image.Height-1,y)));int key=(c.R/16)*256+(c.G/16)*16+c.B/16;List<Color> values;if(!bins.TryGetValue(key,out values))bins[key]=values=new List<Color>();values.Add(c);
            }
            var chosen=bins.OrderByDescending(pair=>pair.Value.Count).First().Value;return Color.FromArgb((int)chosen.Average(c=>c.R),(int)chosen.Average(c=>c.G),(int)chosen.Average(c=>c.B));
        }
        internal static Bitmap Compose(Bitmap source,List<ImageRegion> blocks,out int overflow) {
            var image=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);var extra=new List<string>();overflow=0;
            using(var graphics=Graphics.FromImage(image)) {
                graphics.DrawImageUnscaled(source,0,0);graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                foreach(var block in blocks) {
                    if(String.IsNullOrWhiteSpace(block.Translation)||block.Translation==block.Text)continue;var area=Pixel(block.Bounds,source.Size);if(area.Width<4||area.Height<4)continue;
                    float desired=Math.Max(9,Math.Min(72,area.Height/(float)Math.Max(1,block.Rows)*.85f));
                    Font chosen=null;using(var format=new StringFormat(StringFormat.GenericTypographic)) {
                        format.Trimming=StringTrimming.None;format.FormatFlags=StringFormatFlags.FitBlackBox;
                        for(float size=desired;size>=7.5f;size-=.5f){var font=new Font("Microsoft YaHei",size,FontStyle.Regular,GraphicsUnit.Pixel);var measured=graphics.MeasureString(block.Translation,font,Math.Max(2,area.Width),format);if(measured.Height<=area.Height+1){chosen=font;break;}font.Dispose();}
                        if(chosen==null){extra.Add(block.Translation);overflow++;continue;}
                        Color background=Background(source,area);Color foreground=background.GetBrightness()<.5?Color.FromArgb(235,237,239):Color.FromArgb(23,27,31);
                        var fill=area;fill.Inflate(2,2);fill.Intersect(new Rectangle(Point.Empty,source.Size));using(var brush=new SolidBrush(background))graphics.FillRectangle(brush,fill);
                        using(chosen)using(var brush=new SolidBrush(foreground))graphics.DrawString(block.Translation,chosen,brush,area,format);
                    }
                }
            }
            // Preserve every translated character even when a tiny source label cannot fit.
            if(extra.Count>0) {
                string text="Translation / 译文\n\n"+String.Join("\n\n",extra);int margin=Math.Max(8,source.Width/60),height;
                using(var graphics=Graphics.FromImage(image))using(var font=new Font("Microsoft YaHei",Math.Max(12,Math.Min(24,source.Width/55f)),GraphicsUnit.Pixel))height=(int)Math.Ceiling(graphics.MeasureString(text,font,Math.Max(10,source.Width-2*margin)).Height)+2*margin;
                if((long)source.Width*(source.Height+height)<=64000000) {
                    var combined=new Bitmap(source.Width,source.Height+height,PixelFormat.Format32bppArgb);using(var graphics=Graphics.FromImage(combined))using(var font=new Font("Microsoft YaHei",Math.Max(12,Math.Min(24,source.Width/55f)),GraphicsUnit.Pixel)){graphics.Clear(Color.White);graphics.DrawImageUnscaled(image,0,0);graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;graphics.DrawString(text,font,Brushes.Black,new RectangleF(margin,source.Height+margin,source.Width-2*margin,height-margin));}image.Dispose();image=combined;
                }
            }
            return image;
        }
    }
    internal sealed class ImageTranslationWindow : Form {
        readonly ImageServices api;
        readonly Bitmap original;
        readonly string resultsFolder;
        readonly bool retain;
        readonly ImageCanvas canvas;
        readonly SplitContainer split;
        readonly TextBox text;
        readonly Label status;
        readonly ComboBox language,quality;
        readonly Button translate,extract,toggle,cancel;
        List<ImageRegion> lines,blocks;
        Bitmap translated;
        TranslationBubble bubble;
        int generation;
        bool busy,showOriginal=true;
        string translatedText="",sourceText="",ocrMode="quality";
        internal ImageTranslationWindow(ImageServices services,Bitmap image,string folder,bool persist,List<ImageRegion> known=null,string knownMode="quality") {
            api=services;original=image;resultsFolder=folder;retain=persist;lines=known;ocrMode=knownMode;Text=api.Text("图片翻译与文字提取");Font=new Font("Segoe UI",10);ClientSize=new Size(1020,700);MinimumSize=new Size(680,450);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(244,246,249);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
            var bar=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(6),WrapContents=true};language=new ComboBox{Width=112,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat};language.Items.Add(api.Text("中英自动互译"));language.Items.AddRange(DesktopLanguages.Labels);language.SelectedIndex=0;quality=new ComboBox{Width=100,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat};quality.Items.AddRange(new[]{api.Text("极速翻译"),api.Text("高精度翻译")});quality.SelectedIndex=DesktopLanguages.Mode=="quality"?1:0;quality.SelectedIndexChanged+=delegate{DesktopLanguages.Mode=quality.SelectedIndex==1?"quality":"fast";};bar.Controls.AddRange(new Control[]{language,quality});
            translate=Button("翻译图片",TranslateImage);extract=Button("提取文字",ExtractText);toggle=Button("原图／译图",delegate{if(translated==null)return;showOriginal=!showOriginal;RefreshView();});bar.Controls.AddRange(new Control[]{translate,extract,toggle,Button("复制文字",delegate{string value=text.SelectionLength>0?text.SelectedText:text.Text;if(value.Length>0)Clipboard.SetText(value);}),Button("复制图片",delegate{Clipboard.SetImage(showOriginal||translated==null?original:translated);}),Button("保存译图…",SaveImage)});cancel=Button("取消",Cancel);bar.Controls.Add(cancel);layout.Controls.Add(bar,0,0);
            split=new SplitContainer{Size=new Size(1020,600),Dock=DockStyle.Fill,SplitterDistance=710,Panel2MinSize=200,Panel2Collapsed=true};canvas=new ImageCanvas(original.Size){Dock=DockStyle.Fill};canvas.Selection+=delegate(string value,Rectangle area){if(value.Length<2)return;Cancel();if(bubble!=null&&!bubble.IsDisposed)bubble.Close();bubble=new TranslationBubble(api.Bubble,area,false);bubble.Show(this);bubble.Translate(value);};split.Panel1.Controls.Add(canvas);
            text=new TextBox{Name="ImageExtractedText",Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,Font=new Font("Segoe UI",11),BackColor=Color.White};text.MouseUp+=delegate{if(text.SelectionLength<2)return;if(bubble!=null&&!bubble.IsDisposed)bubble.Close();bubble=new TranslationBubble(api.Bubble,new Rectangle(Cursor.Position,new Size(1,1)),false);bubble.Show(this);bubble.Translate(text.SelectedText);};split.Panel2.Padding=new Padding(12);split.Panel2.Controls.Add(text);layout.Controls.Add(split,0,1);
            status=new Label{AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(10,6,10,6),Text=api.Text("翻译图片可切换原图；提取文字可选择复制。")};layout.Controls.Add(status,0,2);SetBusy(false);RefreshView();
        }
        Button Button(string title,Action action){var button=new Button{Text=api.Text(title),AutoSize=true,MinimumSize=new Size(32,30),FlatStyle=FlatStyle.Flat,Margin=new Padding(3)};button.FlatAppearance.BorderSize=0;button.Click+=delegate{action();};return button;}
        void SetBusy(bool value){busy=value;translate.Enabled=extract.Enabled=language.Enabled=quality.Enabled=!value;cancel.Enabled=value;UseWaitCursor=value;}
        void Cancel(){Interlocked.Increment(ref generation);if(busy)status.Text=api.Text("已取消");SetBusy(false);}
        void Post(int serial,Action action){try{if(!IsDisposed&&IsHandleCreated)BeginInvoke(new Action(delegate{if(!IsDisposed&&serial==Volatile.Read(ref generation))action();}));}catch(InvalidOperationException){}}
        byte[] ImageBytes(){using(var stream=new MemoryStream()){original.Save(stream,ImageFormat.Png);return stream.ToArray();}}
        List<ImageRegion> EnsureText(byte[] bytes,string mode,Func<bool> canceled){return lines!=null&&(mode=="fast"||ocrMode=="quality")?lines:api.Ocr(bytes,mode,canceled);}
        void RefreshView(){canvas.ShowImage(showOriginal||translated==null?original:translated,showOriginal?lines:blocks,!showOriginal);text.Text=showOriginal?sourceText:translatedText;toggle.Enabled=translated!=null;}
        void Adopt(List<ImageRegion> value){lines=value;blocks=ImageTranslationLayout.Group(value);sourceText=String.Join(Environment.NewLine+Environment.NewLine,blocks.Select(block=>block.Text));}
        internal void ExtractText() {
            if(translated!=null&&!showOriginal){split.Panel2Collapsed=false;text.Text=translatedText;return;}
            Cancel();int serial=generation;byte[] bytes=ImageBytes();string mode=quality.SelectedIndex==1?"quality":"fast";SetBusy(true);status.Text=api.Text("正在识别选区…");
            new Thread(delegate(){try{var value=EnsureText(bytes,mode,delegate{return serial!=Volatile.Read(ref generation);});Post(serial,delegate{Adopt(value);ocrMode=mode;showOriginal=true;split.Panel2Collapsed=false;RefreshView();SetBusy(false);status.Text=api.Text("提取完成，可选择和复制文字。");if(retain&&sourceText.Length>0)try{KeepText("OCR",sourceText);}catch(IOException error){status.Text=error.Message;}catch(UnauthorizedAccessException error){status.Text=error.Message;}});}catch(Exception error){Post(serial,delegate{SetBusy(false);status.Text=error.Message;});}}){IsBackground=true,Name="MiniAI image text"}.Start();
        }
        internal void TranslateImage() {
            Cancel();int serial=generation;byte[] bytes=ImageBytes();Bitmap copy=new Bitmap(original);string mode=quality.SelectedIndex==1?"quality":"fast";int selected=language.SelectedIndex;SetBusy(true);status.Text=api.Text("正在识别并翻译图片…");
            new Thread(delegate(){using(copy)try {
                Func<bool> canceled=delegate{return serial!=Volatile.Read(ref generation);};var found=EnsureText(bytes,mode,canceled);var groups=ImageTranslationLayout.Group(found);string source=String.Join("\n\n",groups.Select(block=>block.Text));if(groups.Count==0)throw new IOException(api.Text("选区内未识别出文字，请重新框选。"));string target=selected==0?DesktopLanguages.AutoTarget(source):DesktopLanguages.Codes[selected-1];
                for(int i=0;i<groups.Count;i++){if(canceled())throw new OperationCanceledException();int done=i+1;if(!groups[i].Text.Any(Char.IsLetter)){groups[i].Translation=groups[i].Text;continue;}string value=groups[i].Text;var number=Regex.Match(value,@"^\s*(?:\d+(?:\.\d+)*[.)、]|[（(]\d+[）)])\s+");string prefix=number.Success?number.Value:"";groups[i].Translation=prefix+api.Translate(value.Substring(prefix.Length),target,mode,canceled,delegate(string partial){Post(serial,delegate{status.Text=api.Text("正在翻译…")+" "+done+" / "+groups.Count;});});}
                if(canceled())return;int overflow;var picture=ImageTranslationLayout.Compose(copy,groups,out overflow);string result=String.Join("\n\n",groups.Select(block=>block.Translation));string saved="";
                string saveError="";if(retain&&!canceled())try{saved=KeepText("Translation_"+mode,"Source / 原文\r\n"+source+"\r\n\r\nTranslation / 译文\r\n"+result);picture.Save(Path.ChangeExtension(saved,".png"),ImageFormat.Png);}catch(IOException error){saveError=error.Message;}catch(UnauthorizedAccessException error){saveError=error.Message;}
                if(canceled()){picture.Dispose();return;}
                Post(serial,delegate{lines=found;ocrMode=mode;blocks=groups;sourceText=source;translatedText=result;var old=translated;translated=picture;if(old!=null)old.Dispose();showOriginal=false;RefreshView();SetBusy(false);status.Text=saveError.Length>0?saveError:api.Text(overflow>0?"译图已完成，长译文附在图片下方。":"译图已完成，可切换原图、复制或保存。");});
            }catch(Exception error){Post(serial,delegate{SetBusy(false);status.Text=error.Message;});}}){IsBackground=true,Name="MiniAI image translation"}.Start();
        }
        string KeepText(string prefix,string value){Directory.CreateDirectory(resultsFolder);string path=Path.Combine(resultsFolder,prefix+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".txt");using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))using(var writer=new StreamWriter(stream,new UTF8Encoding(true)))writer.Write(value);return path;}
        void SaveImage(){if(translated==null){TranslateImage();return;}Directory.CreateDirectory(resultsFolder);using(var dialog=new SaveFileDialog{Filter="PNG image|*.png",InitialDirectory=resultsFolder,FileName="Translation.png",DefaultExt="png",AddExtension=true,RestoreDirectory=true})if(dialog.ShowDialog(this)==DialogResult.OK)try{translated.Save(dialog.FileName,ImageFormat.Png);}catch(Exception error){status.Text=error.Message;}}
        internal bool BusyForTest {get{return busy;}}
        internal string TextForTest {get{return text.Text;}}
        internal Bitmap ImageForTest {get{return translated;}}
        protected override void Dispose(bool disposing){if(disposing){Interlocked.Increment(ref generation);if(bubble!=null)bubble.Dispose();if(canvas!=null)canvas.ShowImage(null,null,false);original.Dispose();if(translated!=null)translated.Dispose();}base.Dispose(disposing);}
    }
    internal sealed class ImageCanvas : ScrollableControl {
        readonly Size sourceSize;
        Bitmap image;List<ImageRegion> regions;bool translated,dragging;PointF begin,current;RectangleF view;
        internal event Action<string,Rectangle> Selection;
        internal ImageCanvas(Size size){sourceSize=size;DoubleBuffered=true;BackColor=Color.FromArgb(35,38,42);SetStyle(ControlStyles.ResizeRedraw,true);}
        internal void ShowImage(Bitmap value,List<ImageRegion> items,bool translatedText){image=value;regions=items;translated=translatedText;Invalidate();}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(image==null||ClientSize.Width<=12||ClientSize.Height<=12)return;float scale=Math.Min((ClientSize.Width-12)/(float)image.Width,(ClientSize.Height-12)/(float)image.Height);view=new RectangleF((ClientSize.Width-image.Width*scale)/2,(ClientSize.Height-image.Height*scale)/2,image.Width*scale,image.Height*scale);e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.DrawImage(image,view);if(dragging){var box=RectangleF.FromLTRB(Math.Min(begin.X,current.X),Math.Min(begin.Y,current.Y),Math.Max(begin.X,current.X),Math.Max(begin.Y,current.Y));box=new RectangleF(view.X+box.X*scale,view.Y+box.Y*scale,box.Width*scale,box.Height*scale);using(var brush=new SolidBrush(Color.FromArgb(60,49,140,240)))e.Graphics.FillRectangle(brush,box);e.Graphics.DrawRectangle(Pens.DodgerBlue,box.X,box.Y,box.Width,box.Height);}}
        PointF Pixel(Point location){float scale=view.Width/Math.Max(1,image.Width);return new PointF((location.X-view.X)/scale,(location.Y-view.Y)/scale);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(image==null||e.Button!=MouseButtons.Left||!view.Contains(e.Location))return;begin=current=Pixel(e.Location);dragging=true;Capture=true;}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging){current=Pixel(e.Location);Invalidate();}}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging)return;dragging=false;Capture=false;current=Pixel(e.Location);var box=RectangleF.FromLTRB(Math.Min(begin.X,current.X),Math.Min(begin.Y,current.Y),Math.Max(begin.X,current.X),Math.Max(begin.Y,current.Y));if(box.Width<3)box.Inflate(2,2);if(box.Height<3)box.Inflate(2,2);var selected=new List<string>();foreach(var item in regions??new List<ImageRegion>()){var area=new RectangleF(item.Bounds.X*sourceSize.Width,item.Bounds.Y*sourceSize.Height,item.Bounds.Width*sourceSize.Width,item.Bounds.Height*sourceSize.Height);if(area.IntersectsWith(box))selected.Add(translated?(item.Translation??item.Text):item.Text);}Invalidate();if(selected.Count>0&&Selection!=null)Selection(String.Join("\n",selected),new Rectangle(PointToScreen(e.Location),new Size(1,1)));}
    }
}
