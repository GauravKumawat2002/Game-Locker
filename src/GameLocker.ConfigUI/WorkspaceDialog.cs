/*  */namespace GameLocker.ConfigUI;

internal sealed class WorkspaceDialog : Form
{
    private WorkspaceDialog(string title, string message, string actionText, string? cancelText = null, bool danger = false)
    {
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
        BackColor = WorkspaceTheme.Surface;
        Font = WorkspaceTheme.Body;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(21, 12, 21, 13),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 41));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.Controls.Add(new Label
        {
            Text = title, Font = WorkspaceTheme.Section, ForeColor = danger ? WorkspaceTheme.Error : WorkspaceTheme.Text,
            TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, Margin = Padding.Empty,
        }, 0, 0);
        var content = new TextBox
        {
            Text = message.ReplaceLineEndings(Environment.NewLine), ReadOnly = true, Multiline = true,
            AccessibleName = "Details", BackColor = WorkspaceTheme.Surface, ForeColor = WorkspaceTheme.Text,
            BorderStyle = BorderStyle.None, ScrollBars = bodySize.Height > 230 ? ScrollBars.Vertical : ScrollBars.None,
            Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 5),
        };
        root.Controls.Add(content, 0, 1);

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
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WorkspaceTheme.UseDarkTitleBar(this);
    }

    public static bool Confirm(Form owner, string title, string message, string action,
        string cancel = "Cancel", bool danger = false)
    {
        using var dialog = new WorkspaceDialog(title, message, action, cancel, danger);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    public static void ShowMessage(Form owner, string title, string message, string action, bool error = false)
    {
        using var dialog = new WorkspaceDialog(title, message, action, danger: error);
        dialog.ShowDialog(owner);
    }
}
