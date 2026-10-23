using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniPdf {
    internal sealed class WordText {
        internal string Text,Font,Math;
        internal IntPtr[] Objects;
        internal float X,Y,Top,Width,Size,Height;
        internal Color Color;
        internal bool Bold,Italic;
    }
    internal sealed class WordPage {
        internal SizeF Size;
        internal bool Columns;
        internal byte[] Background;
        internal readonly List<WordText> Text=new List<WordText>();
        internal int Equations;
        internal readonly List<WordPicture> Pictures=new List<WordPicture>();
    }
    internal static class WordLayout {
        internal static string Family(string name) {
            name=Regex.Replace(name??"",@"^[A-Z]{6}\+","");
            if(Regex.IsMatch(name,@"^(CM(?:R|TI|SL|BX(?:TI)?|B)[0-9]|(?:LM|LatinModern)Roman|NimbusRoman)",RegexOptions.IgnoreCase))return "Times New Roman";
            if(Regex.IsMatch(name,@"^(CMSS[0-9]|(?:LM|LatinModern)Sans|NimbusSans)",RegexOptions.IgnoreCase))return "Arial";
            if(Regex.IsMatch(name,@"^(CMTT[0-9]|(?:LM|LatinModern)Mono|NimbusMono)",RegexOptions.IgnoreCase))return "Courier New";
            if(Regex.IsMatch(name,@"^(Times[-,]|TimesNewRoman)",RegexOptions.IgnoreCase))return "Times New Roman";
            if(Regex.IsMatch(name,@"^(Helvetica|Arial)",RegexOptions.IgnoreCase))return "Arial";
            if(Regex.IsMatch(name,@"^(Courier|CourierNew)",RegexOptions.IgnoreCase))return "Courier New";
            if(Regex.IsMatch(name,@"^(SimSun|STSong)",RegexOptions.IgnoreCase))return "SimSun";
            if(Regex.IsMatch(name,@"^(MicrosoftYaHei|Microsoft YaHei)",RegexOptions.IgnoreCase))return "Microsoft YaHei";
            if(Regex.IsMatch(name,@"^(Calibri|Cambria)([-,]|$)",RegexOptions.IgnoreCase))return name.StartsWith("Calibri",StringComparison.OrdinalIgnoreCase)?"Calibri":"Cambria";
            return null;
        }
        internal static bool Safe(IntPtr obj) {
            N.Matrix m;uint r,g,b,a;IntPtr clip=N.FPDFPageObj_GetClipPath(obj);
            return N.FPDFPageObj_GetType(obj)==1&&N.FPDFTextObj_GetTextRenderMode(obj)==0&&
                N.FPDFPageObj_CountMarks(obj)==0&&(clip==IntPtr.Zero||N.FPDFClipPath_CountPaths(clip)<0)&&
                N.FPDFPageObj_GetMatrix(obj,out m)!=0&&Math.Abs(m.b)<.001&&Math.Abs(m.c)<.001&&m.a>0&&m.a<100000&&m.d>0&&m.d<100000&&
                N.FPDFPageObj_GetFillColor(obj,out r,out g,out b,out a)!=0&&a==255;
        }
        internal static RectangleF Bounds(IntPtr obj,SizeF size,PdfDocument document) {
            float l,b,r,t;
            if(N.FPDFPageObj_GetBounds(obj,out l,out b,out r,out t)==0)return RectangleF.Empty;
            var a=OfficeGeometry.Point(document,l,t,size);var z=OfficeGeometry.Point(document,r,b,size);
            return RectangleF.FromLTRB(Math.Min(a.X,z.X),Math.Min(a.Y,z.Y),Math.Max(a.X,z.X),Math.Max(a.Y,z.Y));
        }
        internal static WordPage Read(PdfDocument document,int index,bool equations,OfficeMathSettings settings=null,bool flowing=false) {
            var result=new WordPage{Size=document.PageSize(index)};var glyphs=OfficeGeometry.Read(document,index);
            result.Columns=WordRegions.Columns(glyphs,result.Size);
            var grouped=glyphs.GroupBy(g=>g.Object).ToDictionary(g=>g.Key,g=>g.ToList());
            int count=N.FPDFPage_CountObjects(document.Page);
            PdfDocument.Check(count<=50000,"This page contains too many objects to convert.");
            var objects=new Dictionary<IntPtr,int>();for(int i=0;i<count;i++)objects[N.FPDFPage_GetObject(document.Page,i)]=i;
            var removed=new HashSet<IntPtr>();
            // Rotated pages and oversized Word pages retain their complete appearance.
            bool positioned=N.FPDFPage_GetRotation(document.Page)==0&&result.Size.Width<=1584&&result.Size.Height<=1584;
            if(positioned&&flowing)WordRegions.Figures(document,result,glyphs,objects,removed);
            if(positioned)OfficeMath.Read(document,result,glyphs,objects,removed,equations,settings??new OfficeMathSettings());
            if(positioned&&flowing)WordRegions.Equations(document,result,glyphs,objects,removed);
            var covers=new List<RectangleF>();
            for(int i=count-1;positioned&&i>=0;i--) {
                IntPtr obj=N.FPDFPage_GetObject(document.Page,i);List<OfficeGlyph> chars;
                var box=Bounds(obj,result.Size,document);
                if(removed.Contains(obj))continue;
                if(N.FPDFPageObj_GetType(obj)!=1){if(!box.IsEmpty)covers.Add(box);continue;}
                if(!grouped.TryGetValue(obj,out chars)||chars.Count==0||!Safe(obj))continue;
                var first=chars[0];string family=Family(first.Font);
                bool mathFont=flowing&&OfficeMath.MathFont(first.Font)&&first.Font.IndexOf("CMEX",StringComparison.OrdinalIgnoreCase)<0;
                if(family==null&&mathFont&&chars.All(g=>!g.Bad&&OfficeMath.Token(g.Text)))family="Cambria Math";
                if(family==null||chars.Any(g=>g.Bad||g.Unsupported||Math.Abs(g.Y-first.Y)>.15f||Math.Abs(g.Size-first.Size)>.05f||
                    g.Text.Any(c=>c>=0x590&&c<=0x8ff))||covers.Any(c=>c.IntersectsWith(box)))continue;
                var visible=chars.Where(g=>!String.IsNullOrWhiteSpace(g.Text)).ToArray();
                if(visible.Skip(1).Where((g,j)=>g.X<visible[j].X-.1f).Any())continue;
                var pieces=new List<List<OfficeGlyph>>();var piece=new List<OfficeGlyph>();OfficeGlyph previous=null;
                foreach(var glyph in chars) {
                    if(previous!=null&&glyph.X-previous.Right>first.Size*.8f){pieces.Add(piece);piece=new List<OfficeGlyph>();}
                    piece.Add(glyph);previous=glyph;
                }
                if(piece.Count>0)pieces.Add(piece);
                var lines=new List<WordText>();bool valid=true;
                foreach(var part in pieces) {
                    string text=String.Concat(part.Select(g=>g.Text));
                    if(String.IsNullOrWhiteSpace(text))continue;
                    float width=part.Max(g=>g.Right)-part[0].X;
                    if(width<.1f||part[0].X<0||first.Y<0||part[0].X+width>result.Size.Width+.5f||first.Y>result.Size.Height){valid=false;break;}
                    lines.Add(new WordText{Text=text,Font=family,Size=first.Size,X=part[0].X,Y=first.Y,Width=width,Color=first.Color,
                        Bold=Regex.IsMatch(first.Font,"Bold|CM(?:BX|B[0-9])",RegexOptions.IgnoreCase),Italic=Regex.IsMatch(first.Font,"Italic|Oblique|CMMI|CM(?:TI|SL|BXTI)",RegexOptions.IgnoreCase)});
                }
                if(!valid||lines.Count==0)continue;
                result.Text.AddRange(lines);
                removed.Add(obj);
            }
            result.Text.Sort((a,b)=>{int y=a.Y.CompareTo(b.Y);return y!=0?y:a.X.CompareTo(b.X);});
            document.RemoveWordText(removed);
            // One page at a time, up to 300 dpi, with a bounded pixel budget.
            double scale=Math.Min(300.0/72,Math.Sqrt(16000000.0/(result.Size.Width*result.Size.Height)));
            scale=Math.Min(scale,14000.0/Math.Max(result.Size.Width,result.Size.Height));
            using(var bitmap=document.Render(index,Math.Max(1,(int)Math.Floor(result.Size.Width*scale)),Math.Max(1,(int)Math.Floor(result.Size.Height*scale))))
            using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);result.Background=stream.ToArray();}
            return result;
        }
        internal static string Boxes(WordPage page,ref int id) {
            var xml=new StringBuilder();
            foreach(var line in page.Text) {
                PdfDocument.Check(line.Math==null,"Editable equations require continuous text mode.");
                int number=++id;float em=(float)Math.Round(line.Size*2)/2;
                float top=line.Y-em*.807f;
                xml.Append("<w:r><w:pict><v:shape id=\"text").Append(number).Append("\" type=\"#_x0000_t202\" filled=\"f\" stroked=\"f\" style=\"position:absolute;margin-left:")
                    .Append(OfficeExport.F(line.X)).Append("pt;margin-top:").Append(OfficeExport.F(top)).Append("pt;width:")
                    .Append(OfficeExport.F(Math.Max(line.Width+2,em))).Append("pt;height:").Append(OfficeExport.F(Math.Max(line.Height,em*2.5))).Append("pt;z-index:10;mso-position-horizontal-relative:page;mso-position-vertical-relative:page\"><v:textbox inset=\"0,0,0,0\" style=\"mso-fit-shape-to-text:t\"><w:txbxContent><w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\" w:line=\"")
                    .Append(Math.Round(em*20)).Append("\" w:lineRule=\"exact\"/><w:jc w:val=\"left\"/><w:contextualSpacing/><w:snapToGrid w:val=\"0\"/></w:pPr>");
                {
                    xml.Append("<w:r><w:rPr><w:rFonts w:ascii=\"").Append(OfficeExport.Xml(line.Font)).Append("\" w:hAnsi=\"").Append(OfficeExport.Xml(line.Font)).Append("\" w:eastAsia=\"").Append(OfficeExport.Xml(line.Font)).Append("\"/><w:sz w:val=\"").Append(Math.Round(em*2)).Append("\"/><w:szCs w:val=\"").Append(Math.Round(em*2)).Append("\"/><w:color w:val=\"").Append(line.Color.R.ToString("X2")).Append(line.Color.G.ToString("X2")).Append(line.Color.B.ToString("X2")).Append("\"/><w:fitText w:val=\"").Append(Math.Max(1,Math.Round(line.Width*20))).Append("\" w:id=\"").Append(number).Append("\"/>");
                    if(line.Bold)xml.Append("<w:b/>");if(line.Italic)xml.Append("<w:i/>");
                    xml.Append("</w:rPr><w:t xml:space=\"preserve\">").Append(OfficeExport.Xml(line.Text)).Append("</w:t></w:r>");
                }
                xml.Append("</w:p></w:txbxContent></v:textbox><w10:wrap type=\"none\"/></v:shape></w:pict></w:r>");
            }
            return xml.ToString();
        }
        internal const string Shape="<w:r><w:pict><v:shapetype id=\"_x0000_t202\" coordsize=\"21600,21600\" o:spt=\"202\" path=\"m,l,21600r21600,l21600,xe\"><v:stroke joinstyle=\"miter\"/><v:path gradientshapeok=\"t\" o:connecttype=\"rect\"/></v:shapetype></w:pict></w:r>";
    }
    internal sealed partial class PdfDocument {
        // Called only on the export's private snapshot, never on the open document.
        internal void RemoveWordText(IEnumerable<IntPtr> objects) {
            CloseText();
            foreach(IntPtr obj in objects){Check(N.FPDFPage_RemoveObject(Page,obj)!=0,"Cannot prepare the Word page.");N.FPDFPageObj_Destroy(obj);}
        }
    }
}

