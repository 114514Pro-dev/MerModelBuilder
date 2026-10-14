using System;
using System.Collections.Generic;

namespace MerBuilder.Core
{
    /// <summary>二维向量（图像像素坐标或 Unity 平面坐标共用）。</summary>
    public struct V2
    {
        public double X;
        public double Y;

        public V2(double x, double y) { X = x; Y = y; }

        public static V2 operator +(V2 a, V2 b) { return new V2(a.X + b.X, a.Y + b.Y); }
        public static V2 operator -(V2 a, V2 b) { return new V2(a.X - b.X, a.Y - b.Y); }
        public static V2 operator -(V2 a) { return new V2(-a.X, -a.Y); }
        public static V2 operator *(V2 a, double s) { return new V2(a.X * s, a.Y * s); }
        public static V2 operator *(double s, V2 a) { return new V2(a.X * s, a.Y * s); }

        public double Dot(V2 b) { return X * b.X + Y * b.Y; }
        public double Cross(V2 b) { return X * b.Y - Y * b.X; }
        public double Len { get { return Math.Sqrt(X * X + Y * Y); } }

        public override string ToString()
        {
            return "(" + X.ToString("0.#####") + "," + Y.ToString("0.#####") + ")";
        }
    }

    /// <summary>三维向量，字段语义与 MER JSON 一致（x/y/z）。</summary>
    public struct V3
    {
        public double X;
        public double Y;
        public double Z;

        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static V3 operator -(V3 a) { return new V3(-a.X, -a.Y, -a.Z); }
        public static V3 operator *(V3 a, double s) { return new V3(a.X * s, a.Y * s, a.Z * s); }
        public static V3 operator *(double s, V3 a) { return new V3(a.X * s, a.Y * s, a.Z * s); }

        public double Dot(V3 b) { return X * b.X + Y * b.Y + Z * b.Z; }
        public V3 Cross(V3 b)
        {
            return new V3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
        }
        public double Len { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
        public V3 Mul(V3 b) { return new V3(X * b.X, Y * b.Y, Z * b.Z); }
    }

    /// <summary>3x3 矩阵，行优先 m[row][col]，与参考实现一致。</summary>
    public sealed class M3
    {
        public readonly double[][] M;

        public M3()
        {
            M = new double[3][];
            for (int i = 0; i < 3; i++) M[i] = new double[3];
        }

        public M3(double m00, double m01, double m02,
                  double m10, double m11, double m12,
                  double m20, double m21, double m22)
        {
            M = new double[3][];
            M[0] = new double[] { m00, m01, m02 };
            M[1] = new double[] { m10, m11, m12 };
            M[2] = new double[] { m20, m21, m22 };
        }

        public double At(int r, int c) { return M[r][c]; }
    }

    /// <summary>二维几何工具集。</summary>
    public static class G2
    {
        public const double SinEps = 0.005;
        public const double LengthEps = 0.001;
        public const double ConvexEps = 0.0000001;

        public static V2 Sub(V2 a, V2 b) { return new V2(a.X - b.X, a.Y - b.Y); }
        public static double Cross(V2 a, V2 b) { return a.X * b.Y - a.Y * b.X; }
        public static double Dot(V2 a, V2 b) { return a.X * b.X + a.Y * b.Y; }
        public static double Len(V2 a) { return Math.Sqrt(a.X * a.X + a.Y * a.Y); }

        public static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        public static double SignedArea(IList<V2> points)
        {
            double total = 0.0;
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                V2 p = points[i];
                V2 q = points[(i + 1) % n];
                total += p.X * q.Y - q.X * p.Y;
            }
            return total * 0.5;
        }

        public static bool IsConvex(IList<V2> points)
        {
            int n = points.Count;
            if (n < 3) return false;
            bool gotPos = false, gotNeg = false;
            for (int i = 0; i < n; i++)
            {
                V2 a = points[i];
                V2 b = points[(i + 1) % n];
                V2 c = points[(i + 2) % n];
                double cross = Cross(Sub(b, a), Sub(c, b));
                if (cross > ConvexEps) gotPos = true;
                else if (cross < -ConvexEps) gotNeg = true;
                if (gotPos && gotNeg) return false;
            }
            return true;
        }

        public static bool AreParallelAndEqual(V2 a, V2 b)
        {
            double la = Len(a), lb = Len(b);
            if (la < 1e-7 || lb < 1e-7) return false;
            if (Math.Abs(Cross(a, b)) / (la * lb) >= SinEps) return false;
            if (Dot(a, b) <= 0.0) return false;
            return Math.Abs(la - lb) / Math.Max(la, lb) < LengthEps;
        }

        /// <summary>点是否在逆时针凸多边形内部（含边界，eps 容差）。</summary>
        public static bool InsideConvexCcw(V2 p, IList<V2> poly, double eps)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                V2 a = poly[i];
                V2 b = poly[(i + 1) % n];
                if (Cross(Sub(b, a), Sub(p, a)) < -eps) return false;
            }
            return true;
        }

        /// <summary>去掉共线/重复顶点（比率阈值 0.0005，与参考一致）。</summary>
        public static List<V2> DropCollinear(IList<V2> points)
        {
            List<V2> result = new List<V2>(points);
            bool changed = true;
            while (changed && result.Count > 3)
            {
                changed = false;
                for (int i = 0; i < result.Count; i++)
                {
                    V2 a = result[(i - 1 + result.Count) % result.Count];
                    V2 b = result[i];
                    V2 c = result[(i + 1) % result.Count];
                    V2 ab = Sub(b, a), bc = Sub(c, b);
                    double lab = Len(ab), lbc = Len(bc);
                    if (lab < 1e-9 || lbc < 1e-9 || Math.Abs(Cross(ab, bc)) / (lab * lbc) < 0.0005)
                    {
                        result.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>射线法点在多边形内判定（用于孔洞嵌套归属）。</summary>
        public static bool PointInPolygon(V2 p, IList<V2> poly)
        {
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                V2 a = poly[i];
                V2 b = poly[j];
                bool straddles = (a.Y > p.Y) != (b.Y > p.Y);
                if (!straddles) continue;
                double x = (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X;
                if (p.X < x) inside = !inside;
            }
            return inside;
        }

        /// <summary>线段相交（不含端点相碰的退化情形）。</summary>
        public static bool SegmentsIntersect(V2 p1, V2 p2, V2 p3, V2 p4)
        {
            double d1 = Cross(Sub(p3, p4), Sub(p1, p4));
            double d2 = Cross(Sub(p3, p4), Sub(p2, p4));
            double d3 = Cross(Sub(p1, p2), Sub(p3, p2));
            double d4 = Cross(Sub(p1, p2), Sub(p4, p2));
            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return true;
            return false;
        }

        /// <summary>点是否在三角形内（含边界）。</summary>
        public static bool PointInTriangle(V2 p, V2 a, V2 b, V2 c)
        {
            double d1 = Cross(Sub(b, a), Sub(p, a));
            double d2 = Cross(Sub(c, b), Sub(p, b));
            double d3 = Cross(Sub(a, c), Sub(p, c));
            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        public static List<V2> Reversed(IList<V2> src)
        {
            List<V2> r = new List<V2>(src);
            r.Reverse();
            return r;
        }
    }
}
