using System.Drawing.Drawing2D;

namespace GameLocker.ConfigUI;

internal enum WorkspaceGlyph { Locker, Folder, Files, Minimize, Maximize, Restore, Close }

internal static class WorkspacePaint
{
    public static void Glyph(Graphics graphics, WorkspaceGlyph glyph, Rectangle bounds, Color color, Color background)
    {
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.X, bounds.Y);
        graphics.ScaleTransform(bounds.Width / 24F, bounds.Height / 24F);
        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, 1.6F) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        switch (glyph)
        {
            case WorkspaceGlyph.Locker:
                graphics.DrawArc(pen, 6.5F, 3, 11, 13, 180, 180);
                graphics.FillRectangle(brush, 4, 9, 16, 12);
                using (var key = new SolidBrush(background))
                {
                    graphics.FillEllipse(key, 10, 12, 4, 4);
                    graphics.FillRectangle(key, 11, 15, 2, 3);
                }
                break;
            case WorkspaceGlyph.Folder:
                Point[] folder = [new(2, 6), new(9, 6), new(11, 8), new(22, 8), new(22, 19), new(2, 19)];
                graphics.FillPolygon(brush, folder);
                using (var outline = new Pen(background, 1.1F) { LineJoin = LineJoin.Round })
                    graphics.DrawPolygon(outline, folder);
                break;
            case WorkspaceGlyph.Files:
                graphics.DrawLines(pen, new Point[] { new(5, 4), new(14, 4), new(19, 9), new(19, 20), new(5, 20), new(5, 4) });
                graphics.DrawLines(pen, new Point[] { new(14, 4), new(14, 9), new(19, 9) });
                graphics.DrawLine(pen, 8, 13, 16, 13);
                graphics.DrawLine(pen, 8, 16, 14, 16);
                break;
            case WorkspaceGlyph.Minimize:
                graphics.DrawLine(pen, 3, 12, 21, 12);
                break;
            case WorkspaceGlyph.Maximize:
                graphics.DrawRectangle(pen, 3, 3, 18, 18);
                break;
            case WorkspaceGlyph.Restore:
                graphics.DrawRectangle(pen, 3, 7, 14, 14);
                Point[] restore = [new(7, 7), new(7, 3), new(21, 3), new(21, 17), new(17, 17)];
                graphics.DrawLines(pen, restore);
                break;
            case WorkspaceGlyph.Close:
                graphics.DrawLine(pen, 4, 4, 20, 20);
                graphics.DrawLine(pen, 20, 4, 4, 20);
                break;
        }

        graphics.Restore(state);
    }

    public static void ButtonFace(Graphics graphics, Rectangle bounds, int radius, Color fill, Color border)
    {
        if (bounds.Width <= 2 * radius || bounds.Height <= 2 * radius)
            return;

        bounds.Width--;
        bounds.Height--;
        var diameter = 2 * radius;
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(border);
        graphics.FillPath(brush, path);
        graphics.DrawPath(pen, path);
        graphics.SmoothingMode = previous;
    }

    public static void FocusMark(Graphics graphics, Control control, Rectangle bounds, Color ink)
    {
        var thickness = control.LogicalToDeviceUnits(2);
        var offset = control.LogicalToDeviceUnits(3);
        var width = Math.Min(bounds.Width, control.LogicalToDeviceUnits(16));
        if (width <= 0 || bounds.Height < offset + thickness)
            return;

        using var brush = new SolidBrush(ink);
        graphics.FillRectangle(brush, bounds.Left + (bounds.Width - width) / 2,
            bounds.Bottom - offset - thickness, width, thickness);
    }
}

internal sealed class WorkspaceIcon(WorkspaceGlyph glyph, bool accent = false) : Control
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = WorkspaceTheme.For(this);
        var size = Math.Min(Width, Height) * 9 / 10;
        var ink = accent && !theme.HighContrast ? theme.Pink : theme.Muted;
        WorkspacePaint.Glyph(e.Graphics, glyph,
            new Rectangle((Width - size) / 2, (Height - size) / 2, size, size), ink, BackColor);
    }
}

internal sealed class WorkspaceSurface : Panel
{
    public int CornerRadius { get; init; }

    public WorkspaceSurface()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = WorkspaceTheme.For(this);
        if (CornerRadius > 0)
        {
            e.Graphics.Clear(Parent?.BackColor ?? theme.Canvas);
            WorkspacePaint.ButtonFace(e.Graphics, ClientRectangle, LogicalToDeviceUnits(CornerRadius), BackColor, theme.Line);
            return;
        }

