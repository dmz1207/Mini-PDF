using System;
using MiniAI.Desktop;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed partial class MainWindow {
        ToolStripMenuItem quickTranslateItem,captureTranslateItem,imageTranslateItem;
        ToolStripButton captureTranslateButton,imageTranslateButton,ocrQuick,translationQuick;
        TranslationBubble translationBubble;
        AiHotkey aiHotkey;
        bool aiAvailable,autoTranslate=true,aiReaderAttached;
        void InitializeQuickTranslation(ToolStripMenuItem tools) {
            MiniAiClient.AttachReader();aiReaderAttached=true;
            quickTranslateItem=new ToolStripMenuItem(UiText.Get("划词翻译")){CheckOnClick=true,Checked=true};quickTranslateItem.CheckedChanged+=delegate{autoTranslate=quickTranslateItem.Checked;if(!autoTranslate)CloseTranslation();};tools.DropDownItems.Add(quickTranslateItem);
            captureTranslateItem=new ToolStripMenuItem(UiText.Get("截图翻译（Alt+S）"));captureTranslateItem.Click+=delegate{CaptureTranslation();};tools.DropDownItems.Add(captureTranslateItem);
            imageTranslateItem=new ToolStripMenuItem(UiText.Get("翻译当前页图片"));imageTranslateItem.Click+=delegate{TranslatePageImage();};tools.DropDownItems.Add(imageTranslateItem);
            captureTranslateButton=Button(toolbar,UiText.Get("截图翻译"),CaptureTranslation,UiText.Get("框选屏幕区域，识别并翻译（Alt+S）"),null);
            imageTranslateButton=Button(toolbar,UiText.Get("译图"),TranslatePageImage,UiText.Get("翻译当前页图片"),null);
            mainMenuButton.DropDownOpening+=delegate{RefreshAiAvailability();};Activated+=delegate{RefreshAiAvailability();};RefreshAiAvailability();
        }
        void RefreshAiAvailability() {
            if(quickTranslateItem==null)return;aiAvailable=MiniAiClient.Available;string hint=UiText.Get(aiAvailable?"框选屏幕区域，识别并翻译（Alt+S）":"安装 AI 外挂包以启用");
            quickTranslateItem.Enabled=aiAvailable;quickTranslateItem.ToolTipText=aiAvailable?UiText.Get("松开鼠标自动翻译所选文字"):hint;captureTranslateItem.Enabled=captureTranslateButton.Enabled=aiAvailable;captureTranslateItem.ToolTipText=captureTranslateButton.ToolTipText=hint;
            if(imageTranslateItem!=null)imageTranslateItem.Enabled=imageTranslateButton.Enabled=aiAvailable;
            if(ocrQuick!=null)ocrQuick.Visible=aiAvailable;if(translationQuick!=null)translationQuick.Visible=aiAvailable;
            if(aiAvailable){MiniAiClient.Prewarm();if(aiHotkey==null)aiHotkey=new AiHotkey(CaptureTranslation);}if(!aiAvailable){CloseTranslation();if(aiHotkey!=null){aiHotkey.Dispose();aiHotkey=null;}}
        }
        internal void SelectionTranslation(Point location) {
            if(!aiAvailable||!autoTranslate||Document==null)return;string text=canvas.CopiedText();if(String.IsNullOrWhiteSpace(text)||text.Trim().Length<2)return;
            CloseTranslation();var point=canvas.PointToScreen(location);translationBubble=new TranslationBubble(TranslationBackend(),new Rectangle(point,new Size(1,1)),false);translationBubble.Show(this);translationBubble.Translate(text);
        }
        internal void CloseTranslation(){var popup=translationBubble;translationBubble=null;if(popup!=null&&!popup.IsDisposed){popup.Close();popup.Dispose();}}
        void CaptureTranslation() {
            if(!MiniAiClient.Available)return;CloseTranslation();try{Rectangle area;Bitmap image=MiniAI.Desktop.CaptureOverlay.Pick(this,out area);if(image==null)return;translationBubble=new TranslationBubble(TranslationBackend(),area,true);translationBubble.Show(this);translationBubble.Recognize(image);}catch(Exception error){Report(error.Message);}
        }
        internal static BubbleBackend TranslationBackend() {
            string root=MiniAiClient.PackRoot;return new BubbleBackend{Text=UiText.Get,Translate=delegate(string text,string target,Func<bool> canceled){MiniAiClient.Ensure(root);return MiniAiClient.TranslateText(root,text,target,canceled);},Stream=delegate(string text,string target,string mode,Func<bool> canceled,Action<string> progress){return MiniAiClient.TranslateStreaming(root,text,target,canceled,progress,mode);},Recognize=delegate(byte[] image,string mode,Func<bool> canceled){MiniAiClient.Ensure(root);return String.Join(Environment.NewLine,MiniAiClient.Recognize(root,image,0,canceled,mode).ConvertAll(l=>l.Text));}};
        }
        void TranslatePageImage() {
            if(!MiniAiClient.Available)return;Bitmap image=null;try {
                if(Document==null){using(var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",RestoreDirectory=true}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;using(var loaded=Image.FromFile(dialog.FileName))image=new Bitmap(loaded);}}
                else {var size=Document.PageSize(Document.Index);float scale=Math.Min(2,1700/Math.Max(size.Width,size.Height));image=Document.Render(Document.Index,Math.Max(1,(int)Math.Ceiling(size.Width*scale)),Math.Max(1,(int)Math.Ceiling(size.Height*scale)));}
                string root=MiniAiClient.PackRoot;var bubble=TranslationBackend();var services=new ImageServices{Text=UiText.Get,Bubble=bubble,Translate=bubble.Stream,Ocr=delegate(byte[] data,string mode,Func<bool> canceled){MiniAiClient.Ensure(root);return MiniAiClient.Recognize(root,data,0,canceled,mode).ConvertAll(line=>{float left=1,top=1,right=0,bottom=0;foreach(var point in line.Corners){left=Math.Min(left,point.X);right=Math.Max(right,point.X);top=Math.Min(top,point.Y);bottom=Math.Max(bottom,point.Y);}return new ImageRegion{Text=line.Text,Bounds=RectangleF.FromLTRB(left,top,right,bottom)};});}};
                string results=Environment.GetEnvironmentVariable("MINIAI_RESULTS_DIR");if(String.IsNullOrEmpty(results))results=Path.Combine(Directory.GetParent(root).FullName,"MiniAI Results");var window=new ImageTranslationWindow(services,image,results,true){Icon=Icon};image=null;window.Show(this);window.TranslateImage();
            }catch(Exception error){if(image!=null)image.Dispose();Report(error.Message);}
        }
        void DisposeQuickTranslation(){CloseTranslation();if(aiHotkey!=null){aiHotkey.Dispose();aiHotkey=null;}if(aiReaderAttached){aiReaderAttached=false;MiniAiClient.ReleaseReader();}}
        internal TranslationBubble TranslationBubbleForTest {get{return translationBubble;}}
        internal bool QuickTranslationEnabledForTest {get{return quickTranslateItem!=null&&quickTranslateItem.Enabled&&captureTranslateItem.Enabled;}}
        internal void QuickTranslationContext(ContextMenuStrip menu){var item=menu.Items.Add(UiText.Get("划词翻译"),null,delegate{autoTranslate=!autoTranslate;quickTranslateItem.Checked=autoTranslate;});((ToolStripMenuItem)item).Checked=autoTranslate;item.Enabled=MiniAiClient.Available;item.ToolTipText=UiText.Get("安装 AI 外挂包以启用");var capture=menu.Items.Add(UiText.Get("截图翻译（Alt+S）"),null,delegate{CaptureTranslation();});capture.Enabled=MiniAiClient.Available;capture.ToolTipText=UiText.Get("安装 AI 外挂包以启用");}
    }
}
