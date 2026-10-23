using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniPdf
{
    internal static partial class N
    {
        [DllImport(D)] internal static extern IntPtr FPDFTextObj_GetFont(IntPtr obj);
        [DllImport(D)] internal static extern int FPDFFont_GetIsEmbedded(IntPtr font);
        [DllImport(D)] internal static extern int FPDFPageObj_GetStrokeColor(IntPtr obj, out uint r, out uint g, out uint b, out uint a);
        [DllImport(D)] internal static extern UIntPtr FPDFFont_GetBaseFontName(IntPtr font, byte[] name, UIntPtr length);
        [DllImport(D)] internal static extern int FPDFFont_GetGlyphWidth(IntPtr font, uint unicode, float size, out float width);
        [DllImport(D)] internal static extern int FPDFTextObj_SetFontSize(IntPtr obj, float size);
        [DllImport(D)] internal static extern int FPDFText_SetPositions(IntPtr obj, float[] positions, UIntPtr count);
        [DllImport(D)] internal static extern int FPDFText_GetCharOrigin(IntPtr page, int character, out double x, out double y);
        [DllImport(D)] internal static extern IntPtr FPDFPageObj_GetClipPath(IntPtr obj);
        [DllImport(D)] internal static extern int FPDFClipPath_CountPaths(IntPtr clip);
        [DllImport(D, CharSet = CharSet.Ansi)] internal static extern int FPDFPageObjMark_GetParamValueType(IntPtr mark, string key);
    }
    internal sealed class TextEditOptions
    {
        internal string Text, Face = "保留原字体";
        internal float Size, Width, LineHeight = 1.4f;
        internal Color Color;
        internal bool Wrap, ChangeColor;
        internal N.Rect ResultBounds;
        internal N.Rect[] PageBounds;
    }
    internal sealed class TextEditSelection
    {
        internal int Page, Epoch, Anchor, Scope;
        internal int[] Indices;
        internal string[] Originals;
        internal string Text, FontName;
        internal float Size, Width, Scale, LineHeight = 1.4f;
        internal Color Color;
        internal bool Ocr, Restructure, ChangeFont;
        internal float SizeScale = 1;
        internal SelectedTextData Range;
        internal TextEditSelection[] Children;
        internal N.Rect Bounds;
    }
    internal sealed partial class PdfDocument
    {
        sealed class TextRun
        {
            internal int Index, Mode;
            internal IntPtr Object, Font;
            internal string Text, FontName;
            internal N.Matrix Matrix;
            internal float Size, Width, X, Y;
            internal Color Color, Fill, Stroke;
            internal bool Safe, Basic, Vertical;
            internal N.Rect Bounds;
        }
        internal static N.Rect UnionTextBounds(N.Rect a, N.Rect b)
        { return new N.Rect(Math.Min(a.left, b.left), Math.Min(a.bottom, b.bottom), Math.Max(a.right, b.right), Math.Max(a.top, b.top)); }
        static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
        static bool Near(float a, float b) { return Math.Abs(a - b) < .001f; }
        internal static string TextFontName(IntPtr font)
        {
            ulong length = N.FPDFFont_GetBaseFontName(font, null, UIntPtr.Zero).ToUInt64();
            if (length < 1 || length > 4096) return "PDF font";
            var bytes = new byte[(int)length];
            N.FPDFFont_GetBaseFontName(font, bytes, new UIntPtr(length));
            return Encoding.UTF8.GetString(bytes).TrimEnd((char)0);
        }
        static float GlyphWidth(IntPtr font, char ch, float size)
        {
            float width;
            Check(N.FPDFFont_GetGlyphWidth(font, ch, size, out width) != 0 && Finite(width) && width >= 0 && width < 100000,
                  UiText.Get("无法读取原字体的字宽，请选择其他字体。"));
            return width;
        }
        static bool BasicTextState(IntPtr obj)
        {
            IntPtr clip = N.FPDFPageObj_GetClipPath(obj);
            return N.FPDFPageObj_CountMarks(obj) == 0 &&
                   // A valid object without clip data has a non-null wrapper and CountPaths == -1.
                   // A referenced clip with zero geometric paths can still contain text clipping.
                   (clip == IntPtr.Zero || N.FPDFClipPath_CountPaths(clip) < 0);
        }
        TextRun ReadTextRun(int index)
        {
            IntPtr obj = N.FPDFPage_GetObject(Page, index);
            if (obj == IntPtr.Zero || N.FPDFPageObj_GetType(obj) != 1) return null;
            var run = new TextRun { Index = index, Object = obj, Text = ObjectText(obj), Mode = N.FPDFTextObj_GetTextRenderMode(obj) };
            if (run.Text.Length == 0 || run.Text.Length > 20000) return null;
            Check(N.FPDFPageObj_GetMatrix(obj, out run.Matrix) != 0 && N.FPDFTextObj_GetFontSize(obj, out run.Size) != 0,
                  UiText.Get("无法读取文字位置。"));
            Check(Finite(run.Matrix.a) && Finite(run.Matrix.b) && Finite(run.Matrix.c) && Finite(run.Matrix.d) &&
                  Finite(run.Matrix.e) && Finite(run.Matrix.f), UiText.Get("文字变换不支持编辑。"));
            Check(run.Size >= .1f && run.Size <= 1000 && Finite(run.Size), UiText.Get("文字字号不支持编辑。"));
            run.Font = N.FPDFTextObj_GetFont(obj);
            Check(run.Font != IntPtr.Zero, UiText.Get("无法读取原字体。"));
            run.FontName = TextFontName(run.Font);
            uint r, g, b, a;
            run.Fill = N.FPDFPageObj_GetFillColor(obj, out r, out g, out b, out a) != 0
                ? Color.FromArgb((int)a, (int)r, (int)g, (int)b) : Color.Black;
            run.Stroke = N.FPDFPageObj_GetStrokeColor(obj, out r, out g, out b, out a) != 0
                ? Color.FromArgb((int)a, (int)r, (int)g, (int)b) : Color.Black;
            run.Color = run.Mode == 1 ? run.Stroke : run.Fill;
            run.Basic = BasicTextState(obj); run.Safe = run.Basic && run.Mode == 0;
            var bounds = ObjectBounds(obj); run.Bounds = bounds;
            Check(Finite(bounds.left) && Finite(bounds.right) && Finite(bounds.top) && Finite(bounds.bottom),
                  UiText.Get("文字变换不支持编辑。"));
            float scale = (float)Math.Sqrt(run.Matrix.a * run.Matrix.a + run.Matrix.b * run.Matrix.b);
            if (scale > .0001f)
                run.Width = Math.Max(1, (Math.Abs(run.Matrix.a) * (bounds.right - bounds.left) +
                    Math.Abs(run.Matrix.b) * (bounds.top - bounds.bottom)) / (scale * scale));
            return run;
        }
        static bool Project(N.Matrix basis, float x, float y, out float px, out float py)
        {
            float det = basis.a * basis.d - basis.b * basis.c;
            px = py = 0;
            if (!Finite(det) || Math.Abs(det) < .000001f) return false;
            x -= basis.e; y -= basis.f;
            px = (basis.d * x - basis.c * y) / det;
            py = (-basis.b * x + basis.a * y) / det;
            return Finite(px) && Finite(py);
        }
        static string JoinTextRuns(List<TextRun> row)
        {
            row.Sort((a, b) => a.X.CompareTo(b.X));
            var text = new StringBuilder(); TextRun previous = null;
            foreach (var run in row)
            {
                if (previous != null && text.Length > 0 && !Char.IsWhiteSpace(text[text.Length - 1]) &&
                    !Char.IsWhiteSpace(run.Text[0]) && run.X - previous.X - previous.Width > run.Size * .12f)
                    text.Append(' ');
                text.Append(run.Text); previous = run;
            }
            return text.ToString();
        }
        internal TextEditSelection SelectTextEdit(Pick pick, int scope)
        {
            Check(pick != null && !pick.IsAnnotation && pick.Type == 1, UiText.Get("请选择一个文字对象。"));
            var anchor = ReadTextRun(pick.ObjectIndex);
            Check(anchor != null, UiText.Get("无法读取可编辑文字。"));
            var rows = new List<List<TextRun>> { new List<TextRun> { anchor } };
            var basis = anchor.Matrix;
            ReadTextOrigins(anchor);
            if (scope > 0 && anchor.Safe && !pick.IsOcrText && !anchor.Vertical)
            {
                int count = N.FPDFPage_CountObjects(Page);
                Check(count <= 2000, UiText.Get("此页对象较多，请按单个文字对象编辑。"));
                var candidates = new List<TextRun>();
                for (int i = 0; i < count; i++)
                {
                    var run = i == anchor.Index ? anchor : ReadTextRun(i);
                    if (run == null || !run.Safe || run.Font != anchor.Font || !Near(run.Size, anchor.Size) ||
                        run.Color != anchor.Color || !Near(run.Matrix.a, basis.a) || !Near(run.Matrix.b, basis.b) ||
                        !Near(run.Matrix.c, basis.c) || !Near(run.Matrix.d, basis.d) ||
                        !Project(basis, run.Matrix.e, run.Matrix.f, out run.X, out run.Y)) continue;
                    candidates.Add(run);
                }
                var line = new List<TextRun> { anchor };
                ExpandLine(line, candidates, anchor.Size);
                rows[0] = line;
                if (scope == 2)
                {
                    for (int direction = -1; direction <= 1; direction += 2)
                    {
                        var current = line; float expectedGap = 0;
                        for (int step = 0; step < 15; step++)
                        {
                            float y = current[0].Y, best = Single.MaxValue;
                            TextRun next = null;
                            float left = line[0].X, right = line[line.Count - 1].X + line[line.Count - 1].Width;
                            foreach (var run in candidates)
                            {
                                float gap = (run.Y - y) * direction;
                                if (gap < anchor.Size * .8f || gap > anchor.Size * 1.8f || gap >= best ||
                                    run.X < left - anchor.Size * .6f || run.X > right - Math.Min(anchor.Size, (right - left) * .3f)) continue;
                                best = gap; next = run;
                            }
                            if (next == null || expectedGap > 0 && Math.Abs(best - expectedGap) > anchor.Size * .12f) break;
                            expectedGap = best;
                            var nextLine = new List<TextRun> { next };
                            ExpandLine(nextLine, candidates, anchor.Size);
                            float nextLeft = nextLine[0].X, nextRight = nextLine[nextLine.Count - 1].X + nextLine[nextLine.Count - 1].Width;
                            if (Math.Abs(nextLeft - left) > anchor.Size * .6f ||
                                Math.Min(nextRight, right) - Math.Max(nextLeft, left) < Math.Min(right - left, nextRight - nextLeft) * .6f) break;
                            if (rows.Exists(r => r.Contains(next))) break;
                            rows.Add(nextLine); current = nextLine;
                        }
                    }
                }
            }
            else scope = 0;
            rows.Sort((a, b) => b[0].Y.CompareTo(a[0].Y));
            var indices = new List<int>(); var originals = new List<string>(); var content = new StringBuilder();
            N.Rect allBounds = anchor.Bounds;
            float width = 1, leftMost = Single.MaxValue, rightMost = Single.MinValue;
            foreach (var row in rows)
            {
                if (content.Length > 0) content.Append('\n');
                content.Append(JoinTextRuns(row));
                foreach (var run in row)
                {
                    indices.Add(run.Index); originals.Add(run.Text);
                    allBounds = UnionTextBounds(allBounds, run.Bounds);
                    leftMost = Math.Min(leftMost, run.X); rightMost = Math.Max(rightMost, run.X + run.Width);
                }
            }
            Check(indices.Count <= 256 && content.Length <= 20000, UiText.Get("文字块较大，请缩小编辑范围。"));
            width = Math.Max(width, rightMost - leftMost);
            var first = rows[0][0];
            if (indices.Count == 1)
            {
                var origins = ReadTextOrigins(first);
                if (origins != null) width = Math.Max(width, origins[origins.Length - 1] + GlyphWidth(first.Font, first.Text[first.Text.Length - 1], first.Size));
            }
            float scaleX = (float)Math.Sqrt(first.Matrix.a * first.Matrix.a + first.Matrix.b * first.Matrix.b);
            Check(scaleX > .0001f && Finite(scaleX), UiText.Get("文字变换不支持编辑。"));
            var selection = new TextEditSelection { Page = Index, Epoch = RenderEpoch, Anchor = first.Index, Scope = scope,
                Indices = indices.ToArray(), Originals = originals.ToArray(), Text = content.ToString(), FontName = first.FontName,
                Size = first.Size, SizeScale = (float)Math.Sqrt(first.Matrix.c * first.Matrix.c + first.Matrix.d * first.Matrix.d),
                Width = width, Scale = scaleX, Color = first.Color, Ocr = pick.IsOcrText, Restructure = first.Safe && !first.Vertical, ChangeFont = first.Basic && !first.Vertical && (first.Mode == 0 || first.Mode == 3), Bounds = allBounds };
            if (rows.Count > 1) selection.LineHeight = Math.Max(.8f, Math.Min(3, Math.Abs(rows[0][0].Y - rows[1][0].Y) / first.Size));
            return selection;
        }
        static void ExpandLine(List<TextRun> row, List<TextRun> candidates, float size)
        {
            bool changed;
            do
            {
                changed = false; row.Sort((a, b) => a.X.CompareTo(b.X));
                float left = row[0].X, right = row[row.Count - 1].X + row[row.Count - 1].Width;
                foreach (var run in candidates)
                {
                    if (row.Contains(run) || Math.Abs(run.Y - row[0].Y) > size * .15f) continue;
                    float gap = run.X >= right ? run.X - right : left - run.X - run.Width;
                    if (gap < -.2f || gap > size * 1.2f) continue;
                    row.Add(run); changed = true;
                }
            } while (changed && row.Count <= 256);
            row.Sort((a, b) => a.X.CompareTo(b.X));
        }
        float[] ReadTextOrigins(TextRun run)
        {
            var values = new List<float>(); var text = new StringBuilder(); float? baseline = null;
            int count = N.FPDFText_CountChars(TextPage);
            if (count > 200000) return null;
            for (int i = 0; i < count; i++)
            {
                if (N.FPDFText_GetTextObject(TextPage, i) != run.Object) continue;
                uint unicode = N.FPDFText_GetUnicode(TextPage, i);
                double x, y; float px, py;
                if (unicode == 0 || unicode > 65535 || N.FPDFText_GetCharOrigin(TextPage, i, out x, out y) == 0 ||
                    !Project(run.Matrix, (float)x, (float)y, out px, out py)) return null;
                if (baseline.HasValue && Math.Abs(py - baseline.Value) > .01f) { run.Vertical = true; return null; }
                baseline = py; values.Add(px); text.Append((char)unicode);
            }
            if (text.ToString() != run.Text || values.Count < 2 || Math.Abs(values[0]) > .01f) return null;
            for (int i = 1; i < values.Count; i++) if (values[i] < values[i - 1]) return null;
            return values.ToArray();
        }
        void CheckOriginalCharacters(TextRun run, string text)
        {
            var known = new HashSet<char>(run.Text);
            int count = N.FPDFText_CountChars(TextPage);
            if (count <= 200000)
                for (int i = 0; i < count; i++)
                {
                    IntPtr obj = N.FPDFText_GetTextObject(TextPage, i);
                    if (obj != IntPtr.Zero && N.FPDFTextObj_GetFont(obj) == run.Font)
                    {
                        uint unicode = N.FPDFText_GetUnicode(TextPage, i);
                        if (unicode > 0 && unicode <= 65535) known.Add((char)unicode);
                    }
                }
            string name = run.FontName;
            bool standard = name == "Helvetica" || name == "Helvetica-Bold" || name == "Helvetica-Oblique" || name == "Helvetica-BoldOblique" ||
                name == "Times-Roman" || name == "Times-Bold" || name == "Times-Italic" || name == "Times-BoldItalic" ||
                name == "Courier" || name == "Courier-Bold" || name == "Courier-Oblique" || name == "Courier-BoldOblique";
            standard = standard && N.FPDFFont_GetIsEmbedded(run.Font) == 0;
            foreach (char ch in text)
                Check(Char.IsWhiteSpace(ch) || known.Contains(ch) || standard && ch >= 32 && ch <= 126,
                    UiText.Get("无法确认原字体包含新增字符 U+") + ((int)ch).ToString("X4") +
                    UiText.Get("。请选择自动或系统字体，并核对预览。"));
        }
        static float Tracking(TextRun run, float[] origins)
        {
            if (origins == null) return 0;
            var gaps = new List<float>();
            for (int i = 1; i < origins.Length; i++)
            {
                float gap = origins[i] - origins[i - 1] - GlyphWidth(run.Font, run.Text[i - 1], run.Size);
                if (Math.Abs(gap) <= run.Size) gaps.Add(gap);
            }
            if (gaps.Count == 0) return 0;
            gaps.Sort(); return gaps[gaps.Count / 2];
        }
        static float[] TextPositions(string text, IntPtr font, float size, TextRun original, float[] origins, float tracking)
        {
            if (text.Length < 2) return new float[0];
            int prefix = 0, suffix = 0;
            if (origins != null)
            {
                while (prefix < text.Length && prefix < original.Text.Length && text[prefix] == original.Text[prefix]) prefix++;
                while (suffix < text.Length - prefix && suffix < original.Text.Length - prefix &&
                    text[text.Length - 1 - suffix] == original.Text[original.Text.Length - 1 - suffix]) suffix++;
            }
            var positions = new float[text.Length - 1]; float x = 0;
            for (int i = 1; i < text.Length; i++)
            {
                float gap = tracking;
                int old = i < prefix ? i : i > text.Length - suffix ? i + original.Text.Length - text.Length : -1;
                if (old > 0 && origins != null)
                    gap = (origins[old] - origins[old - 1] - GlyphWidth(original.Font, original.Text[old - 1], original.Size)) * size / original.Size;
                x += GlyphWidth(font, text[i - 1], size) + gap;
                Check(Finite(x) && x >= 0 && x < 100000, UiText.Get("字距超出可编辑范围。"));
                positions[i - 1] = x;
            }
            return positions;
        }
        static List<string> TextLines(string text, IntPtr font, float size, float tracking, bool wrap, float width)
        {
            var lines = new List<string>();
            foreach (string paragraph in text.Replace("\r", "").Replace("\t", "    ").Split('\n'))
            {
                if (!wrap || paragraph.Length == 0) { lines.Add(paragraph); continue; }
                int start = 0;
                while (start < paragraph.Length)
                {
                    int end = start, space = -1; float x = 0;
                    while (end < paragraph.Length)
                    {
                        float advance = GlyphWidth(font, paragraph[end], size) + (end == start ? 0 : tracking);
                        if (x + advance > width + .01f) break;
                        x += advance; if (Char.IsWhiteSpace(paragraph[end])) space = end; end++;
                    }
                    Check(end > start, UiText.Get("文本框太窄，无法容纳一个字符。"));
                    if (end < paragraph.Length && space > start) end = space + 1;
                    if (end < paragraph.Length && end > start + 1 && "，。！？；：、）》】”’".IndexOf(paragraph[end]) >= 0) end--;
                    lines.Add(paragraph.Substring(start, end - start)); start = end;
                    Check(lines.Count <= 400, UiText.Get("换行过多，请增大文本框宽度。"));
                }
            }
            return lines;
        }
        internal bool ApplyTextEdit(TextEditSelection selection, TextEditOptions options, bool preview = false)
        {
            Check(selection != null && options != null && (preview || (selection.Children != null || selection.Page == Index) && selection.Epoch == RenderEpoch),
                  UiText.Get("文字已变化，请重新选择。"));
            Check(options.Text != null && (selection.Range != null || selection.Children != null || !String.IsNullOrWhiteSpace(options.Text)) && options.Text.Length <= 20000 &&
                  Finite(options.Size) && options.Size >= .1f && options.Size <= 1000, UiText.Get("请输入有效文字和字号。"));
            Check(!options.Wrap || Finite(options.Width) && options.Width >= 1 && options.Width <= 14400,
                  UiText.Get("请输入有效文本框宽度。"));
            Check(Finite(options.LineHeight) && options.LineHeight >= .8f && options.LineHeight <= 3,
                  UiText.Get("请输入有效行距。"));
            foreach (char ch in options.Text)
                Check(!Char.IsSurrogate(ch) && !(ch >= 0x590 && ch <= 0x10ff) && !(ch >= 0x1700 && ch <= 0x18af) &&
                      !(ch >= 0xa800 && ch <= 0xabff) && !(ch >= 0xfb1d && ch <= 0xfdff) &&
                      Char.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark &&
                      Char.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.SpacingCombiningMark &&
                      Char.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.EnclosingMark &&
                      !(ch >= 0xfe70 && ch <= 0xfeff) && (!Char.IsControl(ch) || ch == '\n' || ch == '\r' || ch == '\t'),
                      UiText.Get("此文字需要暂未支持的复杂排版，请保留原文或使用其他编辑器。"));
            if (selection.Range != null || selection.Children != null) return ApplySelectedText(selection,options,preview);
            var runs = new List<TextRun>();
            for (int i = 0; i < selection.Indices.Length; i++)
            {
                var run = ReadTextRun(selection.Indices[i]);
                Check(run != null && run.Text == selection.Originals[i], UiText.Get("文字已变化，请重新选择。"));
                runs.Add(run);
            }
            var first = runs[0];
            bool originalFont = options.Face == "保留原字体";
            bool sameColor = !options.ChangeColor && (options.Color.ToArgb() & 0xffffff) == (first.Color.ToArgb() & 0xffffff);
            string requested = options.Text.Replace("\r", "").Replace("\t", "    ");
            options.ResultBounds = selection.Bounds;
            if (originalFont && !options.Wrap && requested == selection.Text.Replace("\r", "") && Near(options.Size, first.Size) && sameColor &&
                (selection.Indices.Length == 1 && requested.IndexOf((char)10) < 0 || Near(options.LineHeight, selection.LineHeight))) return false;
            float[] origins = ReadTextOrigins(first);
            Check(!first.Vertical, UiText.Get("竖排文字暂不支持保真改写。"));
            Check(first.Mode >= 0 && first.Mode <= 3, UiText.Get("此文字用于剪裁，暂不支持安全改写。"));
            foreach (var run in runs)
                for (uint mark = 0; mark < N.FPDFPageObj_CountMarks(run.Object); mark++)
                    Check(N.FPDFPageObjMark_GetParamValueType(N.FPDFPageObj_GetMark(run.Object, mark), "ActualText") == 0,
                          UiText.Get("此文字带有替代文本标记，暂不支持保真改写。"));
            if (originalFont) CheckOriginalCharacters(first, requested);
            float tracking = Tracking(first, origins) * options.Size / first.Size;
            Edit(delegate {
                IntPtr font = originalFont ? first.Font : FontFor(requested, options.Face);
                var lines = TextLines(requested, font, options.Size, tracking, options.Wrap, options.Width);
                bool restructure = !originalFont || runs.Count > 1 || lines.Count > 1;
                Check(!restructure || runs.TrueForAll(r => r.Safe) && !selection.Ocr ||
                      !originalFont && runs.Count == 1 && lines.Count == 1 && first.Basic && (first.Mode == 0 || first.Mode == 3),
                      UiText.Get("此文字带有隐藏、图层或剪裁属性，请保留原字体并按单个对象编辑。"));
                Check(lines.Count <= 400, UiText.Get("换行过多，请增大文本框宽度。"));
                var output = new List<IntPtr>(); var expected = new List<string>();
                for (int line = 0; line < lines.Count; line++)
                {
                    string value = lines[line]; if (value.Length == 0) continue;
                    IntPtr obj = originalFont && line < runs.Count ? runs[line].Object : N.FPDFPageObj_CreateTextObj(Handle, font, options.Size);
                    bool owned = !(originalFont && line < runs.Count);
                    try
                    {
                        Check(obj != IntPtr.Zero && N.FPDFText_SetText(obj, value) != 0 &&
                              N.FPDFTextObj_SetFontSize(obj, options.Size) != 0, UiText.Get("无法设置文字。"));
                        var matrix = first.Matrix;
                        matrix.e -= matrix.c * line * options.Size * options.LineHeight;
                        matrix.f -= matrix.d * line * options.Size * options.LineHeight;
                        Check(N.FPDFPageObj_SetMatrix(obj, ref matrix) != 0, UiText.Get("无法设置文字位置。"));
                        if (owned)
                        {
                            Check(N.FPDFTextObj_SetTextRenderMode(obj, first.Mode) != 0, UiText.Get("无法保留文字显示属性。"));
                            N.FPDFPageObj_SetFillColor(obj, (uint)options.Color.R, (uint)options.Color.G, (uint)options.Color.B, first.Fill.A);
                            N.FPDFPageObj_SetStrokeColor(obj, (uint)options.Color.R, (uint)options.Color.G, (uint)options.Color.B, first.Stroke.A);
                        }
                        else if (!sameColor)
                        {
                            N.FPDFPageObj_SetFillColor(obj, options.Color.R, options.Color.G, options.Color.B, first.Fill.A);
                            N.FPDFPageObj_SetStrokeColor(obj, options.Color.R, options.Color.G, options.Color.B, first.Stroke.A);
                        }
                        if (originalFont && value.Length > 1 && origins != null)
                        {
                            var positions = TextPositions(value, font, options.Size, first, line == 0 ? origins : null, tracking);
                            Check(N.FPDFText_SetPositions(obj, positions, new UIntPtr((uint)positions.Length)) != 0,
                                  UiText.Get("无法保留文字间距。"));
                        }
                        if (owned)
                        {
                            Check(N.FPDFPage_InsertObjectAtIndex(Page, obj, new UIntPtr((uint)Math.Min(N.FPDFPage_CountObjects(Page), selection.Indices[0] + line))) != 0,
                                  UiText.Get("无法插入文字。"));
                            owned = false;
                        }
                        output.Add(obj); expected.Add(value);
                    }
                    finally { if (owned && obj != IntPtr.Zero) N.FPDFPageObj_Destroy(obj); }
                }
                foreach (var run in runs)
                    if (!output.Contains(run.Object))
                    {
                        Check(N.FPDFPage_RemoveObject(Page, run.Object) != 0, UiText.Get("无法删除原文字。"));
                        N.FPDFPageObj_Destroy(run.Object);
                    }
                CloseText();
                for (int i = 0; i < output.Count; i++)
                {
                    Check(ObjectText(output[i]).TrimEnd((char)32, (char)13, (char)10) == expected[i].TrimEnd((char)32, (char)13, (char)10),
                          UiText.Get("原字体无法正确表示输入文字，请选择“自动”或其他系统字体后重试。"));
                    options.ResultBounds = UnionTextBounds(options.ResultBounds, ObjectBounds(output[i]));
                }
            }, delegate {
                // Reparse generated bytes before accepting the undo entry. Re-encoding one
                // malformed font can otherwise drop unrelated text during resource cleanup.
                string expected = SelectedText(0, N.FPDFText_CountChars(TextPage));
                using (var verify = FromBytes(Serialize(false), Password))
                {
                    verify.OpenPage(Index);
                    Check(verify.SelectedText(0, N.FPDFText_CountChars(verify.TextPage)) == expected,
                          UiText.Get("此页字体保存后无法保持文字完整，已取消修改；可尝试明确更换字体。"));
                }
            });
            return true;
        }
    }
}
