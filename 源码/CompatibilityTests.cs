using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MiniPdf
{
    internal static partial class SelfTest
    {
        static double RasterDifference(Bitmap first, Bitmap second)
        {
            if(first.Size!=second.Size) return Double.MaxValue;
            long total=0; int count=0;
            for(int y=0;y<first.Height;y+=4) for(int x=0;x<first.Width;x+=4)
            {
                var a=first.GetPixel(x,y); var b=second.GetPixel(x,y);
                total+=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B); count+=3;
            }
            return total/(double)Math.Max(1,count);
        }
        static void PublicLayerCoverage(string directory)
        {
            var documents=new List<PdfDocument>();
            var names=new List<string>();
            try
            {
                foreach(string name in new[]{"issue11144_reduced","issue12007_reduced","issue18823","issue269_1","issue269_2","visibility_expressions","same-name-A","same-name-B"})
                {
                    string path=Path.Combine(directory,"layer-source-"+name+".pdf");
                    if(!File.Exists(path)) continue;
                    var source=PdfDocument.Open(path,""); documents.Add(source); names.Add(name);
                    using(var expected=source.Render(0,400,400))
                    {
                        var pages=new List<MergePage>{new MergePage{Source=source,Index=0},new MergePage{Source=source,Index=0}};
                        using(var merged=PdfDocument.MergePages(pages))
                        {
                            using(var first=merged.Render(0,400,400)) using(var second=merged.Render(1,400,400))
                                Assert(SamePixels(expected,first)&&SamePixels(expected,second),name+": duplicated merge retains both layer appearances");
                            merged.Save(Path.Combine(directory,"layer-merged-"+name+".pdf"));
                        }
                        using(var duplicate=PdfDocument.Open(path,""))
                        {
                            duplicate.DuplicatePage();
                            using(var actual=duplicate.Render(1,400,400)) Assert(SamePixels(expected,actual),name+": page duplication retains layer defaults");
                            duplicate.Save(Path.Combine(directory,"layer-duplicate-"+name+".pdf"));
                        }
                        using(var inserted=PdfDocument.New())
                        {
                            inserted.Import(source,1);
                            using(var actual=inserted.Render(1,400,400)) Assert(SamePixels(expected,actual),name+": insertion retains layer defaults");
                            inserted.Save(Path.Combine(directory,"layer-insert-"+name+".pdf"));
                        }
                        string extracted=Path.Combine(directory,"layer-extract-"+name+".pdf"); source.Extract(new[]{0},extracted);
                        using(var copy=PdfDocument.Open(extracted,"")) using(var actual=copy.Render(0,400,400))
                            Assert(SamePixels(expected,actual),name+": extraction retains layer defaults");
                        using(var organized=PdfDocument.New()) using(var original=PdfDocument.FromBytes(organized.Serialize(false),""))
                        {
                            organized.Organize(new[]{new MergePage{Source=source,Index=0},new MergePage{Source=original,Index=0}},original);
                            using(var actual=organized.Render(0,400,400)) Assert(SamePixels(expected,actual),name+": page organizer retains foreign layer defaults");
                            organized.Save(Path.Combine(directory,"layer-organize-"+name+".pdf"));
                            organized.Undo(); Assert(organized.Count==1,name+": layered page organization remains one undoable change");
                        }
                    }
                }
                if(documents.Count>1)
                {
                    var pages=new List<MergePage>();
                    foreach(var source in documents) pages.Add(new MergePage{Source=source,Index=0});
                    for(int i=documents.Count-1;i>=0;i--) pages.Add(new MergePage{Source=documents[i],Index=0});
                    using(var merged=PdfDocument.MergePages(pages))
                    {
                        for(int i=0;i<pages.Count;i++) using(var expected=pages[i].Source.Render(0,400,400)) using(var actual=merged.Render(i,400,400))
                            Assert(SamePixels(expected,actual),"mixed-file layer configuration remains independent on page "+i);
                        merged.Save(Path.Combine(directory,"layer-mixed.pdf"));
                    }
                    File.WriteAllLines(Path.Combine(directory,"layer-mixed-sources.txt"),names.Concat(names.AsEnumerable().Reverse()).ToArray());
                }
            }
            finally { foreach(var source in documents) source.Dispose(); }
            string protectedPath=Path.Combine(directory,"layer-encrypted-source.pdf");
            if(File.Exists(protectedPath)) using(var target=PdfDocument.Open(protectedPath,"layer-test-password"))
            using(var foreign=PdfDocument.New())
            {
                int count=target.Count;
                bool rejected=false;
                try { target.Import(foreign,target.Count); } catch(IOException) { rejected=true; }
                Assert(rejected && target.Count==count && !target.Dirty,
                    "unsupported layered import into encrypted target cancels without changing pages or history");
                Assert(N.FPDF_GetSecurityHandlerRevision(target.Handle)>=0,
                    "canceled layered import retains the target encryption");
                var plan=new[]{new MergePage{Source=target,Index=0},new MergePage{Source=target,Index=0}};
                using(var merged=PdfDocument.MergePages(plan))
                using(var expected=target.Render(0,400,400)) using(var actual=merged.Render(0,400,400))
                    Assert(SamePixels(expected,actual),"authorized encrypted source retains layer appearance when merged into a new PDF");
            }
            string damaged=Path.Combine(directory,"public-dangling-content.pdf");
            if(File.Exists(damaged)) using(var source=PdfDocument.Open(damaged,""))
            {
                var pages=new[]{new MergePage{Source=source,Index=0},new MergePage{Source=source,Index=0}};
                using(var merged=PdfDocument.MergePages(pages))
                {
                    using(var expected=source.Render(0,400,400)) using(var actual=merged.Render(0,400,400))
                        Assert(SamePixels(expected,actual),"dangling content reference no longer discards valid page streams during merge");
                    Assert(merged.SelectedText(0,100000).Contains("Goodbye, world!"),"valid text survives repair of a corrupt content array");
                    merged.Save(Path.Combine(directory,"repaired-content-merge.pdf"));
                }
            }
        }
    }
}