namespace MiniPdf {
    internal static partial class OfficeExport {
        static string PageSection(SizeF size,bool last,bool continuous=false) {
            return "<w:sectPr>"+(!last?"<w:type w:val=\"nextPage\"/>":"")+"<w:pgSz w:w=\""+F(Math.Round(Math.Max(10,size.Width)*20))+"\" w:h=\""+F(Math.Round(Math.Max(10,size.Height)*20))+"\"/><w:pgMar w:top=\""+(continuous?"360":"0")+"\" w:right=\""+(continuous?"360":"0")+"\" w:bottom=\""+(continuous?"360":"0")+"\" w:left=\""+(continuous?"360":"0")+"\" w:header=\"0\" w:footer=\"0\" w:gutter=\"0\"/><w:docGrid w:type=\"default\"/></w:sectPr>";
        }
        static string Background(int id,SizeF size,float left=0,float top=0,bool behind=true) {
            string x=F(Math.Round(size.Width*12700)),y=F(Math.Round(size.Height*12700));
            return "<w:r><w:drawing><wp:anchor distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"0\" behindDoc=\""+(behind?"1":"0")+"\" locked=\"1\" layoutInCell=\"1\" allowOverlap=\"1\"><wp:simplePos x=\"0\" y=\"0\"/><wp:positionH relativeFrom=\"page\"><wp:posOffset>"+F(Math.Round(left*12700))+"</wp:posOffset></wp:positionH><wp:positionV relativeFrom=\"page\"><wp:posOffset>"+F(Math.Round(top*12700))+"</wp:posOffset></wp:positionV><wp:extent cx=\""+x+"\" cy=\""+y+"\"/><wp:wrapNone/><wp:docPr id=\""+id+"\" name=\"Page artwork "+id+"\"/><wp:cNvGraphicFramePr/><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\""+id+"\" name=\"Page artwork "+id+"\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"img"+id+"\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\""+x+"\" cy=\""+y+"\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:anchor></w:drawing></w:r>";
        }
        static int SaveLayout(PdfDocument document,OfficeExportOptions options,string full,string temp,Action<int,int> progress) {
            byte[] source=document.GetRenderSource().ReadAll();bool ownsTemp=false;
            try {
                using(var file=CreateTemp(temp,out ownsTemp))
                using(var zip=new System.IO.Compression.ZipArchive(file,System.IO.Compression.ZipArchiveMode.Create,false)) {
                    var body=new StringBuilder();var rels=new StringBuilder();int id=0;
                    SizeF paper=document.PageSize(options.Pages[0]);double paperScale=Math.Min(1,1584.0/Math.Max(paper.Width,paper.Height));
                    paper=new SizeF((float)(paper.Width*paperScale),(float)(paper.Height*paperScale));
                    Part(zip,"_rels/.rels","<Relationships xmlns=\""+P+"\">"+Rel("doc","officeDocument","word/document.xml")+"</Relationships>");
                    for(int i=0;i<options.Pages.Length;i++)using(var copy=PdfDocument.FromBytes(source,document.Password)) {
                        var page=WordLayout.Read(copy,options.Pages[i],options.Continuous&&options.Equations,options.MathSettings,options.Continuous);int image=++id;
                        if(options.Continuous) {
                            FlowPage(page,paper,options,zip,rels,body,ref id);
                            if(i+1==options.Pages.Length)body.Append(PageSection(paper,true,true));
                            PdfDocument.Check(body.Length<64000000,UiText.Get("转换内容过大，请分批导出。"));
                            if(progress!=null)progress(i+1,options.Pages.Length);
                            continue;
                        }
                        string name="media/page"+(i+1)+".png";
                        using(var stream=zip.CreateEntry("word/"+name,System.IO.Compression.CompressionLevel.Optimal).Open())stream.Write(page.Background,0,page.Background.Length);
                        rels.Append(Rel("img"+image,"image",name));
                        double shrink=Math.Min(1,1584.0/Math.Max(page.Size.Width,page.Size.Height));
                        page.Size=new SizeF((float)(page.Size.Width*shrink),(float)(page.Size.Height*shrink));
                        body.Append("<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\" w:line=\"20\" w:lineRule=\"exact\"/><w:rPr><w:sz w:val=\"2\"/></w:rPr>");
                        if(i+1<options.Pages.Length)body.Append(PageSection(page.Size,false));
                        body.Append("</w:pPr>");if(i==0)body.Append(WordLayout.Shape);
                        body.Append(Background(image,page.Size));
                        foreach(var picture in page.Pictures) {
                            int number=++id;string media="media/region"+number+".png";
                            using(var stream=zip.CreateEntry("word/"+media,System.IO.Compression.CompressionLevel.Optimal).Open())stream.Write(picture.Data,0,picture.Data.Length);
                            rels.Append(Rel("img"+number,"image",media));
                            body.Append(Background(number,new SizeF(picture.Width,picture.Height),picture.X,picture.Y,false));
                        }
                        body.Append(WordLayout.Boxes(page,ref id)).Append("</w:p>");
                        if(i+1==options.Pages.Length)body.Append(PageSection(page.Size,true));
                        PdfDocument.Check(body.Length<64000000,UiText.Get("转换内容过大，请分批导出。"));
                        if(progress!=null)progress(i+1,options.Pages.Length);
                    }
                    Part(zip,"word/document.xml","<w:document xmlns:w=\""+W+"\" xmlns:r=\""+R+"\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:w10=\"urn:schemas-microsoft-com:office:word\" xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"><w:body>"+body+"</w:body></w:document>");
                    rels.Append(Rel("settings","settings","settings.xml"));
                    Part(zip,"word/settings.xml","<w:settings xmlns:w=\""+W+"\" xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"><w:doNotAutoCompressPictures/><w:compat><w:compatSetting w:name=\"compatibilityMode\" w:uri=\"http://schemas.microsoft.com/office/word\" w:val=\"15\"/></w:compat><m:mathPr><m:mathFont m:val=\"Cambria Math\"/></m:mathPr></w:settings>");
                    Part(zip,"word/_rels/document.xml.rels","<Relationships xmlns=\""+P+"\">"+rels+"</Relationships>");
                    Part(zip,"[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/settings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/></Types>");
                }
                using(var test=System.IO.Compression.ZipFile.OpenRead(temp))PdfDocument.Check(test.GetEntry("word/document.xml")!=null,UiText.Get("导出文件校验失败。"));
                if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);return 0;
            } finally {if(ownsTemp&&File.Exists(temp))File.Delete(temp);}
        }
    }
}
