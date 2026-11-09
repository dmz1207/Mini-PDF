using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed class ShapeStyle {
        internal Color Stroke=Color.Black;
        internal Color? Fill;
        internal float Width=1.6f;
        internal bool Border=true;
        internal int FillMode=2;
    }
    internal static partial class N {
        [System.Runtime.InteropServices.DllImport(D)]internal static extern int FPDFPageObj_GetStrokeWidth(IntPtr obj,out float width);
    }
    internal sealed partial class PdfDocument {
        internal const string RectangleTag="MiniPDF_Rectangle";
        internal ShapeStyle ReadShapeStyle(Pick pick){
            var obj=ObjectAt(pick);Check(N.FPDFPageObj_GetType(obj)==2,UiText.Get("请选择矩形或路径对象。"));
            int fill,stroke;float width;uint r,g,b,a;
            Check(N.FPDFPath_GetDrawMode(obj,out fill,out stroke)!=0,UiText.Get("无法读取图形样式。"));
            var style=new ShapeStyle{Border=stroke!=0,FillMode=fill==0?2:fill};
            if(N.FPDFPageObj_GetStrokeColor(obj,out r,out g,out b,out a)!=0)style.Stroke=Color.FromArgb((int)a,(int)r,(int)g,(int)b);
            if(N.FPDFPageObj_GetStrokeWidth(obj,out width)!=0&&!Single.IsNaN(width)&&!Single.IsInfinity(width))style.Width=Math.Max(.1f,Math.Min(100,width));
            if(fill!=0&&N.FPDFPageObj_GetFillColor(obj,out r,out g,out b,out a)!=0)style.Fill=Color.FromArgb((int)a,(int)r,(int)g,(int)b);
            return style;
        }
        static void SetShapeStyle(IntPtr obj,ShapeStyle style){
            Check(style!=null&&(style.Border||style.Fill.HasValue)&&style.Width>=.1f&&style.Width<=100&&!Single.IsNaN(style.Width),UiText.Get("请设置填充或边框，线宽为 0.1–100 磅。"));
            Color border=style.Stroke;
            Check(N.FPDFPageObj_SetStrokeColor(obj,border.R,border.G,border.B,border.A)!=0&&N.FPDFPageObj_SetStrokeWidth(obj,style.Width)!=0,UiText.Get("无法设置图形边框。"));
            if(style.Fill.HasValue){var fill=style.Fill.Value;Check(N.FPDFPageObj_SetFillColor(obj,fill.R,fill.G,fill.B,fill.A)!=0,UiText.Get("无法设置图形填充。"));}
            Check(N.FPDFPath_SetDrawMode(obj,style.Fill.HasValue?style.FillMode:0,style.Border?1:0)!=0,UiText.Get("无法设置图形样式。"));
        }
        internal void ApplyShapeStyle(Pick pick,ShapeStyle style){Check(pick!=null&&!pick.IsAnnotation&&pick.Type==2,UiText.Get("请选择矩形或路径对象。"));Edit(delegate{SetShapeStyle(ObjectAt(pick),style);});}
        internal void AddRectangle(N.Rect rect,Color border,float width,Color? fill){
            Check(rect.right>rect.left&&rect.top>rect.bottom,UiText.Get("矩形范围无效。"));
            Edit(delegate{
                IntPtr obj=N.FPDFPageObj_CreateNewRect(rect.left,rect.bottom,rect.right-rect.left,rect.top-rect.bottom);
                Check(obj!=IntPtr.Zero,UiText.Get("无法创建矩形。"));
                try{
                    SetShapeStyle(obj,new ShapeStyle{Stroke=border,Width=width,Fill=fill});
                    Check(N.FPDFPageObj_AddMark(obj,RectangleTag)!=IntPtr.Zero,UiText.Get("无法标记矩形对象。"));
                    Check(N.FPDFPage_InsertObject(Page,obj)!=0,UiText.Get("无法插入矩形。"));obj=IntPtr.Zero;
                }finally{if(obj!=IntPtr.Zero)N.FPDFPageObj_Destroy(obj);}
            });
        }
    }
    internal sealed class ShapeStyleWindow : Form {
        readonly CheckBox fillEnabled,borderEnabled;
        readonly NumericUpDown width;
        readonly Button fillColor,borderColor;
        readonly Panel preview;
        readonly ShapeStyle original;
        Color fill,stroke;
        internal ShapeStyle Style {get{return new ShapeStyle{Fill=fillEnabled.Checked?(Color?)fill:null,Stroke=stroke,Border=borderEnabled.Checked,Width=(float)width.Value,FillMode=original.FillMode};}}
        internal ShapeStyleWindow(ShapeStyle style){
            original=style;fill=style.Fill??Color.Black;stroke=style.Stroke;
            FormUi.Setup(this,"矩形 / 路径样式",new Size(440,290));MinimumSize=new Size(360,280);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=5,Padding=new Padding(12)};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));for(int i=0;i<3;i++)layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
            fillEnabled=new CheckBox{Text=UiText.Get("填充"),AutoSize=true,Checked=style.Fill.HasValue,Anchor=AnchorStyles.Left};borderEnabled=new CheckBox{Text=UiText.Get("边框"),AutoSize=true,Checked=style.Border,Anchor=AnchorStyles.Left};
            fillColor=FormUi.Button("填充颜色…",delegate{Choose(true);});borderColor=FormUi.Button("边框颜色…",delegate{Choose(false);});layout.Controls.Add(fillEnabled,0,0);layout.Controls.Add(fillColor,1,0);layout.Controls.Add(borderEnabled,0,1);layout.Controls.Add(borderColor,1,1);
            layout.Controls.Add(new Label{Text=UiText.Get("线宽（磅）"),AutoSize=true,Anchor=AnchorStyles.Left},0,2);width=new NumericUpDown{Minimum=.1m,Maximum=100,DecimalPlaces=1,Increment=.1m,Value=Math.Min(100,Math.Max(.1m,(decimal)style.Width)),Dock=DockStyle.Fill};layout.Controls.Add(width,1,2);
            preview=new Panel{Dock=DockStyle.Fill,MinimumSize=new Size(100,70),Margin=new Padding(0,8,0,8)};preview.Paint+=delegate(object s,PaintEventArgs e){var r=new Rectangle(12,12,Math.Max(1,preview.Width-24),Math.Max(1,preview.Height-24));e.Graphics.Clear(Color.White);if(fillEnabled.Checked)using(var brush=new SolidBrush(fill))e.Graphics.FillRectangle(brush,r);if(borderEnabled.Checked)using(var pen=new Pen(stroke,Math.Min(12,(float)width.Value)))e.Graphics.DrawRectangle(pen,r);};layout.Controls.Add(preview,0,3);layout.SetColumnSpan(preview,2);
            var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=FormUi.Button("取消",delegate{DialogResult=DialogResult.Cancel;});var ok=FormUi.Button("确定",delegate{if(!fillEnabled.Checked&&!borderEnabled.Checked){MessageBox.Show(this,UiText.Get("请至少保留填充或边框。"));return;}DialogResult=DialogResult.OK;});buttons.Controls.Add(cancel);buttons.Controls.Add(ok);layout.Controls.Add(buttons,0,4);layout.SetColumnSpan(buttons,2);AcceptButton=ok;CancelButton=cancel;
            fillEnabled.CheckedChanged+=delegate{Changed();};borderEnabled.CheckedChanged+=delegate{Changed();};width.ValueChanged+=delegate{Changed();};Changed();
        }
        void Choose(bool isFill){using(var dialog=new ColorDialog{FullOpen=true,Color=isFill?fill:stroke})if(dialog.ShowDialog(this)==DialogResult.OK){if(isFill)fill=dialog.Color;else stroke=dialog.Color;Changed();}}
        void Changed(){fillColor.Enabled=fillEnabled.Checked;borderColor.Enabled=width.Enabled=borderEnabled.Checked;fillColor.BackColor=fill;fillColor.ForeColor=fill.GetBrightness()<.5f?Color.White:Color.Black;borderColor.BackColor=stroke;borderColor.ForeColor=stroke.GetBrightness()<.5f?Color.White:Color.Black;preview.Invalidate();}
    }
    internal sealed partial class MainWindow {
        internal Color? RectangleFill;
        ToolStripDropDownButton rectangleFillButton;
        void InitializeShapeTools(){
            rectangleFillButton=new ToolStripDropDownButton{Text=UiText.Get("填充：无"),ToolTipText=UiText.Get("矩形填充颜色；普通填充可删除，不是永久涂黑。")};
            rectangleFillButton.DropDownItems.Add(UiText.Get("无填充"),null,delegate{SetRectangleFill(null);});rectangleFillButton.DropDownItems.Add(UiText.Get("黑色"),null,delegate{SetRectangleFill(Color.Black);});rectangleFillButton.DropDownItems.Add(UiText.Get("白色"),null,delegate{SetRectangleFill(Color.White);});rectangleFillButton.DropDownItems.Add(UiText.Get("其他颜色…"),null,delegate{using(var dialog=new ColorDialog{FullOpen=true,Color=RectangleFill??Color.Black})if(dialog.ShowDialog(this)==DialogResult.OK)SetRectangleFill(dialog.Color);});editbar.Items.Add(rectangleFillButton);
            Button(editbar,UiText.Get("黑块"),BeginBlackRectangle,UiText.Get("绘制可编辑的实心黑色矩形；脱敏请使用永久涂黑。"),null);
        }
        void SetRectangleFill(Color? color){
            RectangleFill=color;RefreshRectangleFill();var pick=canvas.Selected;
            if(Document!=null&&Document.CanEdit&&pick!=null&&!pick.IsAnnotation&&pick.Type==2){var style=Document.ReadShapeStyle(pick);style.Fill=color;if(!color.HasValue)style.Border=true;Run(delegate{Document.ApplyShapeStyle(pick,style);});canvas.Selected=Document.PickIndex(pick.ObjectIndex);SelectionChanged();}
        }
        void RefreshRectangleFill(){if(rectangleFillButton!=null){rectangleFillButton.Text=UiText.Get(RectangleFill.HasValue?"填充：实色":"填充：无");rectangleFillButton.ForeColor=RectangleFill.HasValue&&RectangleFill.Value.GetBrightness()<.85f?RectangleFill.Value:SystemColors.ControlText;}}
        void BeginBlackRectangle(){Ink=Color.Black;RectangleFill=Color.Black;colorButton.ForeColor=Ink;RefreshRectangleFill();canvas.ClearSelection();SetTool(Tool.Rectangle);}
        void EditShape(Pick pick){using(var dialog=new ShapeStyleWindow(Document.ReadShapeStyle(pick)))if(dialog.ShowDialog(this)==DialogResult.OK){Run(delegate{Document.ApplyShapeStyle(pick,dialog.Style);});canvas.Selected=Document.PickIndex(pick.ObjectIndex);SelectionChanged();canvas.Invalidate();}}
        internal void BlackRectangleForTest(){BeginBlackRectangle();}
        internal void RectangleFillForTest(Color? color){SetRectangleFill(color);}
    }
}
