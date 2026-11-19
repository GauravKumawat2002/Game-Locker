namespace GameLocker.ConfigUI;

internal sealed class WindowTimeline : Control
{
    private const TextFormatFlags TickFormat = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
    private static readonly string[] Ticks = ["00:00", "06:00", "12:00", "18:00", "24:00"];
    private TimeOnly _start;
    private int _durationHours;
    private bool _hasDays;

    public WindowTimeline()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Font = WorkspaceTheme.Ruler;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "24-hour schedule ruler";
    }

    public void SetWindow(TimeOnly start, int duration, bool hasDays)
    {
        if (_start == start && _durationHours == duration && _hasDays == hasDays)
            return;

        _start = start;
        _durationHours = duration;
        _hasDays = hasDays;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = WorkspaceTheme.For(this);
        var label = TextRenderer.MeasureText(e.Graphics, Ticks[0], Font, Size.Empty, TickFormat);
        var left = Math.Max(LogicalToDeviceUnits(22), (label.Width + 1) / 2);
        var right = Width - left;
        if (right <= left)
            return;

        var trackTop = LogicalToDeviceUnits(53);
        using var ruler = new Pen(theme.Text, LogicalToDeviceUnits(2));
        using var interval = new SolidBrush(theme.Yellow);
        using var activePoint = new SolidBrush(theme.HighContrast ? theme.Text : theme.Pink);
        e.Graphics.DrawLine(ruler, left, trackTop, right, trackTop);

        if (_hasDays)
        {
            var start = _start.ToTimeSpan().TotalHours;
            var end = start + _durationHours;
            DrawSpan(start, Math.Min(end, 24));
            if (end > 24)
                DrawSpan(0, end - 24);

            var x = (int)Math.Round(Position(start));
            var side = LogicalToDeviceUnits(11);
            var markerTop = trackTop - LogicalToDeviceUnits(40);
            Point[] marker = [new(x - side, markerTop), new(x + side, markerTop), new(x, markerTop + LogicalToDeviceUnits(14))];
            e.Graphics.FillPolygon(activePoint, marker);

            var endLabel = end == 24 ? "24:00" : _start.AddHours(_durationHours).ToString("HH':'mm");
            var range = $"{_start:HH':'mm} – {endLabel}";
            var textWidth = TextRenderer.MeasureText(e.Graphics, range, Font, Size.Empty, TickFormat).Width;
            var width = Math.Min(Width, textWidth + LogicalToDeviceUnits(12));
            var height = LogicalToDeviceUnits(24);
            var badge = new Rectangle(Math.Clamp(x, 0, Width - width), trackTop - height, width, height);
            e.Graphics.FillRectangle(interval, badge);
            TextRenderer.DrawText(e.Graphics, range, Font, badge, theme.OnYellow,
                TickFormat | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }

        for (var hour = 0; hour <= 24; hour += 6)
        {
            var x = (int)Math.Round(Position(hour));
            var length = LogicalToDeviceUnits(9);
            e.Graphics.DrawLine(ruler, x, trackTop - length, x, trackTop + length);
            TextRenderer.DrawText(e.Graphics, Ticks[hour / 6], Font,
                new Point(x - label.Width / 2, trackTop + LogicalToDeviceUnits(15)), theme.Text, TickFormat);
        }

        float Position(double hour) => left + (float)((right - left) * hour / 24);

        void DrawSpan(double start, double end)
        {
            var x = Position(start);
            var width = Position(end) - x;
            if (width <= 0)
                return;

            e.Graphics.FillRectangle(interval, x, trackTop - 1, width, LogicalToDeviceUnits(2));
        }
    }

}
