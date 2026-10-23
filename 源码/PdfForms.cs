using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace MiniPdf {
    internal enum FieldKind { Text,Multiline,Password,Check,Radio,Combo,EditableCombo,List,MultiList,Signature,Button,Unknown }
    internal sealed class FormChoice {
        internal string Value,Label;
        public override string ToString(){return Label;}
        internal FormChoice Clone(){return (FormChoice)MemberwiseClone();}
    }
    internal sealed class FormWidget {
        internal int Id,Page,Flags=4;internal N.Rect Bounds;internal string On="Yes";
        internal FormWidget Clone(){return (FormWidget)MemberwiseClone();}
    }
    internal sealed class PdfField {
        internal int Id,Flags,MaxLength,Align;
        internal string Name="",Tooltip="",Default="";
        internal string[] Values=new string[0];internal FieldKind Kind;
        internal float Size;internal bool Changed;
        internal Dictionary<string,string> Original=new Dictionary<string,string>();
        internal List<FormWidget> Widgets=new List<FormWidget>();internal List<FormChoice> Choices=new List<FormChoice>();
        internal bool Supported {get{return Kind<=FieldKind.MultiList;}}
        internal bool ReadOnly {get{return (Flags&1)!=0;}}
        internal bool Required {get{return (Flags&2)!=0;}}
        internal string Value {get{return String.Join("\n",Values);}}
        internal PdfField Clone(){var f=(PdfField)MemberwiseClone();f.Values=(string[])Values.Clone();f.Widgets=Widgets.Select(x=>x.Clone()).ToList();f.Choices=Choices.Select(x=>x.Clone()).ToList();return f;}
        public override string ToString(){return Name+"  ·  "+UiText.Get(KindName(Kind));}
        internal static string KindName(FieldKind type){return new[]{"文本框","多行文本","密码框","勾选框","单选组","下拉框","可编辑下拉框","列表框","多选列表","签名字段","按钮","不支持的字段"}[(int)type];}
    }
    internal sealed class PdfForms {
        internal readonly byte[] Source;
        internal readonly int PageCount;
        internal List<PdfField> Fields=new List<PdfField>();
        internal readonly HashSet<int> OriginalWidgets=new HashSet<int>();
        readonly PdfSyntax syntax;readonly Dictionary<string,string> acro;
        internal static string Get(Dictionary<string,string> d,string key,string fallback=""){string value;return d.TryGetValue(key,out value)?value:fallback;}
        static int Int(string text,int fallback=0){int value;return Int32.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out value)?value:fallback;}
        internal static string Number(double value){return value.ToString("0.####",CultureInfo.InvariantCulture);}
        internal static string Unicode(string text){return "<FEFF"+BitConverter.ToString(Encoding.BigEndianUnicode.GetBytes(text??"")).Replace("-","")+">";}
        internal static string Name(string value){var b=new StringBuilder("/");foreach(byte ch in Encoding.UTF8.GetBytes(value??""))if(ch>=33&&ch<=126&&"#%()/<>[]{}".IndexOf((char)ch)<0)b.Append((char)ch);else b.Append('#').Append(ch.ToString("X2"));return b.ToString();}
        internal static string Decode(string value) {
            value=(value??"").Trim();if(value==""||value=="null")return "";
            if(value[0]=='/')return Regex.Replace(value.Substring(1),"#([0-9A-Fa-f]{2})",m=>((char)Convert.ToByte(m.Groups[1].Value,16)).ToString());
            var bytes=new List<byte>();
            if(value[0]=='<'&&!value.StartsWith("<<")) {string hex=Regex.Replace(value.Substring(1,value.Length-2),@"\s","");if(hex.Length%2!=0)hex+="0";for(int i=0;i<hex.Length;i+=2)bytes.Add(Convert.ToByte(hex.Substring(i,2),16));}
            else if(value[0]=='(')for(int i=1;i<value.Length-1;i++) {
                char ch=value[i];if(ch=='\\'&&++i<value.Length-1){ch=value[i];int n=0,used=0;while(ch>='0'&&ch<='7'&&used<3){n=n*8+ch-'0';used++;if(i+1>=value.Length-1||value[i+1]<'0'||value[i+1]>'7')break;ch=value[++i];}if(used>0){bytes.Add((byte)n);continue;}if(ch=='\r'){if(i+1<value.Length-1&&value[i+1]=='\n')i++;continue;}if(ch=='\n')continue;ch=ch=='n'?'\n':ch=='r'?'\r':ch=='t'?'\t':ch=='b'?'\b':ch=='f'?'\f':ch;}bytes.Add((byte)ch);
            } else return value;
            var data=bytes.ToArray();if(data.Length>=2&&data[0]==254&&data[1]==255)return Encoding.BigEndianUnicode.GetString(data,2,data.Length-2);
            if(data.Length>=3&&data[0]==239&&data[1]==187&&data[2]==191)return Encoding.UTF8.GetString(data,3,data.Length-3);
            const string special="•†‡…—–ƒ⁄‹›−‰„“”‘’‚™ﬁﬂŁŒŠŸŽıłœšž\0";var output=new StringBuilder();foreach(byte ch in data)output.Append(ch>=128&&ch<=159?special[ch-128]:ch==160?'€':(char)ch);return output.ToString();
        }
        static string[] Values(PdfSyntax pdf,string value){if(String.IsNullOrEmpty(value)||value=="null")return new string[0];return value.TrimStart().StartsWith("[")?pdf.ArrayValues(value).Select(Decode).ToArray():new[]{Decode(value)};}
        static List<int> Pages(PdfSyntax pdf) {
            var result=new List<int>();var active=new HashSet<int>();Action<int,int> walk=null;walk=delegate(int id,int depth){PdfDocument.Check(depth<64&&active.Add(id),UiText.Get("表单页面树无效。"));var d=pdf.ObjectDictionary(id);if(Get(d,"Type")=="/Page")result.Add(id);else foreach(string child in pdf.ArrayValues(d["Kids"])){int next;PdfDocument.Check(PdfSyntax.TryReference(child,out next),UiText.Get("表单页面树无效。"));walk(next,depth+1);}active.Remove(id);};int root;PdfDocument.Check(PdfSyntax.TryReference(pdf.ObjectDictionary(pdf.Root)["Pages"],out root),UiText.Get("表单页面树无效。"));walk(root,0);return result;
        }
        internal static PdfForms Read(PdfDocument document){PdfDocument.Check(!document.CompatibilityPreview,UiText.Get("兼容预览不能处理表单。"));return new PdfForms(document.Serialize(false,true));}
        internal PdfForms(byte[] data) {
            Source=data;syntax=new PdfSyntax(data);var catalog=syntax.ObjectDictionary(syntax.Root);string value;acro=catalog.TryGetValue("AcroForm",out value)&&value!="null"?syntax.DictionaryValue(value):new Dictionary<string,string>();
            PdfDocument.Check(!acro.ContainsKey("XFA"),UiText.Get("XFA 动态表单暂不支持；请使用标准 PDF 表单。"));
            var pages=Pages(syntax);PageCount=pages.Count;var locations=new Dictionary<int,int>();
            for(int p=0;p<pages.Count;p++){var page=syntax.ObjectDictionary(pages[p]);if(!page.TryGetValue("Annots",out value))continue;foreach(string item in syntax.ArrayValues(value)){int id;if(PdfSyntax.TryReference(item,out id))locations[id]=p;}}
            var seen=new HashSet<int>();Action<int,string,Dictionary<string,string>,int> visit=null;
            visit=delegate(int id,string prefix,Dictionary<string,string> inherited,int depth) {
                PdfDocument.Check(depth<64&&seen.Add(id)&&seen.Count<=20000,UiText.Get("表单字段树无效或过大。"));var d=syntax.ObjectDictionary(id);var effective=new Dictionary<string,string>(inherited);
                foreach(string key in new[]{"FT","Ff","V","DV","DA","Q","Opt","MaxLen"})if(d.TryGetValue(key,out value))effective[key]=value;
                string partial=Decode(Get(d,"T")),name=partial.Length==0?prefix:prefix.Length==0?partial:prefix+"."+partial;
                var children=new List<int>();if(d.TryGetValue("Kids",out value))foreach(string item in syntax.ArrayValues(value)){int child;PdfDocument.Check(PdfSyntax.TryReference(item,out child),UiText.Get("直接内联字段暂不支持编辑。"));children.Add(child);}
                bool subfields=children.Any(child=>syntax.ObjectDictionary(child).ContainsKey("T")||Get(syntax.ObjectDictionary(child),"Subtype")!="/Widget");
                if(subfields){foreach(int child in children)visit(child,name,effective,depth+1);return;}
                if(!effective.ContainsKey("FT"))return;
                var original=new Dictionary<string,string>(d);foreach(var entry in effective)original[entry.Key]=entry.Value;
                var field=new PdfField{Id=id,Name=name,Tooltip=Decode(Get(d,"TU")),Default=String.Join("\n",Values(syntax,Get(effective,"DV"))),Flags=Int(Get(effective,"Ff")),MaxLength=Int(Get(effective,"MaxLen")),Align=Int(Get(effective,"Q")),Values=Values(syntax,Get(effective,"V")),Original=original};
                string type=Get(effective,"FT");field.Kind=type=="/Tx"?((field.Flags&4096)!=0?FieldKind.Multiline:(field.Flags&8192)!=0?FieldKind.Password:FieldKind.Text):type=="/Btn"?((field.Flags&65536)!=0?FieldKind.Button:(field.Flags&32768)!=0?FieldKind.Radio:FieldKind.Check):type=="/Ch"?((field.Flags&131072)!=0?((field.Flags&262144)!=0?FieldKind.EditableCombo:FieldKind.Combo):(field.Flags&2097152)!=0?FieldKind.MultiList:FieldKind.List):type=="/Sig"?FieldKind.Signature:FieldKind.Unknown;
                var size=Regex.Match(Get(effective,"DA",Get(acro,"DA")),@"([-+0-9.]+)\s+Tf\b");float number;if(size.Success&&Single.TryParse(size.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out number))field.Size=Math.Max(0,Math.Min(200,number));
                if(effective.TryGetValue("Opt",out value))foreach(string item in syntax.ArrayValues(value)) {if(item.TrimStart().StartsWith("[")){var pair=syntax.ArrayValues(item);if(pair.Count>=2)field.Choices.Add(new FormChoice{Value=Decode(pair[0]),Label=Decode(pair[1])});}else field.Choices.Add(new FormChoice{Value=Decode(item),Label=Decode(item)});}
                if(Get(d,"Subtype")=="/Widget")children.Add(id);
                foreach(int widgetId in children) {
                    int p;if(!locations.TryGetValue(widgetId,out p))continue;var widget=syntax.ObjectDictionary(widgetId);var rect=syntax.ArrayValues(Get(widget,"Rect","[0 0 0 0]"));if(rect.Count!=4)continue;var numbers=rect.Select(x=>Single.Parse(x,CultureInfo.InvariantCulture)).ToArray();
                    var w=new FormWidget{Id=widgetId,Page=p,Flags=Int(Get(widget,"F","4")),Bounds=new N.Rect(Math.Min(numbers[0],numbers[2]),Math.Min(numbers[1],numbers[3]),Math.Max(numbers[0],numbers[2]),Math.Max(numbers[1],numbers[3]))};
                    if(widget.TryGetValue("AP",out value)){var ap=syntax.DictionaryValue(value);string normal;if(ap.TryGetValue("N",out normal)&&!syntax.IsStreamReference(normal)){var states=syntax.DictionaryValue(normal);foreach(string state in states.Keys)if(state!="Off"){w.On=state;break;}}}
                    field.Widgets.Add(w);OriginalWidgets.Add(widgetId);
                }
                Fields.Add(field);
            };
            if(acro.TryGetValue("Fields",out value))foreach(string item in syntax.ArrayValues(value)){int id;PdfDocument.Check(PdfSyntax.TryReference(item,out id),UiText.Get("直接内联字段暂不支持编辑。"));visit(id,"",new Dictionary<string,string>(),0);}
        }
        internal List<PdfField> Snapshot(){return Fields.Select(f=>f.Clone()).ToList();}
        internal int MissingRequired {get{return Fields.Count(f=>f.Required&&f.Supported&&(f.Values.Length==0||f.Values.All(v=>String.IsNullOrWhiteSpace(v)||v=="Off")));}}
        internal void Validate() {
            PdfDocument.Check(Fields.Count<=10000,UiText.Get("最多支持 10000 个表单字段。"));var names=new HashSet<string>();
            foreach(var field in Fields) {
                PdfDocument.Check(!String.IsNullOrWhiteSpace(field.Name)&&field.Name.Length<=512&&field.Name.Split('.').All(x=>!String.IsNullOrWhiteSpace(x))&&names.Add(field.Name),UiText.Get("字段名称不能为空或重复，层级名称用点分隔。"));
                PdfDocument.Check(field.Values.All(v=>v!=null&&v.Length<=20000)&&field.Default.Length<=20000,UiText.Get("字段文字过长。"));
                if(field.MaxLength>0&&(field.Kind==FieldKind.Text||field.Kind==FieldKind.Multiline||field.Kind==FieldKind.Password))PdfDocument.Check(field.Values.All(v=>v.Length<=field.MaxLength),UiText.Get("字段文字超过最大长度：")+field.Name);
                if(field.Changed&&!field.Supported)throw new InvalidOperationException(UiText.Get("签名、动作按钮及未知字段在这里仅供查看。"));
                if(field.Kind>=FieldKind.Combo&&field.Kind<=FieldKind.MultiList){PdfDocument.Check(field.Choices.Count<=10000&&field.Choices.Select(c=>c.Value).Distinct().Count()==field.Choices.Count,UiText.Get("选项导出值必须唯一。"));if(field.Kind!=FieldKind.EditableCombo)PdfDocument.Check(field.Values.All(v=>field.Choices.Any(c=>c.Value==v)),UiText.Get("填写值不在字段选项内：")+field.Name);}
                if(field.Kind!=FieldKind.MultiList)PdfDocument.Check(field.Values.Length<=1,UiText.Get("此字段只接受一个填写值。"));
                if(field.Kind==FieldKind.Check||field.Kind==FieldKind.Radio) {
                    PdfDocument.Check(field.Widgets.All(w=>!String.IsNullOrEmpty(w.On)&&w.On!="Off"),UiText.Get("勾选或单选的选项值不能是 Off 或空值。"));
                    PdfDocument.Check(field.Values.All(v=>v=="Off"||field.Widgets.Any(w=>w.On==v)),UiText.Get("填写值不在字段选项内：")+field.Name);
                }
                if(field.Kind==FieldKind.Radio)PdfDocument.Check(field.Widgets.Select(w=>w.On).Distinct().Count()==field.Widgets.Count,UiText.Get("单选组的选项值必须唯一。"));
                foreach(var w in field.Widgets)PdfDocument.Check(w.Page>=0&&w.Page<PageCount&&w.Bounds.right-w.Bounds.left>=4&&w.Bounds.top-w.Bounds.bottom>=4&&w.Bounds.right-w.Bounds.left<=14400&&w.Bounds.top-w.Bounds.bottom<=14400,UiText.Get("字段位置或大小无效。"));
            }
            foreach(string name in names){int dot=name.IndexOf('.');while(dot>=0){PdfDocument.Check(!names.Contains(name.Substring(0,dot)),UiText.Get("字段名称不能同时作为其他字段的父组。"));dot=name.IndexOf('.',dot+1);}}
        }
        static string Stream(Dictionary<string,string> dictionary,string data){dictionary["Length"]=data.Length.ToString(CultureInfo.InvariantCulture);return PdfSyntax.Dictionary(dictionary)+"\nstream\n"+data+"\nendstream";}
        static string Box(N.Rect r){return "["+Number(r.left)+" "+Number(r.bottom)+" "+Number(r.right)+" "+Number(r.top)+"]";}
        internal byte[] Build() {
            Validate();var fields=Snapshot();int originalPages=PageCount;byte[] data=Source;var appearance=new List<Tuple<PdfField,FormWidget>>();
            foreach(var f in fields)if(f.Changed&&f.Supported&&f.Kind!=FieldKind.Check&&f.Kind!=FieldKind.Radio)foreach(var w in f.Widgets)appearance.Add(Tuple.Create(f,w));
            if(appearance.Count>0)using(var rendered=PdfDocument.New()) {
                foreach(var item in appearance)rendered.FormAppearancePage(item.Item1,item.Item2);
                rendered.RemoveInitialAppearancePage();byte[] normalized=rendered.Serialize(true);
                using(var source=PdfDocument.FromBytes(normalized,""))using(var target=PdfDocument.FromBytes(Source,"")) {
                    var indices=Enumerable.Range(0,appearance.Count).ToArray();PdfDocument.Check(N.FPDF_ImportPagesByIndex(target.Handle,source.Handle,indices,(uint)indices.Length,originalPages)!=0,UiText.Get("无法生成表单外观。"));data=target.Serialize(false);
                }
            }
            var pdf=new PdfSyntax(data);var pages=Pages(pdf);PdfDocument.Check(pages.Count==PageCount+appearance.Count,UiText.Get("表单外观页数校验失败。"));var changes=new SortedDictionary<int,string>();int next=pdf.Size;
            Func<int,Dictionary<string,string>> dictionary=id=>changes.ContainsKey(id)?pdf.ParseValueDictionary(changes[id]):pdf.ObjectDictionary(id);
            var apIds=new Dictionary<FormWidget,int>();
            for(int i=0;i<appearance.Count;i++) {
                var page=pdf.ObjectDictionary(pages[originalPages+i]);var streams=Get(page,"Contents");int streamId;
                if(!PdfSyntax.TryReference(streams,out streamId)){var list=pdf.ArrayValues(streams);PdfDocument.Check(list.Count==1&&PdfSyntax.TryReference(list[0],out streamId),UiText.Get("无法生成表单外观。"));}
                var stream=pdf.ObjectDictionary(streamId);stream["Type"]="/XObject";stream["Subtype"]="/Form";var bounds=appearance[i].Item2.Bounds;stream["BBox"]="[0 0 "+Number(bounds.right-bounds.left)+" "+Number(bounds.top-bounds.bottom)+"]";stream["Resources"]=Get(page,"Resources","<<>>");stream.Remove("Metadata");int id=next++;changes[id]=Stream(stream,pdf.StreamData(streamId));apIds[appearance[i].Item2]=id;
            }
            // Remove only generated appearance pages from the page tree; AP references retain fonts.
            var remove=new HashSet<int>(pages.Skip(originalPages));Action<int> prune=null;prune=delegate(int id){var d=pdf.ObjectDictionary(id);if(Get(d,"Type")=="/Page")return;var keep=new List<string>();int count=0;foreach(string child in pdf.ArrayValues(d["Kids"])){int childId;PdfSyntax.TryReference(child,out childId);if(remove.Contains(childId))continue;var childDict=pdf.ObjectDictionary(childId);if(Get(childDict,"Type")=="/Pages"){prune(childId);childDict=dictionary(childId);count+=Int(Get(childDict,"Count"));}else count++;keep.Add(child);}d["Kids"]="["+String.Join(" ",keep)+"]";d["Count"]=count.ToString(CultureInfo.InvariantCulture);changes[id]=PdfSyntax.Dictionary(d);};int pageRoot;PdfSyntax.TryReference(pdf.ObjectDictionary(pdf.Root)["Pages"],out pageRoot);if(remove.Count>0)prune(pageRoot);
            foreach(var f in fields){if(f.Id==0)f.Id=next++;else PdfDocument.Check(pdf.ContainsObject(f.Id),UiText.Get("字段引用已变化，请重新打开表单工具。"));foreach(var w in f.Widgets)if(w.Id==0)w.Id=next++;}
            var groups=new Dictionary<string,int>();var kids=new Dictionary<int,List<int>>();kids[0]=new List<int>();
            foreach(var f in fields) {
                string[] parts=f.Name.Split('.');int parent=0;string prefix="";
                for(int i=0;i<parts.Length-1;i++){prefix+=(i==0?"":".")+parts[i];int id;if(!groups.TryGetValue(prefix,out id)){id=next++;groups[prefix]=id;kids[id]=new List<int>();kids[parent].Add(id);var d=new Dictionary<string,string>{{"T",Unicode(parts[i])}};if(parent>0)d["Parent"]=parent+" 0 R";changes[id]=PdfSyntax.Dictionary(d);}parent=id;}
                kids[parent].Add(f.Id);var field=new Dictionary<string,string>(f.Original);field["T"]=Unicode(parts[parts.Length-1]);if(parent==0)field.Remove("Parent");else field["Parent"]=parent+" 0 R";
                if(f.Supported) {
                    field["FT"]=f.Kind<=FieldKind.Password?"/Tx":f.Kind<=FieldKind.Radio?"/Btn":"/Ch";
                    int flags=f.Flags&~(4096|8192|32768|65536|131072|262144|2097152);if(f.Kind==FieldKind.Multiline)flags|=4096;if(f.Kind==FieldKind.Password)flags|=8192;if(f.Kind==FieldKind.Radio)flags|=32768;if(f.Kind==FieldKind.Combo||f.Kind==FieldKind.EditableCombo)flags|=131072;if(f.Kind==FieldKind.EditableCombo)flags|=262144;if(f.Kind==FieldKind.MultiList)flags|=2097152;field["Ff"]=flags.ToString(CultureInfo.InvariantCulture);field["TU"]=Unicode(f.Tooltip);field["Q"]=Math.Max(0,Math.Min(2,f.Align)).ToString(CultureInfo.InvariantCulture);
                    if(f.MaxLength>0)field["MaxLen"]=f.MaxLength.ToString(CultureInfo.InvariantCulture);else field.Remove("MaxLen");
                    if(f.Kind==FieldKind.Check||f.Kind==FieldKind.Radio){field["V"]=Name(f.Values.Length>0?f.Values[0]:"Off");field["DV"]=Name(String.IsNullOrEmpty(f.Default)?"Off":f.Default);}
                    else {field["V"]=f.Kind==FieldKind.MultiList?"["+String.Join(" ",f.Values.Select(Unicode))+"]":Unicode(f.Value);field["DV"]=f.Kind==FieldKind.MultiList?"["+String.Join(" ",f.Default.Split((char)10).Where(v=>v.Length>0).Select(Unicode))+"]":Unicode(f.Default);}
                    if(f.Kind>=FieldKind.Combo)field["Opt"]="["+String.Join(" ",f.Choices.Select(c=>"["+Unicode(c.Value)+" "+Unicode(c.Label)+"]"))+"]";
                    if(f.Changed){field.Remove("RV");field.Remove("I");field.Remove("TI");field["MiniPDFSize"]=Number(f.Size);field["DA"]="(/MiniHelv "+Number(f.Size)+" Tf 0 g)";}
                }
                bool combined=f.Widgets.Count==1&&f.Widgets[0].Id==f.Id;
                if(!combined)field["Kids"]="["+String.Join(" ",f.Widgets.Select(w=>pdf.ContainsObject(w.Id)?pdf.ObjectReference(w.Id):w.Id+" 0 R"))+"]";
                changes[f.Id]=PdfSyntax.Dictionary(field);
                foreach(var w in f.Widgets) {
                    var widget=w.Id==f.Id?dictionary(w.Id):pdf.ContainsObject(w.Id)?pdf.ObjectDictionary(w.Id):new Dictionary<string,string>();widget["Type"]="/Annot";widget["Subtype"]="/Widget";widget["Rect"]=Box(w.Bounds);widget["P"]=pdf.ObjectReference(pages[w.Page]);if(w.Id!=f.Id)widget["Parent"]=pdf.ContainsObject(f.Id)?pdf.ObjectReference(f.Id):f.Id+" 0 R";if(!widget.ContainsKey("F"))widget["F"]="4";
                    if(f.Changed&&f.Supported) {
                        float width=w.Bounds.right-w.Bounds.left,height=w.Bounds.top-w.Bounds.bottom;
                        if(f.Kind==FieldKind.Check||f.Kind==FieldKind.Radio) {
                            string off="1 1 1 rg 0 0 "+Number(width)+" "+Number(height)+" re f 0.45 G 1 w ";
                            string on;
                            if(f.Kind==FieldKind.Radio) {
                                float cx=width/2,cy=height/2,r=Math.Max(1,Math.Min(width,height)/2-1);
                                Func<float,string> circle=radius=>Number(cx+radius)+" "+Number(cy)+" m "+Number(cx+radius)+" "+Number(cy+radius*.552285f)+" "+Number(cx+radius*.552285f)+" "+Number(cy+radius)+" "+Number(cx)+" "+Number(cy+radius)+" c "+Number(cx-radius*.552285f)+" "+Number(cy+radius)+" "+Number(cx-radius)+" "+Number(cy+radius*.552285f)+" "+Number(cx-radius)+" "+Number(cy)+" c "+Number(cx-radius)+" "+Number(cy-radius*.552285f)+" "+Number(cx-radius*.552285f)+" "+Number(cy-radius)+" "+Number(cx)+" "+Number(cy-radius)+" c "+Number(cx+radius*.552285f)+" "+Number(cy-radius)+" "+Number(cx+radius)+" "+Number(cy-radius*.552285f)+" "+Number(cx+radius)+" "+Number(cy)+" c h ";
                                off+=circle(r)+"S\n";on=off+"0 g "+circle(r*.45f)+"f\n";
                            } else {off+=".5 .5 "+Number(width-1)+" "+Number(height-1)+" re S\n";on=off+"0 0 0 RG 2 w 3 "+Number(height*.5f)+" m "+Number(width*.4f)+" 3 l "+Number(width-3)+" "+Number(height-3)+" l S\n";}
                            int a=next++,b=next++;var box=new Dictionary<string,string>{{"Type","/XObject"},{"Subtype","/Form"},{"BBox","[0 0 "+Number(width)+" "+Number(height)+"]"},{"Resources","<<>>"}};changes[a]=Stream(new Dictionary<string,string>(box),off);changes[b]=Stream(new Dictionary<string,string>(box),on);widget["AP"]="<< /N << /Off "+a+" 0 R "+Name(w.On)+" "+b+" 0 R >> >>";widget["AS"]=Name(f.Values.Contains(w.On)?w.On:"Off");
                        } else widget["AP"]="<< /N "+apIds[w]+" 0 R >>";
                    }
                    changes[w.Id]=PdfSyntax.Dictionary(widget);
                }
            }
            foreach(var group in groups){var d=dictionary(group.Value);d["Kids"]="["+String.Join(" ",kids[group.Value].Select(id=>pdf.ContainsObject(id)?pdf.ObjectReference(id):id+" 0 R"))+"]";changes[group.Value]=PdfSyntax.Dictionary(d);}
            for(int p=0;p<originalPages;p++) {
                var d=dictionary(pages[p]);string annots;var values=d.TryGetValue("Annots",out annots)?pdf.ArrayValues(annots):new List<string>();values.RemoveAll(v=>{int id;return PdfSyntax.TryReference(v,out id)&&OriginalWidgets.Contains(id);});foreach(var f in fields)foreach(var w in f.Widgets)if(w.Page==p)values.Add(pdf.ContainsObject(w.Id)?pdf.ObjectReference(w.Id):w.Id+" 0 R");d["Annots"]="["+String.Join(" ",values)+"]";changes[pages[p]]=PdfSyntax.Dictionary(d);
            }
            var form=new Dictionary<string,string>(acro);form["Fields"]="["+String.Join(" ",kids[0].Select(id=>pdf.ContainsObject(id)?pdf.ObjectReference(id):id+" 0 R"))+"]";
            string order;if(form.TryGetValue("CO",out order)){var ids=new HashSet<int>(fields.Select(f=>f.Id));form["CO"]="["+String.Join(" ",pdf.ArrayValues(order).Where(v=>{int id;return PdfSyntax.TryReference(v,out id)&&ids.Contains(id);}))+"]";}
            int defaultFont=next++;changes[defaultFont]="<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>";var resources=form.ContainsKey("DR")?pdf.DictionaryValue(form["DR"]):new Dictionary<string,string>();var resourceFonts=resources.ContainsKey("Font")?pdf.DictionaryValue(resources["Font"]):new Dictionary<string,string>();resourceFonts["MiniHelv"]=defaultFont+" 0 R";resources["Font"]=PdfSyntax.Dictionary(resourceFonts);form["DR"]=PdfSyntax.Dictionary(resources);if(!form.ContainsKey("DA"))form["DA"]="(/MiniHelv 0 Tf 0 g)";
            if(!form.ContainsKey("NeedAppearances"))form["NeedAppearances"]="false";var catalog=pdf.ObjectDictionary(pdf.Root);catalog["AcroForm"]=PdfSyntax.Dictionary(form);changes[pdf.Root]=PdfSyntax.Dictionary(catalog);
            byte[] output=pdf.Append(data,changes);using(var verify=PdfDocument.FromBytes(output,""))PdfDocument.Check(verify.Count==PageCount,UiText.Get("表单保存校验失败。"));return output;
        }
        internal string ExportData() {
            var b=new StringBuilder();using(var writer=XmlWriter.Create(b,new XmlWriterSettings{Indent=true,OmitXmlDeclaration=true})) {writer.WriteStartElement("xfdf","http://ns.adobe.com/xfdf/");writer.WriteStartElement("fields");foreach(var f in Fields)if(f.Supported&&(f.Flags&4)==0){writer.WriteStartElement("field");writer.WriteAttributeString("name",f.Name);foreach(string value in f.Values)writer.WriteElementString("value",value);writer.WriteEndElement();}writer.WriteEndElement();writer.WriteEndElement();}return b.ToString();
        }
        internal int ImportData(string xml) {
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=10*1024*1024};var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(new StringReader(xml),settings))doc.Load(reader);PdfDocument.Check(doc.DocumentElement!=null&&doc.DocumentElement.LocalName=="xfdf",UiText.Get("请选择 XFDF 表单数据文件。"));
            var updates=new Dictionary<PdfField,string[]>();Action<XmlNode,string> walk=null;walk=delegate(XmlNode node,string prefix){foreach(XmlNode child in node.ChildNodes)if(child.LocalName=="field"){string name=child.Attributes["name"]==null?"":child.Attributes["name"].Value;name=prefix.Length==0?name:prefix+"."+name;var values=new List<string>();foreach(XmlNode value in child.ChildNodes)if(value.LocalName=="value")values.Add(value.InnerText);var field=Fields.FirstOrDefault(f=>f.Name==name);if(field!=null&&field.Supported&&!field.ReadOnly)updates[field]=values.ToArray();walk(child,name);}else if(child.LocalName=="fields")walk(child,prefix);};walk(doc.DocumentElement,"");
            var prior=Snapshot();try{foreach(var update in updates){update.Key.Values=update.Value;update.Key.Changed=true;}Validate();return updates.Count;}catch{Fields=prior;throw;}
        }
    }
    internal sealed partial class PdfDocument {
        internal void ApplyForms(PdfForms model) {
            Check(CanEdit&&SignatureCount==0,UiText.Get("此文档不能编辑。"));Check(N.FPDF_GetSecurityHandlerRevision(Handle)<0,UiText.Get("编辑表单结构前，请明确导出未加密副本。"));int page=Index;byte[] data=model.Build();
            Edit(delegate{CloseHandles();Load(data);OpenPage(page);});
        }
        internal void FormAppearancePage(PdfField field,FormWidget widget) {
            float width=widget.Bounds.right-widget.Bounds.left,height=widget.Bounds.top-widget.Bounds.bottom;int at=Count;IntPtr page=N.FPDFPage_New(Handle,at,width,height);Check(page!=IntPtr.Zero,UiText.Get("无法生成表单外观。"));N.FPDF_ClosePage(page);OpenPage(at);
            IntPtr box=N.FPDFPageObj_CreateNewRect(.5f,.5f,width-1,height-1);N.FPDFPageObj_SetFillColor(box,255,255,255,255);N.FPDFPageObj_SetStrokeColor(box,120,125,132,255);N.FPDFPageObj_SetStrokeWidth(box,1);N.FPDFPath_SetDrawMode(box,2,1);N.FPDFPage_InsertObject(Page,box);
            string value=field.Kind==FieldKind.Password?new string('*',field.Value.Length):field.Value;
            if(field.Kind>=FieldKind.Combo) {
                if(field.Kind==FieldKind.List||field.Kind==FieldKind.MultiList)value=String.Join("\n",field.Choices.Select(c=>(field.Values.Contains(c.Value)?"• ":"  ")+c.Label));
                else {var option=field.Choices.FirstOrDefault(c=>c.Value==field.Value);if(option!=null)value=option.Label;}
            }
            if(value.Length>0) {
                float size=field.Size>0?field.Size:Math.Min(12,Math.Max(4,height-6));IntPtr font=FontFor(value,"自动");bool multiline=field.Kind==FieldKind.Multiline||field.Kind==FieldKind.List||field.Kind==FieldKind.MultiList;
                if(!multiline&&field.Size<=0){float total=0;foreach(char ch in value)total+=GlyphWidth(font,ch,size);if(total>width-8)size=Math.Max(2,size*(width-8)/total);}
                var lines=multiline?TextLines(value,font,size,0,true,Math.Max(1,width-8)):new List<string>{value.Replace("\r","").Replace("\n"," ")};int line=0;
                foreach(string text in lines) {
                    float y=height-4-size-line*size*1.25f;if(y<0)break;float measure=0;foreach(char ch in text)measure+=GlyphWidth(font,ch,size);float x=field.Align==1?(width-measure)/2:field.Align==2?width-measure-4:4;
                    if(text.Length>0)InsertText(text,size,"自动",Color.Black,new N.Matrix{a=1,d=1,e=x,f=y},N.FPDFPage_CountObjects(Page));line++;
                }
            }
            Check(N.FPDFPage_GenerateContent(Page)!=0,UiText.Get("无法生成表单外观。"));CloseText();Changed();
        }
        internal void RemoveInitialAppearancePage(){ClosePage();N.FPDFPage_Delete(Handle,0);OpenPage(0);Changed();}
    }
}
