using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using MiniAI.Desktop;

namespace MiniPdf {
    // Convert only the bounded presentation MathML produced by FormulaMarkup.
    // Any unsupported syntax leaves the original high resolution crop intact.
    internal static class WordFormula {
        static readonly XNamespace M="http://schemas.openxmlformats.org/officeDocument/2006/math",W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        static XElement N(string name,params object[] values){return new XElement(M+name,values);}
        static XElement Part(string name,object value){return N(name,value);}
        static object[] Children(XElement node,string variant){return node.Elements().Where(e=>e.Name.LocalName!="annotation").Select(e=>ConvertNode(e,variant)).ToArray();}
        static XElement Run(string text,string variant){return N("r",N("rPr",N("sty",new XAttribute(M+"val",variant=="bold"?"b":variant=="normal"?"p":"i")),variant=="script"||variant=="double-struck"?N("scr",new XAttribute(M+"val",variant=="script"?"script":"double-struck")):null),new XElement(W+"rPr",new XElement(W+"rFonts",new XAttribute(W+"ascii","Cambria Math"),new XAttribute(W+"hAnsi","Cambria Math"))),N("t",new XAttribute(XNamespace.Xml+"space","preserve"),text));}
        static object ConvertNode(XElement node,string variant){
            var child=node.Elements().ToArray();string kind=node.Name.LocalName;
            switch(kind){
                case "math":case "semantics":case "mrow":return Children(node,variant);
                case "mstyle":return Children(node,(string)node.Attribute("mathvariant")??variant);
                case "mi":case "mn":case "mo":case "mtext":return Run(node.Value,(string)node.Attribute("mathvariant")??(kind=="mtext"||kind=="mn"||kind=="mo"?"normal":variant));
                case "mspace":return (string)node.Attribute("width")=="-0.167em"?null:Run(" ","normal");
                case "mfrac":if(child.Length!=2)break;return N("f",(string)node.Attribute("linethickness")=="0"?N("fPr",N("type",new XAttribute(M+"val","noBar"))):null,Part("num",ConvertNode(child[0],variant)),Part("den",ConvertNode(child[1],variant)));
                case "msup":case "msub":if(child.Length!=2)break;return N(kind=="msup"?"sSup":"sSub",Part("e",ConvertNode(child[0],variant)),Part(kind=="msup"?"sup":"sub",ConvertNode(child[1],variant)));
                case "msubsup":if(child.Length!=3)break;return N("sSubSup",Part("e",ConvertNode(child[0],variant)),Part("sub",ConvertNode(child[1],variant)),Part("sup",ConvertNode(child[2],variant)));
                case "msqrt":return N("rad",N("radPr",N("degHide",new XAttribute(M+"val","1"))),N("deg"),Part("e",Children(node,variant)));
                case "mroot":if(child.Length!=2)break;return N("rad",Part("deg",ConvertNode(child[1],variant)),Part("e",ConvertNode(child[0],variant)));
                case "mover":case "munder":
                    if(child.Length!=2)break;
                    if((string)node.Attribute(kind=="mover"?"accent":"accentunder")=="true"){
                        if(kind=="munder"||child[1].Value=="¯")return N("bar",N("barPr",N("pos",new XAttribute(M+"val",kind=="munder"?"bot":"top"))),Part("e",ConvertNode(child[0],variant)));
                        return N("acc",N("accPr",N("chr",new XAttribute(M+"val",child[1].Value))),Part("e",ConvertNode(child[0],variant)));
                    }
                    return N(kind=="mover"?"limUpp":"limLow",Part("e",ConvertNode(child[0],variant)),Part("lim",ConvertNode(child[1],variant)));
                case "mtable":return N("m",child.Select(row=>N("mr",row.Elements().Select(cell=>Part("e",Children(cell,variant))))));
            }
            throw new NotSupportedException("Unsupported MathML element: "+kind);
        }
        internal static bool TryOmml(string tex,out string xml){
            xml=null;try{var tree=XElement.Parse(FormulaMarkup.Convert(tex));xml=N("oMath",Children(tree,"italic")).ToString(SaveOptions.DisableFormatting);return true;}
            catch(FormatException){return false;}catch(NotSupportedException){return false;}
        }
        static SizeF Measure(XElement node){
            var kind=node.Name.LocalName;if(kind=="r")return new SizeF(Math.Max(.3f,node.Elements(M+"t").Sum(t=>t.Value.Length)*.6f),1);
            var parts=node.Elements().Where(e=>!e.Name.LocalName.EndsWith("Pr",StringComparison.Ordinal)).Select(Measure).ToArray();if(parts.Length==0)return SizeF.Empty;
            if(kind=="f"&&parts.Length==2)return new SizeF(Math.Max(parts[0].Width,parts[1].Width)+.3f,parts[0].Height+parts[1].Height+.35f);
            if((kind=="sSup"||kind=="sSub"||kind=="sSubSup")&&parts.Length>=2)return new SizeF(parts[0].Width+parts.Skip(1).Max(s=>s.Width)*.65f,Math.Max(parts[0].Height,1)+.35f*(parts.Length-1));
            if(kind=="m")return new SizeF(parts.Max(s=>s.Width),parts.Sum(s=>s.Height)+.2f*parts.Length);
            return new SizeF(parts.Sum(s=>s.Width),parts.Max(s=>s.Height));
        }
        internal static string FitMath(string xml,float width,float height,out float em){var root=XElement.Parse(xml);var size=Measure(root);em=Math.Max(5,Math.Min(96,Math.Min(width/Math.Max(1,size.Width),height/Math.Max(1,size.Height))));foreach(var run in root.Descendants(M+"r")){var properties=run.Element(W+"rPr");if(properties==null){properties=new XElement(W+"rPr");run.AddFirst(properties);}properties.Add(new XElement(W+"sz",new XAttribute(W+"val",Math.Round(em*2))),new XElement(W+"szCs",new XAttribute(W+"val",Math.Round(em*2))));}return root.ToString(SaveOptions.DisableFormatting);}
        static string TexNode(XElement node){
            string kind=node.Name.LocalName;Func<string,string> part=delegate(string name){var item=node.Element(M+name);if(item==null)throw new FormatException("Missing native equation component.");return TexNode(item);};
            if(kind=="r")return TexSymbols(String.Concat(node.Elements(M+"t").Select(t=>t.Value)));
            if(kind=="f")return @"\frac{"+part("num")+"}{"+part("den")+"}";
            if(kind=="sSub"||kind=="sSup"||kind=="sSubSup")return "{"+part("e")+"}"+(kind!="sSup"?"_{"+part("sub")+"}":"")+(kind!="sSub"?"^{"+part("sup")+"}":"");
            if(kind=="oMath"||kind=="e"||kind=="num"||kind=="den"||kind=="sub"||kind=="sup")return String.Concat(node.Elements().Select(TexNode));
            if(kind.EndsWith("Pr",StringComparison.Ordinal))return "";
            throw new NotSupportedException("Unsupported native equation: "+kind);
        }
        static string TexSymbols(string text){var output=new StringBuilder();const string greek="αβγδεζηθικλμνξοπρστυφχψωΓΔΘΛΞΠΣΥΦΨΩ";string[] names="alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi rho sigma tau upsilon phi chi psi omega Gamma Delta Theta Lambda Xi Pi Sigma Upsilon Phi Psi Omega".Split(' ');foreach(char c in text){int at=greek.IndexOf(c);if(at>=0)output.Append('\\').Append(names[at]).Append(' ');else if(c=='≤')output.Append(@"\leq ");else if(c=='≥')output.Append(@"\geq ");else if(c=='≠')output.Append(@"\neq ");else if(c=='∑')output.Append(@"\sum ");else if(c=='∫')output.Append(@"\int ");else if(c=='∞')output.Append(@"\infty ");else if(c=='×')output.Append(@"\times ");else if(c=='−')output.Append('-');else if("\\{}%&#_^".IndexOf(c)>=0)output.Append('\\').Append(c);else output.Append(c);}return output.ToString();}
        internal static void NativeAsTex(WordPage page){foreach(var line in page.Text)if(line.Math!=null){string tex=!String.IsNullOrEmpty(line.Text)?line.Text:TexNode(XElement.Parse("<root xmlns:m=\""+M+"\" xmlns:w=\""+W+"\">"+line.Math+"</root>").Elements().Single());AsCode(line,tex);}PlaceCode(page);}
        internal static void AsCode(WordText line,string tex){line.Math=null;line.Text=FormulaMarkup.Clean(tex);line.Font="Consolas";line.Size=Math.Max(9,Math.Min(12,line.Size));line.FormulaCode=true;}
        internal static void PlaceCode(WordPage page){foreach(var line in page.Text.Where(t=>t.FormulaCode)){line.Block=!page.Text.Any(t=>t!=line&&!t.FormulaCode&&t.Math==null&&Math.Abs(t.Y-line.Y)<Math.Max(2,line.Size*.3f));string marks=line.Block?"$$":"$";line.Text=" "+marks+line.Text+marks+" ";}}
        internal static void ConvertPictures(WordPage page,string root,Action<string> status,bool texOnly=false){
            int attempted=0;foreach(var picture in page.Pictures.ToArray()){
                if(picture.Figure||picture.Data==null||++attempted>96)continue;
                if(status!=null)status(UiText.Get("正在转换公式…")+" "+attempted);
                try{
                    var reply=MiniAiClient.Request(root,"/v1/ocr",new{image_base64=Convert.ToBase64String(picture.Data),formula_only=true},300);
                    object found;var lines=reply.TryGetValue("lines",out found)?found as System.Collections.IList:null;if(lines==null||lines.Count!=1)continue;
                    var line=lines[0] as System.Collections.Generic.Dictionary<string,object>;object score,value;
                    if(line==null||!line.TryGetValue("formula_confidence",out score)||Convert.ToSingle(score)<.55f||!line.TryGetValue("tex",out value))continue;
                    string tex=Convert.ToString(value),xml=null;if(String.IsNullOrWhiteSpace(tex)||tex.Length>20000||!texOnly&&!TryOmml(tex,out xml))continue;
                    float em=Math.Max(6,Math.Min(24,picture.Height*.65f));if(!texOnly)xml=FitMath(xml,picture.Width,picture.Height,out em);
                    var text=new WordText{Math=xml,Text=tex,X=picture.X,Top=picture.Y,Y=picture.Baseline>0?picture.Baseline:picture.Y+em*.807f,Width=picture.Width,Height=picture.Height,Size=em,Color=Color.Black,Objects=picture.Objects};if(texOnly)AsCode(text,tex);page.Text.Add(text);
                    page.Equations++;page.Pictures.Remove(picture);
                }catch(IOException){}catch(System.Net.WebException){}catch(InvalidOperationException){}
            }
        }
    }
}
