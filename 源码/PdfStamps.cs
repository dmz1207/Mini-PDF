using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniPdf {
    internal sealed class WatermarkOptions {
        internal int[] Pages;
        internal string Text="CONFIDENTIAL";
        internal Bitmap Image;
        internal float Size=48,Angle=45,Opacity=20,ImageWidth=40;
        internal int Position;
        internal bool Tile,Behind,Replace=true;
        internal Color Color=Color.Gray;
    }
    internal static partial class N {
        [DllImport(D)]internal static extern int FPDFPageObj_CountMarks(IntPtr obj);
        [DllImport(D)]internal static extern IntPtr FPDFPageObj_GetMark(IntPtr obj,uint index);
        [DllImport(D,CharSet=CharSet.Ansi)]internal static extern IntPtr FPDFPageObj_AddMark(IntPtr obj,string name);
        [DllImport(D)]internal static extern int FPDFPageObjMark_GetName(IntPtr mark,[Out]byte[] buffer,uint length,out uint needed);
        [DllImport(D)]internal static extern int FPDF_GetSignatureCount(IntPtr doc);
        [DllImport(D)]internal static extern IntPtr FPDF_GetSignatureObject(IntPtr doc,int index);
        [DllImport(D)]internal static extern uint FPDFSignatureObj_GetContents(IntPtr signature,[Out]byte[] buffer,uint length);
        [DllImport(D)]internal static extern uint FPDFSignatureObj_GetByteRange(IntPtr signature,[Out]int[] buffer,uint length);
        [DllImport(D)]internal static extern uint FPDFSignatureObj_GetSubFilter(IntPtr signature,[Out]byte[] buffer,uint length);
        [DllImport(D)]internal static extern uint FPDFSignatureObj_GetReason(IntPtr signature,[Out]byte[] buffer,uint length);
        [DllImport(D)]internal static extern int FPDFPage_Flatten(IntPtr page,int flags);
    }
    internal sealed partial class PdfDocument {
        const string WatermarkTag="MiniPDF_Watermark";
        internal PdfDocument CopyPage(int index){var copy=new PdfDocument();try{copy.Handle=N.FPDF_CreateNewDocument();Check(copy.Handle!=IntPtr.Zero&&N.FPDF_ImportPagesByIndex(copy.Handle,Handle,new[]{index},1,0)!=0,UiText.Get("无法创建页面预览。"));copy.CanEdit=copy.CanPrint=true;copy.OpenPage(0);return copy;}catch{copy.Dispose();throw;}}
        internal PdfDocument PreparePrintPage(int index,bool annotations){IntPtr page=index==Index?Page:N.FPDF_LoadPage(Handle,index);bool widgets=false;try{for(int i=0;i<N.FPDFPage_GetAnnotCount(page);i++){IntPtr a=N.FPDFPage_GetAnnot(page,i);try{if(a!=IntPtr.Zero&&N.FPDFAnnot_GetSubtype(a)==20){widgets=true;break;}}finally{if(a!=IntPtr.Zero)N.FPDFPage_CloseAnnot(a);}}}finally{if(page!=Page&&page!=IntPtr.Zero)N.FPDF_ClosePage(page);}if(!widgets)return null;var copy=CopyPage(index);try{if(!annotations)for(int i=N.FPDFPage_GetAnnotCount(copy.Page)-1;i>=0;i--){IntPtr a=N.FPDFPage_GetAnnot(copy.Page,i);bool remove;try{remove=N.FPDFAnnot_GetSubtype(a)!=20;}finally{N.FPDFPage_CloseAnnot(a);}if(remove)Check(N.FPDFPage_RemoveAnnot(copy.Page,i)!=0,UiText.Get("无法准备打印页面。"));}Check(N.FPDFPage_Flatten(copy.Page,1)!=0,UiText.Get("无法准备打印页面。"));copy.ClosePage();copy.OpenPage(0);return copy;}catch{copy.Dispose();throw;}}
        internal static bool HasMark(IntPtr obj,string name){for(uint i=0;i<N.FPDFPageObj_CountMarks(obj);i++){uint length;if(N.FPDFPageObjMark_GetName(N.FPDFPageObj_GetMark(obj,i),null,0,out length)==0||length>4096)continue;var bytes=new byte[length];if(N.FPDFPageObjMark_GetName(N.FPDFPageObj_GetMark(obj,i),bytes,length,out length)!=0&&Encoding.Unicode.GetString(bytes).TrimEnd('\0')==name)return true;}return false;}
        internal void AddImage(Bitmap image,N.Rect box){Check(image!=null,UiText.Get("请选择图片。"));Edit(delegate{InsertBitmap(image,new N.Matrix{a=box.right-box.left,d=box.top-box.bottom,e=box.left,f=box.bottom},N.FPDFPage_CountObjects(Page),null);});}
        IntPtr InsertBitmap(Bitmap image,N.Matrix matrix,int at,string mark){Check(image.Width<=14000&&image.Height<=14000&&(long)image.Width*image.Height<=32000000,UiText.Get("图片过大，请先缩小至 3200 万像素以内。"));
            var bits=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);IntPtr bitmap=IntPtr.Zero,obj=IntPtr.Zero;
            try{bitmap=N.FPDFBitmap_CreateEx(image.Width,image.Height,4,bits.Scan0,bits.Stride);Check(bitmap!=IntPtr.Zero,UiText.Get("无法读取图片像素。"));obj=N.FPDFPageObj_NewImageObj(Handle);Check(obj!=IntPtr.Zero&&N.FPDFImageObj_SetBitmap(new[]{Page},1,obj,bitmap)!=0,UiText.Get("无法写入图片。"));Check(N.FPDFPageObj_SetMatrix(obj,ref matrix)!=0,UiText.Get("图片定位失败。"));if(mark!=null)Check(N.FPDFPageObj_AddMark(obj,mark)!=IntPtr.Zero,UiText.Get("无法标记水印对象。"));Check(N.FPDFPage_InsertObjectAtIndex(Page,obj,new UIntPtr((uint)at))!=0,UiText.Get("插入图片失败。"));IntPtr inserted=obj;obj=IntPtr.Zero;return inserted;
            }finally{if(obj!=IntPtr.Zero)N.FPDFPageObj_Destroy(obj);if(bitmap!=IntPtr.Zero)N.FPDFBitmap_Destroy(bitmap);image.UnlockBits(bits);}}
        void RemovePageWatermarks(){for(int i=N.FPDFPage_CountObjects(Page)-1;i>=0;i--){IntPtr obj=N.FPDFPage_GetObject(Page,i);if(HasMark(obj,WatermarkTag)){Check(N.FPDFPage_RemoveObject(Page,obj)!=0,UiText.Get("无法删除水印。"));N.FPDFPageObj_Destroy(obj);}}}
        internal int CountWatermarks(){int count=0;for(int p=0;p<Count;p++){IntPtr page=p==Index?Page:N.FPDF_LoadPage(Handle,p);try{for(int i=0;i<N.FPDFPage_CountObjects(page);i++)if(HasMark(N.FPDFPage_GetObject(page,i),WatermarkTag))count++;}finally{if(page!=Page)N.FPDF_ClosePage(page);}}return count;}
        internal void RemoveWatermarks(int[] pages){int prior=Index;Edit(delegate{foreach(int p in pages){OpenPage(p);RemovePageWatermarks();Check(N.FPDFPage_GenerateContent(Page)!=0,UiText.Get("无法生成页面内容。"));}OpenPage(prior);});}
        N.Matrix VisualMatrix(float angle){double x0,y0,x1,y1,x2,y2;N.FPDF_DeviceToPage(Page,0,0,10000,10000,0,0,10000,out x0,out y0);N.FPDF_DeviceToPage(Page,0,0,10000,10000,0,10000,10000,out x1,out y1);N.FPDF_DeviceToPage(Page,0,0,10000,10000,0,0,0,out x2,out y2);double c=Math.Cos(angle*Math.PI/180),s=Math.Sin(angle*Math.PI/180),ux=(x1-x0)/Width,uy=(y1-y0)/Width,vx=(x2-x0)/Height,vy=(y2-y0)/Height;return new N.Matrix{a=(float)(ux*c+vx*s),b=(float)(uy*c+vy*s),c=(float)(-ux*s+vx*c),d=(float)(-uy*s+vy*c)};}
        void CenterStamp(IntPtr obj,float x,float y){double cx,cy;N.FPDF_DeviceToPage(Page,0,0,10000,10000,0,(int)(x*10000),(int)((1-y)*10000),out cx,out cy);var bounds=ObjectBounds(obj);N.FPDFPageObj_Transform(obj,1,0,0,1,cx-(bounds.left+bounds.right)/2,cy-(bounds.bottom+bounds.top)/2);}
        internal void Watermark(WatermarkOptions options){Check(options.Pages!=null&&options.Pages.Length>0,UiText.Get("所选范围没有页面。"));Check(options.Image!=null||!String.IsNullOrWhiteSpace(options.Text),UiText.Get("请输入水印文字或选择图片。"));Check(options.Size>0&&options.Opacity>0&&options.Opacity<=100,UiText.Get("水印设置无效。"));foreach(int p in options.Pages)Check(p>=0&&p<Count,UiText.Get("页码超出范围。"));int prior=Index;
            Edit(delegate{foreach(int page in options.Pages){OpenPage(page);if(options.Replace)RemovePageWatermarks();int rows=options.Tile?3:1,cols=options.Tile?3:1;
                    for(int y=0;y<rows;y++)for(int x=0;x<cols;x++){int at=options.Behind?0:N.FPDFPage_CountObjects(Page);var matrix=VisualMatrix(options.Angle);IntPtr obj;
                        if(options.Image!=null){float width=(float)Width*options.ImageWidth/100/(options.Tile?2:1),height=width*options.Image.Height/options.Image.Width;matrix.a*=width;matrix.b*=width;matrix.c*=height;matrix.d*=height;
                            using(var image=new Bitmap(options.Image.Width,options.Image.Height,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(image))using(var attributes=new ImageAttributes()){var colors=new ColorMatrix();colors.Matrix33=options.Opacity/100;attributes.SetColorMatrix(colors);g.DrawImage(options.Image,new Rectangle(0,0,image.Width,image.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);}obj=InsertBitmap(image,matrix,at,WatermarkTag);}
                        }else{string text=options.Text.Replace("{page}",(page+1).ToString()).Replace("{pages}",Count.ToString());obj=N.FPDFPageObj_CreateTextObj(Handle,FontFor(text,"自动"),options.Size);Check(obj!=IntPtr.Zero,UiText.Get("无法创建文字对象。"));try{Check(N.FPDFText_SetText(obj,text)!=0&&N.FPDFPageObj_SetMatrix(obj,ref matrix)!=0,UiText.Get("无法设置文字。"));Check(N.FPDFPageObj_SetFillColor(obj,options.Color.R,options.Color.G,options.Color.B,(uint)Math.Round(options.Opacity*255/100))!=0&&N.FPDFPageObj_AddMark(obj,WatermarkTag)!=IntPtr.Zero,UiText.Get("无法标记水印对象。"));Check(N.FPDFPage_InsertObjectAtIndex(Page,obj,new UIntPtr((uint)at))!=0,UiText.Get("无法插入文字。"));}catch{N.FPDFPageObj_Destroy(obj);throw;}}
                        CenterStamp(obj,options.Tile?(x+.5f)/cols:.5f,options.Tile?(y+.5f)/rows:options.Position==1?.9f:options.Position==2?.1f:.5f);
                    }Check(N.FPDFPage_GenerateContent(Page)!=0,UiText.Get("无法生成页面内容。"));}OpenPage(prior);});
        }
    }
}
