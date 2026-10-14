using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using MerBuilder.Core;

namespace MerBuilder.Image
{
    public sealed class ImageData
    {
        public int W;
        public int H;
        /// <summary>每像素 ARGB（P/Invoke 顺序为 B,G,R,A 存于内存，此处统一为 ARGB int）。</summary>
        public int[] Pixels;

        public bool Visible(int i)
        {
            return ((Pixels[i] >> 24) & 0xFF) > 0;
        }

        public int R(int i) { return (Pixels[i] >> 16) & 0xFF; }
        public int G(int i) { return (Pixels[i] >> 8) & 0xFF; }
        public int B(int i) { return Pixels[i] & 0xFF; }

        public void SetRGB(int i, int r, int g, int b)
        {
            Pixels[i] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
        }
    }

    public static class ImageIO
    {
        public static ImageData Load(string path)
        {
            using (Bitmap bmp = new Bitmap(path))
            {
                int w = bmp.Width, h = bmp.Height;
                ImageData img = new ImageData();
                img.W = w; img.H = h;
                img.Pixels = new int[w * h];
                using (Bitmap b32 = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(b32))
                    {
                        g.Clear(Color.Transparent);
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        g.DrawImage(bmp, new Rectangle(0, 0, w, h));
                    }
                    BitmapData data = b32.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        int stride = data.Stride;
                        byte[] row = new byte[Math.Abs(stride)];
                        for (int y = 0; y < h; y++)
                        {
                            Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), row, 0, row.Length);
                            for (int x = 0; x < w; x++)
                            {
                                int b = row[x * 4 + 0], gg = row[x * 4 + 1], r = row[x * 4 + 2], a = row[x * 4 + 3];
                                img.Pixels[y * w + x] = (a << 24) | (r << 16) | (gg << 8) | b;
                            }
                        }
                    }
                    finally { b32.UnlockBits(data); }
                }
                return img;
            }
        }
    }

    public sealed class PaletteResult
    {
        /// <summary>按面积从大到小排序后的调色板（ARGB）。</summary>
        public List<int> Order = new List<int>();
        /// <summary>每像素所属调色板槽位（对应 Order 下标）；-1 表示不可见。</summary>
        public int[] Labels;
        public int[] Counts;
    }

    public static class Palette
    {
        /// <summary>确定性 k-means++（与参考实现的种子 0 行为对齐）。</summary>
        public static PaletteResult Build(ImageData img, int k)
        {
            int n = img.W * img.H;
            List<double[]> pts = new List<double[]>();
            List<int> idx = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!img.Visible(i)) continue;
                pts.Add(new double[] { img.R(i), img.G(i), img.B(i) });
                idx.Add(i);
            }
            PaletteResult res = new PaletteResult();
            res.Labels = new int[n];
            for (int i = 0; i < n; i++) res.Labels[i] = -1;
            if (pts.Count == 0) { res.Order = new List<int>(); res.Counts = new int[0]; return res; }

            k = Math.Max(1, Math.Min(k, pts.Count));
            Rng rng = new Rng(0);
            List<double[]> centers = new List<double[]>();
            centers.Add(pts[rng.Next(pts.Count)]);

            for (int c = 1; c < k; c++)
            {
                double total = 0;
                double[] dist = new double[pts.Count];
                for (int i = 0; i < pts.Count; i++)
                {
                    double best = double.MaxValue;
                    for (int j = 0; j < centers.Count; j++)
                    {
                        double d = Dist2(pts[i], centers[j]);
                        if (d < best) best = d;
                    }
                    dist[i] = best;
                    total += best;
                }
                if (total <= 1e-9) break;
                double pick = rng.NextDouble() * total;
                double acc = 0;
                int chosen = pts.Count - 1;
                for (int i = 0; i < pts.Count; i++)
                {
                    acc += dist[i];
                    if (acc >= pick) { chosen = i; break; }
                }
                centers.Add(new double[] { pts[chosen][0], pts[chosen][1], pts[chosen][2] });
            }

            int kk = centers.Count;
            int[] lab = new int[pts.Count];
            for (int iter = 0; iter < 40; iter++)
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    int best = 0; double bd = double.MaxValue;
                    for (int j = 0; j < kk; j++)
                    {
                        double d = Dist2(pts[i], centers[j]);
                        if (d < bd) { bd = d; best = j; }
                    }
                    lab[i] = best;
                }
                for (int j = 0; j < kk; j++)
                {
                    double sr = 0, sg = 0, sb = 0; int cnt = 0;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        if (lab[i] != j) continue;
                        sr += pts[i][0]; sg += pts[i][1]; sb += pts[i][2]; cnt++;
                    }
                    if (cnt > 0) centers[j] = new double[] { sr / cnt, sg / cnt, sb / cnt };
                }
            }

            int[] counts = new int[kk];
            for (int i = 0; i < pts.Count; i++) counts[lab[i]]++;
            List<int> active = new List<int>();
            for (int j = 0; j < kk; j++) if (counts[j] > 0) active.Add(j);
            active.Sort(delegate (int a, int b)
            {
                int cmp = counts[b].CompareTo(counts[a]);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            int[] remap = new int[kk];
            for (int i = 0; i < kk; i++) remap[i] = -1;
            res.Order = new List<int>();
            res.Counts = new int[active.Count];
            for (int rank = 0; rank < active.Count; rank++)
            {
                int j = active[rank];
                remap[j] = rank;
                int r = (int)Math.Round(centers[j][0]);
                int g = (int)Math.Round(centers[j][1]);
                int b = (int)Math.Round(centers[j][2]);
                res.Order.Add(unchecked((int)0xFF000000) | (Clamp8(r) << 16) | (Clamp8(g) << 8) | Clamp8(b));
                res.Counts[rank] = counts[j];
            }
            for (int i = 0; i < pts.Count; i++) res.Labels[idx[i]] = remap[lab[i]];
            return res;
        }

        private static int Clamp8(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }

        private static double Dist2(double[] a, double[] b)
        {
            double dr = a[0] - b[0], dg = a[1] - b[1], db = a[2] - b[2];
            return dr * dr + dg * dg + db * db;
        }
    }

    /// <summary>固定种子的确定性随机数（xorshift），保证结果可复现。</summary>
    public sealed class Rng
    {
        private ulong _s;
        public Rng(ulong seed) { _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }
        public ulong NextUInt64()
        {
            _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17;
            return _s;
        }
        public int Next(int maxExclusive) { return (int)(NextUInt64() % (ulong)Math.Max(1, maxExclusive)); }
        public double NextDouble() { return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0); }
    }

    public sealed class Ring
    {
        public List<V2> Pts = new List<V2>();
        public double Area;
        public int Parent = -1;
        public int Depth;
    }

    public static class Contours
    {
        /// <summary>把二值掩码做高斯模糊，返回带 1 像素零填充的浮点栅格 (W+2)x(H+2)。
        /// 栅格点 (gx,gy) 对应像素坐标 (gx-1, gy-1)。</summary>
        public static float[] BlurMask(bool[] mask, int W, int H, double sigma, out int gw, out int gh)
        {
            gw = W + 2; gh = H + 2;
            float[] src = new float[gw * gh];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    src[(y + 1) * gw + (x + 1)] = mask[y * W + x] ? 1f : 0f;

            if (sigma <= 0.01) return src;
            int rad = (int)Math.Ceiling(3.0 * sigma);
            double[] kernel = new double[rad * 2 + 1];
            double sum = 0;
            for (int i = -rad; i <= rad; i++)
            {
                double v = Math.Exp(-(i * i) / (2.0 * sigma * sigma));
                kernel[i + rad] = v; sum += v;
            }
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

            float[] tmp = new float[gw * gh];
            for (int y = 0; y < gh; y++)
            {
                for (int x = 0; x < gw; x++)
                {
                    double acc = 0;
                    for (int i = -rad; i <= rad; i++)
                    {
                        int xx = x + i;
                        if (xx < 0 || xx >= gw) continue;
                        acc += src[y * gw + xx] * kernel[i + rad];
                    }
                    tmp[y * gw + x] = (float)acc;
                }
            }
            float[] dst = new float[gw * gh];
            for (int y = 0; y < gh; y++)
            {
                for (int x = 0; x < gw; x++)
                {
                    double acc = 0;
                    for (int i = -rad; i <= rad; i++)
                    {
                        int yy = y + i;
                        if (yy < 0 || yy >= gh) continue;
                        acc += tmp[yy * gw + x] * kernel[i + rad];
                    }
                    dst[y * gw + x] = (float)acc;
                }
            }
            return dst;
        }

        private const double Threshold = 0.5;

        /// <summary>行进方格提取 0.5 等值线，返回图像像素坐标下的闭合环。</summary>
        public static List<List<V2>> MarchingSquares(float[] g, int gw, int gh)
        {
            List<double[]> segs = new List<double[]>();     // [startEdge, endEdge, sx, sy, ex, ey]
            Dictionary<int, List<int>> edgeSegs = new Dictionary<int, List<int>>();

            int cellsX = gw - 1, cellsY = gh - 1;
            float[] prim = new float[5];

            for (int y = 0; y < cellsY; y++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    double v00 = g[y * gw + x];
                    double v10 = g[y * gw + x + 1];
                    double v11 = g[(y + 1) * gw + x + 1];
                    double v01 = g[(y + 1) * gw + x];
                    int idx = 0;
                    if (v00 > Threshold) idx |= 1;
                    if (v10 > Threshold) idx |= 2;
                    if (v11 > Threshold) idx |= 4;
                    if (v01 > Threshold) idx |= 8;
                    if (idx == 0 || idx == 15) continue;

                    int eT = HEdge(y, x, gw);
                    int eR = VEdge(y, x + 1, gw);
                    int eB = HEdge(y + 1, x, gw);
                    int eL = VEdge(y, x, gw);

                    switch (idx)
                    {
                        case 1: AddSeg(segs, edgeSegs, eL, eT, Interp(g, gw, x, y, x, y + 1, Threshold), Interp(g, gw, x, y, x + 1, y, Threshold)); break;
                        case 2: AddSeg(segs, edgeSegs, eT, eR, Interp(g, gw, x, y, x + 1, y, Threshold), Interp(g, gw, x + 1, y, x + 1, y + 1, Threshold)); break;
                        case 3: AddSeg(segs, edgeSegs, eL, eR, Interp(g, gw, x, y, x, y + 1, Threshold), Interp(g, gw, x + 1, y, x + 1, y + 1, Threshold)); break;
                        case 4: AddSeg(segs, edgeSegs, eR, eB, Interp(g, gw, x + 1, y, x + 1, y + 1, Threshold), Interp(g, gw, x, y + 1, x + 1, y + 1, Threshold)); break;
                        case 6: AddSeg(segs, edgeSegs, eT, eB, Interp(g, gw, x, y, x + 1, y, Threshold), Interp(g, gw, x, y + 1, x + 1, y + 1, Threshold)); break;
                        case 7: AddSeg(segs, edgeSegs, eL, eB, Interp(g, gw, x, y, x, y + 1, Threshold), Interp(g, gw, x, y + 1, x + 1, y + 1, Threshold)); break;
                        case 8: AddSeg(segs, edgeSegs, eB, eL, Interp(g, gw, x, y + 1, x + 1, y + 1, Threshold), Interp(g, gw, x, y, x, y + 1, Threshold)); break;
                        case 9: AddSeg(segs, edgeSegs, eB, eT, Interp(g, gw, x, y + 1, x + 1, y + 1, Threshold), Interp(g, gw, x, y, x + 1, y, Threshold)); break;
                        case 11: AddSeg(segs, edgeSegs, eB, eR, Interp(g, gw, x, y + 1, x + 1, y + 1, Threshold), Interp(g, gw, x + 1, y, x + 1, y + 1, Threshold)); break;
                        case 12: AddSeg(segs, edgeSegs, eR, eL, Interp(g, gw, x + 1, y, x + 1, y + 1, Threshold), Interp(g, gw, x, y, x, y + 1, Threshold)); break;
                        case 13: AddSeg(segs, edgeSegs, eR, eT, Interp(g, gw, x + 1, y, x + 1, y + 1, Threshold), Interp(g, gw, x, y, x + 1, y, Threshold)); break;
                        case 14: AddSeg(segs, edgeSegs, eT, eL, Interp(g, gw, x, y, x + 1, y, Threshold), Interp(g, gw, x, y, x, y + 1, Threshold)); break;
                        case 5:
                            {
                                double center = (v00 + v10 + v11 + v01) * 0.25;
                                if (center > Threshold)
                                {
                                    AddSeg(segs, edgeSegs, eT, eR, Interp(g, gw, x, y, x + 1, y, Threshold), Interp(g, gw, x + 1, y, x + 1, y + 1, Threshold));
                                    AddSeg(segs, edgeSegs, eB, eL, Interp(g, gw, x, y + 1, x + 1, y + 1, Threshold), Interp(g, gw, x, y, x, y + 1, Threshold));
                                }
                                else
                                {
                                    AddSeg(segs, edgeSegs, eL, eT, Interp(g, gw, x, y, x, y + 1, Threshold), Interp(g, gw, x, y, x + 1, y, Threshold));
                                    AddSeg(segs, edgeSegs, eR, eB, Interp(g, gw, x + 1, y, x + 1, y + 1, Threshold), Interp(g, gw, x, y + 1, x + 1, y + 1, Threshold));
                                }
                                break;
                            }
                        case 10:
                            {
                                double center = (v00 + v10 + v11 + v01) * 0.25;
                                if (center > Threshold)
                                {
                                    AddSeg(segs, edgeSegs, eL, eT, Interp(g, gw, x, y, x, y + 1, Threshold), Interp(g, gw, x, y, x + 1, y, Threshold));
                                    AddSeg(segs, edgeSegs, eR, eB, Interp(g, gw, x + 1, y, x + 1, y + 1, Threshold), Interp(g, gw, x, y + 1, x + 1, y + 1, Threshold));
                                }
                                else
                                {
                                    AddSeg(segs, edgeSegs, eT, eR, Interp(g, gw, x, y, x + 1, y, Threshold), Interp(g, gw, x + 1, y, x + 1, y + 1, Threshold));
                                    AddSeg(segs, edgeSegs, eB, eL, Interp(g, gw, x, y + 1, x + 1, y + 1, Threshold), Interp(g, gw, x, y, x, y + 1, Threshold));
                                }
                                break;
                            }
                    }
                }
            }

            bool[] used = new bool[segs.Count];
            List<List<V2>> rings = new List<List<V2>>();
            for (int s = 0; s < segs.Count; s++)
            {
                if (used[s]) continue;
                List<V2> ring = new List<V2>();
                int cur = s;
                int guard = 0;
                while (guard++ < segs.Count + 4)
                {
                    used[cur] = true;
                    ring.Add(new V2((float)segs[cur][2], (float)segs[cur][3]));
                    int endEdge = (int)segs[cur][1];
                    int nxt = -1;
                    if (edgeSegs.ContainsKey(endEdge))
                    {
                        List<int> lst = edgeSegs[endEdge];
                        for (int i = 0; i < lst.Count; i++)
                        {
                            if (lst[i] != cur && !used[lst[i]]) { nxt = lst[i]; break; }
                        }
                    }
                    if (nxt < 0) break;
                    cur = nxt;
                }
                if (ring.Count >= 4) rings.Add(ring);
            }
            return rings;
        }

        private static int HEdge(int y, int x, int gw) { return ((y * gw + x) << 1); }
        private static int VEdge(int y, int x, int gw) { return ((y * gw + x) << 1) | 1; }

        private static void AddSeg(List<double[]> segs, Dictionary<int, List<int>> map,
            int a, int b, V2 pa, V2 pb)
        {
            int id = segs.Count;
            segs.Add(new double[] { a, b, pa.X, pa.Y, pb.X, pb.Y });
            if (!map.ContainsKey(a)) map[a] = new List<int>();
            map[a].Add(id);
            if (!map.ContainsKey(b)) map[b] = new List<int>();
            map[b].Add(id);
        }

        private static V2 Interp(float[] g, int gw, int x0, int y0, int x1, int y1, double t)
        {
            double a = g[y0 * gw + x0];
            double b = g[y1 * gw + x1];
            double f = (t - a) / (b - a);
            if (f < 0) f = 0; if (f > 1) f = 1;
            return new V2(x0 + (x1 - x0) * f - 1.0, y0 + (y1 - y0) * f - 1.0);
        }

        /// <summary>嵌套判定：面积降序，parent = 已放置的最小包含环；偶数深度为实心。</summary>
        public static List<Ring> BuildRings(List<List<V2>> rawRings, double tol, double minArea, out double[] areas)
        {
            List<Ring> rings = new List<Ring>();
            for (int i = 0; i < rawRings.Count; i++)
            {
                List<V2> pts = Simplify(rawRings[i], tol);
                if (pts.Count < 3) continue;
                double area = Math.Abs(G2.SignedArea(pts));
                if (area < minArea) continue;
                Ring r = new Ring();
                r.Pts = pts;
                r.Area = area;
                rings.Add(r);
            }

            List<int> order = new List<int>();
            for (int i = 0; i < rings.Count; i++) order.Add(i);
            order.Sort(delegate (int a, int b)
            {
                int cmp = rings[b].Area.CompareTo(rings[a].Area);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            List<int> placed = new List<int>();
            for (int oi = 0; oi < order.Count; oi++)
            {
                int i = order[oi];
                V2 rep = RepresentativePoint(rings[i].Pts);
                int par = -1;
                for (int k = 0; k < placed.Count; k++)
                {
                    int j = placed[k];
                    if (G2.PointInPolygon(rep, rings[j].Pts)) par = j;
                }
                rings[i].Parent = par;
                rings[i].Depth = par < 0 ? 0 : rings[par].Depth + 1;
                placed.Add(i);
            }

            areas = new double[rings.Count];
            for (int i = 0; i < rings.Count; i++) areas[i] = rings[i].Area;
            return rings;
        }

        private static V2 RepresentativePoint(List<V2> pts)
        {
            double sx = 0, sy = 0;
            for (int i = 0; i < pts.Count; i++) { sx += pts[i].X; sy += pts[i].Y; }
            V2 c = new V2(sx / pts.Count, sy / pts.Count);
            if (G2.PointInPolygon(c, pts)) return c;
            return pts[0];
        }

        /// <summary>Douglas-Peucker 化简（闭合环）。</summary>
        public static List<V2> Simplify(List<V2> pts, double tol)
        {
            if (pts.Count < 4 || tol <= 0) return new List<V2>(pts);
            bool[] keep = new bool[pts.Count];
            keep[0] = true; keep[pts.Count - 1] = true;
            Stack<int[]> stack = new Stack<int[]>();
            stack.Push(new int[] { 0, pts.Count - 1 });
            while (stack.Count > 0)
            {
                int[] range = stack.Pop();
                int s = range[0], e = range[1];
                double maxD = -1; int maxI = -1;
                for (int i = s + 1; i < e; i++)
                {
                    double d = PointSegDistance(pts[i], pts[s], pts[e]);
                    if (d > maxD) { maxD = d; maxI = i; }
                }
                if (maxD > tol && maxI > 0)
                {
                    keep[maxI] = true;
                    stack.Push(new int[] { s, maxI });
                    stack.Push(new int[] { maxI, e });
                }
            }
            List<V2> outPts = new List<V2>();
            for (int i = 0; i < pts.Count; i++) if (keep[i]) outPts.Add(pts[i]);
            if (outPts.Count < 3) return new List<V2>(pts);
            return outPts;
        }

        private static double PointSegDistance(V2 p, V2 a, V2 b)
        {
            V2 ab = b - a;
            double l2 = ab.Dot(ab);
            if (l2 < 1e-12) return (p - a).Len;
            double t = (p - a).Dot(ab) / l2;
            if (t < 0) t = 0; if (t > 1) t = 1;
            V2 proj = new V2(a.X + ab.X * t, a.Y + ab.Y * t);
            return (p - proj).Len;
        }
    }

    /// <summary>栅格化工具：像素 (i,j) 中心位于 (i,j)。</summary>
    public static class Raster
    {
        public static void FillRing(bool[] target, int W, int H, IList<V2> ring, bool value)
        {
            int n = ring.Count;
            if (n < 3) return;
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (ring[i].Y < minY) minY = ring[i].Y;
                if (ring[i].Y > maxY) maxY = ring[i].Y;
            }
            int y0 = Math.Max(0, (int)Math.Floor(minY));
            int y1 = Math.Min(H - 1, (int)Math.Ceiling(maxY));
            List<double> xs = new List<double>(8);
            for (int j = y0; j <= y1; j++)
            {
                double yc = j;
                xs.Clear();
                for (int i = 0; i < n; i++)
                {
                    V2 a = ring[i], b = ring[(i + 1) % n];
                    if ((a.Y > yc) != (b.Y > yc))
                    {
                        xs.Add(a.X + (yc - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                    }
                }
                if (xs.Count < 2) continue;
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int lo = (int)Math.Ceiling(xs[k]);
                    int hi = (int)Math.Floor(xs[k + 1]);
                    if (lo < 0) lo = 0;
                    if (hi > W - 1) hi = W - 1;
                    for (int i = lo; i <= hi; i++) target[j * W + i] = value;
                }
            }
        }

        /// <summary>多边形（外轮廓 + 孔洞）栅格化为掩码。</summary>
        public static bool[] PolygonsToMask(List<List<List<V2>>> polygons, int W, int H)
        {
            bool[] mask = new bool[W * H];
            for (int p = 0; p < polygons.Count; p++)
            {
                List<List<V2>> rings = polygons[p];
                for (int r = 0; r < rings.Count; r++)
                    FillRing(mask, W, H, rings[r], r == 0);
            }
            return mask;
        }

        /// <summary>把填充色写入图像（用于重建预览与拟合误差）。</summary>
        public static void FillRingInto(ImageData target, IList<V2> ring, int argb)
        {
            int W = target.W, H = target.H;
            int n = ring.Count;
            if (n < 3) return;
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (ring[i].Y < minY) minY = ring[i].Y;
                if (ring[i].Y > maxY) maxY = ring[i].Y;
            }
            int y0 = Math.Max(0, (int)Math.Floor(minY));
            int y1 = Math.Min(H - 1, (int)Math.Ceiling(maxY));
            List<double> xs = new List<double>(8);
            for (int j = y0; j <= y1; j++)
            {
                double yc = j;
                xs.Clear();
                for (int i = 0; i < n; i++)
                {
                    V2 a = ring[i], b = ring[(i + 1) % n];
                    if ((a.Y > yc) != (b.Y > yc))
                        xs.Add(a.X + (yc - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                }
                if (xs.Count < 2) continue;
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int lo = (int)Math.Ceiling(xs[k]);
                    int hi = (int)Math.Floor(xs[k + 1]);
                    if (lo < 0) lo = 0;
                    if (hi > W - 1) hi = W - 1;
                    for (int i = lo; i <= hi; i++) target.Pixels[j * W + i] = argb;
                }
            }
        }
    }
}
