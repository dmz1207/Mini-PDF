using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed class FormWindow : Form {
        internal readonly PdfForms Model;
        readonly ListBox fields;
        FlowLayoutPanel propertyPanel;
        readonly FormCanvas canvas;
        readonly NumericUpDown page,fontSize,maxLength;
        readonly ComboBox tool,kind,alignment,choiceValue;
        readonly TextBox name,tooltip,textValue,defaultValue,options,onValue;
        readonly CheckBox design,readOnly,required,noExport,checkedValue;
        readonly ListBox listValue;
        readonly Label status;
        readonly Button apply,update;
        readonly Timer delay=new Timer{Interval=400};
        readonly Stack<List<PdfField>> undo=new Stack<List<PdfField>>(),redo=new Stack<List<PdfField>>();
        readonly bool editable;
        PdfDocument view;PdfField selected;FormWidget widget;bool building,dirty,pending,closing;int generation;
        internal FormCanvas CanvasForTest {get{return canvas;}}
        internal void DesignForTest(bool enabled){design.Checked=enabled;}
        internal void SelectForTest(string value){Select(Model.Fields.First(f=>f.Name==value),null);RefreshList();}
        internal void HistoryForTest(bool forward){History(forward);}
        internal void DiscardForTest(){closing=true;Close();}
        internal bool ApplyEnabledForTest{get{return apply.Enabled;}}
        internal void CheckedForTest(bool value){checkedValue.Checked=value;}
        internal void UpdateForTest(){update.PerformClick();}
        internal string LayoutDiagnosticsForTest {get{return String.Join(Environment.NewLine,propertyPanel.Controls.Cast<Control>().Where(c=>c.Visible).Select(c=>c.GetType().Name+" "+c.Text.Replace("\r","").Replace("\n","|")+" "+c.Bounds+" min="+c.MinimumSize));}}
        internal bool LayoutFitsForTest {get{var visible=propertyPanel.Controls.Cast<Control>().Where(c=>c.Visible).ToList();return visible.All(c=>c.Height>0)&&visible.Zip(visible.Skip(1),(a,b)=>a.Bottom<=b.Top).All(ok=>ok);}}
        internal bool ReadyForTest {get{return canvas!=null&&view!=null&&canvas.HasImage;}}
        internal FormWindow(PdfDocument document) {
            Model=PdfForms.Read(document);editable=document.CanEdit&&document.SignatureCount==0&&N.FPDF_GetSecurityHandlerRevision(document.Handle)<0;
            FormUi.Setup(this,"表单工具",new Size(1180,730));MinimumSize=new Size(900,570);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(8),ColumnCount=1,RowCount=3};root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
            var bar=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};page=new NumericUpDown{Minimum=1,Maximum=Model.PageCount,Value=document.Index+1,Width=65};design=new CheckBox{Text=UiText.Get("设计字段"),AutoSize=true,Enabled=editable};tool=FormUi.Combo(150);tool.Items.Add(UiText.Get("选择 / 填写"));for(int i=0;i<=8;i++)tool.Items.Add(UiText.Get(PdfField.KindName((FieldKind)i)));tool.SelectedIndex=0;tool.Enabled=false;
            bar.Controls.AddRange(new Control[]{FormUi.Label("页面"),page,design,tool,FormUi.Button("撤销",delegate{History(false);}),FormUi.Button("重做",delegate{History(true);}),FormUi.Button("导入数据…",delegate{Import();}),FormUi.Button("导出数据…",delegate{Export();}),FormUi.Button("重置填写值",delegate{Reset();})});root.Controls.Add(bar,0,0);
            var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,210));content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,355));root.Controls.Add(content,0,1);
            fields=new ListBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,HorizontalScrollbar=true,IntegralHeight=false,Font=Font};content.Controls.Add(fields,0,0);
            canvas=new FormCanvas{Dock=DockStyle.Fill,Model=Model};content.Controls.Add(canvas,1,0);
            var properties=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(8)};propertyPanel=properties;content.Controls.Add(properties,2,0);
            name=Box(properties,"字段名称");tooltip=Box(properties,"提示说明");kind=FormUi.Combo(235);for(int i=0;i<=11;i++)kind.Items.Add(UiText.Get(PdfField.KindName((FieldKind)i)));AddCombo(properties,kind);
            properties.Controls.Add(FormUi.Label("填写值"));textValue=new TextBox{Width=235,Height=68,Multiline=true,ScrollBars=ScrollBars.Vertical};properties.Controls.Add(textValue);
            checkedValue=new CheckBox{Text=UiText.Get("已勾选"),AutoSize=true};properties.Controls.Add(checkedValue);
            choiceValue=FormUi.Combo(235);AddCombo(properties,choiceValue);listValue=new ListBox{Width=235,Height=88,SelectionMode=SelectionMode.MultiExtended};properties.Controls.Add(listValue);
            defaultValue=Box(properties,"默认值");onValue=Box(properties,"当前单选项 / 勾选导出值");options=Box(properties,"选项（每行 导出值|显示文字）");options.Multiline=true;options.Height=65;options.ScrollBars=ScrollBars.Vertical;
            var row=new FlowLayoutPanel{Width=310,AutoSize=true,WrapContents=true};fontSize=new NumericUpDown{Minimum=0,Maximum=200,DecimalPlaces=1,Width=62};maxLength=new NumericUpDown{Minimum=0,Maximum=20000,Width=72};row.Controls.AddRange(new Control[]{FormUi.Label("字号"),fontSize,FormUi.Label("字数"),maxLength});properties.Controls.Add(row);
            alignment=FormUi.Combo(235);alignment.Items.AddRange(new object[]{UiText.Get("左对齐"),UiText.Get("居中"),UiText.Get("右对齐")});AddCombo(properties,alignment);
            readOnly=new CheckBox{Text=UiText.Get("只读"),AutoSize=true};required=new CheckBox{Text=UiText.Get("必填"),AutoSize=true};noExport=new CheckBox{Text=UiText.Get("不导出数据"),AutoSize=true};properties.Controls.AddRange(new Control[]{readOnly,required,noExport});
            update=FormUi.Button("更新字段",delegate{SaveSelected();});properties.Controls.Add(update);properties.Controls.Add(FormUi.Button("复制字段 / 新增单选项",delegate{Duplicate();}));properties.Controls.Add(FormUi.Button("删除所选字段",delegate{Delete();}));properties.Controls.Add(FormUi.Button("字段顺序上移",delegate{Reorder(-1);}));properties.Controls.Add(FormUi.Button("字段顺序下移",delegate{Reorder(1);}));
            var footer=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};footer.Controls.Add(FormUi.Button("关闭",delegate{Close();}));apply=FormUi.Button("应用到文档",delegate{if(pending&&!SaveCurrent())return;if(!editable)return;closing=true;DialogResult=DialogResult.OK;});apply.Enabled=editable;footer.Controls.Add(apply);status=new Label{AutoSize=true,MaximumSize=new Size(650,0),Margin=new Padding(6,9,8,3)};footer.Controls.Add(status);root.Controls.Add(footer,0,2);
            Action fitProperties=delegate {
                int width=Math.Max(150,properties.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-24);
                properties.SuspendLayout();
                foreach(Control control in properties.Controls) {
                    control.MaximumSize=control is Label||control is CheckBox?new Size(width,Int32.MaxValue):Size.Empty;
                    if(control is Panel&&control.Controls.Count==1&&control.Controls[0] is ComboBox)control.Height=control.Controls[0].Height;
                    if(control is TextBox||control is ComboBox||control is ListBox||control is Panel)control.Width=width;
                    if(control is Button){control.AutoSize=false;control.Width=width;control.Height=Math.Max(36,TextRenderer.MeasureText(control.Text,control.Font,new Size(width-16,1000),TextFormatFlags.WordBreak).Height+16);}
                }
                textValue.Height=options.Height=Math.Max(68,Font.Height*4);listValue.Height=Math.Max(88,Font.Height*5);properties.ResumeLayout(true);
            };
            Action fitColumns=delegate{using(var graphics=CreateGraphics())content.ColumnStyles[2].Width=Math.Min(320*graphics.DpiX/96,content.ClientSize.Width*.42f);};content.SizeChanged+=delegate{fitColumns();};Shown+=delegate{fitColumns();fitProperties();};
            properties.SizeChanged+=delegate{fitProperties();};Shown+=delegate{fitProperties();};
            fields.SelectedIndexChanged+=delegate{if(building)return;var next=fields.SelectedItem as PdfField;if(next==selected)return;if(pending){var result=MessageBox.Show(this,UiText.Get("应用尚未提交的字段修改？"),Text,MessageBoxButtons.YesNoCancel);if(result==DialogResult.Cancel){RefreshList();return;}if(result==DialogResult.Yes&&!SaveCurrent()){RefreshList();return;}}Select(next,next==null?null:next.Widgets.FirstOrDefault());};
            page.ValueChanged+=delegate{if(!building){canvas.CancelDrag();RefreshPreview();}};
            design.CheckedChanged+=delegate{tool.Enabled=design.Checked;canvas.Editable=design.Checked&&editable;canvas.AddKind=design.Checked&&tool.SelectedIndex>0?(FieldKind?)(tool.SelectedIndex-1):null;Populate();};
            tool.SelectedIndexChanged+=delegate{canvas.CancelDrag();canvas.AddKind=design.Checked&&tool.SelectedIndex>0?(FieldKind?)(tool.SelectedIndex-1):null;canvas.Cursor=canvas.AddKind.HasValue?Cursors.Cross:Cursors.Default;};
            canvas.Picked+=delegate(PdfField f,FormWidget w){if(pending&&!SaveCurrent())return;Select(f,w);RefreshList();};
            canvas.FieldCreated+=Create;canvas.Moved+=delegate(FormWidget w,N.Rect bounds){Commit(delegate{w.Bounds=bounds;selected.Changed=true;});};
            foreach(Control control in new Control[]{name,tooltip,textValue,defaultValue,onValue,options})control.TextChanged+=delegate{if(!building){pending=true;apply.Enabled=editable;}};
            foreach(var check in new[]{readOnly,required,noExport,checkedValue})check.CheckedChanged+=delegate{if(!building){pending=true;apply.Enabled=editable;}};
            foreach(var number in new[]{fontSize,maxLength})number.ValueChanged+=delegate{if(!building){pending=true;apply.Enabled=editable;}};kind.SelectedIndexChanged+=delegate{if(!building){pending=true;apply.Enabled=editable;}};alignment.SelectedIndexChanged+=delegate{if(!building){pending=true;apply.Enabled=editable;}};choiceValue.SelectedIndexChanged+=delegate{if(!building){pending=true;apply.Enabled=editable;}};listValue.SelectedIndexChanged+=delegate{if(!building){pending=true;apply.Enabled=editable;}};
            delay.Tick+=delegate{delay.Stop();Render();};FormClosing+=ClosingRequested;UiText.Apply(this);RefreshList();if(Model.Fields.Count>0)Select(Model.Fields.FirstOrDefault(f=>f.Widgets.Any(w=>w.Page==document.Index))??Model.Fields[0],null);else Populate();RefreshPreview();
        }
        static void AddCombo(Control parent,ComboBox combo){var panel=new Panel{Width=combo.Width,Height=combo.Height,Margin=new Padding(3)};combo.Dock=DockStyle.Top;panel.Controls.Add(combo);parent.Controls.Add(panel);combo.SizeChanged+=delegate{panel.Height=combo.Height;};}
        static TextBox Box(Control parent,string label){parent.Controls.Add(FormUi.Label(label));var box=new TextBox{Width=235};parent.Controls.Add(box);return box;}
        static string State(List<PdfField> source){var b=new StringBuilder();Action<string> add=s=>b.Append(s.Length).Append(':').Append(s);foreach(var f in source){foreach(var value in new[]{f.Name,((int)f.Kind).ToString(),f.Flags.ToString(),f.MaxLength.ToString(),f.Align.ToString(),PdfForms.Number(f.Size),f.Tooltip,f.Default,f.Value})add(value);foreach(var c in f.Choices){add(c.Value);add(c.Label);}foreach(var w in f.Widgets){add(w.Page.ToString());add(w.On);add(PdfForms.Number(w.Bounds.left));add(PdfForms.Number(w.Bounds.right));add(PdfForms.Number(w.Bounds.bottom));add(PdfForms.Number(w.Bounds.top));}}return b.ToString();}
        void Commit(Action action) {
            if(!editable)return;var prior=Model.Snapshot();string before=State(prior),keep=selected==null?"":selected.Name;
            try{action();Model.Validate();if(before!=State(Model.Fields)){undo.Push(prior);redo.Clear();dirty=true;}pending=false;RefreshList();if(selected!=null&&!Model.Fields.Contains(selected))Select(Model.Fields.FirstOrDefault(f=>f.Name==keep),null);Populate();RefreshPreview();}
            catch(Exception e){Model.Fields=prior;Select(Model.Fields.FirstOrDefault(f=>f.Name==keep),null);RefreshList();MessageBox.Show(this,e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        void RefreshList(){building=true;try{fields.BeginUpdate();fields.Items.Clear();foreach(var f in Model.Fields)fields.Items.Add(f);fields.SelectedItem=selected;fields.EndUpdate();}finally{building=false;}}
        void Select(PdfField field,FormWidget selectedWidget){selected=field;widget=selectedWidget??(field==null?null:field.Widgets.FirstOrDefault());canvas.Selected=field;canvas.Widget=widget;building=true;try{if(widget!=null)page.Value=widget.Page+1;}finally{building=false;}Populate();RefreshPreview();}
        void Populate() {
            building=true;try {
                bool has=selected!=null,supported=has&&selected.Supported,author=editable&&design.Checked&&supported,fill=editable&&supported&&!selected.ReadOnly;
                name.Text=has?selected.Name:"";tooltip.Text=has?selected.Tooltip:"";kind.SelectedIndex=has?(int)selected.Kind:0;textValue.Text=has?selected.Value.Replace("\r","").Replace("\n",Environment.NewLine):"";defaultValue.Text=has?selected.Default:"";onValue.Text=widget==null?"Yes":widget.On;options.Text=has?String.Join(Environment.NewLine,selected.Choices.Select(c=>c.Value+"|"+c.Label)):"";
                fontSize.Value=has?(decimal)Math.Max(0,Math.Min(200,selected.Size)):0;maxLength.Value=has?Math.Max(0,Math.Min(20000,selected.MaxLength)):0;alignment.SelectedIndex=has?Math.Max(0,Math.Min(2,selected.Align)):0;readOnly.Checked=has&&selected.ReadOnly;required.Checked=has&&selected.Required;noExport.Checked=has&&(selected.Flags&4)!=0;
                bool check=supported&&selected.Kind==FieldKind.Check,choice=supported&&selected.Kind>=FieldKind.Radio&&selected.Kind<=FieldKind.EditableCombo,list=supported&&(selected.Kind==FieldKind.List||selected.Kind==FieldKind.MultiList);
                checkedValue.Visible=check;checkedValue.Checked=check&&selected.Values.Any(v=>v!="Off"&&v.Length>0);choiceValue.Parent.Visible=choice;listValue.Visible=list;textValue.Visible=!check&&!choice&&!list;
                choiceValue.Items.Clear();listValue.Items.Clear();
                if(choice||list){var choices=selected.Kind==FieldKind.Radio?selected.Widgets.Select(w=>new FormChoice{Value=w.On,Label=w.On}).ToList():selected.Choices;foreach(var c in choices){choiceValue.Items.Add(c);listValue.Items.Add(c);}choiceValue.DropDownStyle=selected.Kind==FieldKind.EditableCombo?ComboBoxStyle.DropDown:ComboBoxStyle.DropDownList;choiceValue.SelectedItem=choices.FirstOrDefault(c=>c.Value==selected.Value);if(selected.Kind==FieldKind.EditableCombo)choiceValue.Text=selected.Value;listValue.SelectionMode=selected.Kind==FieldKind.MultiList?SelectionMode.MultiExtended:SelectionMode.One;for(int i=0;i<choices.Count;i++)if(selected.Values.Contains(choices[i].Value))listValue.SetSelected(i,true);}
                foreach(Control control in new Control[]{name,tooltip,kind,defaultValue,onValue,options,fontSize,maxLength,alignment,readOnly,required,noExport})control.Enabled=author;
                foreach(Control control in new Control[]{textValue,checkedValue,choiceValue,listValue})control.Enabled=fill;update.Enabled=has&&supported&&editable;
                apply.Enabled=editable&&(dirty||pending);
                status.Text=!editable?UiText.Get("只读查看；签名、权限或加密限制了表单修改。"):Model.Fields.Count+UiText.Get(" 个字段，未填必填项：")+Model.MissingRequired+"  ·  "+UiText.Get("字号 0 表示自动；设计模式下可拖动和缩放。");pending=false;
            }finally{building=false;}
        }
        bool SaveCurrent() {
            if(selected==null||!selected.Supported||!editable){pending=false;return true;}bool saved=false;
            Commit(delegate {
                var f=selected;FieldKind prior=f.Kind;if(design.Checked){f.Name=name.Text.Trim();f.Tooltip=tooltip.Text;f.Kind=(FieldKind)Math.Max(0,Math.Min(8,kind.SelectedIndex));f.Default=defaultValue.Text;f.Size=(float)fontSize.Value;f.MaxLength=(int)maxLength.Value;f.Align=alignment.SelectedIndex;f.Flags=(f.Flags&~7)|(readOnly.Checked?1:0)|(required.Checked?2:0)|(noExport.Checked?4:0);f.Choices=new List<FormChoice>();foreach(string line in options.Lines)if(line.Length>0){int split=line.IndexOf('|');f.Choices.Add(new FormChoice{Value=split<0?line:line.Substring(0,split),Label=split<0?line:line.Substring(split+1)});}if(widget!=null)widget.On=String.IsNullOrWhiteSpace(onValue.Text)?"Yes":onValue.Text.Trim();}
                if(!f.ReadOnly||design.Checked) {
                    if(prior==FieldKind.Check)f.Values=new[]{checkedValue.Checked?(widget==null?"Yes":widget.On):"Off"};
                    else if(prior==FieldKind.Radio||prior==FieldKind.Combo||prior==FieldKind.EditableCombo){var chosen=choiceValue.SelectedItem as FormChoice;f.Values=String.IsNullOrEmpty(choiceValue.Text)?new string[0]:new[]{chosen==null?choiceValue.Text:chosen.Value};}
                    else if(prior==FieldKind.List||prior==FieldKind.MultiList)f.Values=listValue.SelectedItems.Cast<FormChoice>().Select(c=>c.Value).ToArray();
                    else f.Values=String.IsNullOrEmpty(textValue.Text)?new string[0]:new[]{textValue.Text.Replace("\r","")};
                }
                if(prior!=f.Kind){f.Values=new string[0];if(f.Kind==FieldKind.Combo||f.Kind==FieldKind.List)f.Choices.Add(new FormChoice{Value="Option1",Label="Option 1"});}
                f.Changed=true;Model.Validate();saved=true;
            });return saved;
        }
        void SaveSelected(){SaveCurrent();}
        string Unique(string prefix){string value=prefix;int n=1;while(Model.Fields.Any(f=>f.Name==value))value=prefix+(++n);return value;}
        void Create(FieldKind type,N.Rect bounds) {
            if(!editable)return;if(pending&&!SaveCurrent())return;Commit(delegate{var f=new PdfField{Name=Unique("Field"),Kind=type,Changed=true};var w=new FormWidget{Page=(int)page.Value-1,Bounds=bounds};f.Widgets.Add(w);if(type>=FieldKind.Combo)f.Choices.Add(new FormChoice{Value="Option1",Label="Option 1"});Model.Fields.Add(f);selected=f;widget=w;canvas.Selected=f;canvas.Widget=w;});
            building=true;tool.SelectedIndex=0;building=false;canvas.AddKind=null;canvas.Cursor=Cursors.Default;
        }
        void Duplicate(){if(!editable||!design.Checked||selected==null||!selected.Supported)return;if(pending&&!SaveCurrent())return;Commit(delegate{if(selected.Kind==FieldKind.Radio){var w=(widget??selected.Widgets[0]).Clone();w.Id=0;w.On="Option"+(selected.Widgets.Count+1);while(selected.Widgets.Any(x=>x.On==w.On))w.On+="_";w.Bounds.left+=24;w.Bounds.right+=24;selected.Widgets.Add(w);widget=w;selected.Changed=true;}else{var f=selected.Clone();f.Id=0;f.Name=Unique(f.Name+"_copy");f.Changed=true;foreach(var w in f.Widgets){w.Id=0;w.Bounds.left+=12;w.Bounds.right+=12;w.Bounds.top-=12;w.Bounds.bottom-=12;}Model.Fields.Add(f);selected=f;widget=f.Widgets.FirstOrDefault();}canvas.Selected=selected;canvas.Widget=widget;});}
        void Delete(){if(!editable||!design.Checked||selected==null||!selected.Supported)return;Commit(delegate{if(selected.Kind==FieldKind.Radio&&selected.Widgets.Count>1&&widget!=null){selected.Widgets.Remove(widget);selected.Changed=true;widget=selected.Widgets[0];}else{Model.Fields.Remove(selected);selected=null;widget=null;}canvas.Selected=selected;canvas.Widget=widget;});}
        void Reorder(int direction){if(!editable||!design.Checked||selected==null)return;Commit(delegate{int from=Model.Fields.IndexOf(selected),to=from+direction;if(to>=0&&to<Model.Fields.Count){Model.Fields.RemoveAt(from);Model.Fields.Insert(to,selected);}});}
        void History(bool forward){if(!editable)return;var source=forward?redo:undo;if(source.Count==0)return;var target=forward?undo:redo;target.Push(Model.Snapshot());Model.Fields=source.Pop();selected=null;widget=null;canvas.Model=Model;canvas.Selected=null;canvas.Widget=null;pending=false;dirty=undo.Count>0;RefreshList();Populate();RefreshPreview();}
        void Reset(){if(!editable)return;if(MessageBox.Show(this,UiText.Get("将可填写字段恢复为默认值？"),Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;Commit(delegate{foreach(var f in Model.Fields)if(f.Supported&&!f.ReadOnly){f.Values=String.IsNullOrEmpty(f.Default)?new string[0]:f.Kind==FieldKind.MultiList?f.Default.Split('\n'):new[]{f.Default};f.Changed=true;}});}
        void Import(){if(!editable)return;using(var file=new OpenFileDialog{Filter="XFDF (*.xfdf)|*.xfdf",RestoreDirectory=true})if(file.ShowDialog(this)==DialogResult.OK)try{string xml=File.ReadAllText(file.FileName);Commit(delegate{int count=Model.ImportData(xml);status.Text=UiText.Get("已导入字段：")+count;});}catch(Exception e){MessageBox.Show(this,e.Message,Text);}}
        void Export(){if(pending&&!SaveCurrent())return;using(var save=new SaveFileDialog{Filter="XFDF (*.xfdf)|*.xfdf",DefaultExt="xfdf",AddExtension=true,RestoreDirectory=true,FileName="form-data.xfdf"})if(save.ShowDialog(this)==DialogResult.OK)try{File.WriteAllText(save.FileName,Model.ExportData(),new UTF8Encoding(false));}catch(Exception e){MessageBox.Show(this,e.Message,Text);}}
        void RefreshPreview(){generation++;RenderPool.Cancel(canvas,true);delay.Stop();delay.Start();}
        void Render() {
            int version=generation;try {
                byte[] bytes=dirty?Model.Build():Model.Source;var next=PdfDocument.FromBytes(bytes,"");next.OpenPage((int)page.Value-1);var size=next.PageSize(next.Index);float scale=Math.Min(1200/size.Width,1600/size.Height);int w=Math.Max(1,(int)(size.Width*scale)),h=Math.Max(1,(int)(size.Height*scale));var old=view;view=next;canvas.View=view;if(old!=null)old.Dispose();
                bool accepted=RenderPool.Request(canvas,view.GetRenderSource(),"",view.Index,w,h,0,new Rectangle(0,0,w,h),false,delegate(RenderFrame frame,Exception error){try{if(version!=generation||IsDisposed)return;if(error!=null){status.Text=error.Message;return;}if(frame!=null)canvas.SetImage(new Bitmap(frame.Image));}finally{if(frame!=null)frame.Dispose();}});
                if(!accepted)delay.Start();
            }catch(Exception e){status.Text=e.Message;}
        }
        void ClosingRequested(object sender,FormClosingEventArgs e){if(closing||!editable||!dirty&&!pending)return;var answer=MessageBox.Show(this,UiText.Get("关闭前将表单修改应用到当前文档？之后可用 Ctrl+S 保存。"),Text,MessageBoxButtons.YesNoCancel);if(answer==DialogResult.Cancel){e.Cancel=true;return;}if(answer==DialogResult.Yes){if(pending&&!SaveCurrent()){e.Cancel=true;return;}closing=true;DialogResult=DialogResult.OK;}}
        protected override void Dispose(bool disposing){if(disposing){generation++;delay.Dispose();RenderPool.Cancel(canvas,true);if(view!=null)view.Dispose();}base.Dispose(disposing);}
    }
    internal sealed partial class MainWindow {
        void EditForms(){if(Document==null)return;try{using(var window=new FormWindow(Document))if(window.ShowDialog(this)==DialogResult.OK)Run(delegate{Document.ApplyForms(window.Model);});}catch(Exception e){MessageBox.Show(this,e.Message,UiText.Get("表单未打开"),MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    }
}
