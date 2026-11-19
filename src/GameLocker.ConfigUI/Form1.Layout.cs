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
        ForeColor = WorkspaceTheme.Text;
        BackColor = WorkspaceTheme.Canvas;
        DoubleBuffered = true;

        var root = Grid(1, 3);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 79));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildWorkspace(), 0, 1);
        root.Controls.Add(BuildActions(), 0, 2);
        Controls.Add(root);

        AdjustRowHeights();
        ResumeLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WorkspaceTheme.UseDarkTitleBar(this);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        AdjustRowHeights();
    }

    private void AdjustRowHeights()
    {
        _folderList.ItemHeight = LogicalToDeviceUnits(48);
        _extensions.RowTemplate.Height = LogicalToDeviceUnits(30);
        _extensions.ColumnHeadersHeight = LogicalToDeviceUnits(28);
        foreach (DataGridViewRow row in _extensions.Rows)
            row.Height = _extensions.RowTemplate.Height;
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = WorkspaceTheme.Raised };
        header.Paint += (_, args) =>
        {
            using var line = new Pen(WorkspaceTheme.Line);
            args.Graphics.DrawLine(line, 0, header.Height - 1, header.Width, header.Height - 1);
        };
        var content = Grid(3, 1, new Padding(22, 0, 22, 0));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var logo = new WorkspaceIcon(WorkspaceGlyph.Locker, badge: true)
        {
            Size = new Size(43, 43), Anchor = AnchorStyles.Left, TabStop = false,
        };
        content.Controls.Add(logo, 0, 0);

        var identity = Grid(1, 2, new Padding(0, 14, 0, 13));
        identity.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        identity.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        identity.Controls.Add(Caption("GameLocker", WorkspaceTheme.Brand), 0, 0);
        identity.Controls.Add(Caption("Configuration", WorkspaceTheme.Small, WorkspaceTheme.Muted), 0, 1);
        content.Controls.Add(identity, 1, 0);

        _configState = Caption("LOADING CONFIGURATION", WorkspaceTheme.Metadata, WorkspaceTheme.Muted);
        _configState.AutoSize = true;
        _configState.Dock = DockStyle.None;
        _configState.Anchor = AnchorStyles.Right;
        _configState.Padding = new Padding(10, 6, 10, 6);
        _configState.BackColor = WorkspaceTheme.Inset;
        _configState.BorderStyle = BorderStyle.FixedSingle;
        content.Controls.Add(_configState, 2, 0);
        header.Controls.Add(content);
        return header;
    }

    private Control BuildWorkspace()
    {
        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty, TabIndex = 0 };
        var workspace = Grid(2, 1, new Padding(22, 18, 22, 18));
        workspace.Dock = DockStyle.Top;
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        var left = Grid(1, 2);
        left.Margin = new Padding(0, 0, 7, 0);
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 328));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(BuildSchedule(), 0, 0);
        var folders = BuildFolders();
        folders.Margin = new Padding(0, 14, 0, 0);
        left.Controls.Add(folders, 0, 1);
        workspace.Controls.Add(left, 0, 0);

        var right = Grid(1, 2);
        right.Margin = new Padding(7, 0, 0, 0);
        right.TabIndex = 1;
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        right.Controls.Add(BuildVault(), 0, 0);
        var preferences = BuildPreferences();
        preferences.Margin = new Padding(0, 14, 0, 0);
        right.Controls.Add(preferences, 0, 1);
        workspace.Controls.Add(right, 1, 0);

        viewport.Controls.Add(workspace);
        workspace.Height = 578;
        viewport.SizeChanged += (_, _) =>
        {
            var height = Math.Max(viewport.ClientSize.Height, viewport.LogicalToDeviceUnits(578));
            if (workspace.Height != height)
                workspace.Height = height;
        };
        return viewport;
    }

    private Control BuildSchedule()
    {
        var panel = Surface();
        var layout = Grid(1, 7);
        foreach (var height in new[] { 31, 25, 35, 14, 64, 13 })
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(Caption("Gaming schedule", WorkspaceTheme.Section), 0, 0);
        layout.Controls.Add(Caption("Allowed days", WorkspaceTheme.Small, WorkspaceTheme.Muted), 0, 1);

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
                Margin = new Padding(0, 0, index == Week.Length - 1 ? 0 : 5, 0),
            };
            option.CheckedChanged += (_, _) => DraftChanged();
            _toolTip.SetToolTip(option, day);
            days.Controls.Add(option, index, 0);
            _dayOptions[index] = option;
        }
        layout.Controls.Add(days, 0, 2);

        var fields = Grid(3, 1);
        fields.TabIndex = 1;
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var start = BuildField("Start time", "24-hour", out _startInput);
        _startInput.MaxLength = 16;
        _toolTip.SetToolTip(_startInput, "Enter a local start time, such as 18:30.");
        fields.Controls.Add(start, 0, 0);
        var duration = BuildField("Duration", "hours", out _durationInput, withStepper: true);
        _durationInput.MaxLength = 2;
        duration.TabIndex = 1;
        fields.Controls.Add(duration, 2, 0);
        layout.Controls.Add(fields, 0, 4);

        var preview = Grid(1, 4, new Padding(11, 6, 11, 4));
        preview.BackColor = WorkspaceTheme.Inset;
        preview.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
        preview.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        preview.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        preview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var previewHeader = Grid(2, 1);
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        _previewMode = Caption("Window preview", WorkspaceTheme.Small, WorkspaceTheme.Muted);
        _windowState = Caption("", WorkspaceTheme.Metadata, WorkspaceTheme.Accent, ContentAlignment.MiddleRight);
        previewHeader.Controls.Add(_previewMode, 0, 0);
        previewHeader.Controls.Add(_windowState, 1, 0);
        preview.Controls.Add(previewHeader, 0, 0);
        _windowSummary = Caption("", WorkspaceTheme.Data);
        preview.Controls.Add(_windowSummary, 0, 1);
        _timeline = new WindowTimeline
        {
            AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0), TabStop = false,
        };
        preview.Controls.Add(_timeline, 0, 2);
        _transition = Caption("", WorkspaceTheme.Small, WorkspaceTheme.Muted);
        preview.Controls.Add(_transition, 0, 3);
        layout.Controls.Add(preview, 0, 6);
        panel.Controls.Add(layout);
        return panel;
    }

    private TableLayoutPanel BuildField(string title, string unit, out TextBox input, bool withStepper = false)
    {
        var field = Grid(1, 2);
        field.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = Grid(2, 1);
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.Controls.Add(Caption(title, WorkspaceTheme.Small, WorkspaceTheme.Text), 0, 0);
        var unitLabel = Caption(unit, WorkspaceTheme.Metadata, WorkspaceTheme.Muted);
        unitLabel.AutoSize = true;
        heading.Controls.Add(unitLabel, 1, 0);
        field.Controls.Add(heading, 0, 0);

        var surface = new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(1), BackColor = WorkspaceTheme.Inset,
        };
        var row = Grid(withStepper ? 3 : 1, 1, new Padding(8, 1, withStepper ? 3 : 8, 1));
        input = new TextBox
        {
            AccessibleName = title, Font = WorkspaceTheme.Data, BorderStyle = BorderStyle.None,
            BackColor = WorkspaceTheme.Inset, ForeColor = WorkspaceTheme.Text,
            Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = Padding.Empty,
        };
        input.TextChanged += (_, _) => DraftChanged();
        input.GotFocus += (_, _) => surface.Invalidate();
        input.LostFocus += (_, _) => surface.Invalidate();
        var textInput = input;
        surface.Paint += (_, args) =>
        {
            using var pen = new Pen(textInput.Focused ? WorkspaceTheme.Accent : WorkspaceTheme.ControlLine);
            args.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };
        row.Controls.Add(input, 0, 0);
        if (withStepper)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 29));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 29));
            _shorter = new WorkspaceButton("−") { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 3, 3),
                AccessibleName = "Decrease duration", TabIndex = 1 };
            _longer = new WorkspaceButton("+") { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3),
                AccessibleName = "Increase duration", TabIndex = 2 };
            _shorter.Click += (_, _) => AdjustDuration(-1);
            _longer.Click += (_, _) => AdjustDuration(1);
            row.Controls.Add(_shorter, 1, 0);
            row.Controls.Add(_longer, 2, 0);
        }
        surface.Controls.Add(row);
        field.Controls.Add(surface, 0, 1);
        return field;
    }

    private Control BuildFolders()
    {
        var panel = Surface();
        panel.TabIndex = 1;
        var layout = Grid(1, 4);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));

        var heading = Grid(2, 1);
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.Controls.Add(Caption("Game folders", WorkspaceTheme.Section), 0, 0);
        _folderCount = Caption("0 folders", WorkspaceTheme.Metadata, WorkspaceTheme.Muted);
        _folderCount.AutoSize = true;
        heading.Controls.Add(_folderCount, 1, 0);
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(Caption("Folders GameLocker restricts outside play time.", WorkspaceTheme.Small, WorkspaceTheme.Muted), 0, 1);

        var content = new WorkspaceSurface { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(1),
            BackColor = WorkspaceTheme.Inset };
        _folderList = new ListBox
        {
            AccessibleName = "Game folders", Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed,
            BorderStyle = BorderStyle.None, IntegralHeight = false, BackColor = WorkspaceTheme.Inset,
            ForeColor = WorkspaceTheme.Text, TabIndex = 0,
        };
        _folderList.DrawItem += DrawFolder;
        _folderList.SelectedIndexChanged += (_, _) => FolderSelectionChanged();
        _folderEmpty = EmptyPanel(WorkspaceGlyph.Folder, "No game folders", "Use Add folder to choose what GameLocker manages.", out _, out _);
        content.Controls.Add(_folderList);
        content.Controls.Add(_folderEmpty);
        layout.Controls.Add(content, 0, 2);

        var actions = Grid(4, 1, new Padding(0, 9, 0, 0));
        actions.TabIndex = 1;
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        _addFolder = ActionButton(actions, 0, "&Add folder…");
        _removeFolder = ActionButton(actions, 1, "&Remove");
        _testLock = ActionButton(actions, 3, "Test &lock", last: true);
        _addFolder.Click += AddFolder;
        _removeFolder.Click += RemoveFolder;
        _testLock.Click += TestLock;
        layout.Controls.Add(actions, 0, 3);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildVault()
    {
        var panel = Surface();
        var layout = Grid(1, 7);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.Controls.Add(Caption("Vault", WorkspaceTheme.Section), 0, 0);
        _vault = new WorkspaceCheckBox
        {
            Text = "Encrypt selected file types", Font = WorkspaceTheme.Strong, Dock = DockStyle.Fill,
            TabIndex = 0, Margin = Padding.Empty, BackColor = WorkspaceTheme.Surface, Cursor = Cursors.Hand,
        };
        _vault.CheckedChanged += (_, _) =>
        {
            if (_populating)
                return;
            RefreshVault();
            DraftChanged();
        };
        layout.Controls.Add(_vault, 0, 1);
        layout.Controls.Add(Caption("Adds file encryption when a folder locks.", WorkspaceTheme.Small, WorkspaceTheme.Muted), 0, 2);

        _vaultFolder = Caption("", WorkspaceTheme.Strong);
        _vaultDetails = Caption("", WorkspaceTheme.Metadata, WorkspaceTheme.Muted);
        layout.Controls.Add(_vaultFolder, 0, 3);
        layout.Controls.Add(_vaultDetails, 0, 4);
        var content = new WorkspaceSurface { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(1),
            BackColor = WorkspaceTheme.Inset, TabIndex = 1 };
        _extensions = BuildExtensionGrid();
        _vaultEmpty = EmptyPanel(WorkspaceGlyph.Files, "", "", out _vaultEmptyTitle, out _vaultEmptyBody);
        content.Controls.Add(_extensions);
        content.Controls.Add(_vaultEmpty);
        layout.Controls.Add(content, 0, 5);

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
        layout.Controls.Add(actions, 0, 6);
        panel.Controls.Add(layout);
        return panel;
    }

    private DataGridView BuildExtensionGrid()
    {
        var grid = new DataGridView
        {
            AccessibleName = "Vault file types", Dock = DockStyle.Fill, Margin = Padding.Empty,
            BackgroundColor = WorkspaceTheme.Inset, GridColor = WorkspaceTheme.Line, BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            EnableHeadersVisualStyles = false, RowHeadersVisible = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToOrderColumns = false,
            AllowUserToResizeRows = false, AllowUserToResizeColumns = false, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, StandardTab = true,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = WorkspaceTheme.Inset, ForeColor = WorkspaceTheme.Text, Font = WorkspaceTheme.Small,
                SelectionBackColor = WorkspaceTheme.Selected, SelectionForeColor = WorkspaceTheme.OnSelected,
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = WorkspaceTheme.Raised, ForeColor = WorkspaceTheme.Muted, Font = WorkspaceTheme.Metadata,
                SelectionBackColor = WorkspaceTheme.Raised, SelectionForeColor = WorkspaceTheme.Text,
            },
        };
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

    private Control BuildPreferences()
    {
        var panel = Surface();
        panel.TabIndex = 1;
        var layout = Grid(1, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(Caption("Preferences", WorkspaceTheme.Section), 0, 0);
        _notifications = new WorkspaceCheckBox
        {
            Text = "Show lock and unlock notifications", Font = WorkspaceTheme.Body, Dock = DockStyle.Fill,
            Margin = Padding.Empty, BackColor = WorkspaceTheme.Surface, Cursor = Cursors.Hand,
        };
        _notifications.CheckedChanged += (_, _) => DraftChanged();
        layout.Controls.Add(_notifications, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildActions()
    {
        var panel = new WorkspaceSurface { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 1 };
        var bar = Grid(4, 1, new Padding(22, 12, 22, 12));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 147));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 171));
        bar.Controls.Add(BuildLinks(), 0, 0);
        _status = Caption("Loading configuration…", WorkspaceTheme.Small, WorkspaceTheme.Muted);
        _status.Margin = new Padding(16, 0, 14, 0);
        bar.Controls.Add(_status, 1, 0);
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
                Size = new Size(40, 36), Margin = new Padding(0, 0, 6, 0), Image = link.Logo,
                AccessibleRole = AccessibleRole.Link, AccessibleName = $"Open {link.Label} in your browser",
            };
            control.Click += (_, _) => OpenProjectLink(link);
            _toolTip.SetToolTip(control, link.Uri is null ? link.Label : $"{link.Label}\n{link.Uri.AbsoluteUri}");
            links.Controls.Add(control);
        }
        return links;
    }

    private static WorkspaceSurface Surface() =>
        new() { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(15, 12, 15, 12) };

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

    private static Label Caption(string text, Font font, Color? color = null,
        ContentAlignment alignment = ContentAlignment.MiddleLeft) => new()
    {
        Text = text, Font = font, ForeColor = color ?? WorkspaceTheme.Text, Dock = DockStyle.Fill,
        Margin = Padding.Empty, TextAlign = alignment, AutoEllipsis = true, UseMnemonic = false,
    };

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

    private static Panel EmptyPanel(WorkspaceGlyph glyph, string title, string body, out Label heading, out Label details)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = WorkspaceTheme.Inset, TabStop = false,
        };
        var content = Grid(1, 3, new Padding(5, 0, 5, 0));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        content.Controls.Add(new WorkspaceIcon(glyph) { Size = new Size(25, 23), Anchor = AnchorStyles.None, TabStop = false }, 0, 0);
        heading = Caption(title, WorkspaceTheme.Strong, alignment: ContentAlignment.MiddleCenter);
        details = Caption(body, WorkspaceTheme.Small, WorkspaceTheme.Muted, ContentAlignment.TopCenter);
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
        var background = selected ? WorkspaceTheme.Selected : WorkspaceTheme.Inset;
        var foreground = selected ? WorkspaceTheme.OnSelected : WorkspaceTheme.Text;
        using var fill = new SolidBrush(background);
        using var divider = new Pen(WorkspaceTheme.Line);
        e.Graphics.FillRectangle(fill, e.Bounds);
        e.Graphics.DrawLine(divider, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        var glyphSize = LogicalToDeviceUnits(20);
        WorkspaceTheme.DrawGlyph(e.Graphics, WorkspaceGlyph.Folder, new Rectangle(
            e.Bounds.Left + LogicalToDeviceUnits(11), e.Bounds.Top + (e.Bounds.Height - glyphSize) / 2,
            glyphSize, glyphSize), selected ? foreground : WorkspaceTheme.Muted);
        var textLeft = e.Bounds.Left + LogicalToDeviceUnits(42);
        var textRight = e.Bounds.Right - LogicalToDeviceUnits(9);
        var textTop = e.Bounds.Top + LogicalToDeviceUnits(5);
        var split = e.Bounds.Top + e.Bounds.Height / 2;
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, FolderName(path), WorkspaceTheme.Strong,
            Rectangle.FromLTRB(textLeft, textTop, textRight, split), foreground, flags | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, path, WorkspaceTheme.Metadata,
            Rectangle.FromLTRB(textLeft, split, textRight, e.Bounds.Bottom - LogicalToDeviceUnits(5)),
            selected ? foreground : WorkspaceTheme.Muted, flags | TextFormatFlags.PathEllipsis);
        if ((e.State & DrawItemState.Focus) != 0)
            e.DrawFocusRectangle();
    }
}
