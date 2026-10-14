using System;
using System.Collections.Generic;
using System.Globalization;
using MerBuilder.Core;
using MerBuilder.Image;
using MerBuilder.Mer;

namespace MerBuilder.Pipeline
{
    public sealed class ConvertOptions
    {
        public string Name = "model";
        public double WidthUnits = 10.0;      // 模型宽度（米）
        public int PaletteCount = 4;
        public double Blur = 0.8;
        public double SimplifyTol = 0.8;
        public double MinArea = 6.0;
        public bool Optimize = true;
        public bool NoEmpty = false;
        public bool NoRectCover = false;
        public bool FlatThickness = false;
        /// <summary>最前层 z。参考模型中面积最大层（白）位于 z=-0.06（前）、次大层（黑）位于 z=-0.04（后），
        /// 因此 rank=0（面积最大）取 zFront，rank 越大越靠后。</summary>
        public double ZFront = -0.06;
        public double ZBase = -0.04;          // 兼容旧字段（未使用）
        public double ZStep = 0.02;
        public int BlockBudget = 0;           // 0 = 不限制
        public int Cell = 1;                  // 几何填充粗化格（1=逐像素精确）
        public double MinTriangleArea = 0.000002;

        /// <summary>默认剔除背景色层（贴边面积占比最高的实色层）。</summary>
        public bool SkipBackground = true;
        public double BackgroundBorderFrac = 0.35;

        public ConvertOptions Clone()
        {
            return (ConvertOptions)MemberwiseClone();
        }
    }

    public sealed class LayerInfo
    {
        public int Rank;
        public int Argb;
        public string Hex;
        public double Z;
        public int Blocks;
        public int Quads;
        public int Polygons;
        public int Rects;
        public int OrigPixels;
        public int MaskPixels;
        public int ShellCount;
        public int HoleCount;
        public bool Background;
        public string StatsLine;
    }

    /// <summary>跟踪得到的图像空间多边形与栅格归属（用于诊断/跟踪误差）。</summary>
    public sealed class TracedLayer
    {
        public List<List<List<V2>>> Polys = new List<List<List<V2>>>();
        public bool[] Mask;
        public int Argb;
        public double Z;
        public int Rank;
    }

    /// <summary>世界坐标四边形（预览与拟合误差共用真实块几何）。</summary>
    public sealed class WorldQuad
    {
        public double[] P = new double[12]; // 4 顶点 xyz
        public int Argb;
        public int Layer;
        public double Z;
    }

    public sealed class ConvertResult
    {
        public ConvertOptions Options;
        public MerDocument Document = new MerDocument();
        public List<LayerInfo> Layers = new List<LayerInfo>();
        public List<WorldQuad> Quads = new List<WorldQuad>();
        public List<TracedLayer> Traced = new List<TracedLayer>();
        public bool[] Ignore;                 // 剔除（背景）层：拟合误差不计
        public int BackgroundRank = -1;
        public ImageData Source;
        public ImageData Reconstructed;
        public double FitErrorPercent;
        /// <summary>仅跟踪栅格（未分解）与调色板标注的差异率。</summary>
        public double TraceErrorPercent;
        public ImageData DiffImage;
        public int TotalBlocks { get { return Document.Blocks.Count; } }
        public int PrimitiveBlocks { get { return Document.PrimitiveCount; } }
        public int EmptyBlocks { get { return Document.EmptyCount; } }
        public int QuadBlocks;
        public int Iterations;
        public List<string> Log = new List<string>();
    }

    public static class Converter
    {
        public static ConvertResult Run(ImageData img, ConvertOptions opt)
        {
            ConvertOptions cur = opt.Clone();
            ConvertResult result = null;
            for (int attempt = 0; attempt < 7; attempt++)
            {
                result = RunOnce(img, cur);
                result.Iterations = attempt + 1;
                if (cur.BlockBudget <= 0 || result.TotalBlocks <= cur.BlockBudget) break;
                cur = cur.Clone();
                cur.SimplifyTol *= 1.4;
                cur.MinArea *= 1.3;
                cur.Blur = Math.Min(cur.Blur + 0.2, 4.0);
                if (attempt >= 3) cur.Cell = cur.Cell + 1;
                result.Log.Add("块数 " + result.TotalBlocks + " 超过上限 " + cur.BlockBudget +
                               "，自动提升化简强度（第 " + (attempt + 2) + " 轮，阈值 " +
                               cur.SimplifyTol.ToString("0.##", CultureInfo.InvariantCulture) + "px，粗化 " + cur.Cell + "）");
            }
            result.Options = cur;
            return result;
        }

