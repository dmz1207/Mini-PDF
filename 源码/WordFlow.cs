using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniPdf {
    internal static partial class OfficeExport {
        sealed class FlowItem {
            internal WordText Text;
            internal WordPicture Picture;
            internal float X,Y,Width,Height,Size;
            internal bool Block;
        }
        sealed class FlowRow {
            internal readonly List<FlowItem> Items=new List<FlowItem>();
            internal float Y,X,Right,Size;
            internal bool Block;
        }
        static List<WordPicture> Artwork(WordPage page,bool columns) {
            var result=new List<WordPicture>();
            using(var stream=new MemoryStream(page.Background,false))using(var image=new Bitmap(stream))
            using(var small=new Bitmap(Math.Max(1,(int)page.Size.Width),Math.Max(1,(int)page.Size.Height),PixelFormat.Format32bppArgb)) {
                using(var graphics=Graphics.FromImage(small)){graphics.Clear(Color.White);graphics.DrawImage(image,new Rectangle(0,0,small.Width,small.Height));}
                var data=small.LockBits(new Rectangle(0,0,small.Width,small.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                byte[] pixels=new byte[data.Stride*small.Height];try{Marshal.Copy(data.Scan0,pixels,0,pixels.Length);}finally{small.UnlockBits(data);}
                double sx=image.Width/page.Size.Width,sy=image.Height/page.Size.Height;
                for(int side=0;side<(columns?2:1);side++) {
                    int x0=columns?side*small.Width/2:0,x1=columns?(side+1)*small.Width/2:small.Width;
                    int top=-1,bottom=-1,left=x1,right=x0;
                    Action flush=delegate {
                        if(top<0)return;
                        if(right-left>=1&&bottom-top>=1&&!(bottom-top<3&&right-left>72)) {
                            int l=Math.Max(0,(int)Math.Floor((left-1)*sx)),t=Math.Max(0,(int)Math.Floor((top-1)*sy));
                            int r=Math.Min(image.Width,(int)Math.Ceiling((right+2)*sx)),b=Math.Min(image.Height,(int)Math.Ceiling((bottom+2)*sy));
                            using(var region=image.Clone(Rectangle.FromLTRB(l,t,r,b),PixelFormat.Format32bppArgb))
                            using(var bytes=new MemoryStream()){region.Save(bytes,ImageFormat.Png);result.Add(new WordPicture{X=(float)(l/sx),Y=(float)(t/sy),Width=(float)((r-l)/sx),Height=(float)((b-t)/sy),Data=bytes.ToArray()});}
                        }
                        top=-1;bottom=-1;left=x1;right=x0;
                    };
                    for(int y=0;y<small.Height;y++) {
                        bool ink=false;
                        for(int x=x0;x<x1;x++) {
                            int p=y*data.Stride+x*4;
                            if(pixels[p]<240||pixels[p+1]<240||pixels[p+2]<240){ink=true;left=Math.Min(left,x);right=Math.Max(right,x);}
                        }
                        if(ink){if(top<0)top=y;bottom=y;}
                        else if(top>=0&&y-bottom>3)flush();
                    }
                    flush();
                }
            }
            return result;
        }
        static string FlowRun(WordText text,string prefix,float baseline) {
            var color=text.Color.GetBrightness()>.85f?Color.Black:text.Color;
            var xml=new StringBuilder("<w:r><w:rPr><w:rFonts w:ascii=\""+Xml(text.Font)+"\" w:hAnsi=\""+Xml(text.Font)+"\" w:eastAsia=\""+Xml(text.Font)+"\"/><w:sz w:val=\""+Math.Round(text.Size*2)+"\"/><w:color w:val=\""+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2")+"\"/>");
            if(Math.Abs(baseline-text.Y)>.5f)xml.Append("<w:position w:val=\""+F(Math.Round((baseline-text.Y)*2))+"\"/>");
            if(text.Bold)xml.Append("<w:b/>");if(text.Italic)xml.Append("<w:i/>");
            return xml.Append("</w:rPr><w:t xml:space=\"preserve\">").Append(Xml(prefix+text.Text)).Append("</w:t></w:r>").ToString();
        }
        static string FlowImage(ZipArchive zip,StringBuilder rels,WordPicture image,OfficePage page,ref int id,bool block) {
            int number=++id;string name="media/flow"+number+".png";
            using(var stream=zip.CreateEntry("word/"+name,CompressionLevel.Optimal).Open())stream.Write(image.Data,0,image.Data.Length);
            rels.Append(Rel("img"+number,"image",name));
            double scale=Math.Min(1,Math.Min(Math.Max(36,page.Size.Width-72)/image.Width,Math.Max(36,page.Size.Height-80)/image.Height));
            // The existing picture writer uses pixels at 96 dpi. Replace its extent with
            // the source region's physical size so a 300-dpi formula is not enlarged.
            string xml=Picture(number,image.Data,page);
            int begin=xml.IndexOf("<w:r>",StringComparison.Ordinal),end=xml.LastIndexOf("</w:r>",StringComparison.Ordinal)+6;
            xml=xml.Substring(begin,end-begin);
            xml=System.Text.RegularExpressions.Regex.Replace(xml,@"cx=""[0-9]+""","cx=\""+F(Math.Round(image.Width*scale*12700))+"\"");
            xml=System.Text.RegularExpressions.Regex.Replace(xml,@"cy=""[0-9]+""","cy=\""+F(Math.Round(image.Height*scale*12700))+"\"");
            if(!block&&image.Baseline>0)xml=xml.Replace("<w:r>","<w:r><w:rPr><w:position w:val=\""+F(Math.Round((image.Baseline-image.Y-image.Height)*2))+"\"/></w:rPr>");
            return xml;
        }
        static void FlowPage(WordPage page,SizeF paper,OfficeExportOptions options,ZipArchive zip,StringBuilder rels,StringBuilder body,ref int id) {
            float middle=page.Size.Width/2;
            var nativeLines=page.Text.Where(t=>t.Text!=null).GroupBy(t=>Math.Round(t.Y*2));
            bool columns=page.Columns||nativeLines.Count(g=>g.Where(t=>t.X<middle&&t.X+t.Width<middle+2).Sum(t=>t.Text.Length)>35)>=6&&
                nativeLines.Count(g=>g.Where(t=>t.X>=middle).Sum(t=>t.Text.Length)>35)>=6;
            var items=page.Text.Select(t=>new FlowItem{Text=t,X=t.X,Y=t.Y,Width=t.Width,Height=t.Height,Size=t.Size}).ToList();
            var artwork=page.Text.Count==0&&page.Pictures.Count==0?new List<WordPicture>{new WordPicture{Width=page.Size.Width,Height=page.Size.Height,Data=page.Background,Block=true}}:Artwork(page,columns);
            if(options.Images)foreach(var picture in page.Pictures.Concat(artwork)) {
                float baseline=picture.Baseline;
                var nearby=page.Text.Where(t=>t.Y>=picture.Y&&t.Y<=picture.Y+picture.Height+3).OrderBy(t=>Math.Abs(t.Y-picture.Y-picture.Height*.8f)).FirstOrDefault();
                if(baseline==0&&nearby!=null)baseline=nearby.Y;
                bool block=picture.Block||picture.Height>(nearby==null?24:nearby.Size*2)||baseline==0;
                picture.Baseline=baseline;
                items.Add(new FlowItem{Picture=picture,X=picture.X,Y=block?picture.Y:baseline,Width=picture.Width,Height=picture.Height,Size=nearby==null?10:nearby.Size,Block=block});
            }
            float nominal=page.Text.Where(t=>t.Math==null).GroupBy(t=>Math.Round(t.Size*2)).OrderByDescending(g=>g.Sum(t=>t.Text.Length)).Select(g=>(float)g.Key/2).DefaultIfEmpty(10).First();
            items.Sort((a,b)=>a.Y.CompareTo(b.Y));var rows=new List<FlowRow>();
            Action<FlowItem,bool> add=delegate(FlowItem item,bool script) {
                FlowRow row=null;
                if(!item.Block)row=rows.Where(r=>!r.Block&&
                    (!script||!columns||r.Items.Any(i=>(i.X<middle)==(item.X<middle)))&&
                    Math.Abs(r.Y-item.Y)<(script?nominal*.9f:Math.Max(1.5f,item.Size*.22f))).OrderBy(r=>Math.Abs(r.Y-item.Y)).FirstOrDefault();
                if(row==null){row=new FlowRow{Y=item.Y,Size=item.Size,Block=item.Block};rows.Add(row);}
                row.Items.Add(item);row.Size=Math.Max(row.Size,item.Size);
            };
            foreach(var item in items.Where(i=>i.Block||i.Size>=nominal*.85f))add(item,false);
            foreach(var item in items.Where(i=>!i.Block&&i.Size<nominal*.85f))add(item,true);
            rows.Sort((a,b)=>a.Y.CompareTo(b.Y));
            foreach(var row in rows){row.Items.Sort((a,b)=>a.X.CompareTo(b.X));row.X=row.Items.Min(i=>i.X);row.Right=row.Items.Max(i=>i.X+i.Width);}
            var ordered=new List<FlowRow>();var band=new List<FlowRow>();
            Action flush=delegate {
                for(int side=0;side<(columns?2:1);side++)foreach(var row in band) {
                    var selected=row.Items.Where(i=>!columns||(side==0?i.X<middle:i.X>=middle)).ToList();
                    if(selected.Count==0)continue;var copy=new FlowRow{Y=row.Y,Size=row.Size,Block=row.Block,X=selected.Min(i=>i.X),Right=selected.Max(i=>i.X+i.Width)};copy.Items.AddRange(selected);ordered.Add(copy);
                }
                band.Clear();
            };
            foreach(var row in rows) {
                if(columns&&row.Items.Any(i=>i.X<middle-4&&i.X+i.Width>middle+4)){flush();ordered.Add(row);}else band.Add(row);
            }
            flush();bool paragraph=false;FlowRow prior=null;string lastText="";
            foreach(var row in ordered) {
                bool fresh=!paragraph||row.Block||prior.Block||row.Y<prior.Y||row.Y-prior.Y>Math.Max(14,row.Size*1.65f)||
                    Math.Abs(row.Size-prior.Size)>1||row.X-prior.X>4||row.Right-row.X<row.Size*8;
                if(fresh) {
                    if(paragraph)body.Append("</w:p>");
                    body.Append("<w:p><w:pPr><w:spacing w:after=\"60\" w:line=\"240\" w:lineRule=\"auto\"/>");
                    if(row.Block)body.Append("<w:jc w:val=\"center\"/>");
                    body.Append("</w:pPr>");paragraph=true;lastText="";
                }
                FlowItem previous=null;
                foreach(var item in row.Items) {
                    string prefix=lastText.Length>0&&!Char.IsWhiteSpace(lastText[lastText.Length-1])&&!lastText.EndsWith("-",StringComparison.Ordinal)&&
                        (previous==null||item.X-previous.X-previous.Width>Math.Max(1,row.Size*.15f))?" ":"";
                    if(item.Text!=null&&item.Text.Math==null){body.Append(FlowRun(item.Text,prefix,row.Y));lastText=item.Text.Text;}
                    else {
                        if(prefix.Length>0)body.Append("<w:r><w:t xml:space=\"preserve\"> </w:t></w:r>");
                        if(item.Text!=null){body.Append(item.Text.Math);lastText="x";}
                        else {body.Append(FlowImage(zip,rels,item.Picture,new OfficePage{Size=paper},ref id,row.Block));lastText="x";}
                    }
                    previous=item;
                }
                prior=row;
            }
            if(paragraph)body.Append("</w:p>");
        }
    }
}
