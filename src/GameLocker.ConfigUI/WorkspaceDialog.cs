/*  */namespace GameLocker.ConfigUI;

internal sealed class WorkspaceDialog : Form
{
    private readonly Form1 _owner;
    private readonly Label _heading;
    private readonly TextBox _content;
    private readonly bool _danger;
    internal WorkspaceTheme Theme { get; private set; }

    private WorkspaceDialog(Form1 owner, string title, string message, string actionText, string? cancelText = null, bool danger = false)
    {
        _owner = owner;
        _danger = danger;
        Theme = owner.ReadTheme();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        var bodySize = TextRenderer.MeasureText(message, WorkspaceTheme.Body, new Size(488, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        ClientSize = new Size(532, Math.Clamp(bodySize.Height + 126, 192, 378));
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "GameLocker";
        Icon = owner.Icon;
        Font = WorkspaceTheme.Body;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(21, 12, 21, 13),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 41));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        _heading = new Label
        {
            Text = title, Font = WorkspaceTheme.Section,
            TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, Margin = Padding.Empty,
        };
        root.Controls.Add(_heading, 0, 0);
        _content = new TextBox
        {
            Text = message.ReplaceLineEndings(Environment.NewLine), ReadOnly = true, Multiline = true,
            AccessibleName = "Details",
            BorderStyle = BorderStyle.None, ScrollBars = bodySize.Height > 230 ? ScrollBars.Vertical : ScrollBars.None,
            Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 5),
        };
        root.Controls.Add(_content, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            Margin = Padding.Empty, Padding = new Padding(0, 7, 0, 0),
        };
        var action = new WorkspaceButton(actionText, primary: !danger || cancelText is null, danger: danger)
        {
            DialogResult = DialogResult.OK, Size = new Size(153, 37), Margin = Padding.Empty,
        };
        buttons.Controls.Add(action);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
        if (cancelText is not null)
        {
            var cancel = new WorkspaceButton(cancelText)
            {
                DialogResult = DialogResult.Cancel, Size = new Size(112, 37), Margin = new Padding(0, 0, 8, 0),
            };
            buttons.Controls.Add(cancel);
            AcceptButton = cancel;
            CancelButton = cancel;
            ActiveControl = cancel;
        }
        else
        {
            AcceptButton = action;
            CancelButton = action;
            ActiveControl = action;
        }

        ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        _heading.ForeColor = _danger ? Theme.Error : Theme.Text;
        _content.BackColor = Theme.Surface;
        _content.ForeColor = Theme.Text;
        Theme.ApplyTitleBar(this);
        Invalidate(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (_content is null || !WorkspaceTheme.IsAppearanceMessage(message.Msg))
            return;

        var theme = _owner.ReadTheme();
        if (theme != Theme || theme.HighContrast)
        {
            Theme = theme;
            ApplyAppearance();
        }
    }

    public static bool Confirm(Form1 owner, string title, string message, string action,
        string cancel = "Cancel", bool danger = false)
    {
        using var dialog = new WorkspaceDialog(owner, title, message, action, cancel, danger);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    public static void ShowMessage(Form1 owner, string title, string message, string action, bool error = false)
    {
        using var dialog = new WorkspaceDialog(owner, title, message, action, danger: error);
        dialog.ShowDialog(owner);
    }
}
