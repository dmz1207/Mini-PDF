using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniPdf {
    internal sealed class OfficeExportOptions {
        internal bool Excel,Images=true,DetectColumns=true;
        internal int[] Pages;
        internal string TempPath;
    }
    internal static class OfficeExport {
        const string S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        const string R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string P="http://schemas.openxmlformats.org/package/2006/relationships";
        internal static string Xml(string text) {
            var b=new StringBuilder();foreach(char ch in text??"")if((ch>=32&&ch!=0xfffe&&ch!=0xffff)||ch=='\n'||ch=='\r'||ch=='\t')b.Append(ch);
            return SecurityElement.Escape(b.ToString());
        }
        static string F(double value){return value.ToString("0.###",CultureInfo.InvariantCulture);}
        static void Part(ZipArchive zip,string name,string xml){using(var writer=new StreamWriter(zip.CreateEntry(name,CompressionLevel.Optimal).Open(),new UTF8Encoding(false)))writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>"+xml);}
        static string Rel(string id,string type,string target){return "<Relationship Id=\""+id+"\" Type=\""+R+"/"+type+"\" Target=\""+target+"\"/>";}
        static string Font(string name) {
            name=Regex.Replace(name??"Arial",@"^[A-Z]{6}\+","");
            if(name.StartsWith("Times-",StringComparison.Ordinal))return "Times New Roman";
            if(name.StartsWith("Helvetica",StringComparison.Ordinal))return "Arial";
            if(name.StartsWith("Courier",StringComparison.Ordinal))return "Courier New";
            return name=="STSong-Light"?"SimSun":name;
        }
        static string Section(OfficePage page,bool last) {
            return "<w:sectPr>"+(!last?"<w:type w:val=\"nextPage\"/>":"")+"<w:pgSz w:w=\""+F(Math.Round(Math.Max(144,Math.Min(1584,page.Size.Width))*20))+"\" w:h=\""+F(Math.Round(Math.Max(144,Math.Min(1584,page.Size.Height))*20))+"\"/><w:pgMar w:top=\"360\" w:right=\"360\" w:bottom=\"360\" w:left=\"360\" w:header=\"0\" w:footer=\"0\" w:gutter=\"0\"/></w:sectPr>";
        }
        static string Paragraph(OfficeRow row) {
            var b=new StringBuilder("<w:p><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr>");
            for(int i=0;i<row.Cells.Count;i++) {
                var c=row.Cells[i];b.Append("<w:r><w:rPr><w:rFonts w:ascii=\"").Append(Xml(Font(c.Font))).Append("\" w:hAnsi=\"").Append(Xml(Font(c.Font))).Append("\" w:eastAsia=\"").Append(Xml(Font(c.Font))).Append("\"/><w:sz w:val=\"").Append(Math.Round(c.Size*2)).Append("\"/><w:color w:val=\"").Append(c.Color.R.ToString("X2")).Append(c.Color.G.ToString("X2")).Append(c.Color.B.ToString("X2")).Append("\"/>");if(c.Bold)b.Append("<w:b/>");b.Append("</w:rPr><w:t xml:space=\"preserve\">").Append(Xml((i==0?"":"    ")+c.Text)).Append("</w:t></w:r>");
            }
            return b.Append("</w:p>").ToString();
        }
        static string Table(IList<OfficeRow> rows,OfficePage page) {
            int count=rows[0].Cells.Count;var widths=new int[count];
            for(int i=0;i<count;i++)widths[i]=Math.Max(240,(int)Math.Round(((i+1<count?rows[0].Cells[i+1].X:page.Size.Width-18)-rows[0].Cells[i].X)*20));
            var b=new StringBuilder("<w:tbl><w:tblPr><w:tblLayout w:type=\"fixed\"/><w:tblW w:w=\"0\" w:type=\"auto\"/><w:tblCellMar><w:top w:w=\"0\" w:type=\"dxa\"/><w:bottom w:w=\"0\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid>");
            foreach(int width in widths)b.Append("<w:gridCol w:w=\"").Append(width).Append("\"/>");b.Append("</w:tblGrid>");
            foreach(var row in rows){b.Append("<w:tr>");for(int i=0;i<count;i++){var single=new OfficeRow();single.Cells.Add(row.Cells[i]);b.Append("<w:tc><w:tcPr><w:tcW w:type=\"dxa\" w:w=\"").Append(widths[i]).Append("\"/></w:tcPr>").Append(Paragraph(single)).Append("</w:tc>");}b.Append("</w:tr>");}return b.Append("</w:tbl>").ToString();
        }
        static string Picture(int id,byte[] png,OfficePage page) {
            using(var stream=new MemoryStream(png,false))using(var bitmap=Image.FromStream(stream)) {
                double width=Math.Min(bitmap.Width*.75,Math.Max(72,page.Size.Width-36)),height=width*bitmap.Height/bitmap.Width;
                double limit=Math.Max(72,page.Size.Height-72);if(height>limit){width*=limit/height;height=limit;}
                string x=F(Math.Round(width*12700)),y=F(Math.Round(height*12700));
                return "<w:p><w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\""+x+"\" cy=\""+y+"\"/><wp:docPr id=\""+id+"\" name=\"Image "+id+"\"/><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\""+id+"\" name=\"Image "+id+"\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"img"+id+"\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\""+x+"\" cy=\""+y+"\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>";
            }
        }
        internal static string ColumnName(int index){string s="";do{s=(char)('A'+index%26)+s;index=index/26-1;}while(index>=0);return s;}
        internal static bool Numeric(string value) {
            return Regex.IsMatch(value,@"^-?(?:0|[1-9][0-9]{0,14})(?:\.[0-9]+)?$")&&Regex.Replace(value,"[^0-9]","").Length<=15;
        }
        internal static List<List<string>> Cells(OfficePage page,bool detect) {
            var cols=OfficeLayout.Columns(page,detect);var output=new List<List<string>>();
            foreach(var row in page.Rows) {
                var values=new List<string>();for(int i=0;i<cols.Count;i++)values.Add("");
                foreach(var c in row.Cells) {
                    int at=0;if(detect){float best=Single.MaxValue;for(int i=0;i<cols.Count;i++){float gap=Math.Abs(cols[i]-c.X);if(gap<best){best=gap;at=i;}}}
                    values[at]+=(values[at].Length>0?" ":"")+c.Text;
                }
                output.Add(values);
            }
            return output;
        }
        static string Sheet(OfficePage page,bool detect) {
            var rows=Cells(page,detect);int columns=OfficeLayout.Columns(page,detect).Count;
            var b=new StringBuilder("<worksheet xmlns=\""+S+"\"><sheetViews><sheetView workbookViewId=\"0\"/></sheetViews><sheetFormatPr defaultRowHeight=\"16\"/><cols>");
            for(int i=0;i<columns;i++)b.Append("<col min=\"").Append(i+1).Append("\" max=\"").Append(i+1).Append("\" width=\"").Append(columns==1?90:24).Append("\" customWidth=\"1\"/>");b.Append("</cols><sheetData>");
            for(int y=0;y<rows.Count;y++) {
                var row=rows[y];int lines=1;foreach(var text in row)lines=Math.Max(lines,(int)Math.Ceiling(text.Length/(columns==1?80.0:22.0)));
                b.Append("<row r=\"").Append(y+1).Append("\" ht=\"").Append(Math.Min(409,16*lines)).Append("\" customHeight=\"1\">");
                for(int x=0;x<row.Count;x++)if(row[x].Length>0) {
                    string text=row[x];if(text.Length>32767)throw new InvalidOperationException(UiText.Get("单个单元格超过 Excel 的文字上限。"));
                    b.Append("<c r=\"").Append(ColumnName(x)).Append(y+1).Append("\" s=\"1\"");
                    if(Numeric(text))b.Append("><v>").Append(text).Append("</v></c>");
                    else b.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">").Append(Xml(text)).Append("</t></is></c>");
                }
                b.Append("</row>");
            }
            return b.Append("</sheetData><pageMargins left=\"0.25\" right=\"0.25\" top=\"0.4\" bottom=\"0.4\" header=\"0\" footer=\"0\"/></worksheet>").ToString();
        }
        internal static int Save(PdfDocument document,OfficeExportOptions options,string path,Action<int,int> progress) {
            PdfDocument.Check(options.Pages!=null&&options.Pages.Length>0&&options.Pages.Length<=2000,UiText.Get("请选择 1 至 2000 页进行转换。"));
            PdfDocument.Check(options.Pages.All(p=>p>=0&&p<document.Count),UiText.Get("页面范围无效。"));
            string full=Path.GetFullPath(path),temp=options.TempPath??Path.Combine(Path.GetDirectoryName(full),".minipdf-export-"+Guid.NewGuid().ToString("N")+".tmp");int skipped=0;
            var forms=N.FPDF_GetFormType(document.Handle)==1?PdfForms.Read(document):null;
            try {
                using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))using(var zip=new ZipArchive(file,ZipArchiveMode.Create,false)) {
                    string main=options.Excel?"xl/workbook.xml":"word/document.xml";
                    Part(zip,"_rels/.rels","<Relationships xmlns=\""+P+"\">"+Rel("main","officeDocument",main)+"</Relationships>");
                    var content=new StringBuilder("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/>");
                    var body=new StringBuilder();var rels=new StringBuilder();int imageId=0;
                    for(int i=0;i<options.Pages.Length;i++) {
                        var page=OfficeLayout.Read(document,options.Pages[i],!options.Excel&&options.Images);skipped+=page.SkippedImages;OfficeLayout.FormValues(document,page,forms,options.Pages[i]);
                        if(options.Excel) {
                            string name="worksheets/sheet"+(i+1)+".xml";
                            Part(zip,"xl/"+name,Sheet(page,options.DetectColumns));rels.Append(Rel("sheet"+(i+1),"worksheet",name));
                            body.Append("<sheet name=\"Page ").Append(options.Pages[i]+1).Append("\" sheetId=\"").Append(i+1).Append("\" r:id=\"sheet").Append(i+1).Append("\"/>");
                            content.Append("<Override PartName=\"/xl/").Append(name).Append("\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                        } else {
                            for(int row=0;row<page.Rows.Count;) {
                                int end=row+1;var first=page.Rows[row];
                                if(first.Cells.Count>1)while(end<page.Rows.Count) {
                                    var candidate=page.Rows[end];if(candidate.Cells.Count!=first.Cells.Count||candidate.Y-page.Rows[end-1].Y>Math.Max(24,candidate.Size*3))break;
                                    bool aligned=true;for(int c=0;c<first.Cells.Count;c++)if(Math.Abs(first.Cells[c].X-candidate.Cells[c].X)>8)aligned=false;if(!aligned)break;end++;
                                }
                                if(end-row>=2){body.Append(Table(page.Rows.GetRange(row,end-row),page));row=end;}
                                else {body.Append(Paragraph(first));row++;}
                            }
                            foreach(var image in page.Images) {imageId++;string name="media/image"+imageId+".png";using(var stream=zip.CreateEntry("word/"+name,CompressionLevel.Optimal).Open())stream.Write(image,0,image.Length);rels.Append(Rel("img"+imageId,"image",name));body.Append(Picture(imageId,image,page));}
                            if(i+1<options.Pages.Length)body.Append("<w:p><w:pPr>").Append(Section(page,false)).Append("</w:pPr></w:p>");else body.Append(Section(page,true));
                            PdfDocument.Check(body.Length<64000000,UiText.Get("转换内容过大，请分批导出。"));
                        }
                        if(progress!=null)progress(i+1,options.Pages.Length);
                    }
                    if(options.Excel) {
                        Part(zip,main,"<workbook xmlns=\""+S+"\" xmlns:r=\""+R+"\"><bookViews><workbookView/></bookViews><sheets>"+body+"</sheets></workbook>");
                        rels.Append(Rel("styles","styles","styles.xml"));
                        Part(zip,"xl/styles.xml","<styleSheet xmlns=\""+S+"\"><fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"2\"><xf xfId=\"0\"/><xf xfId=\"0\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
                        content.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
                    } else Part(zip,main,"<w:document xmlns:w=\""+W+"\" xmlns:r=\""+R+"\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><w:body>"+body+"</w:body></w:document>");
                    Part(zip,options.Excel?"xl/_rels/workbook.xml.rels":"word/_rels/document.xml.rels","<Relationships xmlns=\""+P+"\">"+rels+"</Relationships>");
                    content.Append("<Override PartName=\"/").Append(main).Append("\" ContentType=\"application/vnd.openxmlformats-officedocument.").Append(options.Excel?"spreadsheetml.sheet.main+xml":"wordprocessingml.document.main+xml").Append("\"/></Types>");Part(zip,"[Content_Types].xml",content.ToString());
                }
                using(var test=ZipFile.OpenRead(temp)){PdfDocument.Check(test.GetEntry("[Content_Types].xml")!=null,UiText.Get("导出文件校验失败。"));}
                if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);return skipped;
            } finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
