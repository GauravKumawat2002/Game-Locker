namespace GameLocker.ConfigUI;

internal sealed class WorkspaceMenuRenderer(Func<WorkspaceTheme> currentTheme) : ToolStripRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var fill = new SolidBrush(currentTheme().Raised);
        e.Graphics.FillRectangle(fill, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var border = new Pen(currentTheme().ControlLine);
        e.Graphics.DrawRectangle(border, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Enabled && e.Item.Selected)
        {
            using var fill = new SolidBrush(currentTheme().Selected);
            e.Graphics.FillRectangle(fill, new Rectangle(Point.Empty, e.Item.Size));
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) =>
        TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, GetInk(e.Item), e.TextFormat);

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var box = e.ImageRectangle;
        using var ink = new Pen(GetInk(e.Item), Math.Max(1.5F, box.Height / 8F));
        var start = new PointF(box.Left + box.Width * 0.22F, box.Top + box.Height * 0.5F);
        var bend = new PointF(box.Left + box.Width * 0.43F, box.Top + box.Height * 0.72F);
        var end = new PointF(box.Left + box.Width * 0.79F, box.Top + box.Height * 0.29F);
        e.Graphics.DrawLines(ink, new[] { start, bend, end });
    }

    private Color GetInk(ToolStripItem item)
    {
        var theme = currentTheme();
        if (!item.Enabled)
            return theme.Disabled;
        if (item.Selected)
            return theme.OnSelected;
        return theme.HighContrast ? SystemColors.ControlText : theme.Text;
    }
}
