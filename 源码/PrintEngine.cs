using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;

namespace MiniPdf {
    internal enum PrintMode { Size, Multiple, Booklet, Poster }
    internal enum PrintScale { Fit, Actual, Shrink, Custom }
    internal sealed class PrintOptions {
        internal int[] Pages;
        internal PrintMode Mode;
        internal PrintScale Sizing;
        internal int Orientation,PerSheet=2,BookletSides,DuplexMode,Dpi=300;
        internal float Scale=100,MarginMm=0,OverlapMm=5;
        internal bool AsImage,Gray,Annotations=true,Reverse,Border,RightBinding,PdfPaper,CutMarks;
    }
    internal sealed class PrintPlacement {
        internal int Page,Rotation;
        internal RectangleF Bounds,Clip;
    }
    internal sealed class PrintSheet {
        internal bool Landscape;
        internal PaperSize Paper;
        internal SizeF Size;
        internal RectangleF Area;
        internal readonly List<PrintPlacement> Items=new List<PrintPlacement>();
    }
    // Preview and the printer use the same physical layout, in hundredths of an inch.
    internal static class PrintEngine {
        internal static int[] SelectPages(int count,int current,int range,string text,int parity,bool reverse) {
            int[] source;
            if(range==1)source=new[]{current};
            else if(range==2)source=PdfDocument.ParsePages(text,count);
            else {source=new int[count];for(int i=0;i<count;i++)source[i]=i;}
            var pages=new List<int>();foreach(int p in source)if(parity==0||(p+1)%2==(parity==1?1:0))pages.Add(p);
            PdfDocument.Check(pages.Count>0,UiText.Get("所选范围没有页面。"));if(reverse)pages.Reverse();return pages.ToArray();
        }
        static PaperSize Paper(PdfDocument document,PrintOptions options,PageSettings settings,int page) {
            if(options.PdfPaper&&options.Mode==PrintMode.Size){var s=document.PageSize(page);int w=(int)Math.Round(s.Width*100/72),h=(int)Math.Round(s.Height*100/72);if(w>h){int t=w;w=h;h=t;}
                if(settings.PrinterSettings.IsValid)foreach(PaperSize paper in settings.PrinterSettings.PaperSizes)if(Math.Abs(paper.Width-w)<5&&Math.Abs(paper.Height-h)<5)return paper;
                return new PaperSize("PDF",Math.Max(1,w),Math.Max(1,h));}
            try {return settings.PaperSize;}catch(InvalidPrinterException){return new PaperSize("A4",827,1169);}
        }
        static bool Landscape(PdfDocument doc,PrintOptions options,int page) {
            if(options.Orientation!=0)return options.Orientation==2;
            if(options.Mode==PrintMode.Booklet||options.Mode==PrintMode.Multiple&&options.PerSheet==2)return true;
            var s=doc.PageSize(page);return s.Width>s.Height;
        }
        static PrintSheet Sheet(PdfDocument doc,PrintOptions options,PageSettings settings,int page) {
            var paper=Paper(doc,options,settings,page);bool landscape=Landscape(doc,options,page);
            var size=landscape?new SizeF(paper.Height,paper.Width):new SizeF(paper.Width,paper.Height);
            float m=options.MarginMm*100/25.4f;var area=new RectangleF(m,m,size.Width-2*m,size.Height-2*m);
            if(settings.PrinterSettings.IsValid){var physical=(PageSettings)settings.Clone();physical.PaperSize=paper;physical.Landscape=landscape;area=RectangleF.Intersect(area,physical.PrintableArea);}
            PdfDocument.Check(area.Width>1&&area.Height>1,UiText.Get("页边距超出可打印区域。"));
            return new PrintSheet{Paper=paper,Landscape=landscape,Size=size,Area=area};
        }
        static PrintPlacement Place(PdfDocument doc,int page,RectangleF cell,PrintOptions options,bool fit) {
            var s=doc.PageSize(page);float w=s.Width*100/72,h=s.Height*100/72;
            float normal=Math.Min(cell.Width/w,cell.Height/h),rotated=Math.Min(cell.Width/h,cell.Height/w);int rotation=0;
            if(rotated>normal*1.001f){float t=w;w=h;h=t;normal=rotated;rotation=1;}
            float scale=fit||options.Sizing==PrintScale.Fit?normal:options.Sizing==PrintScale.Shrink?Math.Min(1,normal):options.Sizing==PrintScale.Custom?options.Scale/100:1;
            w*=scale;h*=scale;return new PrintPlacement{Page=page,Rotation=rotation,Clip=cell,Bounds=new RectangleF(cell.X+(cell.Width-w)/2,cell.Y+(cell.Height-h)/2,w,h)};
        }
        internal static List<PrintSheet> Plan(PdfDocument doc,PrintOptions options,PageSettings settings) {
            PdfDocument.Check(options.Pages!=null&&options.Pages.Length>0,UiText.Get("所选范围没有页面。"));
            PdfDocument.Check(options.Scale>=1&&options.Scale<=6400&&options.MarginMm>=0&&options.OverlapMm>=0,UiText.Get("打印设置无效。"));
            foreach(int p in options.Pages)PdfDocument.Check(p>=0&&p<doc.Count,UiText.Get("页码超出范围。"));
            var result=new List<PrintSheet>();int[] pages=options.Pages;var geometries=new Dictionary<string,PrintSheet>();
            Func<int,PrintSheet> makeSheet=delegate(int page){var paper=Paper(doc,options,settings,page);bool landscape=Landscape(doc,options,page);string key=paper.RawKind+":"+paper.Width+":"+paper.Height+":"+landscape;PrintSheet geometry;if(!geometries.TryGetValue(key,out geometry)){geometry=Sheet(doc,options,settings,page);geometries.Add(key,geometry);}return new PrintSheet{Paper=geometry.Paper,Landscape=geometry.Landscape,Size=geometry.Size,Area=geometry.Area};};
            if(options.Mode==PrintMode.Booklet){int padded=(pages.Length+3)/4*4;
                for(int leaf=0;leaf<padded/4;leaf++)for(int side=0;side<2;side++){
                    if(options.BookletSides==1&&side==1||options.BookletSides==2&&side==0)continue;
                    int a=side==0?padded-1-leaf*2:leaf*2+1,b=side==0?leaf*2:padded-2-leaf*2;if(options.RightBinding){int t=a;a=b;b=t;}
                    var sheet=makeSheet(pages[0]);int[] pair={a,b};for(int k=0;k<2;k++)if(pair[k]<pages.Length){var cell=new RectangleF(sheet.Area.X+k*sheet.Area.Width/2,sheet.Area.Y,sheet.Area.Width/2,sheet.Area.Height);sheet.Items.Add(Place(doc,pages[pair[k]],cell,options,true));}result.Add(sheet);
                }
            }else if(options.Mode==PrintMode.Poster){foreach(int page in pages){var first=makeSheet(page);var s=doc.PageSize(page);float w=s.Width*100/72*options.Scale/100,h=s.Height*100/72*options.Scale/100,overlap=options.OverlapMm*100/25.4f;
                    float dx=first.Area.Width-overlap,dy=first.Area.Height-overlap;PdfDocument.Check(dx>1&&dy>1,UiText.Get("重叠宽度超出可打印区域。"));
                    int cols=Math.Max(1,(int)Math.Ceiling((w-overlap)/dx)),rows=Math.Max(1,(int)Math.Ceiling((h-overlap)/dy));PdfDocument.Check((long)cols*rows+result.Count<=10000,UiText.Get("打印任务超过 10000 张，请减小范围或比例。"));
                    for(int y=0;y<rows;y++)for(int x=0;x<cols;x++){var sheet=makeSheet(page);sheet.Items.Add(new PrintPlacement{Page=page,Bounds=new RectangleF(sheet.Area.X-x*dx,sheet.Area.Y-y*dy,w,h),Clip=sheet.Area});result.Add(sheet);}
                }
            }else {int per=options.Mode==PrintMode.Multiple?options.PerSheet:1;PdfDocument.Check(per==1||per==2||per==4||per==6||per==9||per==16,UiText.Get("打印设置无效。"));
                int columns=per==2?2:per==6?3:(int)Math.Sqrt(per),rows=(per+columns-1)/columns;
                for(int start=0;start<pages.Length;start+=per){var sheet=makeSheet(pages[start]);for(int k=0;k<per&&start+k<pages.Length;k++){var cell=new RectangleF(sheet.Area.X+(k%columns)*sheet.Area.Width/columns,sheet.Area.Y+(k/columns)*sheet.Area.Height/rows,sheet.Area.Width/columns,sheet.Area.Height/rows);sheet.Items.Add(Place(doc,pages[start+k],cell,options,per>1));}result.Add(sheet);}
            }
            PdfDocument.Check(result.Count<=10000,UiText.Get("打印任务超过 10000 张，请减小范围或比例。"));return result;
        }
        internal static void Draw(PdfDocument doc,PrintOptions options,PrintSheet sheet,Graphics graphics,int dpi) {
            foreach(var item in sheet.Items){var visible=RectangleF.Intersect(item.Bounds,item.Clip);if(visible.Width<=0||visible.Height<=0)continue;
                int pw=Math.Max(1,(int)Math.Ceiling(item.Bounds.Width*dpi/100)),ph=Math.Max(1,(int)Math.Ceiling(item.Bounds.Height*dpi/100));
                int left=Math.Max(0,(int)Math.Floor((visible.Left-item.Bounds.Left)*pw/item.Bounds.Width)),top=Math.Max(0,(int)Math.Floor((visible.Top-item.Bounds.Top)*ph/item.Bounds.Height));
                int right=Math.Min(pw,(int)Math.Ceiling((visible.Right-item.Bounds.Left)*pw/item.Bounds.Width)),bottom=Math.Min(ph,(int)Math.Ceiling((visible.Bottom-item.Bounds.Top)*ph/item.Bounds.Height));
                using(var prepared=doc.PreparePrintPage(item.Page,options.Annotations)){var render=prepared??doc;int renderPage=prepared==null?item.Page:0;var state=graphics.Save();try{graphics.SetClip(item.Clip,CombineMode.Intersect);graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;int flags=0x800|(options.Annotations?1:0)|(options.Gray?8:0);
                    for(int y=top;y<bottom;y+=2048)for(int x=left;x<right;x+=2048){var tile=new Rectangle(x,y,Math.Min(2048,right-x),Math.Min(2048,bottom-y));using(var image=render.RenderRegion(renderPage,pw,ph,item.Rotation,tile,flags))graphics.DrawImage(image,new RectangleF(item.Bounds.X+x*item.Bounds.Width/pw,item.Bounds.Y+y*item.Bounds.Height/ph,tile.Width*item.Bounds.Width/pw,tile.Height*item.Bounds.Height/ph),new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel);}
                }finally{graphics.Restore(state);}}

            }
            Decorations(options,sheet,graphics);
        }
        internal static void Decorations(PrintOptions options,PrintSheet sheet,Graphics graphics){if(options.Border)using(var pen=new Pen(Color.Gray,.5f))foreach(var item in sheet.Items)graphics.DrawRectangle(pen,item.Clip.X,item.Clip.Y,item.Clip.Width,item.Clip.Height);
            if(options.CutMarks&&options.Mode==PrintMode.Poster)using(var pen=new Pen(Color.Black,.5f)){var r=sheet.Area;float n=8;graphics.DrawLine(pen,r.Left,r.Top,r.Left+n,r.Top);graphics.DrawLine(pen,r.Left,r.Top,r.Left,r.Top+n);graphics.DrawLine(pen,r.Right,r.Bottom,r.Right-n,r.Bottom);graphics.DrawLine(pen,r.Right,r.Bottom,r.Right,r.Bottom-n);}
        }
        internal static PrintDocument CreateJob(PdfDocument doc,PrintOptions options,PrinterSettings printer,PageSettings pageSettings,Func<int[]> selectPages=null) {
            PdfDocument.Check(doc.CanPrint,UiText.Get("此文档不可打印。"));var print=new PrintDocument{DocumentName=String.IsNullOrEmpty(doc.FilePath)?"Mini PDF":System.IO.Path.GetFileName(doc.FilePath),PrinterSettings=(PrinterSettings)printer.Clone(),DefaultPageSettings=(PageSettings)pageSettings.Clone(),OriginAtMargins=false};
            if(options.DuplexMode>0){PdfDocument.Check(options.DuplexMode==1||print.PrinterSettings.CanDuplex,UiText.Get("所选打印机不支持自动双面打印。"));print.PrinterSettings.Duplex=options.DuplexMode==1?Duplex.Simplex:options.DuplexMode==2?Duplex.Vertical:Duplex.Horizontal;}
            List<PrintSheet> sheets=null;int at=0;
            print.BeginPrint+=delegate{if(!options.AsImage)PdfDocument.Check(N.FPDF_SetPrintMode(0)!=0,UiText.Get("无法初始化原生打印。"));if(selectPages!=null)options.Pages=selectPages();sheets=Plan(doc,options,print.DefaultPageSettings);at=0;};print.QueryPageSettings+=delegate(object sender,QueryPageSettingsEventArgs e){var sheet=sheets[at];e.PageSettings.PaperSize=sheet.Paper;e.PageSettings.Landscape=sheet.Landscape;e.PageSettings.Margins=new Margins(0,0,0,0);};
            print.PrintPage+=delegate(object sender,PrintPageEventArgs e){if(options.AsImage){var state=e.Graphics.Save();try{e.Graphics.TranslateTransform(-e.PageSettings.HardMarginX,-e.PageSettings.HardMarginY);Draw(doc,options,sheets[at],e.Graphics,Math.Min(1200,Math.Max(72,options.Dpi)));}finally{e.Graphics.Restore(state);}}else NativePrinting.Draw(doc,options,sheets[at],e.Graphics,e.PageSettings);at++;e.HasMorePages=at<sheets.Count;};return print;
        }
    }
}
