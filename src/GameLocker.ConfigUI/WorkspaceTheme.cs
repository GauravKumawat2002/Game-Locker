using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GameLocker.ConfigUI;

internal static class WorkspaceTheme
{
    private static bool HighContrast => SystemInformation.HighContrast;

    public static Color Canvas => HighContrast ? SystemColors.Window : Color.FromArgb(17, 20, 24);
    public static Color Surface => HighContrast ? SystemColors.Window : Color.FromArgb(24, 28, 33);
    public static Color Inset => HighContrast ? SystemColors.Window : Color.FromArgb(18, 22, 27);
    public static Color Raised => HighContrast ? SystemColors.Control : Color.FromArgb(30, 35, 42);
    public static Color Hover => HighContrast ? SystemColors.Control : Color.FromArgb(40, 49, 57);
    public static Color Line => HighContrast ? SystemColors.WindowText : Color.FromArgb(57, 67, 76);
    public static Color ControlLine => HighContrast ? SystemColors.WindowText : Color.FromArgb(96, 114, 127);
    public static Color Text => HighContrast ? SystemColors.WindowText : Color.FromArgb(240, 244, 245);
    public static Color Muted => HighContrast ? SystemColors.WindowText : Color.FromArgb(169, 182, 193);
    public static Color Disabled => HighContrast ? SystemColors.GrayText : Color.FromArgb(127, 140, 151);
    public static Color Accent => HighContrast ? SystemColors.Highlight : Color.FromArgb(150, 233, 216);
    public static Color OnAccent => HighContrast ? SystemColors.HighlightText : Color.FromArgb(14, 34, 32);
    public static Color Selected => HighContrast ? SystemColors.Highlight : Color.FromArgb(32, 58, 58);
    public static Color OnSelected => HighContrast ? SystemColors.HighlightText : Color.FromArgb(174, 247, 231);
    public static Color Warn => HighContrast ? SystemColors.WindowText : Color.FromArgb(246, 202, 130);
    public static Color Error => HighContrast ? SystemColors.WindowText : Color.FromArgb(255, 163, 161);

    public static readonly Font Body = new("Segoe UI", 9.75F);
    public static readonly Font Small = new("Segoe UI", 9F);
    public static readonly Font Strong = new("Segoe UI Semibold", 9.75F);
    public static readonly Font Section = new("Segoe UI Semibold", 14F);
    public static readonly Font Brand = new("Segoe UI Semibold", 18F);
    public static readonly Font Data = new("Consolas", 10F);
    public static readonly Font Metadata = new("Consolas", 9F);

    public static void UseDarkTitleBar(Form form)
    {
        // Windows 10 and 11 support different versions of this attribute.
        var enabled = HighContrast ? 0 : 1;
        if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)) != 0)
            _ = DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);

    public static void DrawGlyph(Graphics graphics, WorkspaceGlyph glyph, Rectangle bounds, Color color)
    {
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.X, bounds.Y);
        graphics.ScaleTransform(bounds.Width / 24F, bounds.Height / 24F);
        using var pen = new Pen(color, 1.45F) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        switch (glyph)
        {
            case WorkspaceGlyph.Locker:
                graphics.DrawLines(pen, new Point[] { new(3, 6), new(12, 2), new(21, 6), new(21, 17),
                    new(12, 22), new(3, 17), new(3, 6) });
                graphics.DrawRectangle(pen, 8, 10, 8, 6);
                graphics.DrawArc(pen, 10, 5.6F, 4, 6, 180, 180);
                break;
            case WorkspaceGlyph.Folder:
                graphics.DrawLines(pen, new Point[] { new(2, 6), new(9, 6), new(11, 8), new(22, 8),
                    new(22, 19), new(2, 19), new(2, 6) });
                break;
            case WorkspaceGlyph.Files:
                graphics.DrawLines(pen, new Point[] { new(5, 4), new(14, 4), new(19, 9), new(19, 20),
                    new(5, 20), new(5, 4) });
                graphics.DrawLines(pen, new Point[] { new(14, 4), new(14, 9), new(19, 9) });
                graphics.DrawLine(pen, 8, 13, 16, 13);
                graphics.DrawLine(pen, 8, 16, 14, 16);
                break;
        }

        graphics.Restore(state);
    }
}

internal enum WorkspaceGlyph { Locker, Folder, Files }

internal sealed class WorkspaceIcon(WorkspaceGlyph glyph, bool badge = false) : Control
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var bounds = ClientRectangle;
        if (badge)
        {
            using var brush = new SolidBrush(WorkspaceTheme.Selected);
            using var pen = new Pen(WorkspaceTheme.Line);
            e.Graphics.FillRectangle(brush, bounds);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        var size = Math.Min(Width, Height) * (badge ? 5 : 8) / 10;
        WorkspaceTheme.DrawGlyph(e.Graphics, glyph,
            new Rectangle((Width - size) / 2, (Height - size) / 2, size, size),
            badge ? WorkspaceTheme.OnSelected : WorkspaceTheme.Muted);
    }
}

