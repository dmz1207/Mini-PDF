using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed partial class OfflineWindow {
        string[] photoFiles;
        byte[] photoImage;
        bool textFile;
        Label fileSource;
        Button documentButton;
        ToolTip fileTip;
        bool HasPhoto {get{return photoFiles!=null||photoImage!=null;}}
        static bool Photo(string path){return new[]{".png",".jpg",".jpeg",".bmp",".tif",".tiff",".gif"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
        static bool TextFile(string path){return new[]{".txt",".md",".csv",".tsv"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
        static bool SupportedFiles(string[] paths){return paths!=null&&paths.Length>0&&paths.Length<=64&&(paths.All(Photo)||paths.Length==1&&(TextFile(paths[0])||Path.GetExtension(paths[0]).Equals(".pdf",StringComparison.OrdinalIgnoreCase)));}

        void InitializeFiles(FlowLayoutPanel header,FlowLayoutPanel sources){
            var open=FormUi.Button("打开文件…",delegate{OpenFile();});open.Name="OpenOcrFile";
            var capture=FormUi.Button("截图识别",delegate{CapturePhoto(false);});capture.Name="CaptureOcr";
            var captureTranslate=FormUi.Button("截图翻译",delegate{CapturePhoto(true);});captureTranslate.Name="CaptureOcrTranslate";
            var paste=FormUi.Button("粘贴图片",delegate{PastePhoto();});paste.Name="PasteOcrImage";
            documentButton=FormUi.Button("当前 PDF",delegate{UseCurrentPdf();});documentButton.Name="UseCurrentPdf";
            fileSource=new Label{Name="OcrFileSource",AutoSize=false,AutoEllipsis=true,Width=220,Height=32,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(3,3,8,3)};
            header.Controls.AddRange(new Control[]{open,capture,captureTranslate,paste});sources.Controls.AddRange(new Control[]{documentButton,fileSource});fileTip=new ToolTip();
            fileTip.SetToolTip(capture,UiText.Get("框选屏幕区域提取文字（Alt+Shift+O）"));
            fileTip.SetToolTip(captureTranslate,UiText.Get("框选屏幕区域，识别并翻译（Alt+S）"));
        }
        void EnableFileDrop(Control control){
            control.AllowDrop=true;
            control.DragEnter+=delegate(object sender,DragEventArgs e){e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)&&SupportedFiles(e.Data.GetData(DataFormats.FileDrop) as string[])?DragDropEffects.Copy:DragDropEffects.None;};
            control.DragDrop+=delegate(object sender,DragEventArgs e){try{LoadFiles(e.Data.GetData(DataFormats.FileDrop) as string[]);}catch(Exception error){status.Text=error.Message;}};
            foreach(Control child in control.Controls)EnableFileDrop(child);
        }
        void OpenFile(){
            using(var dialog=new OpenFileDialog{Title=UiText.Get("打开图片、PDF 或文本"),Filter=UiText.Get("支持的文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif;*.pdf;*.txt;*.md;*.csv;*.tsv|图片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif|PDF|*.pdf|文本|*.txt;*.md;*.csv;*.tsv"),Multiselect=true,RestoreDirectory=true}){
                if(dialog.ShowDialog(this)==DialogResult.OK)try{LoadFiles(dialog.FileNames);}catch(Exception error){status.Text=error.Message;}
            }
        }
        void UpdateFileSource(){
            if(photoFiles!=null)fileSource.Text=String.Join("; ",photoFiles.Select(Path.GetFileName));
            else if(!textFile&&photoImage==null)fileSource.Text=document==null?UiText.Get("未打开 PDF"):Path.GetFileName(document.FilePath??"");
            if(String.IsNullOrEmpty(fileSource.Text))fileSource.Text=UiText.Get("当前 PDF");
            fileTip.SetToolTip(fileSource,fileSource.Text);
        }
        void ResetSource(){Cancel();photoFiles=null;photoImage=null;textFile=false;lines.Clear();recognized.Clear();input.Clear();translated.Clear();}
        void UseCurrentPdf(){ResetSource();document=owner.Document;if(document!=null)pages.Text=(document.Index+1).ToString();UpdateFileSource();tabs.SelectedIndex=0;RefreshPack();}
        void LoadFiles(string[] paths){
            PdfDocument.Check(SupportedFiles(paths),UiText.Get("请拖入图片、单个 PDF 或文本文件；多选时请选择图片。"));
            foreach(string path in paths)PdfDocument.Check(File.Exists(path),UiText.Get("找不到文件。"));
            if(paths.Length==1&&TextFile(paths[0])){
                var file=new FileInfo(paths[0]);PdfDocument.Check(file.Length<=5*1024*1024,UiText.Get("文本文件过大，请分段导入。"));
                string value;
                try{using(var reader=new StreamReader(file.FullName,new UTF8Encoding(false,true),true))value=reader.ReadToEnd();}
                catch(DecoderFallbackException){value=File.ReadAllText(file.FullName,Encoding.GetEncoding(936));}
                PdfDocument.Check(value.Length<=100000,UiText.Get("文本文件过大，请分段导入。"));
                ResetSource();textFile=true;recognized.Text=input.Text=value;fileSource.Text=file.Name;UpdateFileSource();
                direction.SelectedIndex=ContainsChinese(value)?1:0;tabs.SelectedIndex=1;RefreshPack();status.Text=UiText.Get("文字已加载，可直接发送到翻译。");return;
            }
            if(paths.Length==1&&!Photo(paths[0])){
                Cancel();owner.OpenDocument(paths[0]);
                PdfDocument.Check(owner.Document!=null&&!String.IsNullOrEmpty(owner.Document.FilePath)&&String.Equals(Path.GetFullPath(owner.Document.FilePath),Path.GetFullPath(paths[0]),StringComparison.OrdinalIgnoreCase),UiText.Get("未能打开 PDF。"));
                UseCurrentPdf();return;
            }
            ResetSource();photoFiles=paths.Select(Path.GetFullPath).ToArray();tabs.SelectedIndex=0;UpdateFileSource();RefreshPack();StartPhotoOcr(false);
        }
        void CapturePhoto(bool translateAfter){
            try{
                Hide();Rectangle selected;
                using(var image=MiniAI.Desktop.CaptureOverlay.Pick(null,out selected)){
                    Show(owner);Activate();
                    if(image!=null)LoadImage(image,UiText.Get("屏幕截图"),translateAfter);
                }
            }catch(Exception error){if(!IsDisposed){Show(owner);status.Text=error.Message;}}
        }
        void PastePhoto(){
            try{
                if(Clipboard.ContainsImage()){using(var image=Clipboard.GetImage())if(image!=null)LoadImage(image,UiText.Get("剪贴板图片"),false);}
                else if(Clipboard.ContainsFileDropList()){var files=Clipboard.GetFileDropList();LoadFiles(files.Cast<string>().ToArray());}
                else status.Text=UiText.Get("剪贴板中没有图片或支持的文件。");
            }catch(Exception error){status.Text=error.Message;}
        }
        void LoadImage(Image image,string name,bool translateAfter){
            PdfDocument.Check((long)image.Width*image.Height<=100000000,UiText.Get("图片过大，请缩小后重试。"));
            byte[] bytes;
            using(var normalized=MiniAiWorker.NormalizePhoto(image,1))using(var stream=new MemoryStream()){normalized.Save(stream,ImageFormat.Png);bytes=stream.ToArray();}
            ResetSource();photoImage=bytes;fileSource.Text=name;UpdateFileSource();tabs.SelectedIndex=0;RefreshPack();StartPhotoOcr(translateAfter);
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keys){
            if(keys==(Keys.Control|Keys.O)){OpenFile();return true;}
            if(keys==(Keys.Alt|Keys.S)){CapturePhoto(true);return true;}
            if(keys==(Keys.Alt|Keys.Shift|Keys.O)){CapturePhoto(false);return true;}
            if(keys==(Keys.Control|Keys.V)&&(Clipboard.ContainsImage()||Clipboard.ContainsFileDropList())){PastePhoto();return true;}
            if(keys==Keys.Escape){if(busy)Cancel();else Close();return true;}
            return base.ProcessCmdKey(ref msg,keys);
        }
        void StartPhotoOcr(bool translateAfter){
            if(busy||!HasPhoto)return;
            try{
                string root=MiniAiClient.PackRoot;PdfDocument.Check(MiniAiClient.AvailableAt(root),UiText.Get("请先加载 MiniAI 离线功能包。"));
                var files=photoFiles==null?null:(string[])photoFiles.Clone();byte[] image=photoImage;
                Cancel();lines.Clear();recognized.Clear();SetBusy(true);int current=generation;var worker=new PdfJob(this,null);job=worker;status.Text=UiText.Get("正在识别…");
                worker.Start(delegate(BinaryWriter writer){
                    MiniAiClient.Ensure(root);writer.Write(image==null?"ocr-files":"ocr-image");writer.Write(root);
                    if(image!=null){writer.Write(image.Length);writer.Write(image);}else{writer.Write(files.Length);foreach(string path in files)writer.Write(path);}
                },delegate(BinaryReader reader){
                    while(true){
                        int code=reader.ReadInt32();if(code<0)throw new IOException(reader.ReadString());if(code==1)return;
                        if(code==0){int done=reader.ReadInt32(),total=reader.ReadInt32();reader.ReadInt32();worker.Post(delegate{if(current==generation)status.Text=UiText.Get("正在识别…")+" "+done+" / "+total;});}
                        else if(code==4){
                            int count=reader.ReadInt32();PdfDocument.Check(count>=0&&count<=5000,"Invalid image OCR result count.");var batch=new List<OcrLine>();for(int i=0;i<count;i++)batch.Add(OcrLine.Read(reader));
                            worker.Post(delegate{if(current!=generation)return;foreach(var line in batch){lines.Add(line);recognized.AppendText(line.Text+Environment.NewLine);}});
                        }else throw new IOException("Invalid image OCR response.");
                    }
                },delegate(Exception error){
                    if(current!=generation)return;job=null;SetBusy(false);status.Text=error==null?String.Format(UiText.Get("识别完成：{0} 行。"),lines.Count):error.Message;
                    if(error==null&&translateAfter&&lines.Count>0){input.Text=recognized.Text;direction.SelectedIndex=ContainsChinese(input.Text)?1:0;tabs.SelectedIndex=1;StartTranslation();}
                },300);
            }catch(Exception error){SetBusy(false);status.Text=error.Message;}
        }
        internal void ImportForTest(params string[] paths){LoadFiles(paths);}
        internal string RecognizedForTest {get{return recognized.Text;}}
        internal bool ApplyEnabledForTest {get{return applyButton.Enabled;}}
    }

    internal static partial class MiniAiWorker {
        internal static Bitmap NormalizePhoto(Image image,int orientation){
            // Rotate a single decoded frame, never the multi-page TIFF container.
            using(var frame=new Bitmap(image)){
                switch(orientation){case 2:frame.RotateFlip(RotateFlipType.RotateNoneFlipX);break;case 3:frame.RotateFlip(RotateFlipType.Rotate180FlipNone);break;case 4:frame.RotateFlip(RotateFlipType.Rotate180FlipX);break;case 5:frame.RotateFlip(RotateFlipType.Rotate90FlipX);break;case 6:frame.RotateFlip(RotateFlipType.Rotate90FlipNone);break;case 7:frame.RotateFlip(RotateFlipType.Rotate270FlipX);break;case 8:frame.RotateFlip(RotateFlipType.Rotate270FlipNone);break;}
                double scale=Math.Min(1,4096.0/Math.Max(frame.Width,frame.Height));var output=new Bitmap(Math.Max(1,(int)(frame.Width*scale)),Math.Max(1,(int)(frame.Height*scale)),PixelFormat.Format24bppRgb);
                using(var graphics=Graphics.FromImage(output)){graphics.Clear(Color.White);graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.DrawImage(frame,new Rectangle(Point.Empty,output.Size));}return output;
            }
        }
        static void OcrPhoto(string root,Image image,int orientation,BinaryWriter writer){
            PdfDocument.Check((long)image.Width*image.Height<=100000000,"Image is too large.");
            using(var scaled=NormalizePhoto(image,orientation))using(var bytes=new MemoryStream()){
                scaled.Save(bytes,ImageFormat.Png);var lines=MiniAiClient.Recognize(root,bytes.ToArray(),-1);
                MiniAiClient.KeepResult(root,"ocr","","",String.Join(Environment.NewLine,lines.Select(line=>line.Text)));
                writer.Write(4);writer.Write(lines.Count);foreach(var line in lines)line.Write(writer);writer.Flush();
            }
        }
        internal static void OcrImage(BinaryReader reader,BinaryWriter writer){
            string root=reader.ReadString();int length=reader.ReadInt32();PdfDocument.Check(length>0&&length<=64*1024*1024,"Invalid image size.");byte[] bytes=reader.ReadBytes(length);PdfDocument.Check(bytes.Length==length,"Incomplete image.");MiniAiClient.Ensure(root);
            using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream))OcrPhoto(root,image,1,writer);writer.Write(1);writer.Flush();
        }
        internal static void OcrFiles(BinaryReader reader,BinaryWriter writer){
            string root=reader.ReadString();int count=reader.ReadInt32();PdfDocument.Check(count>0&&count<=64,"Invalid image file count.");var files=new string[count];for(int n=0;n<count;n++)files[n]=reader.ReadString();MiniAiClient.Ensure(root);
            for(int n=0;n<files.Length;n++)using(var image=Image.FromFile(files[n])){
                PdfDocument.Check((long)image.Width*image.Height<=100000000,"Image is too large.");int frames=Array.IndexOf(image.FrameDimensionsList,FrameDimension.Page.Guid)>=0?image.GetFrameCount(FrameDimension.Page):1;PdfDocument.Check(frames<=1000,"Too many image frames.");
                for(int frame=0;frame<frames;frame++){
                    if(frames>1)image.SelectActiveFrame(FrameDimension.Page,frame);int orientation=1;
                    if(Array.IndexOf(image.PropertyIdList,0x112)>=0)try{orientation=image.GetPropertyItem(0x112).Value[0];}catch(ArgumentException){}
                    writer.Write(0);writer.Write(n+1);writer.Write(count);writer.Write(frame);writer.Flush();OcrPhoto(root,image,orientation,writer);
                }
            }writer.Write(1);writer.Flush();
        }
    }
}
