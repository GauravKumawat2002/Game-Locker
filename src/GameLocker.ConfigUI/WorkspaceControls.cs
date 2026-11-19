using System.Drawing.Drawing2D;

namespace GameLocker.ConfigUI;

internal enum WorkspaceGlyph { Locker, Folder, Files }

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

    public static void Focus(Graphics graphics, Rectangle bounds, Color fill, Color ink) =>
        ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(bounds, -4, -4), ink, fill);
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
    public WorkspaceSurface()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(WorkspaceTheme.For(this).Line);
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
        Cursor = Cursors.Hand;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var theme = WorkspaceTheme.For(this);
        var background = _primary && Enabled ? theme.Yellow : _hovered && Enabled ? theme.Hover : theme.Raised;
        var foreground = !Enabled ? theme.Disabled
            : _primary ? theme.OnYellow : _danger ? theme.Error : theme.Text;
        var border = !Enabled ? theme.Line : _hovered ? theme.Pink : _primary ? theme.OnYellow : theme.ControlLine;
        if (theme.HighContrast && Enabled && !_primary)
            foreground = _hovered ? SystemColors.WindowText : SystemColors.ControlText;

        e.Graphics.Clear(BackColor);
        WorkspacePaint.ButtonFace(e.Graphics, ClientRectangle, LogicalToDeviceUnits(4), background, border);
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
            WorkspacePaint.Focus(e.Graphics, ClientRectangle, background, foreground);
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
        Font = WorkspaceTheme.Strong;
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
            WorkspacePaint.Focus(e.Graphics, ClientRectangle, fill, ink);
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

internal sealed class WorkspaceCheckBox(bool day = false) : CheckBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        var theme = WorkspaceTheme.For(this);
        var selected = Enabled && Checked;
        var foreground = day && Checked ? Enabled ? theme.OnPink : theme.OnSelected
            : Enabled ? theme.Text : theme.Disabled;
        var background = day ? Checked ? Enabled ? theme.Pink : theme.Selected : theme.Inset : BackColor;
        e.Graphics.Clear(BackColor);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            (ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix);
        if (day)
        {
            var border = selected ? theme.Pink : Enabled ? theme.ControlLine : theme.Line;
            WorkspacePaint.ButtonFace(e.Graphics, ClientRectangle, LogicalToDeviceUnits(4), background, border);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, foreground, flags | TextFormatFlags.HorizontalCenter);
        }
        else
        {
            var side = LogicalToDeviceUnits(15);
            var top = (Height - side) / 2;
            var check = new Rectangle(1, top, side, side);
            if (Checked)
            {
                using var brush = new SolidBrush(selected ? theme.Pink : theme.Inset);
                using var border = new Pen(selected ? theme.Pink : theme.Disabled);
                using var tick = new Pen(selected ? theme.OnPink : theme.Disabled, Math.Max(1, LogicalToDeviceUnits(2)));
                e.Graphics.FillRectangle(brush, check);
                e.Graphics.DrawRectangle(border, check);
                Point[] mark = [new(check.Left + side / 4, top + side / 2),
                    new(check.Left + side / 2 - 1, top + side * 3 / 4),
                    new(check.Left + side * 3 / 4 + 1, top + side / 4)];
                e.Graphics.DrawLines(tick, mark);
            }
            else
            {
                using var pen = new Pen(Enabled ? theme.ControlLine : theme.Disabled);
                e.Graphics.DrawRectangle(pen, check);
            }

            var textLeft = LogicalToDeviceUnits(25);
            TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.FromLTRB(textLeft, 0, Width, Height),
                foreground, flags | TextFormatFlags.EndEllipsis);
        }

        if (Focused)
            WorkspacePaint.Focus(e.Graphics, ClientRectangle, background, foreground);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Invalidate();
    }
}
