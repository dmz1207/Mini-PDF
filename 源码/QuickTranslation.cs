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
        ToolStripMenuItem quickTranslateItem,captureTranslateItem;
        ToolStripButton captureTranslateButton,ocrQuick,translationQuick;
        TranslationBubble translationBubble;
        AiHotkey aiHotkey;
        bool aiAvailable,autoTranslate=true;
        void InitializeQuickTranslation(ToolStripMenuItem tools) {
            quickTranslateItem=new ToolStripMenuItem(UiText.Get("划词翻译")){CheckOnClick=true,Checked=true};quickTranslateItem.CheckedChanged+=delegate{autoTranslate=quickTranslateItem.Checked;if(!autoTranslate)CloseTranslation();};tools.DropDownItems.Add(quickTranslateItem);
            captureTranslateItem=new ToolStripMenuItem(UiText.Get("截图翻译（Alt+S）"));captureTranslateItem.Click+=delegate{CaptureTranslation();};tools.DropDownItems.Add(captureTranslateItem);
            captureTranslateButton=Button(toolbar,UiText.Get("截图翻译"),CaptureTranslation,UiText.Get("框选屏幕区域，识别并翻译（Alt+S）"),null);
            mainMenuButton.DropDownOpening+=delegate{RefreshAiAvailability();};Activated+=delegate{RefreshAiAvailability();};RefreshAiAvailability();
        }
        void RefreshAiAvailability() {
            if(quickTranslateItem==null)return;aiAvailable=MiniAiClient.Available;string hint=UiText.Get(aiAvailable?"框选屏幕区域，识别并翻译（Alt+S）":"安装 AI 外挂包以启用");
            quickTranslateItem.Enabled=aiAvailable;quickTranslateItem.ToolTipText=aiAvailable?UiText.Get("松开鼠标自动翻译所选文字"):hint;captureTranslateItem.Enabled=captureTranslateButton.Enabled=aiAvailable;captureTranslateItem.ToolTipText=captureTranslateButton.ToolTipText=hint;
            if(ocrQuick!=null)ocrQuick.Visible=aiAvailable;if(translationQuick!=null)translationQuick.Visible=aiAvailable;
            if(aiAvailable&&aiHotkey==null)aiHotkey=new AiHotkey(CaptureTranslation);if(!aiAvailable){CloseTranslation();if(aiHotkey!=null){aiHotkey.Dispose();aiHotkey=null;}}
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
            string root=MiniAiClient.PackRoot;return new BubbleBackend{Text=UiText.Get,Translate=delegate(string text,string target,Func<bool> canceled){MiniAiClient.Ensure(root);return MiniAiClient.TranslateText(root,text,target,canceled);},Recognize=delegate(byte[] image,Func<bool> canceled){MiniAiClient.Ensure(root);return String.Join(Environment.NewLine,MiniAiClient.Recognize(root,image,0,canceled).ConvertAll(l=>l.Text));}};
        }
        void DisposeQuickTranslation(){CloseTranslation();if(aiHotkey!=null){aiHotkey.Dispose();aiHotkey=null;}}
        internal TranslationBubble TranslationBubbleForTest {get{return translationBubble;}}
        internal bool QuickTranslationEnabledForTest {get{return quickTranslateItem!=null&&quickTranslateItem.Enabled&&captureTranslateItem.Enabled;}}
        internal void QuickTranslationContext(ContextMenuStrip menu){var item=menu.Items.Add(UiText.Get("划词翻译"),null,delegate{autoTranslate=!autoTranslate;quickTranslateItem.Checked=autoTranslate;});((ToolStripMenuItem)item).Checked=autoTranslate;item.Enabled=MiniAiClient.Available;item.ToolTipText=UiText.Get("安装 AI 外挂包以启用");var capture=menu.Items.Add(UiText.Get("截图翻译（Alt+S）"),null,delegate{CaptureTranslation();});capture.Enabled=MiniAiClient.Available;capture.ToolTipText=UiText.Get("安装 AI 外挂包以启用");}
    }
}
