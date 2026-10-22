using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using Windows.Foundation;
using Windows.Storage.Streams;
using SystemPdf = Windows.Data.Pdf.PdfDocument;
using PdfPageRenderOptions = Windows.Data.Pdf.PdfPageRenderOptions;

namespace MiniPdf
{
    // Windows supplies this compatibility renderer. It never rewrites the PDF.
    // Only the isolated fallback worker loads these WinRT types.
    internal sealed class WindowsRaster : IDisposable
    {
        InMemoryRandomAccessStream input;
        SystemPdf document;
        double outputScale = 1;
        internal int Count { get { return checked((int)document.PageCount); } }

        static void Wait(IAsyncInfo operation)
        {
            var elapsed = Stopwatch.StartNew();
            while (operation.Status == AsyncStatus.Started)
            {
                if (elapsed.ElapsedMilliseconds > 30000)
                {
                    operation.Cancel();
                    throw new TimeoutException("Windows PDF rendering timed out.");
                }
                Thread.Sleep(2);
            }
            if (operation.Status != AsyncStatus.Completed)
                throw new IOException("Windows PDF operation failed: " + operation.ErrorCode);
        }
        internal static WindowsRaster Open(PdfSource source, string password)
        {
            var result = new WindowsRaster();
            try
            {
                result.input = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(result.input.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(source.ReadAll());
                    var store = writer.StoreAsync();
                    try { Wait(store); store.GetResults(); } finally { store.Close(); }
                    writer.DetachStream();
                }
                var loading = SystemPdf.LoadFromStreamAsync(result.input, password ?? "");
                try { Wait(loading); result.document = loading.GetResults(); } finally { loading.Close(); }
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        internal void RenderInto(int index, int width, int height, int rotation, Rectangle region, IntPtr pixels)
        {
            if (index < 0 || index >= Count) throw new IOException("Invalid raster page.");
            rotation = (rotation % 4 + 4) % 4;
            int baseWidth = rotation % 2 == 0 ? width : height;
            int baseHeight = rotation % 2 == 0 ? height : width;
            Rectangle crop;
            switch (rotation)
            {
                case 1: crop = new Rectangle(region.Y, baseHeight - region.Right, region.Height, region.Width); break;
                case 2: crop = new Rectangle(baseWidth - region.Right, baseHeight - region.Bottom, region.Width, region.Height); break;
                case 3: crop = new Rectangle(baseWidth - region.Bottom, region.X, region.Height, region.Width); break;
                default: crop = region; break;
            }
            using (var page = document.GetPage((uint)index))
            using (var output = new InMemoryRandomAccessStream())
            {
                uint requestedWidth = (uint)Math.Max(1, Math.Ceiling(crop.Width / outputScale));
                uint requestedHeight = (uint)Math.Max(1, Math.Ceiling(crop.Height / outputScale));
                var options = new PdfPageRenderOptions {
                    DestinationWidth = requestedWidth, DestinationHeight = requestedHeight,
                    IsIgnoringHighContrast = true,
                    SourceRect = new Windows.Foundation.Rect(crop.X * page.Size.Width / baseWidth,
                        crop.Y * page.Size.Height / baseHeight, crop.Width * page.Size.Width / baseWidth,
                        crop.Height * page.Size.Height / baseHeight)
                };
                var rendering = page.RenderToStreamAsync(output, options);
                try { Wait(rendering); rendering.GetResults(); } finally { rendering.Close(); }
                if (output.Size > 256 * 1024 * 1024) throw new IOException("Windows PDF raster exceeds its buffer limit.");
                byte[] bytes = new byte[checked((int)output.Size)];
                using (var reader = new DataReader(output.GetInputStreamAt(0)))
                {
                    var reading = reader.LoadAsync((uint)bytes.Length);
                    try { Wait(reading); reading.GetResults(); } finally { reading.Close(); }
                    reader.ReadBytes(bytes);
                }
                using (var stream = new MemoryStream(bytes, false))
                using (var decoded = new Bitmap(stream))
                using (var target = new Bitmap(region.Width, region.Height, region.Width * 4,
                                               PixelFormat.Format32bppArgb, pixels))
                using (var graphics = Graphics.FromImage(target))
                {
                    // Some Windows builds scale destination sizes with display DPI.
                    // Learn the actual factor, then always return the exact requested pixels.
                    if (requestedWidth >= 64) outputScale = Math.Max(1, Math.Round(decoded.Width / (double)requestedWidth, 2));
                    if (rotation != 0) decoded.RotateFlip(rotation == 1 ? RotateFlipType.Rotate90FlipNone :
                        rotation == 2 ? RotateFlipType.Rotate180FlipNone : RotateFlipType.Rotate270FlipNone);
                    graphics.Clear(Color.White);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    graphics.DrawImage(decoded, new Rectangle(0, 0, region.Width, region.Height));
                }
            }
        }
        public void Dispose()
        {
            document = null;
            if (input != null) { input.Dispose(); input = null; }
        }
    }
}
