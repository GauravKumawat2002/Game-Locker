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
        Font = WorkspaceTheme.Metadata;
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
        var left = (label.Width + 1) / 2 + LogicalToDeviceUnits(2);
        var right = Width - left;
        if (right <= left)
            return;

        var trackTop = LogicalToDeviceUnits(18);
        var trackHeight = LogicalToDeviceUnits(9);
        using var inactive = new SolidBrush(theme.Line);
        using var interval = new SolidBrush(theme.Yellow);
        using var activeOutline = new Pen(theme.OnYellow);
        using var activePoint = new SolidBrush(theme.HighContrast ? theme.Text : theme.Pink);
        using var tick = new Pen(theme.ControlLine);
        e.Graphics.FillRectangle(inactive, left, trackTop, right - left, trackHeight);

        if (_hasDays)
        {
            var start = _start.ToTimeSpan().TotalHours;
            var end = start + _durationHours;
            DrawSpan(start, Math.Min(end, 24));
            if (end > 24)
                DrawSpan(0, end - 24);

            var x = (int)Math.Round(Position(start));
            var side = LogicalToDeviceUnits(5);
            Point[] marker = [new(x - side, trackTop - 2 * side), new(x + side, trackTop - 2 * side), new(x, trackTop)];
            e.Graphics.FillPolygon(activePoint, marker);
        }

        for (var hour = 0; hour <= 24; hour++)
        {
            var x = (int)Math.Round(Position(hour));
            var major = hour % 6 == 0;
            var length = LogicalToDeviceUnits(major ? 8 : 3);
            e.Graphics.DrawLine(tick, x, trackTop + trackHeight, x, trackTop + trackHeight + length);
            if (major)
                TextRenderer.DrawText(e.Graphics, Ticks[hour / 6], Font,
                    new Point(x - label.Width / 2, trackTop + trackHeight + length + LogicalToDeviceUnits(4)),
                    theme.Text, TickFormat);
        }

        float Position(double hour) => left + (float)((right - left) * hour / 24);

        void DrawSpan(double start, double end)
        {
            var x = Position(start);
            var width = Position(end) - x;
            if (width <= 0)
                return;

            e.Graphics.FillRectangle(interval, x, trackTop, width, trackHeight);
            if (width > 1)
                e.Graphics.DrawRectangle(activeOutline, x, trackTop, width, trackHeight - 1);
        }
    }
}
