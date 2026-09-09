using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DLSSFrameGenEnabler.UI;

/// <summary>
/// 现代化按钮控件（WinUI3 风格）
/// </summary>
public class ModernButton : Button
{
    private bool _isHovered;
    private bool _isPressed;

    /// <summary>按钮主题色</summary>
    [Category("Appearance")]
    public Color AccentColor { get; set; } = Color.FromArgb(0, 120, 212);

    /// <summary>悬停时的颜色</summary>
    [Category("Appearance")]
    public Color HoverColor { get; set; } = Color.FromArgb(20, 130, 220);

    /// <summary>按下时的颜色</summary>
    [Category("Appearance")]
    public Color PressedColor { get; set; } = Color.FromArgb(0, 100, 190);

    /// <summary>圆角半径</summary>
    [Category("Appearance")]
    public int CornerRadius { get; set; } = 8;

    /// <summary>是否为次要按钮（透明背景）</summary>
    [Category("Appearance")]
    public bool IsSecondary { get; set; }

    /// <summary>是否为浅色主题（由父窗体设置）</summary>
    [Category("Appearance")]
    public bool IsLightTheme { get; set; }

    public ModernButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw
               | ControlStyles.UserPaint
               | ControlStyles.SupportsTransparentBackColor, true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.CheckedBackColor = Color.Transparent;
        BackColor = Color.Transparent;
        ForeColor = Color.White;
        Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Regular);
        Cursor = Cursors.Hand;
        AutoSize = false;
        Size = new Size(140, 40);
        MinimumSize = new Size(80, 30);
        Padding = new Padding(16, 0, 16, 0);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        _isPressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _isPressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _isPressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        // 尺寸无效时跳过绘制，防止 AddArc 参数异常
        if (Width <= 0 || Height <= 0) return;

        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // 整个按钮区域（留1像素边距给边框）
        var rect = new Rectangle(1, 1, Width - 3, Height - 3);
        // 确保矩形尺寸有效
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var path = GetRoundedRect(rect, CornerRadius);

        // 确定背景色
        Color bgColor;
        if (!Enabled)
        {
            if (IsSecondary)
            {
                bgColor = IsLightTheme
                    ? Color.FromArgb(20, 0, 0, 0)
                    : Color.FromArgb(12, 255, 255, 255);
            }
            else
            {
                bgColor = Color.FromArgb(70, 70, 70);
            }
        }
        else if (IsSecondary)
        {
            if (IsLightTheme)
            {
                // 浅色模式：用黑色透明度实现次要按钮
                bgColor = _isPressed ? Color.FromArgb(45, 0, 0, 0)
                         : _isHovered ? Color.FromArgb(30, 0, 0, 0)
                         : Color.FromArgb(15, 0, 0, 0);
            }
            else
            {
                // 深色模式：用白色透明度实现次要按钮
                bgColor = _isPressed ? Color.FromArgb(55, 255, 255, 255)
                         : _isHovered ? Color.FromArgb(35, 255, 255, 255)
                         : Color.FromArgb(20, 255, 255, 255);
            }
        }
        else
        {
            bgColor = _isPressed ? PressedColor
                     : _isHovered ? HoverColor
                     : AccentColor;
        }

        // 绘制背景（先用父控件背景色填充整个区域，防止透明穿透）
        var parentBg = Parent?.BackColor ?? (IsLightTheme ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32));
        using (var bgBrush = new SolidBrush(parentBg))
        {
            g.FillRectangle(bgBrush, 0, 0, Width, Height);
        }

        // 绘制圆角背景
        using (var brush = new SolidBrush(bgColor))
        {
            g.FillPath(brush, path);
        }

        // 次要按钮绘制边框
        if (IsSecondary)
        {
            Color borderColor;
            if (IsLightTheme)
            {
                borderColor = Enabled ? Color.FromArgb(120, 0, 0, 0) : Color.FromArgb(50, 0, 0, 0);
            }
            else
            {
                borderColor = Enabled ? Color.FromArgb(90, 255, 255, 255) : Color.FromArgb(40, 255, 255, 255);
            }
            using var pen = new Pen(borderColor, 1);
            g.DrawPath(pen, path);
        }

        // 绘制文字（严格限制在按钮内，使用 Graphics.DrawString 更可控）
        if (!string.IsNullOrEmpty(Text))
        {
            var textColor = Enabled ? ForeColor : Color.FromArgb(140, ForeColor);
            // 文字区域：左右各留16像素，上下居中
            var textRect = new RectangleF(16, 0, Width - 32, Height);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces
            };
            using var textBrush = new SolidBrush(textColor);
            g.DrawString(Text, Font, textBrush, textRect, sf);
        }

        path.Dispose();
    }

    private static GraphicsPath GetRoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();

        // 尺寸无效时返回空路径，防止 AddArc 抛出异常
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            path.CloseFigure();
            return path;
        }

        var d = radius * 2;
        if (d <= 0) d = 1;
        if (d > rect.Width) d = rect.Width;
        if (d > rect.Height) d = rect.Height;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
