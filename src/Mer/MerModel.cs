using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MerBuilder.Core;

namespace MerBuilder.Mer
{
    /// <summary>ProjectMER 方块（Empty 或 Primitive）。</summary>
    public sealed class MerBlock
    {
        public const int KindEmpty = 0;
        public const int KindPrimitive = 1;

        public const int Quad = 5;
        public const int FlagsNone = 0;
        public const int FlagsVisible = 2;
        public const int FlagsVisibleCollidable = 3;

        public string Name = "";
        public int ObjectId;
        public int ParentId;
        public V3 Position;
        public V3 Rotation;
        public V3 Scale = new V3(1, 1, 1);
        public int BlockType = KindPrimitive;

        public int PrimitiveType = Quad;
        public int PrimitiveFlags = FlagsVisible;
        public string Color = "#FFFFFF";
        public bool Static = true;

        /// <summary>预览用：所属色彩层序号（-1 表示非几何块）。</summary>
        public int LayerIndex = -1;
    }

    /// <summary>MER 文档：RootObjectId 恒为 0。</summary>
    public sealed class MerDocument
    {
        public const int RootObjectId = 0;
        public List<MerBlock> Blocks = new List<MerBlock>();

        public int PrimitiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Blocks.Count; i++) if (Blocks[i].BlockType == MerBlock.KindPrimitive) n++;
                return n;
            }
        }

        public int EmptyCount { get { return Blocks.Count - PrimitiveCount; } }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder(Blocks.Count * 220 + 64);
            sb.Append("{\"RootObjectId\":0,\"Blocks\":[");
            for (int i = 0; i < Blocks.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendBlock(sb, Blocks[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendBlock(StringBuilder sb, MerBlock b)
        {
            sb.Append("{\"Name\":\"");
            AppendEscaped(sb, b.Name);
            sb.Append("\",\"ObjectId\":").Append(b.ObjectId);
            sb.Append(",\"ParentId\":").Append(b.ParentId);
            sb.Append(",\"Position\":");
            AppendVec(sb, b.Position);
            sb.Append(",\"Rotation\":");
            AppendVec(sb, b.Rotation);
            sb.Append(",\"Scale\":");
            AppendVec(sb, b.Scale);
            sb.Append(",\"BlockType\":").Append(Num(b.BlockType));
            sb.Append(",\"Properties\":{");
            if (b.BlockType == MerBlock.KindPrimitive)
            {
                sb.Append("\"PrimitiveType\":").Append(Num(b.PrimitiveType));
                sb.Append(",\"PrimitiveFlags\":").Append(Num(b.PrimitiveFlags));
                sb.Append(",\"Color\":\"").Append(b.Color).Append("\"");
                sb.Append(",\"Static\":").Append(b.Static ? "true" : "false");
            }
            else
            {
                sb.Append("\"Static\":").Append(b.Static ? "true" : "false");
            }
            sb.Append("}}");
        }

        private static void AppendVec(StringBuilder sb, V3 v)
        {
            sb.Append("{\"x\":").Append(Num(v.X));
            sb.Append(",\"y\":").Append(Num(v.Y));
            sb.Append(",\"z\":").Append(Num(v.Z)).Append('}');
        }

        private static void AppendEscaped(StringBuilder sb, string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append(' ');
                else sb.Append(c);
            }
        }

        private static string Num(int v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>与参考实现一致的取整：5 位小数，|v|&lt;5e-6 归零。</summary>
        public static double Round(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0.0;
            double r = Math.Round(v, 5);
            return Math.Abs(r) < 0.000005 ? 0.0 : r;
        }

        public static V3 RoundVec(V3 v)
        {
            return new V3(Round(v.X), Round(v.Y), Round(v.Z));
        }

        public static string Num(double v)
        {
            double r = Round(v);
            if (r == 0.0) return "0";
            return r.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        /// <summary>MER 校验规则（等价于 mer_validation.validate_schematic）。</summary>
        public List<string> Validate()
        {
            List<string> issues = new List<string>();
            Dictionary<int, int> parents = new Dictionary<int, int>();
            for (int i = 0; i < Blocks.Count; i++)
            {
                MerBlock b = Blocks[i];
                string label = "Block " + (i + 1);
                if (b.ObjectId == RootObjectId) issues.Add(label + " 使用了保留根 ID 0。");
                if (b.ObjectId < 0 || b.ParentId < 0) issues.Add(label + " 的 ID 必须为非负整数。");
                if (parents.ContainsKey(b.ObjectId)) issues.Add("重复 ObjectId " + b.ObjectId + "。");
                else parents[b.ObjectId] = b.ParentId;
                if (b.BlockType != MerBlock.KindEmpty && b.BlockType != MerBlock.KindPrimitive)
                    issues.Add(label + " 的 BlockType 非法：" + b.BlockType);
                if (b.BlockType == MerBlock.KindPrimitive)
                {
                    if (b.PrimitiveType < 0 || b.PrimitiveType > 5) issues.Add(label + " PrimitiveType 非法。");
                    if (b.Color == null || b.Color.Length == 0) issues.Add(label + " 缺少 Color。");
                }
            }

            HashSet<int> connected = new HashSet<int>();
            connected.Add(RootObjectId);
            foreach (int id in parents.Keys)
            {
                HashSet<int> trail = new HashSet<int>();
                int cursor = id;
                while (!connected.Contains(cursor))
                {
                    if (trail.Contains(cursor) || !parents.ContainsKey(cursor))
                    {
                        issues.Add("ObjectId " + id + " 存在环或缺失父级 " + cursor + "。");
                        break;
                    }
                    trail.Add(cursor);
                    cursor = parents[cursor];
                }
                foreach (int t in trail) connected.Add(t);
            }
            return issues;
        }
    }
}
