using System;
using MerBuilder.Core;

namespace MerBuilder.Mer
{
    /// <summary>
    /// 把平面平行四边形/三角形发射为标准 MER 四边形基元（必要时用 TRS 层级剪切）。
    /// 逻辑 1:1 移植自参考实现 mer_triangle_primitives.py。
    /// </summary>
    public static class MerPrimitives
    {
        public const double Epsilon = 0.000001;

        public static V3 Add3(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static V3 Sub3(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static V3 Mul3(V3 a, double s) { return new V3(a.X * s, a.Y * s, a.Z * s); }
        public static double Dot3(V3 a, V3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        public static V3 Cross3(V3 a, V3 b)
        {
            return new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }
        public static double Length3(V3 a) { return Math.Sqrt(Dot3(a, a)); }

        public static V3 Normalize3(V3 a)
        {
            double l = Length3(a);
            if (l < Epsilon) return new V3(0, 0, 0);
            return new V3(a.X / l, a.Y / l, a.Z / l);
        }

        public static bool IsZero(V3 a) { return Length3(a) < Epsilon; }

        public static V3 ProjectOnPlane(V3 vector, V3 normal)
        {
            return Sub3(vector, Mul3(normal, Dot3(vector, normal)));
        }

        public static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        public static M3 LookRotation(V3 forward, V3 upwards)
        {
            V3 zAxis = Normalize3(forward);
            if (IsZero(zAxis)) return null;
            V3 xAxis = Normalize3(Cross3(upwards, zAxis));
            if (IsZero(xAxis)) return null;
            V3 yAxis = Cross3(zAxis, xAxis);
            return new M3(
                xAxis.X, yAxis.X, zAxis.X,
                xAxis.Y, yAxis.Y, zAxis.Y,
                xAxis.Z, yAxis.Z, zAxis.Z);
        }

        public static V3 MatrixToUnityEulerZxy(M3 m)
        {
            double x = Math.Asin(Clamp(-m.At(1, 2), -1.0, 1.0));
            double cosX = Math.Cos(x);
            double y, z;
            if (Math.Abs(cosX) > Epsilon)
            {
                y = Math.Atan2(m.At(0, 2), m.At(2, 2));
                z = Math.Atan2(m.At(1, 0), m.At(1, 1));
            }
            else
            {
                y = Math.Atan2(-m.At(2, 0), m.At(0, 0));
                z = 0.0;
            }
            const double R = 180.0 / Math.PI;
            return new V3(x * R, y * R, z * R);
        }

        public static M3 UnityEulerZxyToMatrix(V3 euler)
        {
            const double D = Math.PI / 180.0;
            double x = euler.X * D, y = euler.Y * D, z = euler.Z * D;
            double cx = Math.Cos(x), sx = Math.Sin(x);
            double cy = Math.Cos(y), sy = Math.Sin(y);
            double cz = Math.Cos(z), sz = Math.Sin(z);
            return new M3(
                (cy * cz) + (sy * sx * sz), (-cy * sz) + (sy * sx * cz), sy * cx,
                cx * sz, cx * cz, -sx,
                (-sy * cz) + (cy * sx * sz), (sy * sz) + (cy * sx * cz), cy * cx);
        }

        /// <summary>单个四边形贴片；不成正方形平行四边形时返回 null。</summary>
        public static MerBlock TryBuildRectangleTile(string name, int objectId, int parentId,
            V3 vUp, V3 vLeft, V3 center, string color, bool isStatic)
        {
            double upLength = Length3(vUp);
            double leftLength = Length3(vLeft);
            if (upLength < Epsilon || leftLength < Epsilon) return null;
            if (Math.Abs(upLength - leftLength) > Math.Max(upLength, leftLength) * 0.0001) return null;

            V3 edgeA = Add3(vLeft, vUp);
            V3 edgeB = Sub3(vLeft, vUp);
            double width = Length3(edgeB);
            double height = Length3(edgeA);
            if (width < Epsilon || height < Epsilon) return null;

            V3 forward = Cross3(edgeB, edgeA);
            M3 rot = LookRotation(forward, edgeA);
            if (rot == null) return null;

            MerBlock b = new MerBlock();
            b.Name = name;
            b.ObjectId = objectId;
            b.ParentId = parentId;
            b.Position = MerDocument.RoundVec(center);
            b.Rotation = MerDocument.RoundVec(MatrixToUnityEulerZxy(rot));
            b.Scale = MerDocument.RoundVec(new V3(width, height, 1.0));
            b.BlockType = MerBlock.KindPrimitive;
            b.PrimitiveType = MerBlock.Quad;
            b.PrimitiveFlags = MerBlock.FlagsVisible;
            b.Color = color;
            b.Static = isStatic;
            return b;
        }

        /// <summary>剪切变换：父 Empty 的旋转/缩放 + 子 Quad 的 Z 角与缩放。</summary>
        public static bool TryGetShearTransforms(V3 vUp, V3 vLeft,
            out V3 parentRotation, out V3 parentScale, out double childAngle, out V3 childScale)
        {
            parentRotation = new V3(0, 0, 0);
            parentScale = new V3(1, 1, 1);
            childAngle = 0;
            childScale = new V3(1, 1, 1);

            if (Dot3(vUp, vUp) < Epsilon * Epsilon || Dot3(vLeft, vLeft) < Epsilon * Epsilon) return false;

            double upSqr = Dot3(vUp, vUp);
            double dotAbs = Math.Abs(Dot3(vLeft, vUp));
            if (dotAbs >= upSqr - Math.Max(upSqr * 0.000001, 0.000000001))
            {
                V3 oldUp = vUp;
                vUp = vLeft;
                vLeft = Mul3(oldUp, -1.0);
            }

            double upLength = Length3(vUp);
            V3 upNormal = Normalize3(vUp);
            if (IsZero(upNormal)) return false;

            V3 perpendicular = ProjectOnPlane(vLeft, upNormal);
            if (Dot3(perpendicular, perpendicular) < Epsilon * Epsilon) return false;

            V3 perpNormal = Normalize3(perpendicular);
            if (IsZero(perpNormal)) return false;

            if (Dot3(perpNormal, vLeft) < 0.0) perpNormal = Mul3(perpNormal, -1.0);

            V3 normal = Cross3(perpNormal, upNormal);
            V3 normalNorm = Normalize3(normal);
            if (IsZero(normalNorm)) return false;

            double leftY = Clamp(Dot3(vLeft, upNormal), -upLength, upLength);
            double leftX = Length3(ProjectOnPlane(vLeft, upNormal));
            double a = Math.Sqrt(Math.Max(2.0 * upLength * (upLength + leftY), 0.000000001));
            double b = Math.Sqrt(Math.Max(2.0 * upLength * (upLength - leftY), 0.000000001));
            double sx = leftX * 2.0 * upLength / Math.Max(a * b, 0.000000001);
            double angle = -Math.Atan2(b, a) * (180.0 / Math.PI);

            M3 parentMatrix = LookRotation(normalNorm, upNormal);
            if (parentMatrix == null) return false;

            parentRotation = MatrixToUnityEulerZxy(parentMatrix);
            parentScale = new V3(sx, 1.0, 1.0);
            childAngle = angle;
            childScale = new V3(b, a, 1.0);
            return true;
        }

        /// <summary>三角形的 3 个平行四边形贴片（含参考实现的 B/C 交换）。</summary>
        public static void TriangleParallelogramTiles(V3 pointA, V3 pointB, V3 pointC,
            out string[] suffixes, out V3[] ups, out V3[] lefts, out V3[] centers)
        {
            V3 tmp = pointB; pointB = pointC; pointC = tmp;
            V3 midAB = Mul3(Add3(pointA, pointB), 0.5);
            V3 midBC = Mul3(Add3(pointB, pointC), 0.5);
            V3 midCA = Mul3(Add3(pointC, pointA), 0.5);

            V3 centerA = Mul3(Add3(pointA, midBC), 0.5);
            V3 centerB = Mul3(Add3(pointB, midCA), 0.5);
            V3 centerC = Mul3(Add3(pointC, midAB), 0.5);

            suffixes = new string[] { "a", "b", "c" };
            ups = new V3[] { Sub3(midCA, centerA), Sub3(midAB, centerB), Sub3(midBC, centerC) };
            lefts = new V3[] { Sub3(pointA, centerA), Sub3(pointB, centerB), Sub3(pointC, centerC) };
            centers = new V3[] { centerA, centerB, centerC };
        }

        /// <summary>三角形 -> 3 片四边形基元（每片可能退化为 Empty+Quad）。返回新 ID。</summary>
        public static void TrianglePointsToPrimitiveBlocks(string name, int objectIdStart, int parentId,
            V3 pointA, V3 pointB, V3 pointC, string color, bool isStatic,
            System.Collections.Generic.List<MerBlock> blocks, int layerIndex, ref int nextId)
        {
            string[] suffixes; V3[] ups, lefts, centers;
            TriangleParallelogramTiles(pointA, pointB, pointC, out suffixes, out ups, out lefts, out centers);
            nextId = objectIdStart;
            for (int i = 0; i < 3; i++)
            {
                string tileName = name + "-tile-" + suffixes[i];
                MerBlock rect = TryBuildRectangleTile(tileName, nextId, parentId, ups[i], lefts[i], centers[i], color, isStatic);
                if (rect != null)
                {
                    rect.LayerIndex = layerIndex;
                    blocks.Add(rect);
                    nextId++;
                    continue;
                }

                V3 pRot, pScale, cScale;
                double cAngle;
                if (!TryGetShearTransforms(ups[i], lefts[i], out pRot, out pScale, out cAngle, out cScale))
                    continue;

                int shearId = nextId;
                MerBlock parent = new MerBlock();
                parent.Name = tileName + "-shear";
                parent.ObjectId = shearId;
                parent.ParentId = parentId;
                parent.Position = MerDocument.RoundVec(centers[i]);
                parent.Rotation = MerDocument.RoundVec(pRot);
                parent.Scale = MerDocument.RoundVec(pScale);
                parent.BlockType = MerBlock.KindEmpty;
                parent.Static = isStatic;
                blocks.Add(parent);
                nextId++;

                MerBlock child = new MerBlock();
                child.Name = tileName;
                child.ObjectId = nextId;
                child.ParentId = shearId;
                child.Position = new V3(0, 0, 0);
                child.Rotation = new V3(0, 0, MerDocument.Round(cAngle));
                child.Scale = MerDocument.RoundVec(cScale);
                child.BlockType = MerBlock.KindPrimitive;
                child.PrimitiveType = MerBlock.Quad;
                child.PrimitiveFlags = MerBlock.FlagsVisible;
                child.Color = color;
                child.Static = isStatic;
                child.LayerIndex = layerIndex;
                blocks.Add(child);
                nextId++;
            }
        }
    }
}
