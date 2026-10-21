using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace MiniPdf
{
    internal sealed class ThumbnailCache : IDisposable
    {
        sealed class Entry
        {
            internal RenderFrame Frame;
            internal LinkedListNode<string> Node;
        }
        readonly Control owner;
        readonly int limit;
        readonly Dictionary<string, Entry> images = new Dictionary<string, Entry>();
        readonly LinkedList<string> recent = new LinkedList<string>();
        readonly HashSet<string> pending = new HashSet<string>(), failed = new HashSet<string>();
        int generation;
        long bytes;
        internal int Pending
        {
            get {
                return pending.Count;
            }
        }
        internal ThumbnailCache(Control control, int count)
        {
            owner = control;
            limit = count;
        }
        internal Bitmap Get(PdfDocument document, int page, int width, int height, int rotation)
        {
            string key = document.Identity + ":" + document.RenderEpoch + ":" + page + ":" + width + ":" + height +
                         ":" + rotation;
            Entry entry;
            if (images.TryGetValue(key, out entry))
            {
                recent.Remove(entry.Node);
                recent.AddLast(entry.Node);
                return entry.Frame.Image;
            }
            if (pending.Contains(key) || failed.Contains(key) || !owner.IsHandleCreated)
                return null;
            int current = generation;
            pending.Add(key);
            try
            {
                if (!RenderPool.Request(
                        owner, document.GetRenderSource(), document.Password, page, width, height, rotation,
                        new Rectangle(0, 0, width, height), true, delegate(RenderFrame frame, Exception error) {
                            if (current != generation)
                            {
                                if (frame != null)
                                    frame.Dispose();
                                return;
                            }
                            pending.Remove(key);
                            if (error != null)
                            {
                                failed.Add(key);
                                return;
                            }
                            if (images.ContainsKey(key))
                            {
                                frame.Dispose();
                                return;
                            }
                            images.Add(key, new Entry { Frame = frame, Node = recent.AddLast(key) });
                            bytes += (long)width * height * 4;
                            while (images.Count > 1 && (images.Count > limit || bytes > 16L * 1024 * 1024))
                            {
                                string old = recent.First.Value;
                                var prior = images[old];
                                bytes -= (long)prior.Frame.Image.Width * prior.Frame.Image.Height * 4;
                                prior.Frame.Dispose();
                                images.Remove(old);
                                recent.RemoveFirst();
                            }
                            owner.Invalidate();
                        }))
                    pending.Remove(key);
            }
            catch
            {
                pending.Remove(key);
                throw;
            }
            return null;
        }
        internal void Clear()
        {
            RenderPool.Cancel(owner, true);
            generation++;
            pending.Clear();
            failed.Clear();
            foreach (var entry in images.Values)
                entry.Frame.Dispose();
            images.Clear();
            recent.Clear();
            bytes = 0;
        }
        public void Dispose()
        {
            Clear();
        }
    }
}