internal sealed class WorkspaceSurface : Panel
{
    public WorkspaceSurface()
    {
        BackColor = WorkspaceTheme.Surface;
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(WorkspaceTheme.Line);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

internal sealed class WorkspaceButton : Button
{
    private readonly bool _primary;
    private readonly bool _danger;
    private bool _hovered;

    public WorkspaceButton(string text, bool primary = false, bool danger = false)
    {
        Text = text;
        Font = WorkspaceTheme.Strong;
        _primary = primary;
        _danger = danger;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        BackColor = WorkspaceTheme.Raised;
        ForeColor = WorkspaceTheme.Text;
        Cursor = Cursors.Hand;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var background = _primary && Enabled ? WorkspaceTheme.Accent
            : _hovered && Enabled ? WorkspaceTheme.Hover : WorkspaceTheme.Raised;
        var foreground = !Enabled ? WorkspaceTheme.Disabled
            : _primary ? WorkspaceTheme.OnAccent : _danger ? WorkspaceTheme.Error : WorkspaceTheme.Text;
        e.Graphics.Clear(background);
        using var pen = new Pen(Focused ? WorkspaceTheme.Accent : _primary && Enabled ? background : WorkspaceTheme.Line);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        if (Image is { } logo)
        {
            var scale = (float)LogicalToDeviceUnits(22) / Math.Max(logo.Width, logo.Height);
            var width = (int)Math.Round(logo.Width * scale);
            var height = (int)Math.Round(logo.Height * scale);
            var bounds = new Rectangle((Width - width) / 2, (Height - height) / 2, width, height);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(logo, bounds);
        }
        else
        {
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | (ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix));
        }
        if (Focused)
        {
            var focus = Rectangle.Inflate(ClientRectangle, -3, -3);
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, foreground, background);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }
}

internal sealed class WorkspaceCheckBox(bool day = false) : CheckBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        var foreground = !Enabled ? WorkspaceTheme.Disabled
            : day && Checked ? WorkspaceTheme.OnSelected : WorkspaceTheme.Text;
        var background = day ? Checked ? WorkspaceTheme.Selected : WorkspaceTheme.Inset : BackColor;
        e.Graphics.Clear(background);
        using var pen = new Pen(Checked && Enabled ? WorkspaceTheme.Accent
            : Enabled ? WorkspaceTheme.ControlLine : WorkspaceTheme.Line);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            (ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix);
        if (day)
        {
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, foreground, flags | TextFormatFlags.HorizontalCenter);
        }
        else
        {
            var side = LogicalToDeviceUnits(15);
            var top = (Height - side) / 2;
            var check = new Rectangle(1, top, side, side);
            if (Checked)
            {
                using var brush = new SolidBrush(Enabled ? WorkspaceTheme.Accent : WorkspaceTheme.Disabled);
                e.Graphics.FillRectangle(brush, check);
                using var tick = new Pen(WorkspaceTheme.OnAccent, Math.Max(1, LogicalToDeviceUnits(2)));
                e.Graphics.DrawLines(tick, new Point[] { new(check.Left + side / 4, top + side / 2),
                    new(check.Left + side / 2 - 1, top + side * 3 / 4),
                    new(check.Left + side * 3 / 4 + 1, top + side / 4) });
            }
            else
            {
                e.Graphics.DrawRectangle(pen, check);
            }

            var textLeft = LogicalToDeviceUnits(25);
            TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.FromLTRB(textLeft, 0, Width, Height),
                foreground, flags | TextFormatFlags.EndEllipsis);
        }

        if (Focused)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3), foreground, background);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Invalidate();
    }
}

internal sealed class WindowTimeline : Control
{
    private const TextFormatFlags TickFormat = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
    private static readonly string[] Ticks = ["00:00", "06:00", "12:00", "18:00", "24:00"];
    private double _startHour;
    private int _durationHours;
    private bool _hasDays;

    public WindowTimeline()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Font = WorkspaceTheme.Metadata;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "24-hour window on a selected day";
    }

    private int TickTop => LogicalToDeviceUnits(8) + LogicalToDeviceUnits(6);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var textHeight = TextRenderer.MeasureText(Ticks[0], Font, Size.Empty, TickFormat).Height;
        return new Size(0, Math.Max(LogicalToDeviceUnits(34), TickTop + textHeight + LogicalToDeviceUnits(4)));
    }

    public void SetWindow(TimeOnly start, int duration, bool hasDays)
    {
        if (_startHour == start.ToTimeSpan().TotalHours && _durationHours == duration && _hasDays == hasDays)
            return;

        _startHour = start.ToTimeSpan().TotalHours;
        _durationHours = duration;
        _hasDays = hasDays;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var inactive = new SolidBrush(WorkspaceTheme.Line);
        using var active = new SolidBrush(WorkspaceTheme.Accent);
        var gap = LogicalToDeviceUnits(2);
        var segmentWidth = (Width + gap) / 24F;
        var height = LogicalToDeviceUnits(8);
        for (var hour = 0; hour < 24; hour++)
        {
            var left = (int)Math.Round(hour * segmentWidth);
            var width = Math.Max(1, (int)Math.Round((hour + 1) * segmentWidth) - left - gap);
            e.Graphics.FillRectangle(inactive, left, 0, width, height);
            if (!_hasDays)
                continue;

            DrawSpan(_startHour, _startHour + _durationHours);
            DrawSpan(_startHour - 24, _startHour + _durationHours - 24);

            void DrawSpan(double start, double end)
            {
                var overlapStart = Math.Max(0, start - hour);
                var overlapEnd = Math.Min(1, end - hour);
                if (overlapEnd > overlapStart)
                    e.Graphics.FillRectangle(active, left + (float)(width * overlapStart), 0,
                        (float)(width * (overlapEnd - overlapStart)), height);
            }
        }

        for (var index = 0; index < Ticks.Length; index++)
        {
            var size = TextRenderer.MeasureText(e.Graphics, Ticks[index], Font, Size.Empty, TickFormat);
            var position = Math.Clamp(Width * index / (Ticks.Length - 1) - size.Width / 2, 0, Math.Max(0, Width - size.Width));
            TextRenderer.DrawText(e.Graphics, Ticks[index], Font,
                new Point(position, TickTop), WorkspaceTheme.Muted, TickFormat);
        }
    }
}
