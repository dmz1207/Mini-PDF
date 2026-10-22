// Development-only per-document probe. Never linked into Mini PDF.exe.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using MiniPdf;

internal static class CorpusProbe
{
    [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
    [DllImport("pdfium.dll")] static extern int FPDF_GetFormType(IntPtr document);
    sealed class PageState
    {
        public int Index, Width, Height, Characters, Annotations;
        public float PointsWidth, PointsHeight;
        public string Pixels, Text, Error;
    }
    static readonly Dictionary<string, object> result = new Dictionary<string, object>();
    static string output;
    static void Phase(string name)
    {
        result["phase"] = name;
        File.WriteAllText(Path.Combine(output, "progress.txt"), name);
    }
    static string Hash(byte[] data)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "");
    }
    static string Pixels(Bitmap image)
    {
        var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly,
                                  PixelFormat.Format32bppArgb);
        try
        {
            byte[] data = new byte[image.Width * image.Height * 4];
            for (int y = 0; y < image.Height; y++)
                Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), data, y * image.Width * 4, image.Width * 4);
            return Hash(data);
        }
        finally { image.UnlockBits(bits); }
    }
    static PageState Snapshot(PdfDocument document, int index)
    {
        var page = new PageState { Index = index };
        document.OpenPage(index);
        var size = document.PageSize(index);
        if (!(size.Width > 0) || !(size.Height > 0) || Single.IsInfinity(size.Width) || Single.IsInfinity(size.Height))
            throw new IOException("Invalid PDF page dimensions.");
        page.PointsWidth = size.Width;
        page.PointsHeight = size.Height;
        double scale = 600 / (double)Math.Max(size.Width, size.Height);
        page.Width = Math.Max(1, Math.Min(600, (int)Math.Round(size.Width * scale)));
        page.Height = Math.Max(1, Math.Min(600, (int)Math.Round(size.Height * scale)));
        using (var bitmap = document.Render(index, page.Width, page.Height)) page.Pixels = Pixels(bitmap);
        page.Characters = N.FPDFText_CountChars(document.TextPage);
        page.Text = Hash(Encoding.UTF8.GetBytes(document.SelectedText(0, Math.Min(1000000, page.Characters))));
        page.Annotations = N.FPDFPage_GetAnnotCount(document.Page);
        return page;
    }
    static bool Same(PageState a, PageState b)
    {
        return a.Pixels == b.Pixels && a.Text == b.Text && a.Characters == b.Characters &&
               a.Annotations == b.Annotations && a.Width == b.Width && a.Height == b.Height &&
               Math.Abs(a.PointsWidth - b.PointsWidth) < .001 && Math.Abs(a.PointsHeight - b.PointsHeight) < .001;
    }
    static List<PageState> ReadPages(PdfDocument document)
    {
        var pages = new List<PageState>();
        for (int index = 0; index < document.Count; index++)
        {
            if (index % 10 == 0) Phase("render:" + index);
            try { pages.Add(Snapshot(document, index)); }
            catch (Exception error) { pages.Add(new PageState { Index = index, Error = error.Message }); }
        }
        return pages;
    }
    static void Test(string input, string password)
    {
        Phase("open");
        using (var document = PdfDocument.Open(input, password))
        {
            result["pages"] = document.Count;
            result["editable"] = document.CanEdit;
            result["signatures"] = document.SignatureCount;
            result["form_type"] = FPDF_GetFormType(document.Handle);
            if (document.Count > 10000) { result["status"] = "page_limit"; return; }
            Phase("outline");
            result["bookmarks"] = document.Bookmarks().Count;
            var before = ReadPages(document);
            result["source_pages"] = before;
            result["rendered_pages"] = before.FindAll(p => p.Error == null).Count;
            if (before.Exists(p => p.Error != null)) { result["status"] = "page_error"; return; }
            Phase("save");
            string copy = Path.Combine(output, "roundtrip.pdf");
            document.Save(copy);
            List<PageState> after;
            using (var reopened = PdfDocument.Open(copy, password))
            {
                if (reopened.Count != before.Count) throw new IOException("Save changed the page count.");
                after = ReadPages(reopened);
            }
            var differences = new List<int>();
            for (int i = 0; i < before.Count; i++) if (!Same(before[i], after[i])) differences.Add(i);
            result["roundtrip_differences"] = differences;
            result["saved_pages"] = after;
            if (document.SignatureCount > 0)
            {
                result["signed_bytes_preserved"] = Hash(File.ReadAllBytes(input)) == Hash(File.ReadAllBytes(copy));
                result["status"] = differences.Count == 0 ? "ok_signed" : "roundtrip_difference";
                return;
            }
            if (document.CanEdit)
            {
                Phase("merge");
                int last = document.Count - 1;
                var selection = new List<MergePage> { new MergePage { Source = document, Index = 0 },
                                                     new MergePage { Source = document, Index = last } };
                using (var merged = PdfDocument.MergePages(selection))
                {
                    string combined = Path.Combine(output, "merged.pdf");
                    merged.Save(combined);
                    using (var reopened = PdfDocument.Open(combined, ""))
                    {
                        if (reopened.Count != 2) throw new IOException("Merge changed the requested page count.");
                        var firstMerged = Snapshot(reopened, 0); var lastMerged = Snapshot(reopened, 1);
                        result["merge_pages"] = new[] { firstMerged, lastMerged };
                        result["merge_preserved"] = Same(after[0], firstMerged) && Same(after[last], lastMerged);
                    }
                }
                Phase("annotation");
                document.OpenPage(0);
                int annotations = N.FPDFPage_GetAnnotCount(document.Page);
                const string marker = "MINIPDF_CORPUS_TEST";
                document.AddNote((float)(document.Width * .1), (float)(document.Height * .9), marker, Color.Gold);
                string annotated = Path.Combine(output, "annotated.pdf");
                document.Save(annotated);
                using (var reopened = PdfDocument.Open(annotated, password))
                {
                    IntPtr annotation = N.FPDFPage_GetAnnot(reopened.Page, annotations);
                    try
                    {
                        uint length = N.FPDFAnnot_GetStringValue(annotation, "Contents", null, 0);
                        byte[] bytes = new byte[length];
                        N.FPDFAnnot_GetStringValue(annotation, "Contents", bytes, length);
                        result["annotation_preserved"] = reopened.Count == before.Count &&
                            N.FPDFPage_GetAnnotCount(reopened.Page) == annotations + 1 &&
                            Encoding.Unicode.GetString(bytes).TrimEnd((char)0) == marker &&
                            Hash(Encoding.UTF8.GetBytes(reopened.SelectedText(0, Math.Min(1000000, N.FPDFText_CountChars(reopened.TextPage))))) == after[0].Text;
                    }
                    finally { if (annotation != IntPtr.Zero) N.FPDFPage_CloseAnnot(annotation); }
                }
            }
            result["status"] = differences.Count == 0 ? "ok" : "roundtrip_difference";
            Phase("done");
        }
    }
    [STAThread]
    static int Main(string[] args)
    {
        output = args[1];
        Directory.CreateDirectory(output);
        SetErrorMode(3);
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var watch = Stopwatch.StartNew();
        bool initialized = false;
        try
        {
            N.SetDefaultDllDirectories(0xa00);
            if (N.LoadLibraryEx(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdfium.dll"), IntPtr.Zero, 0x900) == IntPtr.Zero)
                throw new IOException("Missing PDFium.");
            N.FPDF_InitLibrary(); initialized = true; N.SetProcessDPIAware();
            Test(args[0], args.Length > 2 ? args[2] : "");
            return 0;
        }
        catch (PdfException error) { result["status"] = "open_error"; result["native_error"] = error.Code; result["error"] = error.Message; return 1; }
        catch (Exception error) { result["status"] = "error"; result["error"] = error.GetType().Name + ": " + error.Message; return 1; }
        finally
        {
            result["elapsed_ms"] = watch.Elapsed.TotalMilliseconds;
            File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(result), Encoding.UTF8);
            if (initialized) N.FPDF_DestroyLibrary();
        }
    }
}
