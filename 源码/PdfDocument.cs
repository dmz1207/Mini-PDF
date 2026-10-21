using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniPdf
{
    internal sealed class PdfException : Exception
    {
        public uint Code;
        public PdfException(string text, uint code) : base(text)
        {
            Code = code;
        }
    }
    internal sealed class Pick
    {
        public int ObjectIndex = -1, AnnotIndex = -1, Type;
        public N.Rect Bounds;
        public string Text = "";
        public bool IsOcrText;
        public bool IsAnnotation
        {
            get {
                return AnnotIndex >= 0;
            }
        }
    }
    internal sealed class Bookmark
    {
        public string Title;
        public int Page;
        public List<Bookmark> Children = new List<Bookmark>();
    }
    internal sealed class SearchHit
    {
        public int Page, Start, Count;
        public List<N.Rect> Rectangles;
    }
    internal sealed partial class PdfDocument : IDisposable
    {
        static int nextIdentity;
        internal readonly int Identity = System.Threading.Interlocked.Increment(ref nextIdentity);
        internal IntPtr Handle, Page;
        IntPtr textPage;
        PdfSource inputSource, renderSource;
        bool bytesCurrent = true;
        readonly Dictionary<int, SizeF> pageSizes = new Dictionary<int, SizeF>();
        internal int RenderEpoch { get; private set; }
        internal PdfSource GetRenderSource()
        {
            if (bytesCurrent && inputSource != null)
                return inputSource;
            if (renderSource == null)
                renderSource = PdfSource.FromBytes(Serialize(false));
            return renderSource;
        }
        void Changed()
        {
            RenderEpoch++;
            pageSizes.Clear();
            bytesCurrent = false;
            if (renderSource != null)
            {
                renderSource.Dispose();
                renderSource = null;
            }
        }
        readonly Dictionary<string, IntPtr> fonts = new Dictionary<string, IntPtr>();
        readonly Dictionary<string, HashSet<char>> fontGlyphs = new Dictionary<string, HashSet<char>>();
        sealed class State
        {
            public byte[] Data;
            public int Page, Revision;
        }
        readonly List<State> undo = new List<State>(), redo = new List<State>();
        int revision, savedRevision, clock;
        public string FilePath = "", Password = "";
        public int Index { get; private set; }
        public bool CanEdit { get; private set; }
        public bool CanPrint { get; private set; }
        public int Count
        {
            get {
                return Handle == IntPtr.Zero ? 0 : N.FPDF_GetPageCount(Handle);
            }
        }
        public double Width
        {
            get {
                return N.FPDF_GetPageWidth(Page);
            }
        }
        public double Height
        {
            get {
                return N.FPDF_GetPageHeight(Page);
            }
        }
        public bool Dirty
        {
            get {
                return revision != savedRevision;
            }
        }
        public bool CanUndo
        {
            get {
                return undo.Count > 0;
            }
        }
        public bool CanRedo
        {
            get {
                return redo.Count > 0;
            }
        }
        internal IntPtr TextPage
        {
            get {
                if (textPage == IntPtr.Zero)
                    textPage = N.FPDFText_LoadPage(Page);
                return textPage;
            }
        }

        public static PdfDocument Open(string path, string password)
        {
            var doc = new PdfDocument();
            try
            {
                doc.Password = password ?? "";
                doc.Load(PdfSource.FromFile(path));
                doc.FilePath = Path.GetFullPath(path);
                return doc;
            }
            catch
            {
                doc.Dispose();
                throw;
            }
        }
        internal static PdfDocument FromBytes(byte[] data, string password)
        {
            var doc = new PdfDocument();
            try
            {
                doc.Password = password ?? "";
                doc.Load(data);
                return doc;
            }
            catch
            {
                doc.Dispose();
                throw;
            }
        }
        public static PdfDocument New()
        {
            var doc = new PdfDocument();
            doc.Handle = N.FPDF_CreateNewDocument();
            Check(doc.Handle != IntPtr.Zero, UiText.Get("无法创建 PDF。"));
            doc.Page = N.FPDFPage_New(doc.Handle, 0, 595.276, 841.89);
            Check(doc.Page != IntPtr.Zero, UiText.Get("无法创建页面。"));
            doc.CanEdit = doc.CanPrint = true;
            doc.revision = doc.clock = 1;
            return doc;
        }
        void Load(byte[] data)
        {
            Load(PdfSource.FromBytes(data));
        }
        void Load(PdfSource source)
        {
            inputSource = source;
            bytesCurrent = true;
            pageSizes.Clear();
            RenderEpoch++;
            Handle = N.FPDF_LoadMemDocument64(source.Pointer, new UIntPtr((ulong)source.Length),
                                              Encoding.UTF8.GetBytes(Password + "\0"));
            if (Handle == IntPtr.Zero)
            {
                uint code = N.FPDF_GetLastError();
                inputSource.Dispose();
                inputSource = null;
                throw new PdfException(code == 4 ? UiText.Get("PDF 需要正确的密码。")
                                                 : UiText.Get("无法打开 PDF（错误 ") + code + UiText.Get("）。"),
                                       code);
            }
            if (Count < 1)
            {
                CloseHandles();
                throw new IOException(UiText.Get("这个 PDF 没有可显示的页面。"));
            }
            uint permissions = N.FPDF_GetDocPermissions(Handle);
            bool unsecured = N.FPDF_GetSecurityHandlerRevision(Handle) < 0;
            CanEdit = unsecured || (permissions & 8) != 0;
            CanPrint = unsecured || (permissions & 4) != 0;
            if (SignatureCount > 0)
                CanEdit = false;
            OpenPage(0);
        }
        internal static void Check(bool ok, string message)
        {
            if (!ok)
                throw new InvalidOperationException(message);
        }
        void CloseText()
        {
            if (textPage != IntPtr.Zero)
            {
                N.FPDFText_ClosePage(textPage);
                textPage = IntPtr.Zero;
            }
        }
        void ClosePage()
        {
            CloseText();
            if (Page != IntPtr.Zero)
            {
                N.FPDF_ClosePage(Page);
                Page = IntPtr.Zero;
            }
        }
        void CloseHandles()
        {
            ClosePage();
            foreach (var font in fonts.Values)
                N.FPDFFont_Close(font);
            fonts.Clear();
            fontGlyphs.Clear();
            if (Handle != IntPtr.Zero)
            {
                N.FPDF_CloseDocument(Handle);
                Handle = IntPtr.Zero;
            }
            if (inputSource != null)
            {
                inputSource.Dispose();
                inputSource = null;
            }
            if (renderSource != null)
            {
                renderSource.Dispose();
                renderSource = null;
            }
            pageSizes.Clear();
            RenderEpoch++;
        }
        public void Dispose()
        {
            CloseHandles();
            undo.Clear();
            redo.Clear();
        }
        public void OpenPage(int index)
        {
            if (Page != IntPtr.Zero && index == Index)
                return;
            ClosePage();
            Index = Math.Max(0, Math.Min(Count - 1, index));
            Page = N.FPDF_LoadPage(Handle, Index);
            Check(Page != IntPtr.Zero, UiText.Get("无法读取第 ") + (Index + 1) + UiText.Get(" 页。"));
        }
        public byte[] Serialize(bool subsetFonts)
        {
            using (var stream = new MemoryStream())
            {
                Exception failure = null;
                var buffer = new byte[65536];
                N.WriteBlock callback = delegate(IntPtr self, IntPtr data, uint size)
                {
                    try
                    {
                        if (size > int.MaxValue)
                            throw new IOException(UiText.Get("写入块过大。"));
                        int remaining = (int)size, offset = 0;
                        while (remaining > 0)
                        {
                            int take = Math.Min(buffer.Length, remaining);
                            Marshal.Copy(IntPtr.Add(data, offset), buffer, 0, take);
                            stream.Write(buffer, 0, take);
                            offset += take;
                            remaining -= take;
                        }
                        return 1;
                    }
                    catch (Exception e)
                    {
                        failure = e;
                        return 0;
                    }
                };
                var writer = new N.Writer { version = 1, write = callback };
                int ok = N.FPDF_SaveAsCopy(Handle, ref writer, subsetFonts ? 10u : 2u);
                GC.KeepAlive(callback);
                if (failure != null)
                    throw new IOException(UiText.Get("写入 PDF 失败。"), failure);
                Check(ok != 0, UiText.Get("保存 PDF 失败。"));
                return stream.ToArray();
            }
        }
        internal static void AtomicWrite(string path, byte[] bytes, string password, int expectedPages)
        {
            using (var verify = FromBytes(bytes, password))
                Check(verify.Count == expectedPages, UiText.Get("保存校验失败，页数不一致。"));
            string full = Path.GetFullPath(path), dir = Path.GetDirectoryName(full);
            string temp = Path.Combine(dir, ".minipdf-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    f.Write(bytes, 0, bytes.Length);
                    f.Flush(true);
                }
                if (File.Exists(full))
                    File.Replace(temp, full, null);
                else
                    File.Move(temp, full);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }
        public void Save(string path)
        {
            if (SignatureCount > 0)
            {
                AtomicWrite(path, OriginalBytes, Password, Count);
                FilePath = Path.GetFullPath(path);
                savedRevision = revision;
                return;
            }
            var before = Capture();
            byte[] bytes;
            try
            {
                bytes = Serialize(true);
                using (var verify = FromBytes(bytes, Password))
                    Check(verify.Count == Count, UiText.Get("保存校验失败。"));
                CloseHandles();
                Load(bytes);
                OpenPage(before.Page);
            }
            catch
            {
                Restore(before);
                throw;
            }
            // Font subsetting changes the engine's font encodings. Reload the
            // serialized document before accepting further edits, even if disk I/O fails.
            AtomicWrite(path, bytes, Password, Count);
            FilePath = Path.GetFullPath(path);
            savedRevision = revision;
        }
        State Capture()
        {
            return new State { Data = GetRenderSource().ReadAll(), Page = Index, Revision = revision };
        }
        void Restore(State state)
        {
            CloseHandles();
            Load(state.Data);
            OpenPage(state.Page);
            revision = state.Revision;
        }
        static void Trim(List<State> list)
        {
            long size = 0;
            foreach (var s in list)
                size += s.Data.LongLength;
            while (list.Count > 1 && (list.Count > 12 || size > 64L * 1024 * 1024))
            {
                size -= list[0].Data.LongLength;
                list.RemoveAt(0);
            }
        }
        public void Edit(Action change)
        {
            Check(CanEdit, SignatureCount > 0 ? UiText.Get("已签名的 PDF 以只读方式打开，以保留签名字节。")
                                              : UiText.Get("此 PDF 限制内容修改，请使用具有编辑权限的密码打开。"));
            var before = Capture();
            try
            {
                CloseText();
                change();
                if (Page == IntPtr.Zero)
                    OpenPage(Index);
                Check(N.FPDFPage_GenerateContent(Page) != 0, UiText.Get("无法生成页面内容。"));
                CloseText();
                undo.Add(before);
                Trim(undo);
                redo.Clear();
                revision = ++clock;
                Changed();
            }
            catch
            {
                Restore(before);
                throw;
            }
        }
        public void Undo()
        {
            if (!CanUndo)
                return;
            var current = Capture();
            var prior = undo[undo.Count - 1];
            Restore(prior);
            undo.RemoveAt(undo.Count - 1);
            redo.Add(current);
            Trim(redo);
        }
        public void Redo()
        {
            if (!CanRedo)
                return;
            var current = Capture();
            var next = redo[redo.Count - 1];
            Restore(next);
            redo.RemoveAt(redo.Count - 1);
            undo.Add(current);
            Trim(undo);
        }
        public Bitmap Render(int index, int width, int height, int rotation = 0)
        {
            return RenderRegion(index, width, height, rotation, new Rectangle(0, 0, width, height));
        }
        internal Bitmap RenderRegion(int index, int pageWidth, int pageHeight, int rotation, Rectangle region,
                                     int renderFlags = 3)
        {
            int width = region.Width, height = region.Height;
            if (width < 1 || height < 1 || width > 14000 || height > 14000 || (long)width * height > 24000000)
                throw new InvalidOperationException(UiText.Get("当前缩放超出显示内存限制，请减小缩放比例。"));
            Check(pageWidth > 0 && pageHeight > 0 && region.X >= 0 && region.Y >= 0 &&
                      (long)region.Right <= pageWidth && (long)region.Bottom <= pageHeight,
                  UiText.Get("页面显示范围无效。"));
            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            try
            {
                var bits = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                                           PixelFormat.Format32bppArgb);
                try
                {
                    RenderInto(index, pageWidth, pageHeight, rotation, region, bits.Scan0, bits.Stride, renderFlags);
                }
                finally
                {
                    result.UnlockBits(bits);
                }
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }
        internal bool RenderInto(int index, int pageWidth, int pageHeight, int rotation, Rectangle region,
                                 IntPtr pixels, int stride, int renderFlags = 3, int budget = 0)
        {
            IntPtr page = index == Index ? Page : N.FPDF_LoadPage(Handle, index), bitmap = IntPtr.Zero;
            try
            {
                Check(page != IntPtr.Zero, UiText.Get("无法读取页面。"));
                bitmap = N.FPDFBitmap_CreateEx(region.Width, region.Height, 4, pixels, stride);
                Check(bitmap != IntPtr.Zero, UiText.Get("无法分配页面图像。"));
                N.FPDFBitmap_FillRect(bitmap, 0, 0, region.Width, region.Height, 0xffffffff);
                if (budget > 0)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    N.NeedPause callback = delegate
                    {
                        return watch.ElapsedMilliseconds >= budget ? 1 : 0;
                    };
                    var pause = new N.Pause { version = 1, callback = callback };
                    try
                    {
                        return N.FPDF_RenderPageBitmap_Start(bitmap, page, -region.X, -region.Y, pageWidth, pageHeight,
                                                             rotation, renderFlags, ref pause) == 2;
                    }
                    finally
                    {
                        N.FPDF_RenderPage_Close(page);
                        GC.KeepAlive(callback);
                    }
                }
                N.FPDF_RenderPageBitmap(bitmap, page, -region.X, -region.Y, pageWidth, pageHeight, rotation,
                                        renderFlags);
                return true;
            }
            finally
            {
                if (bitmap != IntPtr.Zero)
                    N.FPDFBitmap_Destroy(bitmap);
                if (page != IntPtr.Zero && page != Page)
                    N.FPDF_ClosePage(page);
            }
        }
        internal static PdfDocument FromShared(PdfSource source, string password)
        {
            var doc = new PdfDocument { Password = password };
            try
            {
                doc.Load(source);
                return doc;
            }
            catch
            {
                doc.Dispose();
                throw;
            }
        }
        public SizeF PageSize(int index)
        {
            SizeF cached;
            if (pageSizes.TryGetValue(index, out cached))
                return cached;
            N.PageSize size;
            Check(N.FPDF_GetPageSizeByIndexF(Handle, index, out size) != 0, UiText.Get("页面不可用。"));
            cached = new SizeF(size.width, size.height);
            pageSizes[index] = cached;
            return cached;
        }
        public N.Pt ToPage(Point point, int width, int height, int rotation = 0)
        {
            double x, y;
            Check(N.FPDF_DeviceToPage(Page, 0, 0, width, height, rotation, point.X, point.Y, out x, out y) != 0,
                  UiText.Get("坐标转换失败。"));
            return new N.Pt((float)x, (float)y);
        }
        public Point ToDevice(float x, float y, int width, int height, int rotation = 0)
        {
            int dx, dy;
            N.FPDF_PageToDevice(Page, 0, 0, width, height, rotation, x, y, out dx, out dy);
            return new Point(dx, dy);
        }
        internal static bool Contains(N.Rect rect, N.Pt p, float tolerance)
        {
            return p.x >= rect.left - tolerance && p.x <= rect.right + tolerance && p.y >= rect.bottom - tolerance &&
                   p.y <= rect.top + tolerance;
        }
        internal static N.Rect ObjectBounds(IntPtr obj)
        {
            float l, b, r, t;
            Check(N.FPDFPageObj_GetBounds(obj, out l, out b, out r, out t) != 0, UiText.Get("无法读取对象范围。"));
            return new N.Rect(l, b, r, t);
        }
        internal IntPtr ObjectAt(Pick selection)
        {
            Check(selection != null && !selection.IsAnnotation, UiText.Get("请选择页面对象。"));
            IntPtr obj = N.FPDFPage_GetObject(Page, selection.ObjectIndex);
            Check(obj != IntPtr.Zero, UiText.Get("对象已失效，请重新选择。"));
            return obj;
        }
        public Pick PickObject(N.Pt point)
        {

            for (int i = N.FPDFPage_GetAnnotCount(Page) - 1; i >= 0; i--)
            {
                IntPtr a = N.FPDFPage_GetAnnot(Page, i);
                if (a == IntPtr.Zero)
                    continue;
                try
                {
                    int type = N.FPDFAnnot_GetSubtype(a);
                    N.Rect r;
                    if (type != 2 && type != 20 && N.FPDFAnnot_GetRect(a, out r) != 0 && Contains(r, point, 2))
                        return new Pick { AnnotIndex = i, Type = type, Bounds = r, Text = AnnotText(a) };
                }
                finally
                {
                    N.FPDFPage_CloseAnnot(a);
                }
            }
            for (int i = N.FPDFPage_CountObjects(Page) - 1; i >= 0; i--)
            {
                IntPtr image = N.FPDFPage_GetObject(Page, i);
                if (N.FPDFPageObj_GetType(image) == 3 && HasMark(image, "MiniPDF_Signature") &&
                    Contains(ObjectBounds(image), point, 2))
                    return PagePick(i, image);
            }
            // OCR text may be invisible or be painted before a full-page scan image.
            int character = CharAt(point);
            IntPtr text = character < 0 ? IntPtr.Zero : N.FPDFText_GetTextObject(TextPage, character);
            if (text != IntPtr.Zero)
                for (int i = 0; i < N.FPDFPage_CountObjects(Page); i++)
                    if (N.FPDFPage_GetObject(Page, i) == text)
                    {
                        var pick = PagePick(i, text);
                        if (Contains(pick.Bounds, point, 2))
                        {
                            for (int j = i + 1; j < N.FPDFPage_CountObjects(Page); j++)
                            {
                                IntPtr above = N.FPDFPage_GetObject(Page, j);
                                if (N.FPDFPageObj_GetType(above) == 3 && Contains(ObjectBounds(above), point, 0))
                                {
                                    pick.IsOcrText = true;
                                    break;
                                }
                            }
                            return pick;
                        }
                    }
            for (int i = N.FPDFPage_CountObjects(Page) - 1; i >= 0; i--)
            {
                IntPtr obj = N.FPDFPage_GetObject(Page, i);
                float l, b, r, t;
                if (N.FPDFPageObj_GetBounds(obj, out l, out b, out r, out t) == 0)
                    continue;
                var rect = new N.Rect(l, b, r, t);
                if (Contains(rect, point, 2))
                    return PagePick(i, obj);
            }
            return null;
        }
        Pick PagePick(int index, IntPtr obj)
        {
            int type = N.FPDFPageObj_GetType(obj);
            bool hidden = false;
            if (type == 1)
            {
                int mode = N.FPDFTextObj_GetTextRenderMode(obj);
                uint r, g, b, a;
                hidden = mode == 3 || mode == 7 ||
                         (N.FPDFPageObj_GetFillColor(obj, out r, out g, out b, out a) != 0 && a == 0);
            }
            return new Pick { ObjectIndex = index, Type = type, Bounds = ObjectBounds(obj),
                              Text = type == 1 ? ObjectText(obj) : "", IsOcrText = hidden };
        }
        public string ObjectText(IntPtr obj)
        {
            uint size = N.FPDFTextObj_GetText(obj, TextPage, null, 0);
            if (size == 0 || size > 16 * 1024 * 1024)
                return "";
            var buffer = new byte[size];
            N.FPDFTextObj_GetText(obj, TextPage, buffer, size);
            return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }
        static string AnnotText(IntPtr a)
        {
            uint size = N.FPDFAnnot_GetStringValue(a, "Contents", null, 0);
            if (size < 2 || size > 1024 * 1024)
                return "";
            var b = new byte[size];
            N.FPDFAnnot_GetStringValue(a, "Contents", b, size);
            return Encoding.Unicode.GetString(b).TrimEnd('\0');
        }
        public float ObjectFontSize(Pick pick)
        {
            float size = 12;
            N.FPDFTextObj_GetFontSize(ObjectAt(pick), out size);
            return Math.Max(1, size);
        }
        public Color ObjectColor(Pick pick)
        {
            uint r, g, b, a;
            if (N.FPDFPageObj_GetFillColor(ObjectAt(pick), out r, out g, out b, out a) == 0)
                return Color.Black;
            return Color.FromArgb((int)a, (int)r, (int)g, (int)b);
        }
        IntPtr FontFor(string text, string face)
        {
            bool ascii = true;
            foreach (char ch in text)
                if (ch > 126)
                {
                    ascii = false;
                    break;
                }
            string key =
                (face == "自动" || String.IsNullOrEmpty(face)) ? (ascii ? "Helvetica" : "Microsoft YaHei") : face;
            IntPtr font;
            if (fonts.TryGetValue(key, out font))
            {
                CheckFontCharacters(key, text);
                return font;
            }
            if (key == "Helvetica" || key == "Times-Roman" || key == "Courier")
            {
                Check(ascii, UiText.Get("所选标准字体不包含这些字符，请选择“自动”或合适的系统字体。"));
                font = N.FPDFText_LoadStandardFont(Handle, key);
            }
            else
            {
                using (var f = new Font(key, 12, FontStyle.Regular, GraphicsUnit.Point)) using (
                    var graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    IntPtr hdc = graphics.GetHdc(), hf = f.ToHfont(), old = N.SelectObject(hdc, hf);
                    // For TTC faces GDI's table=0 view keeps collection-relative offsets.
                    // Request the complete collection first, then fall back to an ordinary TTF.
                    try
                    {
                        uint table = 0x66637474;
                        uint length = N.GetFontData(hdc, table, 0, null, 0);
                        if (length == 0xffffffff)
                        {
                            table = 0;
                            length = N.GetFontData(hdc, 0, 0, null, 0);
                        }
                        Check(length != 0xffffffff && length > 0 && length < 64000000,
                              UiText.Get("无法读取系统字体。"));
                        var bytes = new byte[length];
                        Check(N.GetFontData(hdc, table, 0, bytes, length) == length,
                              UiText.Get("系统字体读取不完整。"));
                        // Explicit Unicode CIDs avoid ambiguous reverse glyph mappings
                        // (e.g. regular space and NBSP sharing the same system-font glyph).
                        var chars = new char[65536];
                        var glyphs = new ushort[65536];
                        for (int i = 0; i < chars.Length; i++)
                            chars[i] = (char)i;
                        Check(N.GetGlyphIndicesW(hdc, chars, chars.Length, glyphs, 1) != 0xffffffff,
                              UiText.Get("无法读取字体字符映射。"));
                        var mapping = new byte[131072];
                        for (int i = 0; i < glyphs.Length; i++)
                        {
                            ushort glyph = glyphs[i] == 65535 ? (ushort)0 : glyphs[i];
                            mapping[i * 2] = (byte)(glyph >> 8);
                            mapping[i * 2 + 1] = (byte)glyph;
                        }
                        var codes = new List<int>();
                        for (int i = 1; i < glyphs.Length; i++)
                            if (glyphs[i] != 0 && glyphs[i] != 65535 && !Char.IsSurrogate((char)i))
                                codes.Add(i);
                        var supported = new HashSet<char>();
                        foreach (int code in codes)
                            supported.Add((char)code);
                        fontGlyphs[key] = supported;
                        var cmap = new StringBuilder("/CIDInit /ProcSet findresource begin\n12 dict " +
                                                     "begin\nbegincmap\n/CIDSystemInfo << /Registry " +
                                                     "(Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName " +
                                                     "/MiniPdfUnicode def\n/CMapType 2 " +
                                                     "def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
                        for (int offset = 0; offset < codes.Count; offset += 100)
                        {
                            int count = Math.Min(100, codes.Count - offset);
                            cmap.Append(count).Append(" beginbfchar\n");
                            for (int i = offset; i < offset + count; i++)
                            {
                                string hex = codes[i].ToString("X4");
                                cmap.Append('<').Append(hex).Append("> <").Append(hex).Append(">\n");
                            }
                            cmap.Append("endbfchar\n");
                        }
                        cmap.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend");
                        font = N.FPDFText_LoadCidType2Font(Handle, bytes, length, cmap.ToString(), mapping,
                                                           (uint)mapping.Length);
                    }
                    finally
                    {
                        N.SelectObject(hdc, old);
                        N.DeleteObject(hf);
                        graphics.ReleaseHdc(hdc);
                    }
                }
            }
            Check(font != IntPtr.Zero, UiText.Get("无法载入字体，请换用其他字体。"));
            fonts.Add(key, font);
            CheckFontCharacters(key, text);
            return font;
        }
        void CheckFontCharacters(string key, string text)
        {
            HashSet<char> supported;
            if (!fontGlyphs.TryGetValue(key, out supported))
            {
                foreach (char ch in text)
                    if (ch > 126)
                        throw new InvalidOperationException(
                            UiText.Get("所选标准字体不包含这些字符，请选择“自动”或合适的系统字体。"));
                return;
            }
            foreach (char ch in text)
            {
                if (ch == '\r' || ch == '\n' || ch == '\t')
                    continue;
                if (!supported.Contains(ch))
                    throw new InvalidOperationException(UiText.Get("所选字体不支持部分字符（U+") +
                                                        ((int)ch).ToString("X4") +
                                                        UiText.Get("），请更换字体或删除该字符。"));
            }
        }
        void InsertText(string text, float size, string face, Color color, N.Matrix matrix, int at, int renderMode = 0,
                        uint opacity = 255)
        {
            IntPtr font = FontFor(text, face);
            int line = 0;
            foreach (string value in text.Replace("\r", "").Replace("\t", "    ").Split('\n'))
            {
                if (value.Length > 0)
                {
                    IntPtr obj = N.FPDFPageObj_CreateTextObj(Handle, font, size);
                    Check(obj != IntPtr.Zero, UiText.Get("无法创建文字对象。"));
                    try
                    {
                        Check(N.FPDFText_SetText(obj, value) != 0, UiText.Get("无法设置文字。"));
                        Check(N.FPDFTextObj_SetTextRenderMode(obj, renderMode) != 0,
                              UiText.Get("无法保留文字显示属性。"));
                        N.FPDFPageObj_SetFillColor(obj, color.R, color.G, color.B, opacity);
                        N.FPDFPageObj_SetStrokeColor(obj, color.R, color.G, color.B, opacity);
                        var m = matrix;
                        float offset = -line * size * 1.4f;
                        m.e += m.c * offset;
                        m.f += m.d * offset;
                        Check(N.FPDFPageObj_SetMatrix(obj, ref m) != 0, UiText.Get("无法设置文字位置。"));
                        Check(N.FPDFPage_InsertObjectAtIndex(Page, obj, new UIntPtr((uint)at)) != 0,
                              UiText.Get("无法插入文字。"));
                        obj = IntPtr.Zero;
                        at++;
                    }
                    finally
                    {
                        if (obj != IntPtr.Zero)
                            N.FPDFPageObj_Destroy(obj);
                    }
                }
                line++;
            }
        }
        public void AddText(string text, float x, float y, float size, string face, Color color)
        {
            Check(!String.IsNullOrWhiteSpace(text), UiText.Get("请输入文字。"));
            Edit(delegate {
                InsertText(text, size, face, color, new N.Matrix { a = 1, d = 1, e = x, f = y },
                           N.FPDFPage_CountObjects(Page));
            });
        }
        public void ReplaceText(Pick pick, string text, float size, string face, Color color)
        {
            Check(pick != null && !pick.IsAnnotation && pick.Type == 1, UiText.Get("请选择一个文字对象。"));
            Check(!String.IsNullOrWhiteSpace(text), UiText.Get("删除文字请使用删除键。"));
            Edit(delegate {
                IntPtr obj = ObjectAt(pick);
                N.Matrix m;
                int mode = N.FPDFTextObj_GetTextRenderMode(obj);
                Check(mode >= 0 && mode <= 7, UiText.Get("无法读取文字显示属性。"));
                uint r, g, b, alpha;
                if (N.FPDFPageObj_GetFillColor(obj, out r, out g, out b, out alpha) == 0)
                    alpha = 255;
                Check(N.FPDFPageObj_GetMatrix(obj, out m) != 0, UiText.Get("无法读取文字位置。"));
                Check(N.FPDFPage_RemoveObject(Page, obj) != 0, UiText.Get("无法删除原文字。"));
                N.FPDFPageObj_Destroy(obj);
                InsertText(text, size, face, color, m, pick.ObjectIndex, mode, alpha);
            });
        }
        public void Remove(Pick pick)
        {
            Check(pick != null, UiText.Get("请先选择对象。"));
            Edit(delegate {
                if (pick.IsAnnotation)
                    Check(N.FPDFPage_RemoveAnnot(Page, pick.AnnotIndex) != 0, UiText.Get("无法删除批注。"));
                else
                {
                    IntPtr obj = ObjectAt(pick);
                    Check(N.FPDFPage_RemoveObject(Page, obj) != 0, UiText.Get("无法删除对象。"));
                    N.FPDFPageObj_Destroy(obj);
                }
            });
        }
        public void Move(Pick pick, float dx, float dy)
        {
            Check(pick != null, UiText.Get("请选择对象。"));
            if (pick.IsAnnotation)
                throw new InvalidOperationException(UiText.Get("批注暂不支持移动，可撤销后重新添加。"));
            Edit(delegate { N.FPDFPageObj_Transform(ObjectAt(pick), 1, 0, 0, 1, dx, dy); });
        }
        internal Pick PickIndex(int index)
        {
            return index >= 0 && index < N.FPDFPage_CountObjects(Page)
                       ? PagePick(index, N.FPDFPage_GetObject(Page, index))
                       : null;
        }
        internal void ScaleAt(Pick pick, float ratio, N.Pt anchor)
        {
            Check(pick != null && !pick.IsAnnotation && ratio > 0 && ratio <= 100, UiText.Get("尺寸必须大于零。"));
            Edit(delegate {
                N.FPDFPageObj_Transform(ObjectAt(pick), ratio, 0, 0, ratio, anchor.x * (1 - ratio),
                                        anchor.y * (1 - ratio));
            });
        }
        public void Resize(Pick pick, float width, float height)
        {
            Check(pick != null && !pick.IsAnnotation, UiText.Get("请选择页面对象。"));
            var r = pick.Bounds;
            Check(r.right > r.left && r.top > r.bottom && width > 0 && height > 0, UiText.Get("尺寸必须大于零。"));
            Edit(delegate {
                double sx = width / (r.right - r.left), sy = height / (r.top - r.bottom);
                N.FPDFPageObj_Transform(ObjectAt(pick), sx, 0, 0, sy, r.left * (1 - sx), r.top * (1 - sy));
            });
        }
        public void AddImage(string path, N.Rect box)
        {
            using (var input = Image.FromFile(path))
            {
                Check(input.Width <= 14000 && input.Height <= 14000 && (long)input.Width * input.Height <= 32000000,
                      UiText.Get("图片过大，请先缩小至 3200 万像素以内。"));
                using (var image = new Bitmap(input.Width, input.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(image))
                    {
                        g.Clear(Color.Transparent);
                        g.DrawImage(input, 0, 0, image.Width, image.Height);
                    }
                    Edit(delegate {
                        var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height),
                                                  ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        IntPtr bm = IntPtr.Zero, obj = IntPtr.Zero;
                        try
                        {
                            bm = N.FPDFBitmap_CreateEx(image.Width, image.Height, 4, bits.Scan0, bits.Stride);
                            Check(bm != IntPtr.Zero, UiText.Get("无法读取图片像素。"));
                            obj = N.FPDFPageObj_NewImageObj(Handle);
                            Check(obj != IntPtr.Zero, UiText.Get("无法创建图片对象。"));
                            Check(N.FPDFImageObj_SetBitmap(new[] { Page }, 1, obj, bm) != 0,
                                  UiText.Get("无法写入图片。"));
                            var m = new N.Matrix { a = box.right - box.left, d = box.top - box.bottom, e = box.left,
                                                   f = box.bottom };
                            Check(N.FPDFPageObj_SetMatrix(obj, ref m) != 0, UiText.Get("图片定位失败。"));
                            IntPtr inserted = obj;
                            obj = IntPtr.Zero;
                            Check(N.FPDFPage_InsertObject(Page, inserted) != 0, UiText.Get("插入图片失败。"));
                        }
                        finally
                        {
                            if (obj != IntPtr.Zero)
                                N.FPDFPageObj_Destroy(obj);
                            if (bm != IntPtr.Zero)
                                N.FPDFBitmap_Destroy(bm);
                            image.UnlockBits(bits);
                        }
                    });
                }
            }
        }
        public void AddRectangle(N.Rect r, Color color, float width)
        {
            Edit(delegate {
                IntPtr obj = N.FPDFPageObj_CreateNewRect(r.left, r.bottom, r.right - r.left, r.top - r.bottom);
                Check(obj != IntPtr.Zero, UiText.Get("无法创建矩形。"));
                N.FPDFPageObj_SetStrokeColor(obj, color.R, color.G, color.B, 255);
                N.FPDFPageObj_SetStrokeWidth(obj, width);
                N.FPDFPath_SetDrawMode(obj, 0, 1);
                Check(N.FPDFPage_InsertObject(Page, obj) != 0, UiText.Get("无法插入矩形。"));
            });
        }
        void ConfigureAnnot(IntPtr a, N.Rect r, Color c)
        {
            Check(a != IntPtr.Zero, UiText.Get("无法创建批注。"));
            Check(N.FPDFAnnot_SetRect(a, ref r) != 0, UiText.Get("批注范围设置失败。"));
            N.FPDFAnnot_SetFlags(a, 4);
            N.FPDFAnnot_SetColor(a, 0, c.R, c.G, c.B, 255);
            N.FPDFAnnot_SetStringValue(a, "T", "Mini PDF");
            N.FPDFAnnot_SetStringValue(a, "M", "D:" + DateTime.Now.ToString("yyyyMMddHHmmss"));
        }
        public void AddMarkup(List<N.Rect> rectangles, int subtype, Color color)
        {
            Check(rectangles.Count > 0, UiText.Get("请框选需要标记的位置。"));
            Edit(delegate {
                IntPtr a = N.FPDFPage_CreateAnnot(Page, subtype);
                try
                {
                    var union
                    = rectangles[0];
                    foreach (var r in rectangles)
                    {
                        union
                        .left = Math.Min(union.left, r.left);
                        union
                        .right = Math.Max(union.right, r.right);
                        union
                        .top = Math.Max(union.top, r.top);
                        union
                        .bottom = Math.Min(union.bottom, r.bottom);
                    }
                    ConfigureAnnot(a, union, color);
                    foreach (var r in rectangles)
                    {
                        var q = new N.Quad { x1 = r.left, y1 = r.top,    x2 = r.right, y2 = r.top,
                                             x3 = r.left, y3 = r.bottom, x4 = r.right, y4 = r.bottom };
                        Check(N.FPDFAnnot_AppendAttachmentPoints(a, ref q) != 0, UiText.Get("无法设置标记范围。"));
                    }
                }
                finally
                {
                    if (a != IntPtr.Zero)
                        N.FPDFPage_CloseAnnot(a);
                }
            });
        }
        public void AddInk(List<N.Pt> points, Color color, float width)
        {
            Check(points.Count >= 2, UiText.Get("请绘制至少一条笔画。"));
            Edit(delegate {
                IntPtr a = N.FPDFPage_CreateAnnot(Page, 15);
                try
                {
                    float l = points[0].x, r = l, b = points[0].y, t = b;
                    foreach (var p in points)
                    {
                        l = Math.Min(l, p.x);
                        r = Math.Max(r, p.x);
                        b = Math.Min(b, p.y);
                        t = Math.Max(t, p.y);
                    }
                    ConfigureAnnot(a, new N.Rect(l - width, b - width, r + width, t + width), color);
                    Check(N.FPDFAnnot_SetBorder(a, 0, 0, width) != 0, UiText.Get("无法设置笔迹宽度。"));
                    Check(N.FPDFAnnot_AddInkStroke(a, points.ToArray(), new UIntPtr((uint)points.Count)) >= 0,
                          UiText.Get("无法写入手写笔迹。"));
                }
                finally
                {
                    if (a != IntPtr.Zero)
                        N.FPDFPage_CloseAnnot(a);
                }
            });
        }
        public void AddNote(float x, float y, string contents, Color color)
        {
            Edit(delegate {
                IntPtr a = N.FPDFPage_CreateAnnot(Page, 1);
                try
                {
                    ConfigureAnnot(a, new N.Rect(x, y - 22, x + 22, y), color);
                    Check(N.FPDFAnnot_SetStringValue(a, "Contents", contents) != 0, UiText.Get("无法写入便签。"));
                }
                finally
                {
                    if (a != IntPtr.Zero)
                        N.FPDFPage_CloseAnnot(a);
                }
            });
        }
        public void EditNote(Pick pick, string value)
        {
            Check(pick != null && pick.IsAnnotation && pick.Type == 1, UiText.Get("请选择便签。"));
            Edit(delegate {
                IntPtr a = N.FPDFPage_GetAnnot(Page, pick.AnnotIndex);
                try
                {
                    Check(N.FPDFAnnot_SetStringValue(a, "Contents", value) != 0, UiText.Get("无法更新便签。"));
                }
                finally
                {
                    if (a != IntPtr.Zero)
                        N.FPDFPage_CloseAnnot(a);
                }
            });
        }
        public void Rotate(int quarterTurns)
        {
            Edit(delegate { N.FPDFPage_SetRotation(Page, (N.FPDFPage_GetRotation(Page) + quarterTurns + 4) % 4); });
        }
        public void InsertBlank()
        {
            Edit(delegate {
                int after = Index + 1;
                ClosePage();
                IntPtr p = N.FPDFPage_New(Handle, after, 595.276, 841.89);
                Check(p != IntPtr.Zero, UiText.Get("无法插入空白页。"));
                N.FPDF_ClosePage(p);
                OpenPage(after);
            });
        }
        public void DeletePage()
        {
            Check(Count > 1, UiText.Get("至少需要保留一页。"));
            Edit(delegate {
                int index = Index;
                ClosePage();
                N.FPDFPage_Delete(Handle, index);
                OpenPage(Math.Min(index, Count - 1));
            });
        }
        public void DuplicatePage()
        {
            using (var source = FromBytes(Serialize(false), Password))
            {
                int index = Index;
                Edit(delegate {
                    ClosePage();
                    Check(N.FPDF_ImportPagesByIndex(Handle, source.Handle, new[] { index }, 1, index + 1) != 0,
                          UiText.Get("无法复制页面。"));
                    OpenPage(index + 1);
                });
            }
        }
        public void MovePage(int destination)
        {
            Check(destination >= 0 && destination < Count, UiText.Get("页码超出范围。"));
            int from = Index;
            if (from == destination)
                return;
            Edit(delegate {
                ClosePage();
                Check(N.FPDF_MovePages(Handle, new[] { from }, 1, destination) != 0, UiText.Get("无法移动页面。"));
                OpenPage(destination);
            });
        }
        public void Import(PdfDocument source, int destination)
        {
            int[] pages = new int[source.Count];
            for (int i = 0; i < pages.Length; i++)
                pages[i] = i;
            Edit(delegate {
                ClosePage();
                Check(N.FPDF_ImportPagesByIndex(Handle, source.Handle, pages, (uint)pages.Length, destination) != 0,
                      UiText.Get("无法合并页面。"));
                OpenPage(destination);
            });
        }
        public void Extract(int[] indices, string path)
        {
            using (var output = new PdfDocument())
            {
                output.Handle = N.FPDF_CreateNewDocument();
                Check(output.Handle != IntPtr.Zero, UiText.Get("无法创建导出文件。"));
                Check(N.FPDF_ImportPagesByIndex(output.Handle, Handle, indices, (uint)indices.Length, 0) != 0,
                      UiText.Get("页面提取失败。"));
                AtomicWrite(path, output.Serialize(true), "", indices.Length);
            }
        }
        public int CharAt(N.Pt p)
        {
            return N.FPDFText_GetCharIndexAtPos(TextPage, p.x, p.y, 5, 5);
        }
        public string SelectedText(int start, int count)
        {
            if (count <= 0 || start < 0)
                return "";
            count = Math.Min(count, N.FPDFText_CountChars(TextPage) - start);
            if (count <= 0)
                return "";
            var bytes = new byte[checked((count + 1) * 2)];
            int got = N.FPDFText_GetText(TextPage, start, count, bytes);
            return got <= 0 ? "" : Encoding.Unicode.GetString(bytes, 0, Math.Min(bytes.Length, got * 2)).TrimEnd('\0');
        }
        public List<N.Rect> TextRects(int start, int count)
        {
            var result = new List<N.Rect>();
            if (start < 0 || count < 1)
                return result;
            int total = N.FPDFText_CountRects(TextPage, start, count);
            for (int i = 0; i < Math.Min(total, 20000); i++)
            {
                double l, t, r, b;
                if (N.FPDFText_GetRect(TextPage, i, out l, out t, out r, out b) != 0)
                    result.Add(new N.Rect((float)l, (float)b, (float)r, (float)t));
            }
            return result;
        }
        public string AllText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Count; i++)
            {
                IntPtr page = i == Index ? Page : N.FPDF_LoadPage(Handle, i);
                if (page == IntPtr.Zero)
                    continue;
                IntPtr tp = N.FPDFText_LoadPage(page);
                try
                {
                    int n = N.FPDFText_CountChars(tp);
                    if (n > 0)
                    {
                        var b = new byte[checked((n + 1) * 2)];
                        int got = N.FPDFText_GetText(tp, 0, n, b);
                        if (got > 0)
                            sb.Append(Encoding.Unicode.GetString(b, 0, Math.Min(b.Length, got * 2)).TrimEnd('\0'));
                    }
                    sb.AppendLine();
                    sb.AppendLine();
                }
                finally
                {
                    if (tp != IntPtr.Zero)
                        N.FPDFText_ClosePage(tp);
                    if (page != Page)
                        N.FPDF_ClosePage(page);
                }
            }
            return sb.ToString();
        }
        public SearchHit Find(string query, int fromPage, int fromChar, bool wrap)
        {
            if (String.IsNullOrEmpty(query))
                return null;
            for (int step = 0; step < Count + (wrap ? 1 : 0); step++)
            {
                int index = (fromPage + step) % Count;
                if (!wrap && fromPage + step >= Count)
                    break;
                IntPtr p = index == Index ? Page : N.FPDF_LoadPage(Handle, index);
                if (p == IntPtr.Zero)
                    continue;
                IntPtr tp = N.FPDFText_LoadPage(p), search = IntPtr.Zero;
                try
                {
                    search = N.FPDFText_FindStart(tp, query, 0, step == 0 ? Math.Max(0, fromChar) : 0);
                    if (search != IntPtr.Zero && N.FPDFText_FindNext(search) != 0)
                    {
                        int start = N.FPDFText_GetSchResultIndex(search), count = N.FPDFText_GetSchCount(search);
                        if (step == Count && start >= fromChar)
                            return null;
                        var rects = new List<N.Rect>();
                        int nr = N.FPDFText_CountRects(tp, start, count);
                        for (int j = 0; j < nr; j++)
                        {
                            double l, t, r, b;
                            N.FPDFText_GetRect(tp, j, out l, out t, out r, out b);
                            rects.Add(new N.Rect((float)l, (float)b, (float)r, (float)t));
                        }
                        return new SearchHit { Page = index, Start = start, Count = count, Rectangles = rects };
                    }
                }
                finally
                {
                    if (search != IntPtr.Zero)
                        N.FPDFText_FindClose(search);
                    if (tp != IntPtr.Zero)
                        N.FPDFText_ClosePage(tp);
                    if (p != Page)
                        N.FPDF_ClosePage(p);
                }
            }
            return null;
        }
        public List<Bookmark> Bookmarks()
        {
            var seen = new HashSet<IntPtr>();
            return BookmarkChildren(IntPtr.Zero, seen, 0);
        }
        List<Bookmark> BookmarkChildren(IntPtr parent, HashSet<IntPtr> seen, int depth)
        {
            var result = new List<Bookmark>();
            if (depth > 24)
                return result;
            IntPtr b = N.FPDFBookmark_GetFirstChild(Handle, parent);
            while (b != IntPtr.Zero && seen.Count < 10000 && seen.Add(b))
            {
                uint n = N.FPDFBookmark_GetTitle(b, null, 0);
                string title = "";
                if (n > 1 && n < 131072)
                {
                    var bytes = new byte[n];
                    N.FPDFBookmark_GetTitle(b, bytes, n);
                    title = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
                }
                IntPtr dest = N.FPDFBookmark_GetDest(Handle, b);
                if (dest == IntPtr.Zero)
                    dest = N.FPDFAction_GetDest(Handle, N.FPDFBookmark_GetAction(b));
                result.Add(new Bookmark { Title = title,
                                          Page = dest == IntPtr.Zero ? -1 : N.FPDFDest_GetDestPageIndex(Handle, dest),
                                          Children = BookmarkChildren(b, seen, depth + 1) });
                b = N.FPDFBookmark_GetNextSibling(Handle, b);
            }
            return result;
        }
        public string LinkAt(N.Pt point, out int page)
        {
            page = -1;
            IntPtr link = N.FPDFLink_GetLinkAtPoint(Page, point.x, point.y);
            if (link == IntPtr.Zero)
                return null;
            IntPtr dest = N.FPDFLink_GetDest(Handle, link), a = N.FPDFLink_GetAction(link);
            if (dest == IntPtr.Zero)
                dest = N.FPDFAction_GetDest(Handle, a);
            if (dest != IntPtr.Zero)
            {
                page = N.FPDFDest_GetDestPageIndex(Handle, dest);
                return null;
            }
            if (a == IntPtr.Zero || N.FPDFAction_GetType(a) != 3)
                return null;
            uint len = N.FPDFAction_GetURIPath(Handle, a, null, 0);
            if (len < 2 || len > 65536)
                return null;
            var bytes = new byte[len];
            N.FPDFAction_GetURIPath(Handle, a, bytes, len);
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }
        public static int[] ParsePages(string input, int max)
        {
            var result = new List<int>();
            var seen = new HashSet<int>();
            foreach (string item in input.Replace('，', ',').Split(','))
            {
                string[] range = item.Trim().Split('-');
                int first, last;
                if (range.Length < 1 || range.Length > 2 || !int.TryParse(range[0].Trim(), out first))
                    throw new FormatException(UiText.Get("页码格式示例：1,3-5,8"));
                last = first;
                if (range.Length == 2 && !int.TryParse(range[1].Trim(), out last))
                    throw new FormatException(UiText.Get("页码范围无效。"));
                if (first < 1 || last > max || last < first)
                    throw new FormatException(UiText.Get("页码必须在 1 到 ") + max +
                                              UiText.Get(" 之间，并按小到大填写区间。"));
                for (int p = first; p <= last; p++)
                    if (seen.Add(p - 1))
                        result.Add(p - 1);
            }
            Check(result.Count > 0, UiText.Get("请选择至少一页。"));
            return result.ToArray();
        }
    }
}