        private static ConvertResult RunOnce(ImageData img, ConvertOptions opt)
        {
            ConvertResult res = new ConvertResult();
            res.Options = opt;
            res.Source = img;

            int W = img.W, H = img.H;
            double k = opt.WidthUnits / W;

            PaletteResult palette = Palette.Build(img, opt.PaletteCount);
            int layerCount = palette.Order.Count;
            double zFront = opt.FlatThickness ? -0.05 : opt.ZFront;
            double zStep = opt.FlatThickness ? 0.05 : opt.ZStep;

            MerDocument doc = res.Document;
            int nextId = 1;

            // ---- 背景层识别：贴边像素最多的实色层 ----
            res.Ignore = new bool[W * H];
            int bg = -1;
            if (opt.SkipBackground && layerCount > 1)
            {
                int[] borderCount = new int[layerCount];
                int borderTotal = 0;
                for (int x = 0; x < W; x++)
                {
                    AddBorder(palette, x, img, borderCount, ref borderTotal);
                    AddBorder(palette, (H - 1) * W + x, img, borderCount, ref borderTotal);
                }
                for (int y = 0; y < H; y++)
                {
                    AddBorder(palette, y * W, img, borderCount, ref borderTotal);
                    AddBorder(palette, y * W + W - 1, img, borderCount, ref borderTotal);
                }
                int best = 0;
                for (int r = 0; r < layerCount; r++) if (borderCount[r] > best) { best = borderCount[r]; bg = r; }
                if (best < opt.BackgroundBorderFrac * borderTotal) bg = -1;
            }
            res.BackgroundRank = bg;
            if (bg >= 0)
            {
                for (int i = 0; i < W * H; i++) if (palette.Labels[i] == bg) res.Ignore[i] = true;
                res.Log.Add("剔除背景色层 " + HexOf(palette.Order[bg]) + "（贴边实色层）");
            }

            for (int rank = 0; rank < layerCount; rank++)
            {
                if (rank == bg) continue;

                bool[] mask = new bool[W * H];
                int visible = 0;
                for (int i = 0; i < W * H; i++)
                {
                    if (img.Visible(i) && palette.Labels[i] == rank) { mask[i] = true; visible++; }
                }
                if (visible == 0) continue;

                int gw, gh;
                float[] grid = Contours.BlurMask(mask, W, H, opt.Blur, out gw, out gh);
                List<List<V2>> rawRings = Contours.MarchingSquares(grid, gw, gh);
                double[] areas;
                List<Ring> rings = Contours.BuildRings(rawRings, opt.SimplifyTol, 1.0, out areas);

                // 组装多边形：偶数深度为实心外轮廓，其直接子环为孔洞。
                List<List<List<V2>>> polygons = new List<List<List<V2>>>();
                for (int i = 0; i < rings.Count; i++)
                {
                    if (rings[i].Depth % 2 != 0) continue;
                    if (rings[i].Area < opt.MinArea) continue;
                    List<List<V2>> poly = new List<List<V2>>();
                    poly.Add(rings[i].Pts);
                    for (int j = 0; j < rings.Count; j++)
                    {
                        if (rings[j].Parent == i) poly.Add(rings[j].Pts);
                    }
                    polygons.Add(poly);
                }
                if (polygons.Count == 0) continue;

                bool[] regionMask = Raster.PolygonsToMask(polygons, W, H);
                int maskPixels = 0;
                for (int i = 0; i < regionMask.Length; i++) if (regionMask[i]) maskPixels++;

                int argb = palette.Order[rank];
                string hex = HexOf(argb);
                double z = Math.Round(zFront + zStep * rank, 5);

                TracedLayer tl = new TracedLayer();
                tl.Polys = polygons;
                tl.Mask = regionMask;
                tl.Argb = argb;
                tl.Rank = rank;
                tl.Z = z;
                res.Traced.Add(tl);

                // ---- 几何填充：掩码 -> 轴对齐矩形 -> 四边形块（1 块/矩形）----
                string prefix = opt.Name + "-c" + rank;
                List<Rect2> rects = RectFill.Decompose(regionMask, W, H, opt.Cell);
                for (int r = 0; r < rects.Count; r++)
                {
                    Rect2 rc = rects[r];
                    double cxp = (rc.X0 + rc.X1) * 0.5;
                    double cyp = (rc.Y0 + rc.Y1) * 0.5;
                    double cx = (cxp - W * 0.5) * k;
                    double cy = (H * 0.5 - cyp) * k;
                    double rw = rc.Wpx * k;
                    double rh = rc.Hpx * k;

                    MerBlock b = new MerBlock();
                    b.Name = prefix + "-r-" + (r + 1).ToString("D5");
                    b.ObjectId = nextId++;
                    b.ParentId = 0;
                    b.Position = new V3(MerDocument.Round(cx), MerDocument.Round(cy), z);
                    b.Rotation = new V3(0, 0, 0);
                    b.Scale = new V3(MerDocument.Round(rw), MerDocument.Round(rh), 1);
                    b.BlockType = MerBlock.KindPrimitive;
                    b.PrimitiveType = MerBlock.Quad;
                    b.PrimitiveFlags = MerBlock.FlagsVisible;
                    b.Color = hex;
                    b.Static = true;
                    b.LayerIndex = rank;
                    doc.Blocks.Add(b);
                }

                LayerInfo li = new LayerInfo();
                li.Rank = rank;
                li.Argb = argb;
                li.Hex = hex;
                li.Z = z;
                li.Blocks = rects.Count;
                li.Quads = rects.Count;
                li.Polygons = polygons.Count;
                li.Rects = rects.Count;
                li.OrigPixels = visible;
                li.MaskPixels = maskPixels;
                li.ShellCount = polygons.Count;
                li.HoleCount = 0;
                for (int i = 0; i < polygons.Count; i++) li.HoleCount += polygons[i].Count - 1;
                li.Background = (rank == bg);
                li.StatsLine = polygons.Count + " 轮廓 -> " + rects.Count + " 矩形填充";
                res.Layers.Add(li);

                res.Log.Add("色彩层 " + rank + " " + hex + " z=" + MerDocument.Num(z) +
                            "：多边形 " + polygons.Count + " -> " + rects.Count + " 块");
            }

            // ---- 跟踪质量：逐层掩码合成后与调色板标注对比（不计背景层） ----
            ImageData tracedImg = PreviewRenderer.RenderTraced(res, W, H);
            int traceBad = 0, traceTotal = 0;
            for (int i = 0; i < W * H; i++)
            {
                if (!img.Visible(i) || res.Ignore[i]) continue;
                traceTotal++;
                int want = palette.Order[palette.Labels[i]] | unchecked((int)0xFF000000);
                if (tracedImg.Pixels[i] != want) traceBad++;
            }
            res.TraceErrorPercent = traceTotal == 0 ? 0.0 : 100.0 * traceBad / traceTotal;

            // ---- 预览 / 拟合误差：严格按真实块 TRS 重建 ----
            res.Quads = RebuildQuads(doc);
            Res res2 = Rasterize(res, img, W, H, k);
            res.Reconstructed = res2.Image;
            res.FitErrorPercent = res2.Error;
            res.QuadBlocks = res.Document.PrimitiveCount;

            return res;
        }

