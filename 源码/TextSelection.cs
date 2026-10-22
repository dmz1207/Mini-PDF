using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MiniPdf
{
    internal struct TextRange
    {
        internal int Page, Start, Count;
    }
    // Each cached text page owns its handles. No PDFium handle crosses threads.
    internal sealed class PdfTextPage : IDisposable
    {
        internal readonly IntPtr Page, Text;
        internal readonly int Count;
        List<KeyValuePair<int, N.Rect>> boxes;
        internal PdfTextPage(IntPtr document, int index)
        {
            Page = N.FPDF_LoadPage(document, index);
            PdfDocument.Check(Page != IntPtr.Zero, UiText.Get("无法读取页面。"));
            Text = N.FPDFText_LoadPage(Page);
            Count = Text == IntPtr.Zero ? 0 : Math.Max(0, N.FPDFText_CountChars(Text));
        }
        internal N.Pt ToPage(Point point, int width, int height, int rotation)
        {
            double x, y;
            PdfDocument.Check(N.FPDF_DeviceToPage(Page, 0, 0, width, height, rotation, point.X, point.Y, out x, out y) != 0,
                              UiText.Get("坐标转换失败。"));
            return new N.Pt((float)x, (float)y);
        }
        internal Point ToDevice(float x, float y, int width, int height, int rotation)
        {
            int dx, dy;
            N.FPDF_PageToDevice(Page, 0, 0, width, height, rotation, x, y, out dx, out dy);
            return new Point(dx, dy);
        }
        internal int Hit(N.Pt point, bool nearest)
        {
            int direct = N.FPDFText_GetCharIndexAtPos(Text, point.x, point.y, 5, 5);
            if (direct >= 0) return direct;
            if (boxes == null)
            {
                boxes = new List<KeyValuePair<int, N.Rect>>();
                for (int i = 0; i < Math.Min(Count, 200000); i++)
                {
                    uint character = N.FPDFText_GetUnicode(Text, i);
                    N.Rect r;
                    if (character <= 32 || N.FPDFText_GetLooseCharBox(Text, i, out r) == 0) continue;
                    if (r.right > r.left && r.top > r.bottom)
                        boxes.Add(new KeyValuePair<int, N.Rect>(i, r));
                }
            }
            double best = Double.MaxValue;
            int result = -1;
            foreach (var entry in boxes)
            {
                var r = entry.Value;
                double dx = Math.Max(0, Math.Max(r.left - point.x, point.x - r.right));
                double dy = Math.Max(0, Math.Max(r.bottom - point.y, point.y - r.top));
                if (!nearest && (dx > .5 || dy > .5)) continue;
                double distance = dx * dx + dy * dy * 16;
                if (distance < best) { best = distance; result = entry.Key; }
            }
            return result;
        }
        internal string Read(int start, int count)
        {
            start = Math.Max(0, start);
            count = Math.Min(count, Count - start);
            if (count <= 0) return "";
            byte[] bytes = new byte[checked((count + 1) * 2)];
            int got = N.FPDFText_GetText(Text, start, count, bytes);
            return got <= 0 ? "" : Encoding.Unicode.GetString(bytes, 0, Math.Min(bytes.Length, got * 2)).TrimEnd((char)0);
        }
        internal List<N.Rect> Rectangles(int start, int count)
        {
            var rectangles = new List<N.Rect>();
            int total = N.FPDFText_CountRects(Text, start, count);
            for (int i = 0; i < Math.Min(total, 20000); i++)
            {
                double l, t, r, b;
                if (N.FPDFText_GetRect(Text, i, out l, out t, out r, out b) != 0)
                    rectangles.Add(new N.Rect((float)l, (float)b, (float)r, (float)t));
            }
            return rectangles;
        }
        public void Dispose()
        {
            if (Text != IntPtr.Zero) N.FPDFText_ClosePage(Text);
            N.FPDF_ClosePage(Page);
        }
    }
    internal sealed partial class PdfDocument
    {
        readonly Dictionary<int, PdfTextPage> selectionPages = new Dictionary<int, PdfTextPage>();
        readonly Queue<int> selectionOrder = new Queue<int>();
        internal PdfTextPage SelectionPage(int index)
        {
            PdfTextPage page;
            if (selectionPages.TryGetValue(index, out page)) return page;
            page = new PdfTextPage(Handle, index);
            selectionPages.Add(index, page);
            selectionOrder.Enqueue(index);
            while (selectionPages.Count > 4)
            {
                int old = selectionOrder.Dequeue();
                selectionPages[old].Dispose();
                selectionPages.Remove(old);
            }
            return page;
        }
        void ClearSelectionPages()
        {
            foreach (var page in selectionPages.Values) page.Dispose();
            selectionPages.Clear();
            selectionOrder.Clear();
        }
        internal void AddTextMarkup(List<TextRange> ranges, int subtype, Color color)
        {
            var rectangles = new List<KeyValuePair<int, List<N.Rect>>>();
            foreach (var range in ranges)
            {
                var shapes = SelectionPage(range.Page).Rectangles(range.Start, range.Count);
                if (shapes.Count > 0) rectangles.Add(new KeyValuePair<int, List<N.Rect>>(range.Page, shapes));
            }
            Check(rectangles.Count > 0, UiText.Get("请框选需要标记的位置。"));
            int original = Index;
            Edit(delegate {
                foreach (var page in rectangles) { OpenPage(page.Key); AppendMarkup(page.Value, subtype, color); }
                OpenPage(original);
            });
        }
    }
    internal sealed partial class PdfCanvas
    {
        int textAnchorPage = -1, textEndPage = -1, textEndChar = -1;
        System.Windows.Forms.Timer selectionScroll;
        internal List<TextRange> TextRanges(int first, int end)
        {
            var result = new List<TextRange>();
            if (Doc == null || TextStart < 0 || TextCount < 1) return result;
            if (textAnchorPage < 0)
            {
                if (Doc.Index >= first && Doc.Index < end)
                    result.Add(new TextRange { Page = Doc.Index, Start = TextStart, Count = TextCount });
                return result;
            }
            int aPage = textAnchorPage, aChar = startChar, bPage = textEndPage, bChar = textEndChar;
            if (aPage > bPage || aPage == bPage && aChar > bChar)
            {
                int swap = aPage; aPage = bPage; bPage = swap;
                swap = aChar; aChar = bChar; bChar = swap;
            }
            for (int page = Math.Max(first, aPage); page <= bPage && page < end; page++)
            {
                int count = Doc.SelectionPage(page).Count;
                int start = page == aPage ? Math.Max(0, aChar) : 0;
                int stop = page == bPage ? Math.Min(count, bChar + 1) : count;
                if (stop > start) result.Add(new TextRange { Page = page, Start = start, Count = stop - start });
            }
            return result;
        }
        internal bool PreserveTextSelection()
        {
            if (TextStart < 0 || TextCount < 1) return false;
            if (textAnchorPage < 0)
            {
                textAnchorPage = textEndPage = Doc.Index;
                startChar = TextStart; textEndChar = TextStart + TextCount - 1;
            }
            return true;
        }
        internal List<TextRange> TextRanges() { return TextRanges(0, Doc == null ? 0 : Doc.Count); }
        void BeginTextSelection()
        {
            textAnchorPage = textEndPage = Doc.Index;
            startChar = Doc.SelectionPage(Doc.Index).Hit(startPoint, false);
            textEndChar = startChar;
            TextStart = startChar;
            TextCount = startChar < 0 ? 0 : 1;
        }
        int SelectionTarget(Point point)
        {
            int target = HitPage(point);
            if (target >= 0) return target;
            int first, end;
            VisibleRange(ClientRectangle, out first, out end);
            long best = Int64.MaxValue;
            target = Doc.Index;
            for (int i = first; i < end; i++)
            {
                var r = ScreenRect(pageRects[i]);
                if (r.IsEmpty) continue;
                long dx = Math.Max(0, Math.Max(r.Left - point.X, point.X - r.Right));
                long dy = Math.Max(0, Math.Max(r.Top - point.Y, point.Y - r.Bottom));
                long distance = dx * dx + dy * dy;
                if (distance < best) { best = distance; target = i; }
            }
            return target;
        }
        void UpdateTextSelection(Point point)
        {
            if (Math.Abs(point.X - origin.X) + Math.Abs(point.Y - origin.Y) < 3) return;
            if (startChar < 0)
            {
                startChar = Doc.SelectionPage(textAnchorPage).Hit(startPoint, true);
                if (startChar < 0) startChar = 0;
            }
            textEndPage = SelectionTarget(point);
            var r = ScreenRect(pageRects[textEndPage]);
            var text = Doc.SelectionPage(textEndPage);
            var position = text.ToPage(new Point(point.X - r.X, point.Y - r.Y), r.Width, r.Height, ViewRotation);
            textEndChar = text.Hit(position, true);
            TextStart = startChar;
            TextCount = textAnchorPage == textEndPage ? Math.Abs(textEndChar - startChar) + 1 : 1;
            if (textAnchorPage == textEndPage) TextStart = Math.Min(startChar, textEndChar);
            if (selectionScroll == null)
            {
                selectionScroll = new System.Windows.Forms.Timer { Interval = 30 };
                selectionScroll.Tick += delegate { SelectionScrollTick(); };
            }
            if (point.Y < 20 || point.Y > ClientSize.Height - 20) selectionScroll.Start();
            else selectionScroll.Stop();
        }
        void SelectionScrollTick()
        {
            if (!down || Doc == null || Mode != Tool.Read) { StopSelectionScroll(); return; }
            int delta = current.Y < 20 ? -24 : current.Y > ClientSize.Height - 20 ? 24 : 0;
            if (delta == 0) { StopSelectionScroll(); return; }
            int before = AutoScrollPosition.Y;
            AutoScrollPosition = new Point(-AutoScrollPosition.X, Math.Max(0, -before + delta));
            if (AutoScrollPosition.Y != before) { UpdateTextSelection(current); Invalidate(); }
        }
        void StopSelectionScroll() { if (selectionScroll != null) selectionScroll.Stop(); }
        Rectangle SelectionRectangle(int index, N.Rect shape)
        {
            var page = ScreenRect(pageRects[index]);
            var text = Doc.SelectionPage(index);
            Point a = text.ToDevice(shape.left, shape.top, page.Width, page.Height, ViewRotation),
                  b = text.ToDevice(shape.right, shape.bottom, page.Width, page.Height, ViewRotation);
            return new Rectangle(page.X + Math.Min(a.X, b.X), page.Y + Math.Min(a.Y, b.Y),
                                 Math.Max(1, Math.Abs(b.X - a.X)), Math.Max(1, Math.Abs(b.Y - a.Y)));
        }
        void PaintTextSelection(Graphics graphics)
        {
            int first, end;
            VisibleRange(ClientRectangle, out first, out end);
            using (var brush = new SolidBrush(Color.FromArgb(75, 48, 129, 220)))
                foreach (var range in TextRanges(first, end))
                    foreach (var shape in Doc.SelectionPage(range.Page).Rectangles(range.Start, range.Count))
                        graphics.FillRectangle(brush, SelectionRectangle(range.Page, shape));
        }
        string SelectionText()
        {
            if (Doc == null) return "";
            var text = new StringBuilder();
            foreach (var range in TextRanges())
            {
                string part = Doc.SelectionPage(range.Page).Read(range.Start, range.Count).TrimEnd('\r', '\n');
                if (part.Length == 0) continue;
                if (text.Length > 0) text.Append("\r\n");
                text.Append(part);
            }
            return text.ToString();
        }
    }
}
