namespace GameLocker.ConfigUI;

public partial class Form1
{
    private readonly ToolTip _toolTip = new() { AutoPopDelay = 9000 };
    private CheckBox[] _dayOptions = null!;
    private TextBox _startInput = null!;
    private TextBox _durationInput = null!;
    private WindowTimeline _timeline = null!;
    private Label _previewMode = null!;
    private Label _windowSummary = null!;
    private Label _transitionTitle = null!;
    private Label _transition = null!;
    private Label _windowState = null!;
    private Label _configState = null!;
    private Label _status = null!;
    private Label _folderCount = null!;
    private ListBox _folderList = null!;
    private Panel _folderEmpty = null!;
    private Button _addFolder = null!;
    private Button _removeFolder = null!;
    private Button _testLock = null!;
    private CheckBox _notifications = null!;
    private CheckBox _vault = null!;
    private Label _vaultFolder = null!;
    private Label _vaultDetails = null!;
    private Label _vaultEmptyTitle = null!;
    private Label _vaultEmptyBody = null!;
    private Panel _vaultEmpty = null!;
    private DataGridView _extensions = null!;
    private Button _scan = null!;
    private Button _selectSaves = null!;
    private Button _selectNone = null!;
    private Button _shorter = null!;
    private Button _longer = null!;
    private Button _save = null!;
    private Button _clear = null!;

