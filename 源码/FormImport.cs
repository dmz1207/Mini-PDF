using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MiniPdf {
    // PDFium page import does not reconnect AcroForm parents. Carry field metadata
    // through private widget dictionaries, so foreign references are remapped normally.
    internal static class FormImport {
        const string Meta="MiniPDFImportField",Resources="MiniPDFImportDR";
        internal static byte[] Tag(PdfDocument document,byte[] bytes,out bool tagged) {
            tagged=false;if(N.FPDF_GetFormType(document.Handle)!=1)return bytes;
            var pdf=new PdfSyntax(bytes);var catalog=pdf.ObjectDictionary(pdf.Root);string formValue;
            var acro=catalog.TryGetValue("AcroForm",out formValue)&&formValue!="null"?pdf.DictionaryValue(formValue):new Dictionary<string,string>();
            var changes=new SortedDictionary<int,string>();var metas=new Dictionary<int,int>();int next=pdf.Size;
            foreach(int page in Pages(pdf)) {
                var pageData=pdf.ObjectDictionary(page);string values;if(!pageData.TryGetValue("Annots",out values))continue;
                foreach(string reference in pdf.ArrayValues(values)) {
                    int id;if(!PdfSyntax.TryReference(reference,out id)||!pdf.ContainsObject(id)||!pdf.ObjectValue(id).StartsWith("<<"))continue;var widget=pdf.ObjectDictionary(id);if(PdfForms.Get(widget,"Subtype")!="/Widget")continue;
                    var chain=new List<Dictionary<string,string>>{widget};var ids=new List<int>{id};var seen=new HashSet<int>{id};var current=widget;
                    for(int depth=0;depth<64;depth++) {
                        string parent;int parentId;if(!current.TryGetValue("Parent",out parent)||!PdfSyntax.TryReference(parent,out parentId)||!seen.Add(parentId)||!pdf.ContainsObject(parentId)||pdf.IsStreamReference(parent))break;
                        current=pdf.ObjectDictionary(parentId);string type=PdfForms.Get(current,"Type");if(type=="/Page"||type=="/Pages")break;chain.Add(current);ids.Add(parentId);
                    }
                    int anchor=widget.ContainsKey("FT")||widget.ContainsKey("T")||ids.Count==1?id:ids[1];int meta;
                    if(!metas.TryGetValue(anchor,out meta)) {
                        var field=new Dictionary<string,string>();var names=new List<string>();
                        for(int i=chain.Count-1;i>=0;i--) {
                            var source=chain[i];foreach(string key in new[]{"FT","Ff","V","DV","DA","Q","Opt","MaxLen","TU","TM","AA","A","RV","DS","TI","I","Lock"}){string value;if(source.TryGetValue(key,out value))field[key]=value;}
                            string name=PdfForms.Decode(PdfForms.Get(source,"T"));if(name.Length>0)names.Add(name);
                        }
                        if(!field.ContainsKey("FT"))continue;
                        field["T"]=PdfForms.Unicode(names.Count>0?String.Join(".",names):"Field"+anchor);
                        if(!field.ContainsKey("DA")&&acro.ContainsKey("DA"))field["DA"]=acro["DA"];
                        meta=next++;metas[anchor]=meta;changes[meta]=PdfSyntax.Dictionary(field);
                    }
                    var w=new Dictionary<string,string>(widget);w[Meta]=meta+" 0 R";w[Resources]=PdfForms.Get(acro,"DR","<<>>");changes[id]=PdfSyntax.Dictionary(w);tagged=true;
                }
            }
            return tagged?pdf.Append(bytes,changes):bytes;
        }
        static List<int> Pages(PdfSyntax pdf) {
            var output=new List<int>();var seen=new HashSet<int>();Action<int,int> walk=null;walk=delegate(int id,int depth){PdfDocument.Check(depth<64&&seen.Add(id),"Invalid imported page tree.");var d=pdf.ObjectDictionary(id);if(PdfForms.Get(d,"Type")=="/Page")output.Add(id);else foreach(string child in pdf.ArrayValues(d["Kids"])){int next;PdfDocument.Check(PdfSyntax.TryReference(child,out next),"Invalid imported page tree.");walk(next,depth+1);}};int root;PdfSyntax.TryReference(pdf.ObjectDictionary(pdf.Root)["Pages"],out root);walk(root,0);return output;
        }
        static string Literal(string text){return "("+text.Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)")+ ")";}
        sealed class Group {internal int MetaId,Id;internal string Name;internal Dictionary<string,string> Field,DR;internal List<int> Widgets=new List<int>();}
        internal static byte[] Recover(byte[] bytes) {
            var pdf=new PdfSyntax(bytes);var pages=Pages(pdf);var groups=new Dictionary<int,Group>();var widgetIds=new HashSet<int>();
            foreach(int page in pages) {
                var d=pdf.ObjectDictionary(page);string annots;if(!d.TryGetValue("Annots",out annots))continue;
                foreach(string item in pdf.ArrayValues(annots)) {
                    int id;if(!PdfSyntax.TryReference(item,out id)||!pdf.ContainsObject(id)||!pdf.ObjectValue(id).StartsWith("<<"))continue;var w=pdf.ObjectDictionary(id);string tag;int meta;
                    if(PdfForms.Get(w,"Subtype")!="/Widget"||!w.TryGetValue(Meta,out tag)||!PdfSyntax.TryReference(tag,out meta))continue;
                    Group group;if(!groups.TryGetValue(meta,out group)){var field=pdf.ObjectDictionary(meta);group=new Group{MetaId=meta,Name=PdfForms.Decode(PdfForms.Get(field,"T")),Field=field,DR=pdf.DictionaryValue(PdfForms.Get(w,Resources,"<<>>"))};groups[meta]=group;}
                    group.Widgets.Add(id);widgetIds.Add(id);
                }
            }
            if(groups.Count==0)return bytes;
            var catalog=pdf.ObjectDictionary(pdf.Root);string value;var form=catalog.TryGetValue("AcroForm",out value)&&value!="null"?pdf.DictionaryValue(value):new Dictionary<string,string>();var roots=new List<string>();var names=new HashSet<string>();
            // Retain any original fields that were not part of this private import.
            Func<int,HashSet<int>,bool> contains=null;contains=delegate(int id,HashSet<int> active){if(widgetIds.Contains(id))return true;if(active.Count>20000||!active.Add(id)||!pdf.ContainsObject(id))return false;var d=pdf.ObjectDictionary(id);string children;if(!d.TryGetValue("Kids",out children))return false;foreach(string child in pdf.ArrayValues(children)){int next;if(PdfSyntax.TryReference(child,out next)&&contains(next,active))return true;}return false;};
            if(form.TryGetValue("Fields",out value))foreach(string root in pdf.ArrayValues(value)){int id;if(PdfSyntax.TryReference(root,out id)&&!contains(id,new HashSet<int>())){roots.Add(root);try{names.Add(PdfForms.Decode(PdfForms.Get(pdf.ObjectDictionary(id),"T")));}catch(InvalidOperationException){}}}
            var changes=new SortedDictionary<int,string>();int number=pdf.Size;var parents=new Dictionary<string,int>();var childrenById=new Dictionary<int,List<string>>();
            var dr=form.ContainsKey("DR")?pdf.DictionaryValue(form["DR"]):new Dictionary<string,string>();var fonts=dr.ContainsKey("Font")?pdf.DictionaryValue(dr["Font"]):new Dictionary<string,string>();
            foreach(var group in groups.Values) {
                string original=String.IsNullOrEmpty(group.Name)?"Field":group.Name,name=original;int suffix=1;while(names.Contains(name))name=original+"_"+(++suffix);names.Add(name);group.Name=name;group.Id=number++;
                string[] parts=name.Split('.');int parent=0;string path="";
                for(int i=0;i<parts.Length-1;i++){path+=(i==0?"":".")+parts[i];int id;if(!parents.TryGetValue(path,out id)){id=number++;parents[path]=id;childrenById[id]=new List<string>();var d=new Dictionary<string,string>{{"T",PdfForms.Unicode(parts[i])}};if(parent>0){d["Parent"]=parent+" 0 R";childrenById[parent].Add(id+" 0 R");}else roots.Add(id+" 0 R");changes[id]=PdfSyntax.Dictionary(d);}parent=id;}
                var field=new Dictionary<string,string>(group.Field);field["T"]=PdfForms.Unicode(parts[parts.Length-1]);field["Kids"]="["+String.Join(" ",group.Widgets.Select(id=>pdf.ObjectReference(id)))+"]";
                if(parent>0){field["Parent"]=parent+" 0 R";childrenById[parent].Add(group.Id+" 0 R");}else{field.Remove("Parent");roots.Add(group.Id+" 0 R");}
                if(group.DR.TryGetValue("Font",out value)) {
                    var sourceFonts=pdf.DictionaryValue(value);string da=PdfForms.Decode(PdfForms.Get(field,"DA",PdfForms.Get(form,"DA")));int f=0;
                    foreach(var font in sourceFonts){string unique="MF"+group.Id+"F"+(f++);fonts[unique]=font.Value;string token=PdfForms.Name(font.Key);da=Regex.Replace(da,Regex.Escape(token)+@"(?=\s)","/"+unique);}
                    if(da.Length>0)field["DA"]=Literal(da);
                }
                changes[group.Id]=PdfSyntax.Dictionary(field);
                foreach(int id in group.Widgets){var widget=pdf.ObjectDictionary(id);widget.Remove(Meta);widget.Remove(Resources);foreach(string key in new[]{"FT","T","TU","TM","Ff","V","DV","DA","Q","Opt","MaxLen","Kids"})widget.Remove(key);widget["Parent"]=group.Id+" 0 R";changes[id]=PdfSyntax.Dictionary(widget);}
            }
            foreach(var parent in parents){var d=pdf.ParseValueDictionary(changes[parent.Value]);d["Kids"]="["+String.Join(" ",childrenById[parent.Value])+"]";changes[parent.Value]=PdfSyntax.Dictionary(d);}
            dr["Font"]=PdfSyntax.Dictionary(fonts);form["DR"]=PdfSyntax.Dictionary(dr);form["Fields"]="["+String.Join(" ",roots)+"]";catalog["AcroForm"]=PdfSyntax.Dictionary(form);changes[pdf.Root]=PdfSyntax.Dictionary(catalog);return pdf.Append(bytes,changes);
        }
    }
}
