using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed partial class MainWindow {
        ToolStripButton selectQuick,handQuick;
        Color highlightInk=Color.FromArgb(255,220,56);
        void OrganizePages(){if(Document==null||!Document.CanEdit)return;using(var dialog=new MergeWindow(Document,true))if(dialog.ShowDialog(this)==DialogResult.OK)Run(delegate{Document.Organize(dialog.Plan.AllPages(),dialog.BaseSource);});}
        internal void HighlightSelection(Color color){if(Document==null)return;highlightInk=Color.FromArgb(255,color.R,color.G,color.B);Ink=highlightInk;colorButton.ForeColor=Ink;
            if(canvas.TextStart>=0&&canvas.TextCount>0){var ranges=canvas.TextRanges();if(ranges.Count>0){Run(delegate{Document.AddTextMarkup(ranges,9,highlightInk);});canvas.ClearSelection();SetTool(Tool.Read);return;}}
            SetTool(Tool.Highlight);
        }
        ToolStripDropDownButton HighlightButton(){var button=new ToolStripDropDownButton(UiText.Get("高亮")){ToolTipText=UiText.Get("选择文字后高亮；无文字选择时框选高亮"),ShowDropDownArrow=true};PopulateHighlights(button.DropDownItems);return button;}
        void PopulateHighlights(ToolStripItemCollection items){Color[] colors={Color.FromArgb(255,220,56),Color.FromArgb(255,244,117),Color.FromArgb(155,222,95),Color.FromArgb(74,207,164),Color.FromArgb(92,203,242),Color.FromArgb(113,154,255),Color.FromArgb(183,146,238),Color.FromArgb(243,142,198),Color.FromArgb(255,156,139),Color.FromArgb(255,184,85),Color.FromArgb(202,178,144),Color.FromArgb(175,184,195)};string[] names={"黄色","浅黄色","绿色","薄荷绿","天蓝色","蓝色","紫色","粉色","珊瑚色","橙色","棕色","灰色"};for(int i=0;i<colors.Length;i++){Color color=colors[i];var item=new ToolStripMenuItem(UiText.Get(names[i])){BackColor=color,ForeColor=Color.FromArgb(30,40,50)};item.Click+=delegate{HighlightSelection(color);};items.Add(item);}items.Add(new ToolStripSeparator());items.Add(UiText.Get("自定义高亮颜色…"),null,delegate{using(var dialog=new ColorDialog{Color=highlightInk,FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK)HighlightSelection(dialog.Color);});}
        void AddHighlightContext(ContextMenuStrip menu){if(Document==null||!Document.CanEdit||canvas.TextStart<0||canvas.TextCount<1)return;var item=new ToolStripMenuItem(UiText.Get("高亮所选文字"));PopulateHighlights(item.DropDownItems);menu.Items.Add(item);}
    }
    internal sealed partial class PdfDocument
    {
        // Retain original page objects and destinations when reordering/deleting.
        internal void Organize(IList<MergePage> desired, PdfDocument original)
        {
            Check(desired!=null && desired.Count>0 && desired.Count<=10000,
                  UiText.Get("请至少保留一页，最多支持 10000 页。"));
            int prior=Index;
            bool importing=false;
            var seen=new HashSet<int>();
            var sources=new List<PdfDocument>{this};
            var rotations=new int[desired.Count];
            for(int i=0;i<desired.Count;i++)
            {
                var item=desired[i];
                if(item.Source!=original || !seen.Add(item.Index)) importing=true;
                if(!sources.Contains(item.Source)) sources.Add(item.Source);
                IntPtr page=N.FPDF_LoadPage(item.Source.Handle,item.Index);
                try
                {
                    Check(page!=IntPtr.Zero,UiText.Get("无法读取页面。"));
                    rotations[i]=(N.FPDFPage_GetRotation(page)+item.Rotation)%4;
                }
                finally { if(page!=IntPtr.Zero) N.FPDF_ClosePage(page); }
            }
            using(var layers=importing ? new LayerImport(sources) : null)
            Edit(delegate {
                ClosePage();
                if(layers!=null) layers.BeginTarget(this);
                var order=new List<int>(); for(int i=0;i<Count;i++) order.Add(i);
                int next=Count;
                var ids=new List<int>(); var used=new HashSet<int>();
                foreach(var item in desired)
                {
                    int id=item.Source==original ? item.Index : -1;
                    if(id<0 || id>=next || !used.Add(id))
                    {
                        id=next++;
                        var source=layers==null ? item.Source : layers.Prepared(item.Source);
                        Check(N.FPDF_ImportPagesByIndex(Handle,source.Handle,new[]{item.Index},1,Count)!=0,
                              UiText.Get("无法插入页面。"));
                        hasImportedFonts=true; order.Add(id); used.Add(id);
                    }
                    ids.Add(id);
                }
                for(int i=order.Count-1;i>=0;i--) if(!used.Contains(order[i]))
                { N.FPDFPage_Delete(Handle,i); order.RemoveAt(i); }
                for(int i=0;i<ids.Count;i++)
                {
                    int from=order.IndexOf(ids[i]); Check(from>=0,UiText.Get("页面顺序无效。"));
                    if(from!=i)
                    {
                        Check(N.FPDF_MovePages(Handle,new[]{from},1,i)!=0,UiText.Get("无法移动页面。"));
                        int id=order[from]; order.RemoveAt(from); order.Insert(i,id);
                    }
                    IntPtr page=N.FPDF_LoadPage(Handle,i);
                    try { Check(page!=IntPtr.Zero,UiText.Get("无法读取页面。")); N.FPDFPage_SetRotation(page,rotations[i]); }
                    finally { if(page!=IntPtr.Zero) N.FPDF_ClosePage(page); }
                }
                if(layers!=null) layers.EndTarget(this);
                int selected=ids.IndexOf(prior);
                OpenPage(selected<0 ? Math.Min(prior,Count-1) : selected);
            });
        }
    }
}