        private static void AddBorder(PaletteResult palette, int i, ImageData img, int[] counts, ref int total)
        {
            total++;
            int l = palette.Labels[i];
            if (l >= 0) counts[l]++;
        }

        private sealed class Res
        {
            public ImageData Image;
            public double Error;
        }

        private static string HexOf(int argb)
        {
            return "#" + ((argb >> 16) & 0xFF).ToString("X2") +
                        ((argb >> 8) & 0xFF).ToString("X2") +
                        (argb & 0xFF).ToString("X2");
        }

        /// <summary>从真实块层级（父 Empty + 子 Quad）还原世界坐标顶点。</summary>
        public static List<WorldQuad> RebuildQuads(MerDocument doc)
        {
            Dictionary<int, MerBlock> byId = new Dictionary<int, MerBlock>();
            for (int i = 0; i < doc.Blocks.Count; i++) byId[doc.Blocks[i].ObjectId] = doc.Blocks[i];

            List<WorldQuad> quads = new List<WorldQuad>(doc.Blocks.Count);
            double[][] corners = new double[][] {
                new double[] { -0.5, -0.5, 0 }, new double[] { 0.5, -0.5, 0 },
                new double[] { 0.5, 0.5, 0 }, new double[] { -0.5, 0.5, 0 }
            };

            for (int i = 0; i < doc.Blocks.Count; i++)
            {
                MerBlock b = doc.Blocks[i];
                if (b.BlockType != MerBlock.KindPrimitive) continue;

                WorldQuad wq = new WorldQuad();
                wq.Argb = ParseHex(b.Color);
                wq.Layer = b.LayerIndex;
                for (int c = 0; c < 4; c++)
                {
                    V3 local = LocalPoint(corners[c], b.Rotation, b.Scale);
                    V3 world = local + b.Position;
                    MerBlock parent;
                    if (b.ParentId != MerDocument.RootObjectId && byId.TryGetValue(b.ParentId, out parent))
                    {
                        V3 pl = LocalPoint(new double[] { local.X, local.Y, local.Z }, parent.Rotation, parent.Scale);
                        world = new V3(pl.X + parent.Position.X, pl.Y + parent.Position.Y, pl.Z + parent.Position.Z);
                    }
                    wq.P[c * 3 + 0] = world.X;
                    wq.P[c * 3 + 1] = world.Y;
                    wq.P[c * 3 + 2] = world.Z;
                }
                wq.Z = (wq.P[2] + wq.P[5] + wq.P[8] + wq.P[11]) * 0.25;
                quads.Add(wq);
            }
            return quads;
        }

