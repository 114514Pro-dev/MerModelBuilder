using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using MerBuilder.Core;
using MerBuilder.Image;

namespace MerBuilder.Pipeline
{
    public static class PreviewRenderer
    {
        /// <summary>把重建图等比绘制到指定画布（最近邻保留硬边）。</summary>
        public static void DrawReconstructed(Graphics g, ImageData img, Rectangle box, Color background)
        {
            using (SolidBrush bg = new SolidBrush(background)) g.FillRectangle(bg, box);

            if (img == null || img.W <= 0 || img.H <= 0) return;
            double scale = Math.Min((double)box.Width / img.W, (double)box.Height / img.H);
            int w = (int)Math.Round(img.W * scale);
            int h = (int)Math.Round(img.H * scale);
            int ox = box.X + (box.Width - w) / 2;
            int oy = box.Y + (box.Height - h) / 2;

            using (Bitmap bmp = new Bitmap(img.W, img.H, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                for (int y = 0; y < img.H; y++)
                {
                    for (int x = 0; x < img.W; x++)
                    {
                        int argb = img.Pixels[y * img.W + x];
                        if (((argb >> 24) & 0xFF) == 0) argb = 0;
                        bmp.SetPixel(x, y, Color.FromArgb(argb));
                    }
                }
                InterpolationMode old = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(bmp, new Rectangle(ox, oy, w, h));
                g.InterpolationMode = old;
            }
        }

        /// <summary>伪三维预览：绕 Y 轴偏航，按深度做画家排序，层间自然产生视差。</summary>
        public static void DrawQuads(Graphics g, ConvertResult res, Rectangle box, Color background, double yawDeg)
        {
            using (SolidBrush bg = new SolidBrush(background)) g.FillRectangle(bg, box);
            if (res == null || res.Quads.Count == 0) return;

            double yaw = yawDeg * Math.PI / 180.0;
            double cy = Math.Cos(yaw), sy = Math.Sin(yaw);

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < res.Quads.Count; i++)
            {
                double[] p = res.Quads[i].P;
                for (int c = 0; c < 4; c++)
                {
                    double x = p[c * 3], z = p[c * 3 + 2];
                    double rx = x * cy + z * sy;
                    if (rx < minX) minX = rx;
                    if (rx > maxX) maxX = rx;
                    double y = p[c * 3 + 1];
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX <= minX || maxY <= minY) return;

            double scale = Math.Min(box.Width / (maxX - minX), box.Height / (maxY - minY)) * 0.92;
            double ox = box.X + box.Width * 0.5;
            double oy = box.Y + box.Height * 0.5;
            double cxm = (minX + maxX) * 0.5, cym = (minY + maxY) * 0.5;

            List<int> order = new List<int>(res.Quads.Count);
            for (int i = 0; i < res.Quads.Count; i++) order.Add(i);
            double[] depth = new double[res.Quads.Count];
            for (int i = 0; i < res.Quads.Count; i++)
            {
                double[] p = res.Quads[i].P;
                double acc = 0;
                for (int c = 0; c < 4; c++)
                {
                    double x = p[c * 3], z = p[c * 3 + 2];
                    acc += -x * sy + z * cy;
                }
                depth[i] = acc * 0.25;
            }
            order.Sort(delegate (int a, int b) { return depth[a].CompareTo(depth[b]); });

            PointF[] pts = new PointF[4];
            for (int oi = 0; oi < order.Count; oi++)
            {
                int i = order[oi];
                WorldQuad wq = res.Quads[i];
                bool ok = true;
                for (int c = 0; c < 4; c++)
                {
                    double x = wq.P[c * 3], y = wq.P[c * 3 + 1], z = wq.P[c * 3 + 2];
                    double rx = x * cy + z * sy;
                    double rz = -x * sy + z * cy;
                    double persp = 1.0 / (1.0 + rz * 0.035);
                    if (persp < 0.2) { ok = false; break; }
                    double sx = (rx - cxm) * persp;
                    double syy = (y - cym) * persp;
                    pts[c] = new PointF((float)(ox + sx * scale), (float)(oy - syy * scale));
                }
                if (!ok) continue;
                int argb = wq.Argb | unchecked((int)0xFF000000);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(argb)))
                {
                    g.FillPolygon(brush, pts);
                }
            }
        }

        /// <summary>诊断用：按各层跟踪栅格由后到前合成（与几何填充等价）。</summary>
        public static ImageData RenderTraced(ConvertResult res, int W, int H)
        {
            ImageData img = new ImageData();
            img.W = W; img.H = H;
            img.Pixels = new int[W * H];
            List<TracedLayer> layers = new List<TracedLayer>(res.Traced);
            layers.Sort(delegate (TracedLayer a, TracedLayer b) { return b.Z.CompareTo(a.Z); });
            for (int i = 0; i < layers.Count; i++)
            {
                TracedLayer tl = layers[i];
                if (tl.Mask == null) continue;
                int argb = tl.Argb | unchecked((int)0xFF000000);
                for (int p = 0; p < W * H; p++) if (tl.Mask[p]) img.Pixels[p] = argb;
            }
            return img;
        }

        /// <summary>把 ImageData 转成 Bitmap（用于界面缩略图）。</summary>
        public static Bitmap ToBitmap(ImageData img)
        {
            if (img == null) return null;
            Bitmap bmp = new Bitmap(img.W, img.H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int y = 0; y < img.H; y++)
                for (int x = 0; x < img.W; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(img.Pixels[y * img.W + x]));
            return bmp;
        }
    }
}
