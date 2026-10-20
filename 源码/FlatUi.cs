using System.Drawing;
using System.Windows.Forms;

namespace MiniPdf {
    internal sealed partial class MainWindow {
        static readonly ToolStripRenderer flatRenderer=new FlatRenderer();
        sealed class FlatRenderer : ToolStripProfessionalRenderer {
            internal FlatRenderer(){RoundedEdges=false;}
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Color.White);}
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){if(e.ToolStrip is ToolStripDropDown)using(var pen=new Pen(Color.FromArgb(220,226,231)))e.Graphics.DrawRectangle(pen,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);}
            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){}
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var pen=new Pen(Color.FromArgb(224,229,234))){if(e.Vertical)e.Graphics.DrawLine(pen,e.Item.Width/2,6,e.Item.Width/2,e.Item.Height-7);else e.Graphics.DrawLine(pen,8,e.Item.Height/2,e.Item.Width-9,e.Item.Height/2);}}
            static void ItemBackground(ToolStripItemRenderEventArgs e){bool active=e.Item.Selected||e.Item.Pressed;var button=e.Item as ToolStripButton;bool check=button!=null&&button.Checked;if(active||check)using(var brush=new SolidBrush(e.Item.Pressed||check?Color.FromArgb(219,234,246):Color.FromArgb(236,242,247)))e.Graphics.FillRectangle(brush,new Rectangle(Point.Empty,e.Item.Size));}
            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e){ItemBackground(e);}
            protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e){ItemBackground(e);}
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){ItemBackground(e);}
        }
    }
}
