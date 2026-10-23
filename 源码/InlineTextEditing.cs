using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed partial class PdfCanvas {
        Panel inlinePanel;
        TextBox inlineInput;
        TextEditSelection inlineSelection;
        PdfDocument inlineDocument;
        TextEditOptions inlineOptions;
        internal bool InlineEditing {get{return inlinePanel!=null;}}
        internal string InlineDraft {get{return inlineInput==null?"":inlineInput.Text;}set{if(inlineInput!=null)inlineInput.Text=value;}}
        internal void BeginInlineText(TextEditSelection selection,TextEditOptions options=null) {
            if(!ConfirmInlineText())return;
            inlineSelection=selection;inlineDocument=Doc;inlineOptions=options;
            inlinePanel=new Panel{BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle};
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));inlinePanel.Controls.Add(root);
            var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,BackColor=Color.FromArgb(240,245,250)};
            bar.Controls.Add(FormUi.Button("应用 Ctrl+Enter",delegate{ApplyInlineText();}));
            bar.Controls.Add(FormUi.Button("取消 Esc",delegate{EndInlineText();}));
            bar.Controls.Add(FormUi.Button("窗口编辑…",delegate{PopInlineText();}));root.Controls.Add(bar,0,0);
            inlineInput=new TextBox{Multiline=true,Dock=DockStyle.Fill,WordWrap=true,ScrollBars=ScrollBars.Vertical,AcceptsReturn=true,MaxLength=20000,
                Font=new Font("Segoe UI",Math.Max(10,Math.Min(30,selection.Size*selection.SizeScale*Zoom))),Text=(options==null?selection.Text:options.Text).Replace("\r","").Replace("\n",Environment.NewLine)};
            inlineInput.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){EndInlineText();e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.Enter){ApplyInlineText();e.SuppressKeyPress=true;}};
            root.Controls.Add(inlineInput,0,1);Controls.Add(inlinePanel);inlinePanel.BringToFront();Scroll+=PlaceInlineText;SizeChanged+=PlaceInlineText;PlaceInlineText(null,EventArgs.Empty);inlineInput.Focus();inlineInput.SelectAll();
        }
        internal void PopInlineText() {if(inlinePanel==null)return;var selected=inlineSelection;var options=ReadInlineText();EndInlineText();owner.EditTextInWindow(selected,options.Text,options);}
        TextEditOptions ReadInlineText(){var options=inlineOptions??new TextEditOptions{Size=inlineSelection.Size,Color=inlineSelection.Color,Width=inlineSelection.Width,LineHeight=inlineSelection.LineHeight};options.Text=inlineInput.Text;return options;}
        void PlaceInlineText(object sender,EventArgs e) {
            if(inlinePanel==null||Doc!=inlineDocument)return;
            var rect=SelectionRectangle(inlineSelection.Page,inlineSelection.Bounds);
            float dpi=Math.Max(1,Font.Height/15f);
            int width=Math.Min(Math.Max((int)(420*dpi),rect.Width+12),Math.Max(180,ClientSize.Width-12));
            int height=Math.Min(Math.Max((int)(180*dpi),rect.Height+64),Math.Max(100,ClientSize.Height-12));
            inlinePanel.Bounds=new Rectangle(Math.Max(2,Math.Min(rect.Left-4,ClientSize.Width-width-4)),Math.Max(2,Math.Min(rect.Top-42,ClientSize.Height-height-4)),width,height);
        }
        internal void EndInlineText() {
            var panel=inlinePanel;var font=inlineInput==null?null:inlineInput.Font;inlinePanel=null;inlineInput=null;inlineSelection=null;inlineDocument=null;inlineOptions=null;
            Scroll-=PlaceInlineText;SizeChanged-=PlaceInlineText;if(panel!=null){Controls.Remove(panel);panel.Dispose();}if(font!=null)font.Dispose();if(!IsDisposed&&!Disposing)Focus();
        }
        internal bool ApplyInlineText() {
            if(inlinePanel==null)return true;
            try {
                PdfDocument.Check(Doc==inlineDocument,UiText.Get("文字已变化，请重新选择。"));
                var selected=inlineSelection;Doc.OpenPage(selected.Page);
                var options=ReadInlineText();
                Doc.ApplyTextEdit(selected,options);EndInlineText();owner.Run(delegate{});return true;
            }catch(Exception error){if(inlineSelection!=null)inlineSelection.Epoch=Doc.RenderEpoch;MessageBox.Show(this,error.Message,UiText.Get("操作未完成"),MessageBoxButtons.OK,MessageBoxIcon.Warning);if(inlineInput!=null)inlineInput.Focus();return false;}
        }
        internal bool ConfirmInlineText() {
            if(inlinePanel==null)return true;
            bool formatChanged=inlineOptions!=null&&(inlineOptions.Face!="保留原字体"||inlineOptions.Wrap||inlineOptions.ChangeColor||Math.Abs(inlineOptions.Size-inlineSelection.Size)>.001f||Math.Abs(inlineOptions.LineHeight-inlineSelection.LineHeight)>.001f);
            if(!formatChanged&&inlineInput.Text.Replace("\r","")==inlineSelection.Text.Replace("\r","")){EndInlineText();return true;}
            var answer=MessageBox.Show(this,UiText.Get("应用文档内尚未确认的文字修改？"),"Mini PDF",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
            if(answer==DialogResult.Cancel)return false;if(answer==DialogResult.No){EndInlineText();return true;}return ApplyInlineText();
        }
    }
    internal sealed partial class MainWindow {
        internal void EditTextInWindow(TextEditSelection selected,string draft=null,TextEditOptions options=null) {
            using(var dialog=new TextEditWindow(Document,selected)){if(options!=null)dialog.SetOptions(options);else if(draft!=null)dialog.SetDraft(draft);var result=dialog.ShowDialog(this);if(result==DialogResult.OK)Run(delegate{Document.ApplyTextEdit(dialog.Selection,dialog.Options);});else if(result==DialogResult.Retry||draft!=null){canvas.BeginInlineText(dialog.Selection,dialog.Options);}}
        }
    }
}
