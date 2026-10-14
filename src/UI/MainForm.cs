using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using MerBuilder.Image;
using MerBuilder.Pipeline;

namespace MerBuilder.UI
{
    /// <summary>深色主题调色板与绘制工具。</summary>
    internal static class Theme
    {
        public static readonly Color Window = Color.FromArgb(11, 15, 20);
        public static readonly Color Panel = Color.FromArgb(18, 24, 32);
        public static readonly Color PanelAlt = Color.FromArgb(23, 30, 40);
        public static readonly Color TabActive = Color.FromArgb(30, 41, 54);
        public static readonly Color Border = Color.FromArgb(34, 45, 58);
        public static readonly Color BorderSoft = Color.FromArgb(27, 36, 47);
        public static readonly Color Text = Color.FromArgb(230, 238, 246);
        public static readonly Color TextSub = Color.FromArgb(148, 162, 178);
        public static readonly Color TextMuted = Color.FromArgb(102, 116, 132);
        public static readonly Color Accent = Color.FromArgb(46, 190, 112);
        public static readonly Color AccentHover = Color.FromArgb(66, 214, 134);
        public static readonly Color AccentText = Color.FromArgb(9, 22, 15);
        public static readonly Color Danger = Color.FromArgb(198, 84, 84);
        public static readonly Color DangerHover = Color.FromArgb(222, 104, 104);
        public static readonly Color Preview = Color.FromArgb(8, 11, 15);