    private static readonly DayOfWeek[] Week =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    private void InitializeWorkspace()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1104, 762);
        MinimumSize = new Size(828, 530);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "GameLocker Configuration";
        Font = WorkspaceTheme.Body;
        ForeColor = Theme.Text;
        BackColor = Theme.Canvas;
        DoubleBuffered = true;

        var root = Grid(1, 3);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 71));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 63));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildWorkspace(), 0, 1);
        root.Controls.Add(BuildActions(), 0, 2);
        Controls.Add(root);

        AdjustRowHeights();
        ResumeLayout();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        AdjustRowHeights();
    }

    private void AdjustRowHeights()
    {
        _folderList.ItemHeight = LogicalToDeviceUnits(47);
        _extensions.RowTemplate.Height = LogicalToDeviceUnits(30);
        _extensions.ColumnHeadersHeight = LogicalToDeviceUnits(28);
        foreach (DataGridViewRow row in _extensions.Rows)
            row.Height = _extensions.RowTemplate.Height;
    }

    private Control BuildHeader()
    {
        var header = BindBackground(new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, 2),
        }, theme => theme.Canvas);
        header.Paint += (_, args) =>
        {
            var thickness = header.LogicalToDeviceUnits(2);
            using var line = new SolidBrush(Theme.Pink);
            args.Graphics.FillRectangle(line, 0, header.Height - thickness, header.Width, thickness);
        };
        var content = Grid(3, 1, new Padding(20, 0, 20, 0));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.Controls.Add(new WorkspaceIcon(WorkspaceGlyph.Locker, accent: true)
        {
            Size = new Size(39, 42), Anchor = AnchorStyles.Left, TabStop = false, Margin = Padding.Empty,
        }, 0, 0);

        var identity = Grid(3, 1);
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 27));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var name = Caption("GameLocker", WorkspaceTheme.Brand);
        name.AutoSize = true;
        identity.Controls.Add(name, 0, 0);
        var rule = BindBackground(new Panel { Width = 1, Height = 28, Anchor = AnchorStyles.None }, theme => theme.ControlLine);
        identity.Controls.Add(rule, 1, 0);
        identity.Controls.Add(Caption("Configuration", WorkspaceTheme.Transition, theme => theme.Muted), 2, 0);
        content.Controls.Add(identity, 1, 0);

        var themeSelector = Grid(4, 1);
        themeSelector.Anchor = AnchorStyles.Right;
        themeSelector.Size = new Size(257, 34);
        themeSelector.AccessibleName = "Appearance";
        themeSelector.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        themeSelector.Controls.Add(Caption("Theme", WorkspaceTheme.Strong), 0, 0);
        var index = 1;
        foreach (var mode in Enum.GetValues<WorkspaceThemeMode>())
        {
            themeSelector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3));
            var option = new WorkspaceThemeOption(mode)
            {
                Checked = mode == _themeMode, Margin = new Padding(index == 1 ? 0 : 6, 0, 0, 0),
            };
            option.CheckedChanged += (_, _) =>
            {
                if (option.Checked)
                    SelectTheme(mode);
            };
            themeSelector.Controls.Add(option, index++, 0);
        }
        content.Controls.Add(themeSelector, 2, 0);
        header.Controls.Add(content);
        return header;
    }

    private Control BuildWorkspace()
    {
        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty, TabIndex = 1 };
        var workspace = Grid(2, 1, new Padding(16, 15, 16, 15));
        workspace.Dock = DockStyle.Top;
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 59));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 41));

        var schedule = BuildSchedule(out var scheduleLayout);
        schedule.Margin = new Padding(0, 0, 6, 0);
        workspace.Controls.Add(schedule, 0, 0);

        var sidebar = Grid(1, 2);
        sidebar.Margin = new Padding(6, 0, 0, 0);
        sidebar.TabIndex = 1;
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 228));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.Controls.Add(BuildFolders(), 0, 0);
        var vault = BuildVault();
        vault.Margin = new Padding(0, 12, 0, 0);
        sidebar.Controls.Add(vault, 0, 1);
        workspace.Controls.Add(sidebar, 1, 0);

        const int minimumWorkspaceHeight = 543;
        workspace.Height = minimumWorkspaceHeight;
        viewport.Controls.Add(workspace);
        var compactRows = new[] { 35, 19, 31, 8, 70, 8, 150 };
        var normalRows = new[] { 44, 25, 37, 16, 79, 19, 160 };
        var wasCompact = false;
        viewport.SizeChanged += (_, _) =>
        {
            var height = Math.Max(viewport.ClientSize.Height, viewport.LogicalToDeviceUnits(minimumWorkspaceHeight));
            if (workspace.Height != height)
                workspace.Height = height;

            var compact = viewport.ClientSize.Height < viewport.LogicalToDeviceUnits(430);
            if (compact == wasCompact)
                return;

            wasCompact = compact;
            var rows = compact ? compactRows : normalRows;
            scheduleLayout.SuspendLayout();
            for (var index = 0; index < rows.Length; index++)
                scheduleLayout.RowStyles[index].Height = scheduleLayout.LogicalToDeviceUnits(rows[index]);
            scheduleLayout.ResumeLayout();
        };
        return viewport;
    }

    private Control BuildSchedule(out TableLayoutPanel layout)
    {
        var panel = Surface();
        panel.Padding = new Padding(20, 14, 20, 14);
        layout = Grid(1, 9);
        foreach (var height in new[] { 44, 25, 37, 16, 79, 19, 160 })
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(Caption("Gaming schedule", WorkspaceTheme.Schedule), 0, 0);
        layout.Controls.Add(Caption("Allowed days", WorkspaceTheme.Small, theme => theme.Muted), 0, 1);

        var days = Grid(7, 1);
        _dayOptions = new CheckBox[Week.Length];
        for (var index = 0; index < Week.Length; index++)
        {
            days.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / Week.Length));
            var day = Week[index].ToString();
            var option = new WorkspaceCheckBox(day: true)
            {
                Text = day[..3], AccessibleName = day, Font = WorkspaceTheme.Strong, Dock = DockStyle.Fill,
                TabIndex = index, Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, index == Week.Length - 1 ? 0 : 6, 0),
            };
            option.CheckedChanged += (_, _) => DraftChanged();
            _toolTip.SetToolTip(option, day);
            days.Controls.Add(option, index, 0);
            _dayOptions[index] = option;
        }
        layout.Controls.Add(days, 0, 2);

        var fields = Grid(3, 1);
        fields.TabIndex = 1;
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 15));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        var start = BuildField("Start time", "24-hour", out _startInput);
        _startInput.MaxLength = 16;
        _startInput.TextChanged += (_, _) =>
        {
            // Legacy settings can include seven fractional digits. Keep all digits in the single-line field.
            var font = _startInput.TextLength > 11 ? WorkspaceTheme.PreciseClock : WorkspaceTheme.Clock;
            if (_startInput.Font != font)
                _startInput.Font = font;
        };
        _toolTip.SetToolTip(_startInput, "Enter a local start time, such as 18:30.");
        fields.Controls.Add(start, 0, 0);
        var duration = BuildField("Duration", "hours", out _durationInput, withStepper: true);
        _durationInput.MaxLength = 2;
        duration.TabIndex = 1;
        fields.Controls.Add(duration, 2, 0);
        layout.Controls.Add(fields, 0, 4);
        layout.Controls.Add(BuildPreview(), 0, 6);

        _notifications = new WorkspaceCheckBox
        {
            Text = "Show lock and unlock notifications", Font = WorkspaceTheme.Body, Dock = DockStyle.Fill,
            Margin = new Padding(0, 9, 0, 0), Cursor = Cursors.Hand, TabIndex = 2,
        };
        _notifications.CheckedChanged += (_, _) => DraftChanged();
        layout.Controls.Add(_notifications, 0, 8);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildPreview()
    {
        var preview = Grid(1, 4);
        foreach (var height in new[] { 22, 22, 57 })
            preview.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        preview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var previewHeader = Grid(2, 1);
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        _previewMode = Caption("Window preview", WorkspaceTheme.Small, theme => theme.Muted);
        _windowState = Caption("", WorkspaceTheme.Metadata, alignment: ContentAlignment.MiddleRight);
        previewHeader.Controls.Add(_previewMode, 0, 0);
        previewHeader.Controls.Add(_windowState, 1, 0);
        preview.Controls.Add(previewHeader, 0, 0);
        _windowSummary = Caption("", WorkspaceTheme.Data);
        preview.Controls.Add(_windowSummary, 0, 1);
        _timeline = new WindowTimeline { Dock = DockStyle.Fill, Margin = Padding.Empty, TabStop = false };
        preview.Controls.Add(_timeline, 0, 2);

        var transitionBand = BindBackground(new WorkspaceSurface
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(11, 4, 11, 4),
        }, theme => theme.Raised);
        var transitionContent = Grid(2, 1);
        transitionContent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        transitionContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        transitionContent.Controls.Add(new WorkspaceIcon(WorkspaceGlyph.Locker, accent: true)
        {
            Size = new Size(23, 24), Anchor = AnchorStyles.Left, Margin = Padding.Empty, TabStop = false,
        }, 0, 0);
        var transitionLabels = Grid(1, 2);
        transitionLabels.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        transitionLabels.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _transitionTitle = Caption("", WorkspaceTheme.Strong, theme => theme.HighContrast ? theme.Text : theme.Pink);
        _transition = Caption("", WorkspaceTheme.Transition);
        transitionLabels.Controls.Add(_transitionTitle, 0, 0);
        transitionLabels.Controls.Add(_transition, 0, 1);
        transitionContent.Controls.Add(transitionLabels, 1, 0);
        transitionBand.Controls.Add(transitionContent);
        preview.Controls.Add(transitionBand, 0, 3);
        return preview;
    }

    private TableLayoutPanel BuildField(string title, string unit, out TextBox input, bool withStepper = false)
    {
        var field = Grid(1, 2);
        field.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = Grid(2, 1);
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.Controls.Add(Caption(title, WorkspaceTheme.Transition), 0, 0);
        var unitLabel = Caption(unit, WorkspaceTheme.Metadata, theme => theme.Muted);
        unitLabel.AutoSize = true;
        heading.Controls.Add(unitLabel, 1, 0);
        field.Controls.Add(heading, 0, 0);

        var surface = BindBackground(new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(1),
        }, theme => theme.Inset);
        var row = Grid(withStepper ? 2 : 1, 1, new Padding(9, 1, withStepper ? 3 : 9, 1));
        input = BindBackground(new TextBox
        {
            AccessibleName = title, Font = WorkspaceTheme.Clock, BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = Padding.Empty,
        }, theme => theme.Inset);
        BindForeground(input, theme => theme.Text);
        var textInput = input;
        input.TextChanged += (_, _) => DraftChanged();
        input.GotFocus += (_, _) => surface.Invalidate();
        input.LostFocus += (_, _) => surface.Invalidate();
        surface.Paint += (_, args) =>
        {
            using var pen = new Pen(textInput.Focused ? Theme.Pink : Theme.ControlLine);
            args.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };
        row.Controls.Add(input, 0, 0);
        if (withStepper)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 33));
            var stepper = Grid(1, 2);
            stepper.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            stepper.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _longer = new WorkspaceButton("+") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 1),
                AccessibleName = "Increase duration", TabIndex = 0 };
            _shorter = new WorkspaceButton("−") { Dock = DockStyle.Fill, Margin = new Padding(0, 1, 0, 0),
                AccessibleName = "Decrease duration", TabIndex = 1 };
            _shorter.Click += (_, _) => AdjustDuration(-1);
            _longer.Click += (_, _) => AdjustDuration(1);
            stepper.Controls.Add(_longer, 0, 0);
            stepper.Controls.Add(_shorter, 0, 1);
            row.Controls.Add(stepper, 1, 0);
        }
        surface.Controls.Add(row);
        field.Controls.Add(surface, 0, 1);
        return field;
    }

    private Control BuildFolders()
    {
        var panel = Surface();
        var layout = Grid(1, 3);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var heading = Grid(2, 1);
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.Controls.Add(Caption("Game folders", WorkspaceTheme.Section), 0, 0);
        _folderCount = Caption("0 folders", WorkspaceTheme.Metadata, theme => theme.Muted);
        _folderCount.AutoSize = true;
        heading.Controls.Add(_folderCount, 1, 0);
        layout.Controls.Add(heading, 0, 0);

        var content = BindBackground(new WorkspaceSurface
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(1),
        }, theme => theme.Inset);
        _folderList = BindBackground(new ListBox
        {
            AccessibleName = "Game folders", Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed,
            BorderStyle = BorderStyle.None, IntegralHeight = false, TabIndex = 0,
        }, theme => theme.Inset);
        BindForeground(_folderList, theme => theme.Text);
        _folderList.DrawItem += DrawFolder;
        _folderList.SelectedIndexChanged += (_, _) => FolderSelectionChanged();
        _folderEmpty = EmptyPanel(WorkspaceGlyph.Folder, "No game folders", "Add a folder to put it on the schedule.", out _, out _);
        content.Controls.Add(_folderList);
        content.Controls.Add(_folderEmpty);
        layout.Controls.Add(content, 0, 1);

        var actions = Grid(3, 1, new Padding(0, 8, 0, 0));
        actions.TabIndex = 1;
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        _addFolder = ActionButton(actions, 0, "&Add folder…");
        _testLock = ActionButton(actions, 1, "Test &lock");
        _removeFolder = ActionButton(actions, 2, "&Remove", last: true);
        _addFolder.Click += AddFolder;
        _testLock.Click += TestLock;
        _removeFolder.Click += RemoveFolder;
        layout.Controls.Add(actions, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildVault()
    {
        var panel = Surface();
        panel.TabIndex = 1;
        var layout = Grid(1, 6);
        foreach (var height in new[] { 36, 28, 23, 22 })
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.Controls.Add(Caption("Vault", WorkspaceTheme.Section), 0, 0);
        _vault = new WorkspaceCheckBox
        {
            Text = "Encrypt selected file types", Font = WorkspaceTheme.Strong, Dock = DockStyle.Fill,
            TabIndex = 0, Margin = Padding.Empty, Cursor = Cursors.Hand,
        };
        _vault.CheckedChanged += (_, _) =>
        {
            if (_populating)
                return;
            RefreshVault();
            DraftChanged();
        };
        layout.Controls.Add(_vault, 0, 1);

        _vaultFolder = Caption("", WorkspaceTheme.Strong);
        _vaultDetails = Caption("", WorkspaceTheme.Metadata, theme => theme.Muted);
        layout.Controls.Add(_vaultFolder, 0, 2);
        layout.Controls.Add(_vaultDetails, 0, 3);
        var content = BindBackground(new WorkspaceSurface
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(1), TabIndex = 1,
        }, theme => theme.Inset);
        _extensions = BuildExtensionGrid();
        _vaultEmpty = EmptyPanel(WorkspaceGlyph.Files, "", "", out _vaultEmptyTitle, out _vaultEmptyBody);
        content.Controls.Add(_extensions);
        content.Controls.Add(_vaultEmpty);
        layout.Controls.Add(content, 0, 4);

        var actions = Grid(3, 1, new Padding(0, 9, 0, 0));
        actions.TabIndex = 2;
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        _scan = ActionButton(actions, 0, "Scan file types");
        _selectSaves = ActionButton(actions, 1, "Select saves");
        _selectNone = ActionButton(actions, 2, "Clear", last: true);
        _selectNone.AccessibleName = "Clear file type selection";
        _toolTip.SetToolTip(_selectSaves, "Select known save-file extensions in this folder.");
        _toolTip.SetToolTip(_selectNone, "Clear every selected file type for this folder.");
        _scan.Click += ScanFolder;
        _selectSaves.Click += (_, _) => SelectExtensions(saveFilesOnly: true);
        _selectNone.Click += (_, _) => SelectExtensions(saveFilesOnly: false);
        layout.Controls.Add(actions, 0, 5);
        panel.Controls.Add(layout);
        return panel;
    }

    private DataGridView BuildExtensionGrid()
    {
        var grid = new DataGridView
        {
            AccessibleName = "Vault file types", Dock = DockStyle.Fill, Margin = Padding.Empty,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            EnableHeadersVisualStyles = false, RowHeadersVisible = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToOrderColumns = false,
            AllowUserToResizeRows = false, AllowUserToResizeColumns = false, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, StandardTab = true,
        };
        ApplyGridAppearance(grid);
        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Encrypt", HeaderText = "Use", FillWeight = 12, MinimumWidth = 34,
            FlatStyle = FlatStyle.Flat, SortMode = DataGridViewColumnSortMode.NotSortable,
            ToolTipText = "Encrypt this file type",
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FileType", HeaderText = "Type", ReadOnly = true, FillWeight = 24,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Font = WorkspaceTheme.Metadata },
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FileCount", HeaderText = "Files", ReadOnly = true, FillWeight = 14, SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FileSize", HeaderText = "Size", ReadOnly = true, FillWeight = 23, SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Font = WorkspaceTheme.Metadata },
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Risk", HeaderText = "Risk", ReadOnly = true, FillWeight = 27, SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += ExtensionSelectionChanged;
        return grid;
    }

    private void ApplyGridAppearance(DataGridView grid)
    {
        grid.BackgroundColor = Theme.Inset;
        grid.GridColor = Theme.Line;
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            Font = WorkspaceTheme.Small,
            BackColor = Theme.Inset, ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selected, SelectionForeColor = Theme.OnSelected,
            Alignment = DataGridViewContentAlignment.MiddleLeft, WrapMode = DataGridViewTriState.False,
        };
        var headingText = Theme.HighContrast ? SystemColors.ControlText : Theme.Muted;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = WorkspaceTheme.Metadata,
            BackColor = Theme.Raised, ForeColor = headingText,
            SelectionBackColor = Theme.Raised, SelectionForeColor = headingText,
            Alignment = DataGridViewContentAlignment.MiddleLeft, WrapMode = DataGridViewTriState.False,
        };
    }

    private Control BuildActions()
    {
        var panel = BindBackground(new WorkspaceSurface
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 2,
        }, theme => theme.Canvas);
        var bar = Grid(4, 1, new Padding(16, 10, 16, 10));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 147));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 171));
        bar.Controls.Add(BuildLinks(), 0, 0);
        var feedback = Grid(1, 2, new Padding(12, 0, 13, 0));
        feedback.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
        feedback.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _configState = Caption("Loading configuration", WorkspaceTheme.Strong);
        _status = Caption("", WorkspaceTheme.Small);
        feedback.Controls.Add(_configState, 0, 0);
        feedback.Controls.Add(_status, 0, 1);
        bar.Controls.Add(feedback, 1, 0);
        _clear = ActionButton(bar, 2, "Clear configuration", danger: true);
        _save = ActionButton(bar, 3, "&Save configuration", primary: true, last: true);
        _clear.Click += ClearConfiguration;
        _save.Click += SaveConfiguration;
        _toolTip.SetToolTip(_save, "Save configuration (Ctrl+S)");
        panel.Controls.Add(bar);
        return panel;
    }

    private Control BuildLinks()
    {
        var links = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Left, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false, Margin = Padding.Empty, TabIndex = 0,
        };
        foreach (var link in ProjectLinks.All)
        {
            var control = new WorkspaceButton(string.Empty)
            {
                Size = new Size(38, 36), Margin = new Padding(0, 0, 6, 0), Image = link.Logo(Theme),
                AccessibleRole = AccessibleRole.Link, AccessibleName = $"Open {link.Label} in your browser",
            };
            _appearanceBindings.Add(() => control.Image = link.Logo(Theme));
            control.Click += (_, _) => OpenProjectLink(link);
            _toolTip.SetToolTip(control, link.Uri is null ? link.Label : $"{link.Label}\n{link.Uri.AbsoluteUri}");
            links.Controls.Add(control);
        }
        return links;
    }

    private WorkspaceSurface Surface() =>
        BindBackground(new WorkspaceSurface
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(13, 11, 13, 11),
        }, theme => theme.Surface);

    private static TableLayoutPanel Grid(int columns, int rows, Padding padding = default)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, Margin = Padding.Empty, Padding = padding,
        };
        if (columns == 1)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        if (rows == 1)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return grid;
    }

    private Label Caption(string text, Font font, Func<WorkspaceTheme, Color>? color = null,
        ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        var label = new Label
        {
            Text = text, Font = font, Dock = DockStyle.Fill, Margin = Padding.Empty,
            TextAlign = alignment, AutoEllipsis = true, UseMnemonic = false,
        };
        if (color is not null)
            BindForeground(label, color);
        return label;
    }

    private static Button ActionButton(TableLayoutPanel layout, int column, string title,
        bool primary = false, bool danger = false, bool last = false)
    {
        var button = new WorkspaceButton(title, primary, danger)
        {
            Dock = DockStyle.Fill, Margin = new Padding(0, 0, last ? 0 : 7, 0), TabIndex = column,
        };
        layout.Controls.Add(button, column, 0);
        return button;
    }

    private Panel EmptyPanel(WorkspaceGlyph glyph, string title, string body, out Label heading, out Label details)
    {
        var panel = BindBackground(new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, TabStop = false,
        }, theme => theme.Inset);
        var content = Grid(1, 3, new Padding(5, 0, 5, 0));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        content.Controls.Add(new WorkspaceIcon(glyph) { Size = new Size(25, 23), Anchor = AnchorStyles.None, TabStop = false }, 0, 0);
        heading = Caption(title, WorkspaceTheme.Strong, alignment: ContentAlignment.MiddleCenter);
        details = Caption(body, WorkspaceTheme.Small, theme => theme.Muted, ContentAlignment.TopCenter);
        content.Controls.Add(heading, 0, 1);
        content.Controls.Add(details, 0, 2);
        panel.Controls.Add(content);
        content.Dock = DockStyle.None;
        void CenterContent()
        {
            content.SetBounds(0, Math.Max(0, (panel.Height - content.Height) / 2), panel.Width, content.Height);
        }
        content.Height = 63;
        panel.SizeChanged += (_, _) => CenterContent();
        CenterContent();
        return panel;
    }

    private void DrawFolder(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _folderList.Items[e.Index] is not string path)
            return;

        var selected = (e.State & DrawItemState.Selected) != 0;
        var background = selected ? Theme.Selected : Theme.Inset;
        var foreground = selected ? Theme.OnSelected : Theme.Text;
        using var fill = new SolidBrush(background);
        using var divider = new Pen(Theme.Line);
        e.Graphics.FillRectangle(fill, e.Bounds);
        e.Graphics.DrawLine(divider, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        var glyphSize = LogicalToDeviceUnits(20);
        var glyphColor = Theme.HighContrast ? foreground : Theme.Yellow;
        WorkspacePaint.Glyph(e.Graphics, WorkspaceGlyph.Folder, new Rectangle(
            e.Bounds.Left + LogicalToDeviceUnits(10), e.Bounds.Top + (e.Bounds.Height - glyphSize) / 2,
            glyphSize, glyphSize), glyphColor, Theme.HighContrast ? background : Theme.OnYellow);
        var textLeft = e.Bounds.Left + LogicalToDeviceUnits(42);
        var textRight = e.Bounds.Right - LogicalToDeviceUnits(9);
        var textTop = e.Bounds.Top + LogicalToDeviceUnits(5);
        var split = e.Bounds.Top + e.Bounds.Height / 2;
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, FolderName(path), WorkspaceTheme.Strong,
            Rectangle.FromLTRB(textLeft, textTop, textRight, split), foreground, flags | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, path, WorkspaceTheme.Metadata,
            Rectangle.FromLTRB(textLeft, split, textRight, e.Bounds.Bottom - LogicalToDeviceUnits(5)),
            selected ? foreground : Theme.Muted, flags | TextFormatFlags.PathEllipsis);
        if ((e.State & DrawItemState.Focus) != 0)
            e.DrawFocusRectangle();
    }
}
