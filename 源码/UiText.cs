using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MiniPdf {
    internal static partial class UiText {
        internal static bool English=true;
        static readonly Dictionary<string,string> translations=CreateTranslations();
        static readonly Dictionary<string,string> originals=CreateOriginals();
        static Dictionary<string,string> CreateOriginals(){var result=new Dictionary<string,string>(StringComparer.Ordinal);foreach(var pair in translations)if(!result.ContainsKey(pair.Value))result.Add(pair.Value,pair.Key);return result;}
        internal static string Get(string text){if(!English||String.IsNullOrEmpty(text))return text;string value;return translations.TryGetValue(text,out value)?value:text;}
        internal static string Source(string text){if(String.IsNullOrEmpty(text))return text;string value;return originals.TryGetValue(text,out value)?value:text;}
        static string Refresh(string text){return Get(Source(text));}
        internal static void Apply(Control control){
            if(control==null)return;
            bool documentPage=control is TabPage&&control.Parent!=null&&control.Parent.Name=="DocumentTabs";
            if(!documentPage&&!(control is TextBoxBase)&&!(control is ComboBox)&&!(control is ListBox))control.Text=Refresh(control.Text);
            control.AccessibleName=Refresh(control.AccessibleName);
            var strip=control as ToolStrip;if(strip!=null)foreach(ToolStripItem item in strip.Items)Apply(item);
            var combo=control as ComboBox;if(combo!=null&&combo.DropDownStyle==ComboBoxStyle.DropDownList){int selected=combo.SelectedIndex;for(int i=0;i<combo.Items.Count;i++)if(combo.Items[i] is string){string old=(string)combo.Items[i],text=Refresh(old);if(text!=old)combo.Items[i]=text;}if(selected>=0&&combo.SelectedIndex!=selected)combo.SelectedIndex=selected;}
            foreach(Control child in control.Controls)Apply(child);
        }
        static void Apply(ToolStripItem item){if(!(item is ToolStripControlHost))item.Text=Refresh(item.Text);item.ToolTipText=Refresh(item.ToolTipText);item.AccessibleName=Refresh(item.AccessibleName);var parent=item as ToolStripDropDownItem;if(parent!=null&&parent.HasDropDownItems)foreach(ToolStripItem child in parent.DropDownItems)Apply(child);}
    }
}