        using var pen = new Pen(theme.Line);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

internal sealed class WorkspaceButton : Button
{
    private readonly bool _primary;
    private readonly bool _danger;
    private readonly bool _bare;
    private bool _hovered;

    public WorkspaceGlyph? Symbol { get; set; }
    public int ImageSize { get; init; } = 27;

    public WorkspaceButton(string text, bool primary = false, bool danger = false, bool bare = false)
    {
        Text = text;
        Font = WorkspaceTheme.Strong;
        _primary = primary;
        _danger = danger;
        _bare = bare;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var theme = WorkspaceTheme.For(this);
        var background = _primary && Enabled ? theme.Yellow : _hovered && Enabled ? theme.Hover
            : _bare ? BackColor : theme.Raised;
        var foreground = !Enabled ? theme.Disabled
            : _primary ? theme.OnYellow : _danger ? theme.Error : theme.Text;
        var border = !Enabled ? theme.Line : _hovered ? theme.Pink : _primary ? theme.OnYellow : theme.ControlLine;
        if (theme.HighContrast && Enabled && !_primary)
            foreground = _hovered ? SystemColors.WindowText : SystemColors.ControlText;

        e.Graphics.Clear(BackColor);
        if (_bare)
            e.Graphics.Clear(background);
        else
            WorkspacePaint.ButtonFace(e.Graphics, ClientRectangle, LogicalToDeviceUnits(6), background, border);

        var textBounds = ClientRectangle;
        var hasSymbol = Image is not null || Symbol is not null;
        if (hasSymbol && Text.Length > 0)
        {
            var indent = LogicalToDeviceUnits(42);
            textBounds = Rectangle.FromLTRB(indent, 0, Width, Height);
        }
        var center = hasSymbol && Text.Length > 0 ? LogicalToDeviceUnits(18) : Width / 2;
        if (Image is { } logo)
        {
            var scale = (float)LogicalToDeviceUnits(ImageSize) / Math.Max(logo.Width, logo.Height);
            var width = (int)Math.Round(logo.Width * scale);
            var height = (int)Math.Round(logo.Height * scale);
            var bounds = new Rectangle(center - width / 2, (Height - height) / 2, width, height);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(logo, bounds);
        }
        else if (Symbol is { } symbol)
        {
            var size = LogicalToDeviceUnits(18);
            WorkspacePaint.Glyph(e.Graphics, symbol, new Rectangle(center - size / 2, (Height - size) / 2, size, size),
                foreground, background);
        }
        if (Text.Length > 0)
        {
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, foreground,
                (hasSymbol || TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | (ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix));
        }
        if (Focused)
            WorkspacePaint.FocusMark(e.Graphics, this, ClientRectangle, foreground);
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

internal sealed class WorkspaceThemeOption : RadioButton
{
    public WorkspaceThemeOption(WorkspaceThemeMode mode)
    {
        Appearance = Appearance.Button;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        Text = mode.ToString();
        AccessibleName = $"{mode} theme";
        Font = WorkspaceTheme.Link;
        Cursor = Cursors.Hand;
        Dock = DockStyle.Fill;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var theme = WorkspaceTheme.For(this);
        var hovered = ClientRectangle.Contains(PointToClient(MousePosition));
        var fill = Checked ? theme.Pink : hovered ? theme.Hover : theme.Inset;
        var ink = Checked ? theme.OnPink : theme.Text;
        e.Graphics.Clear(BackColor);
        WorkspacePaint.ButtonFace(e.Graphics, ClientRectangle, LogicalToDeviceUnits(4), fill,
            Checked || hovered ? theme.Pink : theme.ControlLine);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if (Focused)
            WorkspacePaint.FocusMark(e.Graphics, this, ClientRectangle, ink);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Invalidate();
    }
}

internal sealed class WorkspaceDayOption : CheckBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        var theme = WorkspaceTheme.For(this);
        var ink = Enabled ? Checked ? theme.OnPink : theme.Text : Checked ? theme.OnSelected : theme.Disabled;
        var fill = Checked ? Enabled ? theme.Pink : theme.Selected : theme.Inset;
        var border = Enabled ? Checked ? theme.Pink : theme.ControlLine : theme.Line;
        e.Graphics.Clear(BackColor);
        WorkspacePaint.ButtonFace(e.Graphics, ClientRectangle, LogicalToDeviceUnits(6), fill, border);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix);
        if (Focused)
            WorkspacePaint.FocusMark(e.Graphics, this, ClientRectangle, ink);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Invalidate();
    }
}
