using System;
using System.Collections.Generic;

namespace MerBuilder.Mer
{
    /// <summary>整数像素矩形（端点均为包含）。</summary>
    public struct Rect2
    {
        public int X0, Y0, X1, Y1;

        public Rect2(int x0, int y0, int x1, int y1)
        {
            X0 = x0; Y0 = y0; X1 = x1; Y1 = y1;
        }

        public int Wpx { get { return X1 - X0 + 1; } }
        public int Hpx { get { return Y1 - Y0 + 1; } }
        public int Area { get { return Wpx * Hpx; } }
    }

    /// <summary>
    /// 把二值掩码分解为「横平竖直的矩形」集合。
    /// 贪心：每次在尚未覆盖的最上、最左像素处取面积最大的锚定矩形，并标记覆盖。
    /// 分解并集与掩码逐像素一致，因此几何填充零误差。
    /// </summary>
    public static class RectFill
    {
        /// <summary>把掩码分解为矩形；cell>1 时先按 cell×cell 粗化（以更少块逼近轮廓）。</summary>
        public static List<Rect2> Decompose(bool[] mask, int W, int H, int cell)
        {
            if (cell <= 1) return DecomposeExact(mask, W, H);

            int cw = (W + cell - 1) / cell;
            int ch = (H + cell - 1) / cell;
            bool[] cm = new bool[cw * ch];
            for (int cy = 0; cy < ch; cy++)
            {
                int y1 = Math.Min(H, (cy + 1) * cell);
                for (int cx = 0; cx < cw; cx++)
                {
                    int x1 = Math.Min(W, (cx + 1) * cell);
                    int on = 0, tot = 0;
                    for (int y = cy * cell; y < y1; y++)
                    {
                        int row = y * W;
                        for (int x = cx * cell; x < x1; x++) { tot++; if (mask[row + x]) on++; }
                    }
                    cm[cy * cw + cx] = tot > 0 && on * 2 >= tot;
                }
            }

            List<Rect2> coarse = DecomposeExact(cm, cw, ch);
            List<Rect2> outRects = new List<Rect2>(coarse.Count);
            for (int i = 0; i < coarse.Count; i++)
            {
                Rect2 r = coarse[i];
                outRects.Add(new Rect2(r.X0 * cell, r.Y0 * cell,
                    Math.Min(W - 1, r.X1 * cell + cell - 1),
                    Math.Min(H - 1, r.Y1 * cell + cell - 1)));
            }
            return outRects;
        }

        public static List<Rect2> Decompose(bool[] mask, int W, int H)
        {
            return DecomposeExact(mask, W, H);
        }

        private static List<Rect2> DecomposeExact(bool[] mask, int W, int H)
        {
            bool[] used = new bool[W * H];
            List<Rect2> rects = new List<Rect2>(64);

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i0 = y * W + x;
                    if (!mask[i0] || used[i0]) continue;

                    // 以 (x,y) 为左上锚点：逐列求可用高度，得到宽度 w 时的最大高度 = 前缀最小值。
                    int maxRun = 0;
                    int[] colH = new int[W - x];
                    for (int c = 0; c < colH.Length; c++)
                    {
                        int col = x + c;
                        int h = 0;
                        while (y + h < H && mask[(y + h) * W + col] && !used[(y + h) * W + col]) h++;
                        if (h == 0) break;
                        colH[c] = h;
                        maxRun = c + 1;
                    }

                    int bestW = 1, bestH = 1;
                    int bestArea = 0;
                    int runMin = int.MaxValue;
                    for (int w = 1; w <= maxRun; w++)
                    {
                        if (colH[w - 1] < runMin) runMin = colH[w - 1];
                        int area = w * runMin;
                        if (area > bestArea) { bestArea = area; bestW = w; bestH = runMin; }
                    }

                    Rect2 r = new Rect2(x, y, x + bestW - 1, y + bestH - 1);
                    for (int yy = r.Y0; yy <= r.Y1; yy++)
                    {
                        int row = yy * W;
                        for (int xx = r.X0; xx <= r.X1; xx++) used[row + xx] = true;
                    }
                    rects.Add(r);
                }
            }
            return rects;
        }
    }
}
