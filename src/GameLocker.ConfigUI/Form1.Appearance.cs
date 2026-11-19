namespace GameLocker.ConfigUI;

public partial class Form1
{
    private readonly List<Action> _appearanceBindings = [];
    private readonly WorkspaceThemePreferences _themePreferences;
    private WorkspaceThemeMode _themeMode;
    internal WorkspaceTheme Theme { get; private set; }

    internal WorkspaceTheme ReadTheme() => WorkspaceTheme.Resolve(_themeMode);

    private void SelectTheme(WorkspaceThemeMode mode)
    {
        if (mode == _themeMode)
            return;

        _themeMode = mode;
        RefreshAppearance();
        if (!_themePreferences.TrySave(mode))
            ShowStatus("Could not save the theme preference for next time.", StatusTone.Warning);
    }

    private void RefreshAppearance()
    {
        var theme = ReadTheme();
        if (theme == Theme && !theme.HighContrast)
            return;

        Theme = theme;
        BackColor = theme.Canvas;
        ForeColor = theme.Text;
        foreach (var bind in _appearanceBindings)
            bind();

        ApplyGridAppearance(_extensions);
        foreach (DataGridViewRow row in _extensions.Rows)
            ApplyRiskAppearance(row);
        ApplyStatusAppearance();
        ApplyVaultStateAppearance();
        ApplyScheduleAppearance();
        theme.ApplyTitleBar(this);
        Invalidate(true);
    }

    private T BindBackground<T>(T control, Func<WorkspaceTheme, Color> color) where T : Control
    {
        void Apply() => control.BackColor = color(Theme);
        Apply();
        _appearanceBindings.Add(Apply);
        return control;
    }

    private void BindForeground(Control control, Func<WorkspaceTheme, Color> color)
    {
        void Apply() => control.ForeColor = color(Theme);
        Apply();
        _appearanceBindings.Add(Apply);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RefreshAppearance();
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (_status is not null && WorkspaceTheme.IsAppearanceMessage(message.Msg))
            RefreshAppearance();
    }
}
