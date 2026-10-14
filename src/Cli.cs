using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MerBuilder.Core;
using MerBuilder.Image;
using MerBuilder.Mer;
using MerBuilder.Pipeline;

namespace MerBuilder
{
    /// <summary>无界面命令行：便于批处理与回归自检。</summary>
    public static class Cli
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        private static void AttachConsoleIfPossible()
        {
            try
            {
                if (AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    StreamWriter w = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8);
                    w.AutoFlush = true;
                    Console.SetOut(w);
                }
            }
            catch { }
        }

        private static readonly List<string> Report = new List<string>();

        private static void Say(string line)
        {
            Report.Add(line);
            Console.WriteLine(line);
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

        /// <summary>自检：验证剪切层级（Empty 父 + Quad 子）能否精确重建目标平行四边形。</summary>
        private static int SelfTest()
        {
            double[][] corners = new double[][] {
                new double[] { -0.5, -0.5, 0 }, new double[] { 0.5, -0.5, 0 },
                new double[] { 0.5, 0.5, 0 }, new double[] { -0.5, 0.5, 0 } };
            Rng rng = new Rng(12345);
            double worst = 0; int n = 0, skipped = 0;
            for (int t = 0; t < 500; t++)
            {
                double lx = (rng.NextDouble() * 2 - 1) * 3, ly = (rng.NextDouble() * 2 - 1) * 3;
                double ux = (rng.NextDouble() * 2 - 1) * 3, uy = (rng.NextDouble() * 2 - 1) * 3;
                double ll = Math.Sqrt(lx * lx + ly * ly), lu = Math.Sqrt(ux * ux + uy * uy);
                if (ll < 0.2 || lu < 0.2) { skipped++; continue; }
                double cosT = Math.Abs((lx * ux + ly * uy) / (ll * lu));
                if (cosT > 0.9) { skipped++; continue; }
                V3 vLeft = new V3(lx, ly, 0), vUp = new V3(ux, uy, 0);
                V3 center = new V3(0, 0, -0.04);

                // 目标：以 2*vLeft、2*vUp 为对角线的平行四边形四角（无序集合）。
                V2[] want = new V2[] {
                    new V2(center.X - vLeft.X, center.Y - vLeft.Y), new V2(center.X + vLeft.X, center.Y + vLeft.Y),
                    new V2(center.X - vUp.X, center.Y - vUp.Y), new V2(center.X + vUp.X, center.Y + vUp.Y) };

                V3 pRot, pScale, cScale; double cAngle;
                if (!MerPrimitives.TryGetShearTransforms(vUp, vLeft, out pRot, out pScale, out cAngle, out cScale)) { skipped++; continue; }
                V3 pRotR = MerDocument.RoundVec(pRot), pScaleR = MerDocument.RoundVec(pScale);
                V3 cScaleR = MerDocument.RoundVec(cScale);
                double cAngleR = MerDocument.Round(cAngle);

                V2[] got = new V2[4];
                for (int c = 0; c < 4; c++)
                {
                    V3 childLocal = LocalPoint(corners[c], new V3(0, 0, cAngleR), cScaleR);
                    V3 world = LocalPoint(new double[] { childLocal.X, childLocal.Y, childLocal.Z }, pRotR, pScaleR);
                    got[c] = new V2(world.X + center.X, world.Y + center.Y);
                }
                for (int c = 0; c < 4; c++)
                {
                    double best = double.MaxValue;
                    for (int w = 0; w < 4; w++)
                    {
                        double d = (got[c] - want[w]).Len;
                        if (d < best) best = d;
                    }
                    if (best > worst) worst = best;
                }
                n++;
            }
            Console.WriteLine("selftest: 样本 " + n + "  跳过 " + skipped + "  最大顶点误差 " +
                worst.ToString("0.######", CultureInfo.InvariantCulture));
            return worst < 1e-4 ? 0 : 1;
        }

        public static int Run(string[] args)
        {
            AttachConsoleIfPossible();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--selftest") return SelfTest();
            string image = null;
            string outDir = null;
            string name = null;
            int palette = 4;
            double tol = 0.8, blur = 0.8, width = 10.0, minArea = 6.0;
            int budget = 0;
            bool noEmpty = false, noRect = false, raw = false, keepBg = false;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "--out": outDir = args[++i]; break;
                    case "--name": name = args[++i]; break;
                    case "--palette": palette = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--tol": tol = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--blur": blur = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--width": width = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--min-area": minArea = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--budget": budget = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--no-empty": noEmpty = true; break;
                    case "--no-rect": noRect = true; break;
                    case "--keep-bg": keepBg = true; break;
                    case "--raw": raw = true; break;
                    default: image = a; break;
                }
            }
            if (string.IsNullOrEmpty(image))
            {
                Console.WriteLine("用法: MerModelBuilder --cli <图片> [--out 目录] [--palette 4] [--tol 1.2] [--blur 1.2] [--width 10] [--min-area 6] [--budget 0] [--no-empty] [--no-rect]");
                return 2;
            }
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(image)), "mer-out");
            if (string.IsNullOrEmpty(name)) name = Path.GetFileNameWithoutExtension(image);
            Directory.CreateDirectory(outDir);

            ImageData img = ImageIO.Load(image);
            ConvertOptions opt = new ConvertOptions();
            opt.Name = name;
            opt.PaletteCount = palette;
            opt.SimplifyTol = tol;
            opt.Blur = blur;
            opt.WidthUnits = width;
            opt.MinArea = minArea;
            opt.BlockBudget = budget;
            opt.NoEmpty = noEmpty;
            opt.NoRectCover = noRect;
            opt.Optimize = !raw;
            opt.SkipBackground = !keepBg;

            ConvertResult res = Converter.Run(img, opt);
            string jsonPath = Path.Combine(outDir, name + ".json");
            File.WriteAllText(jsonPath, res.Document.ToJson());

            Bitmap bmp = new Bitmap(768, 768);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(255, 12, 16, 22));
                PreviewRenderer.DrawReconstructed(g, res.Reconstructed, new Rectangle(0, 0, 768, 768), Color.FromArgb(255, 12, 16, 22));
            }
            bmp.Save(Path.Combine(outDir, name + ".preview.png"), ImageFormat.Png);
            bmp.Dispose();

            ImageData traced = PreviewRenderer.RenderTraced(res, img.W, img.H);
            using (Bitmap tb = new Bitmap(img.W, img.H))
            {
                for (int y = 0; y < img.H; y++)
                    for (int x = 0; x < img.W; x++)
                        tb.SetPixel(x, y, Color.FromArgb(traced.Pixels[y * img.W + x]));
                tb.Save(Path.Combine(outDir, name + ".trace.png"), ImageFormat.Png);
            }

            List<string> issues = res.Document.Validate();
            Say("图片     : " + image + "  " + img.W + "x" + img.H);
            Say("方案     : tol=" + tol + " blur=" + blur + " palette=" + palette + " budget=" + budget);
            Say("总块数   : " + res.TotalBlocks + "（实体 " + res.PrimitiveBlocks + " + Empty " + res.EmptyBlocks + "）");
            Say("四边形   : " + res.QuadBlocks + "  轮次 " + res.Iterations);
            Say("拟合误差 : " + res.FitErrorPercent.ToString("0.00", CultureInfo.InvariantCulture) +
                "%   跟踪误差 " + res.TraceErrorPercent.ToString("0.00", CultureInfo.InvariantCulture) + "%");
            if (res.BackgroundRank >= 0)
                Say("背景剔除 : 层" + res.BackgroundRank + "（贴边实色层；--keep-bg 可保留）");
            {
                bool[] any = new bool[img.W * img.H];
                for (int t = 0; t < res.Traced.Count; t++)
                {
                    bool[] m = res.Traced[t].Mask;
                    if (m == null) continue;
                    for (int i = 0; i < m.Length; i++) if (m[i]) any[i] = true;
                }
                int unc = 0, over = 0, tot = 0;
                for (int i = 0; i < any.Length; i++)
                {
                    if (!img.Visible(i) || res.Ignore[i]) continue;
                    tot++;
                    bool cov = res.Reconstructed != null && res.Reconstructed.Pixels[i] != 0;
                    if (!cov && any[i]) unc++;
                    if (cov && !any[i]) over++;
                }
                Say("覆盖检查 : 非背景 " + tot + "px  未覆盖 " + unc + "  越界 " + over);
            }
            for (int i = 0; i < res.Layers.Count; i++)
            {
                LayerInfo li = res.Layers[i];
                Say("  层" + li.Rank + " " + li.Hex + " z=" + li.Z + " 块=" + li.Blocks +
                    " | 原图 " + li.OrigPixels + "px 轮廓 " + li.MaskPixels + "px 外轮廓 " + li.ShellCount +
                    " 孔洞 " + li.HoleCount + " | " + li.StatsLine);
            }
            Say("校验     : " + (issues.Count == 0 ? "通过" : string.Join("; ", issues.ToArray())));
            Say("输出     : " + jsonPath);
            File.WriteAllText(Path.Combine(outDir, name + ".report.txt"), string.Join("\r\n", Report.ToArray()), Encoding.UTF8);
            return issues.Count == 0 ? 0 : 1;
        }
    }
}