        public static readonly Font Body = new Font("Microsoft YaHei UI", 9f);
        public static readonly Font BodyBold = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        public static readonly Font Small = new Font("Microsoft YaHei UI", 8f);
        public static readonly Font Brand = new Font("Microsoft YaHei UI", 21f, FontStyle.Bold);
        public static readonly Font Crystal = new Font("Consolas", 8.5f);
        public static readonly Font CardTitle = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0 || r.Width < radius * 2 + 2 || r.Height < radius * 2 + 2)
            {
                p.AddRectangle(r);
                return p;
            }
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public const TextFormatFlags Left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        public const TextFormatFlags Wrap = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
    }

    /// <summary>圆角卡片容器（可选竖向渐变）。</summary>
    internal class CardPanel : Panel
    {
        public int Radius = 10;
        public Color BorderColor = Theme.Border;
        public Color Fill = Theme.Panel;
        public bool Gradient;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Window;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.Rounded(r, Radius))
            {
                if (Gradient)
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(r, Theme.PanelAlt, Fill, 90f))
                        g.FillPath(b, p);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Fill)) g.FillPath(b, p);
                }
                using (Pen pen = new Pen(BorderColor)) g.DrawPath(pen, p);
            }
            base.OnPaint(e);
        }
    }

    /// <summary>自绘扁平按钮：普通 / 主色 / 幽灵 / 危险。</summary>
    internal sealed class FlatButton : Control
    {
        public enum Style { Normal, Accent, Ghost, Danger }

        public Style Kind = Style.Normal;
        private bool _hover, _down;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Window;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill, border, text;
            switch (Kind)
            {
                case Style.Accent:
                    fill = _hover ? Theme.AccentHover : Theme.Accent;
                    border = fill;
                    text = Theme.AccentText;
                    break;
                case Style.Ghost:
                    fill = _hover ? Theme.PanelAlt : Color.Transparent;
                    border = Color.Transparent;
                    text = _hover ? Theme.Text : Theme.TextSub;
                    break;
                case Style.Danger:
                    fill = _hover ? Color.FromArgb(48, 26, 26) : Color.Transparent;
                    border = Theme.Danger;
                    text = _hover ? Theme.DangerHover : Theme.Danger;
                    break;
                default:
                    fill = _hover ? Theme.TabActive : Theme.PanelAlt;
                    border = _hover ? Theme.Border : Theme.BorderSoft;
                    text = Theme.Text;
                    break;
            }
            if (_down && Kind != Style.Accent) fill = Theme.PanelAlt;

            using (GraphicsPath p = Theme.Rounded(r, 8))
            {
                if (fill.A > 0) { using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p); }
                if (border.A > 0) { using (Pen pen = new Pen(border)) g.DrawPath(pen, p); }
            }
            TextRenderer.DrawText(g, Text, Font, r, Enabled ? text : Theme.TextMuted, Theme.Center);
        }
    }

    /// <summary>方案卡片：标题 + 两行统计，可选中/悬停。</summary>
    internal sealed class SchemeCard : Control
    {
        public string Title = "";
        public string Line1 = "";
        public string Line2 = "";
        public bool Selected;
        private bool _hover;

        public SchemeCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Window;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Selected ? Theme.PanelAlt : Theme.Panel;
            Color border = Selected ? Theme.Accent : (_hover ? Theme.Border : Theme.BorderSoft);
            using (GraphicsPath p = Theme.Rounded(r, 8))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                using (Pen pen = new Pen(border)) g.DrawPath(pen, p);
            }
            if (Selected)
            {
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                using (GraphicsPath p2 = Theme.Rounded(new Rectangle(0, 12, 3, Height - 25), 2))
                    g.FillPath(b, p2);
            }
            TextRenderer.DrawText(g, Title, Theme.CardTitle, new Rectangle(12, 8, Width - 20, 18),
                Selected ? Theme.Text : Theme.TextSub, Theme.Left);
            TextRenderer.DrawText(g, Line1, Theme.Small, new Rectangle(12, 30, Width - 20, 16), Theme.TextSub, Theme.Left);
            TextRenderer.DrawText(g, Line2, Theme.Small, new Rectangle(12, 47, Width - 20, 16), Theme.TextMuted, Theme.Left);
        }
    }

    /// <summary>分段标签：普通（胶囊底色）或下划线式。</summary>
    internal sealed class SegTabs : Control
    {
        public string[] Items = new string[0];
        public int Selected;
        public bool Underline;
        public event EventHandler Changed;
        private int _hover = -1;

        public SegTabs()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Window;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
        }

        private int Hit(Point p)
        {
            if (Items.Length == 0) return -1;
            int seg = Math.Max(1, Width / Items.Length);
            int i = p.X / seg;
            if (i < 0) i = 0;
            if (i >= Items.Length) i = Items.Length - 1;
            return i;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = Hit(e.Location);
            if (h != _hover) { _hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int h = Hit(e.Location);
            if (h >= 0 && h != Selected)
            {
                Selected = h;
                Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Items.Length == 0) return;
            int seg = Width / Items.Length;

            if (Underline)
            {
                for (int i = 0; i < Items.Length; i++)
                {
                    Rectangle r = new Rectangle(i * seg + 8, 0, seg - 16, Height);
                    Color c = i == Selected ? Theme.Text : (_hover == i ? Theme.TextSub : Theme.TextMuted);
                    TextRenderer.DrawText(g, Items[i], Font, r, c, Theme.Center);
                }
                int tw = TextRenderer.MeasureText(Items[Selected], Font).Width;
                int cxp = Selected * seg + seg / 2;
                using (Pen pen = new Pen(Theme.Accent, 2))
                    g.DrawLine(pen, cxp - tw / 2 - 4, Height - 3, cxp + tw / 2 + 4, Height - 3);
                return;
            }

            Rectangle box = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.Rounded(box, 9))
            {
                using (SolidBrush b = new SolidBrush(Theme.PanelAlt)) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.BorderSoft)) g.DrawPath(pen, p);
            }
            for (int i = 0; i < Items.Length; i++)
            {
                Rectangle r = new Rectangle(i * seg + 2, 2, seg - 4, Height - 5);
                bool act = i == Selected;
                if (act)
                {
                    using (GraphicsPath p = Theme.Rounded(r, 7))
                    {
                        using (SolidBrush b = new SolidBrush(Theme.TabActive)) g.FillPath(b, p);
                        using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
                    }
                }
                TextRenderer.DrawText(g, Items[i], Font, r,
                    act ? Theme.Text : (_hover == i ? Theme.TextSub : Theme.TextMuted), Theme.Center);
            }
        }
    }

    /// <summary>可折叠分组标题：点击展开/收起目标面板。</summary>
    internal sealed class CollapseHeader : Control
    {
        public string Title = "";
        public string Sub = "";
        public bool Expanded;
        public Control Target;
        private bool _hover;

        public CollapseHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Window;
            Font = Theme.BodyBold;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Expanded = !Expanded;
            if (Target != null) Target.Visible = Expanded;
            if (Parent != null) Parent.PerformLayout();
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush b = new SolidBrush(_hover ? Theme.PanelAlt : Theme.Window))
            using (GraphicsPath p = Theme.Rounded(r, 8))
                g.FillPath(b, p);

            string caret = Expanded ? "\u25BE" : "\u25B8";
            TextRenderer.DrawText(g, caret, Theme.Body, new Rectangle(8, 0, 18, Height),
                Theme.TextSub, Theme.Left);
            TextRenderer.DrawText(g, Title, Font, new Rectangle(28, 0, Width - 34, Height),
                Theme.Text, Theme.Left);
            if (!string.IsNullOrEmpty(Sub))
            {
                int tw = TextRenderer.MeasureText(Title, Font).Width;
                TextRenderer.DrawText(g, Sub, Theme.Small, new Rectangle(28 + tw + 10, 0, Width - 40 - tw, Height),
                    Theme.TextMuted, Theme.Left);
            }
            using (Pen pen = new Pen(Theme.BorderSoft))
                g.DrawLine(pen, 8, Height - 1, Width - 8, Height - 1);
        }
    }

    /// <summary>图像/预览画布。</summary>
    internal sealed class ImageBox : Control
    {
        public Bitmap SourceImage;
        public ConvertResult Result;
        public int View;          // 0 正视 / 1 旋转 / 2 随机
        public double Yaw;
        public bool Placeholder = true;

        public ImageBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Preview;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Preview);

            if (Result != null)
            {
                if (View == 0) PreviewRenderer.DrawReconstructed(g, Result.Reconstructed, ClientRectangle, Theme.Preview);
                else PreviewRenderer.DrawQuads(g, Result, ClientRectangle, Theme.Preview, Yaw);
                return;
            }

            if (SourceImage != null)
            {
                double s = Math.Min((double)(Width - 16) / SourceImage.Width, (double)(Height - 16) / SourceImage.Height);
                if (s > 0)
                {
                    int w = Math.Max(1, (int)(SourceImage.Width * s));
                    int h = Math.Max(1, (int)(SourceImage.Height * s));
                    int ox = (Width - w) / 2, oy = (Height - h) / 2;
                    InterpolationMode old = g.InterpolationMode;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(SourceImage, new Rectangle(ox, oy, w, h));
                    g.InterpolationMode = old;
                }
                return;
            }

            if (Placeholder)
                TextRenderer.DrawText(g, "未选择参考图", Theme.Small, ClientRectangle, Theme.TextMuted, Theme.Center);
        }
    }

    public sealed partial class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private sealed class SchemeState
        {
            public ConvertOptions Opt;
            public ConvertResult Res;
            public bool Done;
        }

        private const int LeftWidth = 310;

        // 数据
        private ImageData _source;
        private string _sourcePath;
        private readonly List<SchemeState> _schemes = new List<SchemeState>();
        private int _current = -1;
        private bool _busy;
        private bool _cancel;
        private readonly Random _rng = new Random();

        // 控件
        private ImageBox _thumb, _preview;
        private Panel _schemeRow;
        private readonly List<SchemeCard> _cards = new List<SchemeCard>();
        private Label _statusLeft, _previewStats;
        private TextBox _log;
        private SegTabs _tabsMain, _tabsView;
        private FlatButton _btnGen, _btnUnity, _btnExport, _btnCancel, _btnPick, _btnClear, _btnOpenOut;
        private CheckBox _chkOpt, _chkNoEmpty, _chkNoGrad, _chkFlat, _chkBudget, _chkGlass;
        private NumericUpDown _numBudget, _numTol, _numMinArea, _numBlur, _numWidth, _numPalette;
        private TextBox _txtOut;
        private List<CardPanel> _cardPanels = new List<CardPanel>();

        public MainForm()
        {
            Text = "MER 离线建模助手";
            MinimumSize = new Size(1180, 760);
            Size = new Size(1320, 850);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            DoubleBuffered = true;
            BuildUi();
            AppendLog("MER 离线建模助手已就绪 · 请选择参考图后点击「生成预览」。");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)); } catch { }
        }

        // ---------------------------------------------------------------- 布局
        private void BuildUi()
        {
            Panel header = BuildHeader();
            Panel status = BuildStatusBar();
            Panel left = BuildLeftColumn();
            Panel content = BuildContent();

            Controls.Add(content);
            Controls.Add(left);
            Controls.Add(header);
            Controls.Add(status);
        }

        private Panel BuildHeader()
        {
            Panel h = new Panel();
            h.Dock = DockStyle.Top;
            h.Height = 74;
            h.BackColor = Theme.Window;
            h.Paint += delegate (object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                TextRenderer.DrawText(g, "MER MODEL BUILDER", Theme.Crystal,
                    new Rectangle(22, 12, 300, 14), Theme.TextMuted, Theme.Left);
                TextRenderer.DrawText(g, "离线图像建模", Theme.Brand, new Rectangle(20, 24, 420, 40), Theme.Text, Theme.Left);
                string right = "图像算法 · 少块优先";
                int w = TextRenderer.MeasureText(right, Theme.Small).Width;
                TextRenderer.DrawText(g, right, Theme.Small,
                    new Rectangle(h.Width - w - 24, 30, w, 18), Theme.TextMuted, Theme.Left);
                using (Pen pen = new Pen(Theme.BorderSoft))
                    g.DrawLine(pen, 0, h.Height - 1, h.Width, h.Height - 1);
            };
            return h;
        }

        private Panel BuildStatusBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 26;
            bar.BackColor = Color.FromArgb(8, 11, 15);
            bar.Paint += delegate (object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.BorderSoft)) e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
            };
            _statusLeft = new Label();
            _statusLeft.AutoSize = false;
            _statusLeft.Dock = DockStyle.Left;
            _statusLeft.Width = 900;
            _statusLeft.Padding = new Padding(16, 0, 0, 0);
            _statusLeft.TextAlign = ContentAlignment.MiddleLeft;
            _statusLeft.ForeColor = Theme.TextSub;
            _statusLeft.Font = Theme.Small;
            _statusLeft.BackColor = Color.Transparent;
            _statusLeft.Text = "就绪 · 等待选择参考图";
            bar.Controls.Add(_statusLeft);

            Label ver = new Label();
            ver.AutoSize = false;
            ver.Dock = DockStyle.Right;
            ver.Width = 220;
            ver.TextAlign = ContentAlignment.MiddleRight;
            ver.Padding = new Padding(0, 0, 16, 0);
            ver.ForeColor = Theme.TextMuted;
            ver.Font = Theme.Small;
            ver.BackColor = Color.Transparent;
            ver.Text = "v1.3.4 · C# / WinForms";
            bar.Controls.Add(ver);
            return bar;
        }

        private Panel BuildLeftColumn()
        {
            Panel col = new Panel();
            col.Dock = DockStyle.Left;
            col.Width = LeftWidth;
            col.BackColor = Theme.Window;
            col.Paint += delegate (object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.BorderSoft)) e.Graphics.DrawLine(pen, col.Width - 1, 0, col.Width - 1, col.Height);
            };

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            flow.BackColor = Theme.Window;
            flow.Padding = new Padding(14, 12, 8, 16);
            col.Controls.Add(flow);

            const int W = 272;

            _tabsMain = new SegTabs();
            _tabsMain.Items = new string[] { "主方案", "备用方案" };
            _tabsMain.Size = new Size(W, 34);
            _tabsMain.Margin = new Padding(0, 0, 0, 12);
            _tabsMain.Changed += delegate { AppendLog("已切换到「" + _tabsMain.Items[_tabsMain.Selected] + "」。"); };
            flow.Controls.Add(_tabsMain);

            flow.Controls.Add(MakeLabel("圆柱 · 样本圆环 + 几何填充", Theme.CardTitle, Theme.Text, W, 4, 0));
            flow.Controls.Add(MakeWrap("按轮廓提取实色区域，逐层生成轴对齐四边形块；背景色层默认剔除，块数少、缝隙无。", Theme.Small, Theme.TextMuted, W, 0, 14));

            flow.Controls.Add(MakeLabel("参考图", Theme.BodyBold, Theme.TextSub, W, 0, 6));
            flow.Controls.Add(BuildSourceRow(W));

            _chkOpt = MakeCheck("优化模型", true, W);
            _chkNoEmpty = MakeCheck("不使用 Empty", false, W);
            _chkNoGrad = MakeCheck("无法梯度填充生成", false, W);
            _chkFlat = MakeCheck("5 厘米厚 · 0.1 米，模型整体可切为方块", false, W);
            flow.Controls.Add(_chkOpt);
            flow.Controls.Add(_chkNoEmpty);
            flow.Controls.Add(_chkNoGrad);
            flow.Controls.Add(_chkFlat);

            flow.Controls.Add(BuildBudgetRow(W));
            flow.Controls.Add(MakeWrap("上限 2000 总块（实体 + Empty）；勾选后按自定义值限制，超出将自动提升化简强度。", Theme.Small, Theme.TextMuted, W, 0, 14));

            flow.Controls.Add(BuildCollapse("节点设置", "阈值 · 面积", W, BuildNodeGroup(W)));
            flow.Controls.Add(BuildCollapse("样本优化（可选）", "模糊 σ · 配色", W, BuildSampleGroup(W)));
            flow.Controls.Add(BuildCollapse("Unity 与导出", "宽度 · 目录", W, BuildExportGroup(W)));
            flow.Controls.Add(BuildCollapse("外观 · 毛玻璃与渐变", "卡片质感", W, BuildAppearanceGroup(W)));

            return col;
        }

        private Panel BuildSourceRow(int W)
        {
            Panel row = new Panel();
            row.BackColor = Theme.Window;
            row.Size = new Size(W, 104);
            row.Margin = new Padding(0, 0, 0, 12);

            _thumb = new ImageBox();
            _thumb.Size = new Size(100, 100);
            _thumb.Location = new Point(0, 0);
            _thumb.Placeholder = true;

            _btnPick = new FlatButton();
            _btnPick.Text = "选择图片";
            _btnPick.Size = new Size(96, 30);
            _btnPick.Location = new Point(110, 2);
            _btnPick.Click += delegate { PickImage(); };

            _btnClear = new FlatButton();
            _btnClear.Text = "清除";
            _btnClear.Kind = FlatButton.Style.Ghost;
            _btnClear.Size = new Size(96, 28);
            _btnClear.Location = new Point(110, 38);
            _btnClear.Click += delegate { ClearImage(); };

            Label hint = new Label();
            hint.AutoSize = false;
            hint.Size = new Size(96, 34);
            hint.Location = new Point(110, 70);
            hint.ForeColor = Theme.TextMuted;
            hint.Font = Theme.Small;
            hint.BackColor = Color.Transparent;
            hint.Text = "支持 JPG / PNG / BMP";

            row.Controls.Add(_thumb);
            row.Controls.Add(_btnPick);
            row.Controls.Add(_btnClear);
            row.Controls.Add(hint);
            return row;
        }

        private Panel BuildBudgetRow(int W)
        {
            Panel row = new Panel();
            row.BackColor = Theme.Window;
            row.Size = new Size(W, 30);
            row.Margin = new Padding(0, 0, 0, 8);

            _chkBudget = MakeCheck("自定义总块数", false, 160);
            _chkBudget.Location = new Point(0, 4);
            _chkBudget.CheckedChanged += delegate { _numBudget.Enabled = _chkBudget.Checked; };

            _numBudget = MakeNumeric(1500, 50, 200000, 0);
            _numBudget.Size = new Size(96, 26);
            _numBudget.Location = new Point(W - 96, 1);
            _numBudget.Enabled = false;

            row.Controls.Add(_chkBudget);
            row.Controls.Add(_numBudget);
            return row;
        }

        private Panel BuildCollapse(string title, string sub, int W, Panel target)
        {
            CollapseHeader head = new CollapseHeader();
            head.Title = title;
            head.Sub = sub;
            head.Size = new Size(W, 32);
            head.Margin = new Padding(0, 0, 0, 0);
            head.Target = target;

            // 用容器把标题与内容打包，确保 FlowLayoutPanel 顺序正确
            Panel wrap = new Panel();
            wrap.BackColor = Theme.Window;
            wrap.Size = new Size(W, 32);
            wrap.Margin = new Padding(0, 0, 0, 8);
            head.Location = new Point(0, 0);
            head.Width = W;
            wrap.Controls.Add(head);
            target.Visible = false;
            target.Location = new Point(0, 32);
            target.Width = W;
            wrap.Controls.Add(target);
            head.MouseDown += delegate
            {
                wrap.Height = head.Expanded ? 32 + target.Height + 6 : 32;
                wrap.PerformLayout();
            };
            return wrap;
        }

        private Panel BuildNodeGroup(int W)
        {
            Panel g = new Panel();
            g.BackColor = Theme.Window;
            g.Size = new Size(W, 62);
            Label l1 = MakeLabel("简化阈值 (px)", Theme.Small, Theme.TextSub, 120, 0, 0);
            l1.Location = new Point(0, 6);
            g.Controls.Add(l1);
            _numTol = MakeNumeric(0.8m, 0.1m, 20m, 1, 1);
            _numTol.Size = new Size(96, 24);
            _numTol.Location = new Point(140, 2);
            g.Controls.Add(_numTol);
            Label l2 = MakeLabel("最小面积 (px)", Theme.Small, Theme.TextSub, 120, 0, 0);
            l2.Location = new Point(0, 36);
            g.Controls.Add(l2);
            _numMinArea = MakeNumeric(6m, 1m, 5000m, 1, 1);
            _numMinArea.Size = new Size(96, 24);
            _numMinArea.Location = new Point(140, 32);
            g.Controls.Add(_numMinArea);
            return g;
        }

        private Panel BuildSampleGroup(int W)
        {
            Panel g = new Panel();
            g.BackColor = Theme.Window;
            g.Size = new Size(W, 62);
            Label l1 = MakeLabel("模糊 σ (px)", Theme.Small, Theme.TextSub, 120, 0, 0);
            l1.Location = new Point(0, 6);
            g.Controls.Add(l1);
            _numBlur = MakeNumeric(0.8m, 0m, 8m, 1, 1);
            _numBlur.Size = new Size(96, 24);
            _numBlur.Location = new Point(140, 2);
            g.Controls.Add(_numBlur);
            Label l2 = MakeLabel("配色数 (2~8)", Theme.Small, Theme.TextSub, 120, 0, 0);
            l2.Location = new Point(0, 36);
            g.Controls.Add(l2);
            _numPalette = MakeNumeric(4, 2, 8, 0);
            _numPalette.Size = new Size(96, 24);
            _numPalette.Location = new Point(140, 32);
            g.Controls.Add(_numPalette);
            return g;
        }

        private Panel BuildExportGroup(int W)
        {
            Panel g = new Panel();
            g.BackColor = Theme.Window;
            g.Size = new Size(W, 96);
            Label l1 = MakeLabel("模型宽度 (米)", Theme.Small, Theme.TextSub, 120, 0, 0);
            l1.Location = new Point(0, 6);
            g.Controls.Add(l1);
            _numWidth = MakeNumeric(10m, 0.5m, 200m, 1, 1);
            _numWidth.Size = new Size(96, 24);
            _numWidth.Location = new Point(140, 2);
            g.Controls.Add(_numWidth);

            Label l2 = MakeLabel("输出目录", Theme.Small, Theme.TextSub, 120, 0, 0);
            l2.Location = new Point(0, 38);
            g.Controls.Add(l2);
            _txtOut = new TextBox();
            _txtOut.BackColor = Theme.PanelAlt;
            _txtOut.ForeColor = Theme.Text;
            _txtOut.BorderStyle = BorderStyle.FixedSingle;
            _txtOut.Font = Theme.Crystal;
            _txtOut.Size = new Size(W - 0, 26);
            _txtOut.Location = new Point(0, 60);
            _txtOut.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mer-out");
            g.Controls.Add(_txtOut);
            return g;
        }

        private Panel BuildAppearanceGroup(int W)
        {
            Panel g = new Panel();
            g.BackColor = Theme.Window;
            g.Size = new Size(W, 34);
            _chkGlass = MakeCheck("毛玻璃卡片（柔和渐变）", true, W);
            _chkGlass.Location = new Point(0, 4);
            _chkGlass.CheckedChanged += delegate
            {
                for (int i = 0; i < _cardPanels.Count; i++) { _cardPanels[i].Gradient = _chkGlass.Checked; _cardPanels[i].Invalidate(); }
                _schemeRow.Invalidate();
            };
            g.Controls.Add(_chkGlass);
            return g;
        }

        private Panel BuildContent()
        {
            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = Theme.Window;
            content.Padding = new Padding(16, 8, 16, 8);

            // 预览卡片：Fill（最后停靠）
            CardPanel previewCard = new CardPanel();
            previewCard.Dock = DockStyle.Fill;
            previewCard.Padding = new Padding(14, 10, 14, 12);
            _cardPanels.Add(previewCard);

            Label ptitle = new Label();
            ptitle.AutoSize = false;
            ptitle.Dock = DockStyle.Top;
            ptitle.Height = 22;
            ptitle.ForeColor = Theme.Text;
            ptitle.Font = Theme.BodyBold;
            ptitle.BackColor = Theme.Panel;
            ptitle.Text = "模型预览";

            _previewStats = new Label();
            _previewStats.AutoSize = false;
            _previewStats.Dock = DockStyle.Top;
            _previewStats.Height = 20;
            _previewStats.ForeColor = Theme.TextMuted;
            _previewStats.Font = Theme.Small;
            _previewStats.BackColor = Theme.Panel;
            _previewStats.Text = "尚未生成 · 总块数 0/0";

            _tabsView = new SegTabs();
            _tabsView.Items = new string[] { "正视", "旋转模型", "随机旋转" };
            _tabsView.Underline = true;
            _tabsView.Dock = DockStyle.Top;
            _tabsView.Height = 30;
            _tabsView.Changed += delegate
            {
                _preview.View = _tabsView.Selected;
                if (_tabsView.Selected == 1) _preview.Yaw = 32;
                if (_tabsView.Selected == 2) _preview.Yaw = _rng.Next(-60, 61);
                _preview.Invalidate();
            };

            _preview = new ImageBox();
            _preview.Dock = DockStyle.Fill;

            previewCard.Controls.Add(_preview);
            previewCard.Controls.Add(_tabsView);
            previewCard.Controls.Add(_previewStats);
            previewCard.Controls.Add(ptitle);

            // 日志卡片：Bottom
            CardPanel logCard = new CardPanel();
            logCard.Dock = DockStyle.Bottom;
            logCard.Height = 186;
            logCard.Padding = new Padding(14, 8, 14, 12);
            _cardPanels.Add(logCard);

            Label ltitle = new Label();
            ltitle.AutoSize = false;
            ltitle.Dock = DockStyle.Top;
            ltitle.Height = 24;
            ltitle.ForeColor = Theme.Text;
            ltitle.Font = Theme.BodyBold;
            ltitle.BackColor = Theme.Panel;
            ltitle.Text = "处理记录";

            _btnOpenOut = new FlatButton();
            _btnOpenOut.Text = "打开输出";
            _btnOpenOut.Kind = FlatButton.Style.Ghost;
            _btnOpenOut.Size = new Size(84, 24);
            _btnOpenOut.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnOpenOut.Location = new Point(logCard.Width - 84 - 14, 8);
            _btnOpenOut.Click += delegate { OpenOutput(); };
            logCard.Controls.Add(_btnOpenOut);
            logCard.Resize += delegate { _btnOpenOut.Location = new Point(logCard.Width - _btnOpenOut.Width - 14, 8); };

            _log = new TextBox();
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.WordWrap = false;
            _log.BorderStyle = BorderStyle.None;
            _log.BackColor = Color.FromArgb(13, 17, 23);
            _log.ForeColor = Theme.TextSub;
            _log.Font = Theme.Crystal;
            _log.Dock = DockStyle.Fill;
            logCard.Controls.Add(_log);
            logCard.Controls.Add(ltitle);

            // 方案卡行：Top
            _schemeRow = new Panel();
            _schemeRow.Dock = DockStyle.Top;
            _schemeRow.Height = 74;
            _schemeRow.BackColor = Theme.Window;
            _schemeRow.Resize += delegate { LayoutCards(); };

            // 说明行：Top
            Label caption = new Label();
            caption.AutoSize = false;
            caption.Dock = DockStyle.Top;
            caption.Height = 22;
            caption.ForeColor = Theme.TextMuted;
            caption.Font = Theme.Small;
            caption.BackColor = Color.Transparent;
            caption.Padding = new Padding(4, 0, 0, 0);
            caption.TextAlign = ContentAlignment.MiddleLeft;
            caption.Text = "5 个方案 · 点击选择 · Unity / JSON 输出元数据";

            // 工具条：Top
            Panel toolbar = BuildToolbar();

            content.Controls.Add(previewCard);
            content.Controls.Add(logCard);
            content.Controls.Add(_schemeRow);
            content.Controls.Add(caption);
            content.Controls.Add(toolbar);
            return content;
        }

        private Panel BuildToolbar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 46;
            bar.BackColor = Theme.Window;

            _btnGen = new FlatButton();
            _btnGen.Text = "生成预览";
            _btnGen.Size = new Size(92, 32);
            _btnGen.Location = new Point(4, 6);
            _btnGen.Click += async delegate { await GenerateAsync(); };

            _btnUnity = new FlatButton();
            _btnUnity.Text = "生成到 Unity / MER";
            _btnUnity.Size = new Size(156, 32);
            _btnUnity.Location = new Point(102, 6);
            _btnUnity.Click += delegate { GenerateToUnity(); };

            _btnExport = new FlatButton();
            _btnExport.Text = "导出 JSON";
            _btnExport.Kind = FlatButton.Style.Accent;
            _btnExport.Size = new Size(102, 32);
            _btnExport.Location = new Point(264, 6);
            _btnExport.Click += delegate { ExportJson(); };

            _btnCancel = new FlatButton();
            _btnCancel.Text = "取消";
            _btnCancel.Kind = FlatButton.Style.Danger;
            _btnCancel.Size = new Size(84, 32);
            _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnCancel.Location = new Point(bar.Width - 84 - 4, 6);
            _btnCancel.Click += delegate { _cancel = true; AppendLog("已请求取消（当前轮结束后停止）。"); };
            bar.Resize += delegate { _btnCancel.Location = new Point(bar.Width - _btnCancel.Width - 4, 6); };

            bar.Controls.Add(_btnGen);
            bar.Controls.Add(_btnUnity);
            bar.Controls.Add(_btnExport);
            bar.Controls.Add(_btnCancel);
            return bar;
        }

        // ---------------------------------------------------------------- 小组件
        private Label MakeLabel(string text, Font font, Color color, int width, int top, int bottom)
        {
            Label l = new Label();
            l.AutoSize = false;
            l.Size = new Size(width, (int)Math.Ceiling(font.GetHeight()) + 4);
            l.ForeColor = color;
            l.Font = font;
            l.BackColor = Color.Transparent;
            l.Text = text;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Margin = new Padding(0, top, 0, bottom);
            return l;
        }

        private Label MakeWrap(string text, Font font, Color color, int width, int top, int bottom)
        {
            Label l = new Label();
            l.AutoSize = true;
            l.MaximumSize = new Size(width, 0);
            l.ForeColor = color;
            l.Font = font;
            l.BackColor = Color.Transparent;
            l.Margin = new Padding(0, top, 0, bottom);
            return l;
        }

        private CheckBox MakeCheck(string text, bool check, int width)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Checked = check;
            c.AutoSize = true;
            c.ForeColor = Theme.TextSub;
            c.Font = Theme.Body;
            c.BackColor = Color.Transparent;
            c.FlatStyle = FlatStyle.Flat;
            c.Margin = new Padding(0, 2, 0, 2);
            c.MaximumSize = new Size(width, 0);
            return c;
        }

        private NumericUpDown MakeNumeric(decimal value, decimal min, decimal max, int decimals, decimal step)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.DecimalPlaces = decimals;
            n.Increment = step;
            n.Value = value;
            n.BackColor = Theme.PanelAlt;
            n.ForeColor = Theme.Text;
            n.BorderStyle = BorderStyle.FixedSingle;
            n.Font = Theme.Body;
            return n;
        }

        private NumericUpDown MakeNumeric(decimal value, decimal min, decimal max, int decimals)
        {
            return MakeNumeric(value, min, max, decimals, 1);
        }

        private void EnsureCards(int count)
        {
            while (_cards.Count < count)
            {
                int idx = _cards.Count;
                SchemeCard card = new SchemeCard();
                card.Height = 62;
                card.Click += delegate { SelectScheme(idx); };
                _cards.Add(card);
                _schemeRow.Controls.Add(card);
            }
            for (int i = 0; i < _cards.Count; i++) _cards[i].Visible = i < count;
            LayoutCards();
        }

        private void LayoutCards()
        {
            int n = 0;
            for (int i = 0; i < _cards.Count; i++) if (_cards[i].Visible) n++;
            if (n == 0) return;
            int gap = 10;
            int total = _schemeRow.ClientSize.Width - 4;
            int w = (total - gap * (n - 1)) / n;
            if (w < 80) w = 80;
            int x = 2;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (!_cards[i].Visible) continue;
                _cards[i].Bounds = new Rectangle(x, 6, w, 62);
                x += w + gap;
            }
        }

        private void SelectScheme(int idx)
        {
            if (idx < 0 || idx >= _schemes.Count || !_schemes[idx].Done) return;
            _current = idx;
            for (int i = 0; i < _cards.Count; i++) _cards[i].Selected = (i == idx);
            _preview.Result = _schemes[idx].Res;
            _preview.Invalidate();
            UpdateStats();
        }

        // ---------------------------------------------------------------- 行为
        private void PickImage()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "选择参考图";
                dlg.Filter = "图片 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _source = ImageIO.Load(dlg.FileName);
                    _sourcePath = dlg.FileName;
                    if (_thumb.SourceImage != null) _thumb.SourceImage.Dispose();
                    _thumb.SourceImage = PreviewRenderer.ToBitmap(_source);
                    _thumb.Result = null;
                    _thumb.Placeholder = false;
                    _thumb.Invalidate();
                    AppendLog("已载入参考图：" + Path.GetFileName(dlg.FileName) + "（" + _source.W + "×" + _source.H + "）");
                    _statusLeft.Text = "已载入 " + Path.GetFileName(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "读取图片失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ClearImage()
        {
            _source = null;
            _sourcePath = null;
            if (_thumb.SourceImage != null) { _thumb.SourceImage.Dispose(); _thumb.SourceImage = null; }
            _thumb.Result = null;
            _thumb.Placeholder = true;
            _thumb.Invalidate();
            _preview.Result = null;
            _preview.Invalidate();
            _schemes.Clear();
            _current = -1;
            for (int i = 0; i < _cards.Count; i++) { _cards[i].Visible = false; }
            _previewStats.Text = "尚未生成 · 总块数 0/0";
            _statusLeft.Text = "就绪 · 等待选择参考图";
            AppendLog("已清除参考图与预览。");
        }

        private ConvertOptions BuildBaseOptions()
        {
            ConvertOptions o = new ConvertOptions();
            o.Name = string.IsNullOrEmpty(_sourcePath) ? "model" : Path.GetFileNameWithoutExtension(_sourcePath);
            if (_tabsMain != null && _tabsMain.Selected == 1) o.Name += "_alt";
            o.WidthUnits = (double)_numWidth.Value;
            o.PaletteCount = (int)_numPalette.Value;
            o.SimplifyTol = (double)_numTol.Value;
            o.Blur = (double)_numBlur.Value;
            o.MinArea = (double)_numMinArea.Value;
            o.Optimize = _chkOpt.Checked;
            o.NoEmpty = _chkNoEmpty.Checked;
            o.NoRectCover = _chkNoGrad.Checked;
            o.FlatThickness = _chkFlat.Checked;
            o.SkipBackground = true;
            o.BlockBudget = _chkBudget.Checked ? (int)_numBudget.Value : 2000;
            return o;
        }

        private async Task GenerateAsync()
        {
            if (_busy) return;
            if (_source == null)
            {
                MessageBox.Show(this, "请先选择参考图。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _busy = true;
            _cancel = false;
            SetBusy(true);
            AppendLog("开始生成预览 · 逐层几何填充（背景色层默认剔除）。");

            try
            {
                ConvertOptions baseOpt = BuildBaseOptions();
                List<ConvertOptions> schemes = Converter.BuildSchemes(baseOpt, baseOpt.BlockBudget);
                _schemes.Clear();
                _current = -1;
                EnsureCards(schemes.Count);
                for (int i = 0; i < schemes.Count; i++)
                {
                    SchemeState st = new SchemeState();
                    st.Opt = schemes[i];
                    _schemes.Add(st);
                    SchemeCard card = _cards[i];
                    card.Selected = false;
                    card.Title = Converter.SchemeTitles[i] + " · " + Converter.SchemeNotes[i];
                    card.Line1 = "生成中…";
                    card.Line2 = "";
                    card.Invalidate();
                }

                for (int i = 0; i < schemes.Count; i++)
                {
                    if (_cancel) { AppendLog("已取消，剩余方案跳过。"); break; }
                    ConvertOptions opt = _schemes[i].Opt;
                    ConvertOptions captured = opt;
                    ConvertResult r = await Task.Run(delegate { return Converter.Run(_source, captured); });
                    SchemeState st = _schemes[i];
                    st.Res = r;
                    st.Done = true;

                    SchemeCard card = _cards[i];
                    card.Line1 = r.TotalBlocks + " 总块 · 含 Empty";
                    card.Line2 = "拟合误差 " + r.FitErrorPercent.ToString("0.0") + "%";
                    card.Invalidate();

                    AppendLog(Converter.SchemeTitles[i] + " 完成：" + r.TotalBlocks + " 块（实体 " + r.PrimitiveBlocks +
                              " + Empty " + r.EmptyBlocks + "）· 拟合 " + r.FitErrorPercent.ToString("0.00") +
                              "% · 跟踪 " + r.TraceErrorPercent.ToString("0.00") + "%");
                    if (i == 0 || _current < 0) SelectScheme(i);
                }
                AppendLog("生成结束。");
            }
            catch (Exception ex)
            {
                AppendLog("生成失败：" + ex.Message);
                MessageBox.Show(this, "生成失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _busy = false;
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _btnGen.Enabled = !busy;
            _btnUnity.Enabled = !busy;
            _btnExport.Enabled = !busy;
            _btnPick.Enabled = !busy;
            Cursor = busy ? Cursors.AppStarting : Cursors.Default;
            if (busy) _statusLeft.Text = "正在生成预览…";
        }

        private void GenerateToUnity()
        {
            SchemeState st = Current();
            if (st == null) { MessageBox.Show(this, "请先生成预览。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string dir = EnsureOutDir();
            if (dir == null) return;
            string baseName = st.Opt.Name;
            File.WriteAllText(Path.Combine(dir, baseName + ".json"), st.Res.Document.ToJson());
            SavePreviewPng(st.Res, Path.Combine(dir, baseName + ".preview.png"));
            AppendLog("已输出到 " + dir + "：" + baseName + ".json");
            _statusLeft.Text = "已输出 MER JSON：" + Path.Combine(dir, baseName + ".json");
        }

        private void ExportJson()
        {
            SchemeState st = Current();
            if (st == null) { MessageBox.Show(this, "请先生成预览。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "导出 MER JSON";
                dlg.Filter = "MER 蓝图 (*.json)|*.json";
                dlg.FileName = st.Opt.Name + ".json";
                dlg.InitialDirectory = EnsureOutDir();
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dlg.FileName, st.Res.Document.ToJson());
                AppendLog("已导出 JSON：" + dlg.FileName);
                _statusLeft.Text = "已导出：" + dlg.FileName;
            }
        }

        private SchemeState Current()
        {
            if (_current < 0 || _current >= _schemes.Count) return null;
            return _schemes[_current].Done ? _schemes[_current] : null;
        }

        private string EnsureOutDir()
        {
            string dir = _txtOut.Text;
            if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mer-out");
            try { Directory.CreateDirectory(dir); return dir; }
            catch (Exception ex) { MessageBox.Show(this, "无法创建输出目录：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return null; }
        }

        private void OpenOutput()
        {
            string dir = _txtOut.Text;
            if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mer-out");
            try
            {
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex) { AppendLog("打开输出目录失败：" + ex.Message); }
        }

        private static void SavePreviewPng(ConvertResult res, string path)
        {
            using (Bitmap bmp = new Bitmap(768, 768))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                    PreviewRenderer.DrawReconstructed(g, res.Reconstructed, new Rectangle(0, 0, 768, 768), Theme.Preview);
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private void UpdateStats()
        {
            SchemeState st = Current();
            if (st == null) { _previewStats.Text = "尚未生成 · 总块数 0/0"; return; }
            int budget = st.Opt.BlockBudget > 0 ? st.Opt.BlockBudget : 2000;
            string line = "总块数 " + st.Res.TotalBlocks + "/" + budget + "（含 Empty） · " +
                          st.Res.PrimitiveBlocks + " 实体 + " + st.Res.EmptyBlocks + " Empty · 拟合误差 " +
                          st.Res.FitErrorPercent.ToString("0.0") + "%";
            _previewStats.Text = line;
            _statusLeft.Text = "方案预览 · " + line;
        }

        private void AppendLog(string line)
        {
            if (_log == null) return;
            string stamp = "[" + DateTime.Now.ToString("HH:mm:ss") + "] ";
            _log.AppendText(stamp + line + Environment.NewLine);
        }
    }
}
