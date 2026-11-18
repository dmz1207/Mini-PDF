using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MiniPdf {
    internal static partial class SelfTest {
        static Bitmap ZoomCapture(PdfCanvas view){var image=new Bitmap(view.ClientSize.Width,view.ClientSize.Height);using(var graphics=Graphics.FromImage(image)){graphics.Clear(view.BackColor);typeof(PdfCanvas).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,new object[]{new PaintEventArgs(graphics,new Rectangle(Point.Empty,image.Size))});}return image;}
        static double BlueCoverage(Bitmap image,Rectangle page){
            var r=Rectangle.Intersect(new Rectangle(Point.Empty,image.Size),page);r.Inflate(-12,-12);int blue=0,count=0;
            for(int y=r.Top;y<r.Bottom;y+=5)for(int x=r.Left;x<r.Right;x+=5){var c=image.GetPixel(x,y);count++;if(c.B>100&&c.B>c.R*1.6&&c.B>c.G*1.3)blue++;}
            return count==0?0:(double)blue/count;
        }
        static void ZoomContinuity(string directory){
            string fixture=Path.Combine(directory,"zoom-fixture.pdf");
            using(var document=PdfDocument.New()){
                document.AddRectangle(new N.Rect(0,0,595.276f,841.89f),Color.Navy,1,Color.FromArgb(25,60,150));
                for(int y=70;y<800;y+=50)document.AddText("ZOOM CONTINUITY 123456789",40,y,18,"Helvetica",Color.White);
                document.Save(fixture);
            }
            using(var form=new MainWindow(null)){
                form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;form.ClientSize=new Size(1080,760);
                form.LoadForTest(PdfDocument.Open(fixture,""));form.Show();Application.DoEvents();var view=form.CanvasForTest;
                form.ApplyZoom("80%");using(var warm=ZoomCapture(view)){}AwaitRaster(form);
                using(var image=ZoomCapture(view)){image.Save(Path.Combine(directory,"zoom-initial.png"),ImageFormat.Png);log.AppendLine("Initial blue coverage: "+BlueCoverage(image,view.PageRectangle));Assert(BlueCoverage(image,view.PageRectangle)>.8,"initial page raster is visible");}
                foreach(string zoom in new[]{"125%","95%","200%","450%","390%","115%"}){
                    int prior=view.PreviewFramesForTest;form.ApplyZoom(zoom);
                    // No message pump: completion callbacks cannot deliver the new
                    // resolution yet, so this captures the actual transitional frame.
                    using(var image=ZoomCapture(view)){
                        Assert(view.PreviewFramesForTest>prior&&BlueCoverage(image,view.PageRectangle)>.8,"first frame retains page content at "+zoom);
                        if(zoom=="125%")image.Save(Path.Combine(directory,"zoom-pending.png"),ImageFormat.Png);
                    }
                    AwaitRaster(form);
                    using(var image=ZoomCapture(view))Assert(BlueCoverage(image,view.PageRectangle)>.8,"completed raster stays visible at "+zoom);
                    Assert(view.CacheBytesForTest<=64L*1024*1024,"zoom cache stays within its 64 MiB budget at "+zoom);
                }
                for(int i=0;i<30;i++){form.ApplyZoom((100+i*3)+"%");using(var image=ZoomCapture(view))Assert(BlueCoverage(image,view.PageRectangle)>.8,"rapid zoom retains content "+i);}
                AwaitRaster(form);uint before=N.GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0);
                using(var image=ZoomCapture(view))image.Save(Path.Combine(directory,"zoom-sharp.png"),ImageFormat.Png);
                int previews=view.PreviewFramesForTest;using(var image=ZoomCapture(view)){}
                Assert(view.PreviewFramesForTest==previews,"ready full-resolution page bypasses scaled previews");
                // An edit is a new render epoch. Old pixels must never be reused
                // as a preview for an edited, reloaded or different document.
                form.Document.AddText("NEW EPOCH",80,400,30,"Helvetica",Color.Red);view.Rebuild(false);previews=view.PreviewFramesForTest;
                using(var image=ZoomCapture(view)){}Assert(view.PreviewFramesForTest==previews,"document edits discard all old preview pixels");
                AwaitRaster(form);Assert(N.GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0)<=before+20,"zoom transition graphics resources are released");
                form.Document.Save(fixture);form.Close();Application.DoEvents();
            }
            RenderPool.Shutdown();Assert(RenderPool.ProcessCount==0,"zoom cancellation leaves no owned render workers after shutdown");
        }
        internal static int RunZoom(string directory){
            logPath=Path.Combine(directory,"zoom-tests.txt");
            try{ZoomContinuity(directory);log.AppendLine("TOTAL: "+assertions+" zoom assertions, 0 failures");File.WriteAllText(logPath,log.ToString());return 0;}
            catch(Exception error){log.AppendLine(error.ToString());File.WriteAllText(logPath,log.ToString());return 1;}
        }
    }
}