        private static V3 LocalPoint(double[] p, V3 rotation, V3 scale)
        {
            double sx = p[0] * scale.X, sy = p[1] * scale.Y, sz = p[2] * scale.Z;
            M3 m = MerPrimitives.UnityEulerZxyToMatrix(rotation);
            return new V3(
                m.At(0, 0) * sx + m.At(0, 1) * sy + m.At(0, 2) * sz,
                m.At(1, 0) * sx + m.At(1, 1) * sy + m.At(1, 2) * sz,
                m.At(2, 0) * sx + m.At(2, 1) * sy + m.At(2, 2) * sz);
        }

        private static int ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return unchecked((int)0xFFFFFFFF);
            string s = hex[0] == '#' ? hex.Substring(1) : hex;
            if (s.Length == 6) s = "FF" + s;
            try
            {
                return unchecked((int)uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
            catch { return unchecked((int)0xFFFFFFFF); }
        }

        /// <summary>按 z 由后到前合成真实块，得到重建图与拟合误差（不计背景层）。</summary>
        private static Res Rasterize(ConvertResult res, ImageData src, int W, int H, double k)
        {
            List<WorldQuad> quads = new List<WorldQuad>(res.Quads);
            quads.Sort(delegate (WorldQuad a, WorldQuad b)
            {
                int cmp = b.Z.CompareTo(a.Z);   // z 大（靠后）先画
                return cmp != 0 ? cmp : a.Layer.CompareTo(b.Layer);
            });

            ImageData outImg = new ImageData();
            outImg.W = W; outImg.H = H;
            outImg.Pixels = new int[W * H];

            List<V2> ring = new List<V2>(4);
            for (int q = 0; q < quads.Count; q++)
            {
                WorldQuad wq = quads[q];
                ring.Clear();
                for (int c = 0; c < 4; c++)
                {
                    double ux = wq.P[c * 3 + 0], uy = wq.P[c * 3 + 1];
                    ring.Add(new V2(ux / k + W * 0.5, H * 0.5 - uy / k));
                }
                Raster.FillRingInto(outImg, ring, wq.Argb);
            }

            int mismatched = 0, visible = 0;
            int n2 = W * H;
            for (int i = 0; i < n2; i++)
            {
                if (!src.Visible(i) || res.Ignore[i]) continue;
                visible++;
                if (outImg.Pixels[i] == 0) { mismatched++; continue; }
                int dr = ((src.Pixels[i] >> 16) & 0xFF) - ((outImg.Pixels[i] >> 16) & 0xFF);
                int dg = ((src.Pixels[i] >> 8) & 0xFF) - ((outImg.Pixels[i] >> 8) & 0xFF);
                int db = (src.Pixels[i] & 0xFF) - (outImg.Pixels[i] & 0xFF);
                if (dr * dr + dg * dg + db * db > 3 * 32 * 32) mismatched++;
            }
            Res r = new Res();
            r.Image = outImg;
            r.Error = visible == 0 ? 0.0 : (100.0 * mismatched / visible);
            return r;
        }

        /// <summary>生成 4~5 个候选方案的参数。</summary>
        public static List<ConvertOptions> BuildSchemes(ConvertOptions baseOpt, int budget)
        {
            List<ConvertOptions> list = new List<ConvertOptions>();
            ConvertOptions a = baseOpt.Clone(); a.Name = "scheme1"; a.SimplifyTol = 0.8; a.Blur = 0.8; a.MinArea = 6; a.BlockBudget = budget; list.Add(a);
            ConvertOptions b = baseOpt.Clone(); b.Name = "scheme2"; b.SimplifyTol = 1.4; b.Blur = 1.0; b.MinArea = 12; b.BlockBudget = budget; list.Add(b);
            ConvertOptions c = baseOpt.Clone(); c.Name = "scheme3"; c.SimplifyTol = 1.0; c.Blur = 0.9; c.MinArea = 8; c.BlockBudget = budget; list.Add(c);
            ConvertOptions d = baseOpt.Clone(); d.Name = "scheme4"; d.SimplifyTol = 0.5; d.Blur = 0.6; d.MinArea = 3; d.BlockBudget = budget; list.Add(d);
            ConvertOptions e = baseOpt.Clone(); e.Name = "scheme5"; e.SimplifyTol = 0.35; e.Blur = 0.45; e.MinArea = 2; e.BlockBudget = budget; list.Add(e);
            return list;
        }

        public static string[] SchemeTitles = new string[] { "方案1", "方案2", "方案3", "方案4", "方案5" };
        public static string[] SchemeNotes = new string[] { "基准 · 均衡", "精简 · 少块", "平滑 · 稳定", "精细 · 细节", "最精细 · 高保真" };
    }
}
