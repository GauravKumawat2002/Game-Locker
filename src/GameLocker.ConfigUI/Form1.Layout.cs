namespace GameLocker.ConfigUI;

public partial class Form1
{
    private readonly ToolTip _toolTip = new() { AutoPopDelay = 9000 };
    private CheckBox[] _dayOptions = null!;
    private TextBox _startInput = null!;
    private TextBox _durationInput = null!;
    private WindowTimeline _timeline = null!;
    private Button _startEarlier = null!;
    private Button _startLater = null!;
    private Label _transitionTitle = null!;
    private Label _transition = null!;
    private readonly List<ContextMenuStrip> _menus = [];
    private Label _configState = null!;
    private Label _status = null!;
    private ListBox _folderList = null!;
    private Panel _folderEmpty = null!;
    private Button _addFolder = null!;
    private ToolStripMenuItem _removeFolder = null!;
    private ToolStripMenuItem _testLock = null!;
    private ToolStripMenuItem _notifications = null!;
    private ToolStripMenuItem _vault = null!;
    private Label _vaultEmptyTitle = null!;
    private Label _vaultEmptyBody = null!;
    private Panel _vaultEmpty = null!;
    private DataGridView _extensions = null!;
    private ToolStripMenuItem _scan = null!;
    private ToolStripMenuItem _selectSaves = null!;
    private ToolStripMenuItem _selectNone = null!;
    private Button _shorter = null!;
    private Button _longer = null!;
    private Button _save = null!;
    private ToolStripMenuItem _clear = null!;

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
        FormBorderStyle = FormBorderStyle.None;
        Size = new Size(1206, 490);
        MinimumSize = new Size(940, 455);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "GameLocker Configuration";
        Font = WorkspaceTheme.Body;
        ForeColor = Theme.Text;
        BackColor = Theme.Canvas;
        DoubleBuffered = true;

        var root = Grid(1, 3);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
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
        _folderList.ItemHeight = LogicalToDeviceUnits(52);
        _extensions.RowTemplate.Height = LogicalToDeviceUnits(36);
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
        var content = Grid(5, 1, new Padding(27, 0, 3, 0));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 345));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 162));

        _notifications = new ToolStripMenuItem("Show lock and unlock notifications") { CheckOnClick = true };
        _notifications.CheckedChanged += (_, _) => DraftChanged();
        _clear = new ToolStripMenuItem("Clear configuration");
        _clear.Click += ClearConfiguration;
        var menu = CreateMenu(_notifications, _clear);
        var options = new WorkspaceButton("", bare: true)
        {
            Symbol = WorkspaceGlyph.Locker, Size = new Size(45, 46),
            Anchor = AnchorStyles.Left, Margin = Padding.Empty,
            AccessibleName = "Configuration options",
        };
        options.Click += (_, _) => menu.Show(options, new Point(0, options.Height));
        _toolTip.SetToolTip(options, "Configuration options");
        content.Controls.Add(options, 0, 0);

        var identity = Grid(3, 1);
        identity.Margin = new Padding(0, 0, 0, 6);
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var name = Caption("GameLocker", WorkspaceTheme.Brand);
        name.AutoSize = true;
        identity.Controls.Add(name, 0, 0);
        var rule = BindBackground(new Panel { Width = 1, Height = 28, Anchor = AnchorStyles.None }, theme => theme.Text);
        identity.Controls.Add(rule, 1, 0);
        var subtitle = Caption("Configuration", WorkspaceTheme.Subtitle);
        identity.Controls.Add(subtitle, 2, 0);
        content.Controls.Add(identity, 1, 0);
        foreach (var dragSurface in new Control[] { header, content, identity, name, rule, subtitle })
            dragSurface.MouseDown += DragCaption;

        var themeSelector = Grid(4, 1);
        themeSelector.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        themeSelector.Height = 34;
        themeSelector.AccessibleName = "Appearance";
        themeSelector.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        themeSelector.Controls.Add(Caption("Theme:", WorkspaceTheme.Link), 0, 0);
        var index = 1;
        foreach (var mode in Enum.GetValues<WorkspaceThemeMode>())
        {
            themeSelector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3));
            var option = new WorkspaceThemeOption(mode)
            {
                Checked = mode == _themeMode, Margin = new Padding(0, 0, index < 3 ? 10 : 0, 0),
            };
            option.CheckedChanged += (_, _) =>
            {
                if (option.Checked)
                    SelectTheme(mode);
            };
            themeSelector.Controls.Add(option, index++, 0);
        }
        content.Controls.Add(themeSelector, 2, 0);

        var gap = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        gap.MouseDown += DragCaption;
        content.Controls.Add(gap, 3, 0);
        var chrome = Grid(3, 1);
        for (var column = 0; column < 3; column++)
            chrome.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3));
        var minimize = CaptionButton(WorkspaceGlyph.Minimize, "Minimize");
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        var maximize = CaptionButton(WorkspaceGlyph.Maximize, "Maximize");
        maximize.Click += (_, _) => ToggleMaximize();
        SizeChanged += (_, _) =>
        {
            var maximized = WindowState == FormWindowState.Maximized;
            maximize.Symbol = maximized ? WorkspaceGlyph.Restore : WorkspaceGlyph.Maximize;
            maximize.AccessibleName = maximized ? "Restore window" : "Maximize";
            maximize.Invalidate();
        };
        var close = CaptionButton(WorkspaceGlyph.Close, "Close");
        close.Click += (_, _) => Close();
        chrome.Controls.Add(minimize, 0, 0);
        chrome.Controls.Add(maximize, 1, 0);
        chrome.Controls.Add(close, 2, 0);
        content.Controls.Add(chrome, 4, 0);
        header.Controls.Add(content);
        return header;
    }

    private static WorkspaceButton CaptionButton(WorkspaceGlyph glyph, string label) => new("", bare: true)
    {
        Symbol = glyph, Dock = DockStyle.Fill, Margin = Padding.Empty,
        AccessibleName = label, TabStop = false,
    };

    private Control BuildWorkspace()
    {
        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty, TabIndex = 1 };
        var workspace = Grid(2, 1, new Padding(14, 13, 14, 12));
        workspace.Dock = DockStyle.Top;
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 59.85F));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40.15F));

        var schedule = BuildSchedule();
        schedule.Margin = new Padding(0, 0, 10, 0);
        workspace.Controls.Add(schedule, 0, 0);

        var sidebar = Grid(1, 2);
        sidebar.Margin = new Padding(9, 0, 0, 0);
        sidebar.TabIndex = 1;
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.Controls.Add(BuildFolders(), 0, 0);
        var vault = BuildVault();
        vault.Margin = new Padding(0, 13, 0, 0);
        sidebar.Controls.Add(vault, 0, 1);
        workspace.Controls.Add(sidebar, 1, 0);

        workspace.Height = 383;
        viewport.Controls.Add(workspace);
        return viewport;
    }

    private Control BuildSchedule()
    {
        var panel = Surface();
        BindBackground(panel, theme => theme.IsDark ? theme.Surface : theme.Inset);
        panel.Padding = new Padding(16, 6, 13, 13);
        var layout = Grid(1, 7);
        foreach (var height in new[] { 43, 43, 16, 82, 16, 100, 38 })
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.Controls.Add(SectionHeading("Gaming schedule", WorkspaceTheme.Schedule), 0, 0);

        var days = Grid(7, 1, new Padding(19, 7, 14, 0));
        _dayOptions = new CheckBox[Week.Length];
        for (var index = 0; index < Week.Length; index++)
        {
            days.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / Week.Length));
            var day = Week[index].ToString();
            var option = new WorkspaceDayOption
            {
                Text = day[..3], AccessibleName = day, Font = WorkspaceTheme.Day, Dock = DockStyle.Fill,
                TabIndex = index, Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, index == Week.Length - 1 ? 0 : 11, 0),
            };
            option.CheckedChanged += (_, _) => DraftChanged();
            _toolTip.SetToolTip(option, day);
            days.Controls.Add(option, index, 0);
            _dayOptions[index] = option;
        }
        layout.Controls.Add(days, 0, 1);

        var fields = Grid(3, 1, new Padding(20, 0, 16, 0));
        fields.TabIndex = 1;
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47F));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53F));
        var start = BuildField("Start time", out _startInput, out _startLater, out _startEarlier, 209);
        _startInput.MaxLength = 16;
        _startInput.TextChanged += (_, _) =>
        {
            var font = _startInput.TextLength > 6 ? WorkspaceTheme.PreciseClock : WorkspaceTheme.Clock;
            if (_startInput.Font != font)
                _startInput.Font = font;
        };
        _startLater.AccessibleName = "Move start time 15 minutes later";
        _startEarlier.AccessibleName = "Move start time 15 minutes earlier";
        _startLater.Click += (_, _) => AdjustStart(15);
        _startEarlier.Click += (_, _) => AdjustStart(-15);
        _toolTip.SetToolTip(_startInput, "Enter a local 24-hour start time, such as 18:30.");
        fields.Controls.Add(start, 0, 0);

        var dividingRule = BindBackground(new Panel
        {
            Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0),
        }, theme => theme.ControlLine);
        fields.Controls.Add(dividingRule, 1, 0);

        var duration = BuildField("Duration", out _durationInput, out _longer, out _shorter, 195, "hours");
        _durationInput.MaxLength = 2;
        _longer.AccessibleName = "Increase duration";
        _shorter.AccessibleName = "Decrease duration";
        _shorter.Click += (_, _) => AdjustDuration(-1);
        _longer.Click += (_, _) => AdjustDuration(1);
        duration.Margin = new Padding(56, 0, 0, 0);
        duration.TabIndex = 1;
        fields.Controls.Add(duration, 2, 0);
        layout.Controls.Add(fields, 0, 3);

        _timeline = new WindowTimeline { Dock = DockStyle.Fill, Margin = Padding.Empty, TabStop = false };
        layout.Controls.Add(_timeline, 0, 5);
        layout.Controls.Add(BuildTransition(), 0, 6);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildTransition()
    {
        var transitionBand = BindBackground(new WorkspaceSurface
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(12, 2, 10, 2),
            CornerRadius = 6,
        }, theme => theme.Raised);
        var content = Grid(3, 1);
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.Controls.Add(new WorkspaceIcon(WorkspaceGlyph.Locker, accent: true)
        {
            Size = new Size(31, 33), Anchor = AnchorStyles.Left, Margin = Padding.Empty, TabStop = false,
        }, 0, 0);
        _transitionTitle = Caption("", WorkspaceTheme.Transition);
        _transitionTitle.AutoSize = true;
        _transitionTitle.Margin = new Padding(0, 0, 6, 0);
        _transition = Caption("", WorkspaceTheme.Transition);
        _transition.AutoSize = true;
        _transition.Dock = DockStyle.None;
        _transition.Anchor = AnchorStyles.Left;
        content.Controls.Add(_transitionTitle, 1, 0);
        content.Controls.Add(_transition, 2, 0);
        transitionBand.Controls.Add(content);
        return transitionBand;
    }

    private TableLayoutPanel BuildField(string title, out TextBox input, out Button increase, out Button decrease,
        int width, string? suffix = null)
    {
        var field = Grid(1, 2);
        field.MaximumSize = new Size(width, 0);
        field.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        field.Controls.Add(Caption(title, WorkspaceTheme.Transition), 0, 0);
        var surface = BindBackground(new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(1),
        }, theme => theme.Inset);
        var row = Grid(suffix is null ? 2 : 3, 1);
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        if (suffix is not null)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 67));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 39));
        input = BindBackground(new TextBox
        {
            AccessibleName = title, Font = WorkspaceTheme.Clock, BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            TextAlign = HorizontalAlignment.Center,
            Margin = new Padding(4, 0, 4, 0),
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
        if (suffix is not null)
            row.Controls.Add(Caption(suffix, WorkspaceTheme.Transition), 1, 0);
        var stepper = Grid(1, 2);
        stepper.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        stepper.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        increase = new WorkspaceButton("+")
        {
            Font = WorkspaceTheme.Transition, Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0,
        };
        decrease = new WorkspaceButton("−")
        {
            Font = WorkspaceTheme.Transition, Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 1,
        };
        stepper.Controls.Add(increase, 0, 0);
        stepper.Controls.Add(decrease, 0, 1);
        row.Controls.Add(stepper, row.ColumnCount - 1, 0);
        surface.Controls.Add(row);
        field.Controls.Add(surface, 0, 1);
        return field;
    }

    private Control BuildFolders()
    {
        var panel = Surface();
        panel.Padding = new Padding(14, 4, 12, 10);
        var layout = Grid(1, 3);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.Controls.Add(SectionHeading("Game folders", WorkspaceTheme.Section), 0, 0);

        _removeFolder = new ToolStripMenuItem("Remove folder");
        _removeFolder.Click += RemoveFolder;
        _testLock = new ToolStripMenuItem("Test lock");
        _testLock.Click += TestLock;
        var menu = CreateMenu(_testLock, _removeFolder);
        var content = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        _folderList = BindBackground(new ListBox
        {
            AccessibleName = "Game folders", Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed,
            BorderStyle = BorderStyle.None, IntegralHeight = false, ContextMenuStrip = menu,
        }, theme => theme.Surface);
        BindForeground(_folderList, theme => theme.Text);
        _folderList.DrawItem += DrawFolder;
        _folderList.SelectedIndexChanged += (_, _) => FolderSelectionChanged();
        _folderList.MouseDown += (_, e) =>
        {
            var index = _folderList.IndexFromPoint(e.Location);
            if (index >= 0 && e.Button == MouseButtons.Right)
                _folderList.SelectedIndex = index;
        };
        _folderList.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && _folderList.IndexFromPoint(e.Location) >= 0 &&
                e.X >= _folderList.ClientSize.Width - LogicalToDeviceUnits(52))
                menu.Show(_folderList, e.Location);
        };
        _toolTip.SetToolTip(_folderList, "Select a folder for Vault settings. Open a folder’s menu to test its lock or remove it.");
        _folderEmpty = EmptyPanel(WorkspaceGlyph.Folder, "No game folders", "Add a folder to put it on the schedule.", out _, out _);
        content.Controls.Add(_folderList);
        content.Controls.Add(_folderEmpty);
        layout.Controls.Add(content, 0, 1);

        _addFolder = new WorkspaceButton("+  &Add folder")
        {
            Font = WorkspaceTheme.Day, Dock = DockStyle.Fill, Margin = new Padding(1, 8, 1, 0),
        };
        _addFolder.Click += AddFolder;
        layout.Controls.Add(_addFolder, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildVault()
    {
        var panel = Surface();
        panel.Padding = new Padding(14, 4, 12, 10);
        var layout = Grid(1, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _vault = new ToolStripMenuItem("Encrypt selected file types") { CheckOnClick = true };
        _vault.CheckedChanged += (_, _) =>
        {
            if (_populating)
                return;
            RefreshVault();
            DraftChanged();
        };
        _scan = new ToolStripMenuItem("Scan file types");
        _selectSaves = new ToolStripMenuItem("Select save files");
        _selectNone = new ToolStripMenuItem("Clear file selections");
        _scan.Click += ScanFolder;
        _selectSaves.Click += (_, _) => SelectExtensions(saveFilesOnly: true);
        _selectNone.Click += (_, _) => SelectExtensions(saveFilesOnly: false);
        var menu = CreateMenu(_vault, _scan, _selectSaves, _selectNone);
        panel.ContextMenuStrip = menu;
        var heading = SectionHeading("Vault", WorkspaceTheme.Section, menu);
        layout.Controls.Add(heading, 0, 0);

        var content = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        _extensions = BuildExtensionGrid();
        _extensions.ContextMenuStrip = menu;
        _vaultEmpty = EmptyPanel(WorkspaceGlyph.Files, "", "", out _vaultEmptyTitle, out _vaultEmptyBody);
        _vaultEmpty.Cursor = Cursors.Hand;
        _vaultEmpty.Click += (_, _) => menu.Show(heading, new Point(0, heading.Height));
        content.Controls.Add(_extensions);
        content.Controls.Add(_vaultEmpty);
        layout.Controls.Add(content, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private DataGridView BuildExtensionGrid()
    {
        var grid = new DataGridView
        {
            AccessibleName = "Vault file types", Dock = DockStyle.Fill, Margin = Padding.Empty,
            BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.None,
            ColumnHeadersVisible = false, RowHeadersVisible = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToOrderColumns = false,
            AllowUserToResizeRows = false, AllowUserToResizeColumns = false, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ScrollBars = ScrollBars.Vertical,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, StandardTab = true,
        };
        ApplyGridAppearance(grid);
        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Encrypt", HeaderText = "Encrypt", FillWeight = 13, MinimumWidth = 35,
            FlatStyle = FlatStyle.Flat, SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter },
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FileType", HeaderText = "Type", ReadOnly = true, FillWeight = 30,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Description", HeaderText = "Description", ReadOnly = true, FillWeight = 57,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });

        grid.RowPrePaint += (_, e) =>
        {
            var rowBounds = new Rectangle(e.RowBounds.X, e.RowBounds.Y,
                grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible), e.RowBounds.Height);
            var active = grid.Rows[e.RowIndex].Cells[0].Value is true;
            using var brush = new SolidBrush(Theme.Surface);
            e.Graphics.FillRectangle(brush, rowBounds);
            if (active)
                WorkspacePaint.ButtonFace(e.Graphics, rowBounds, LogicalToDeviceUnits(5), Theme.Selected, Theme.Selected);
            else
            {
                using var pen = new Pen(Theme.Line);
                e.Graphics.DrawLine(pen, rowBounds.Left, rowBounds.Bottom - 1, rowBounds.Right, rowBounds.Bottom - 1);
            }
        };
        grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is not { } graphics)
                return;

            var active = grid.Rows[e.RowIndex].Cells[0].Value is true;
            var ink = active ? Theme.OnSelected : Theme.Text;
            if (e.ColumnIndex == 0)
            {
                var side = LogicalToDeviceUnits(19);
                var circle = new Rectangle(e.CellBounds.Left + (e.CellBounds.Width - side) / 2,
                    e.CellBounds.Top + (e.CellBounds.Height - side) / 2, side, side);
                using var pen = new Pen(ink, LogicalToDeviceUnits(2));
                graphics.DrawEllipse(pen, circle);
                if (active)
                {
                    using var fill = new SolidBrush(ink);
                    graphics.FillEllipse(fill, Rectangle.Inflate(circle, -LogicalToDeviceUnits(4), -LogicalToDeviceUnits(4)));
                }
            }
            else
            {
                TextRenderer.DrawText(graphics, Convert.ToString(e.FormattedValue), e.ColumnIndex == 1 ? WorkspaceTheme.Day : WorkspaceTheme.Body,
                    e.CellBounds, ink, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
            if (grid.ContainsFocus && grid.CurrentCellAddress == new Point(e.ColumnIndex, e.RowIndex))
                WorkspacePaint.FocusMark(graphics, grid, e.CellBounds, ink);
            e.Handled = true;
        };
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
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, 1, 0, 0), TabIndex = 2,
        }, theme => theme.IsDark ? theme.Inset : theme.Canvas);
        var bar = Grid(3, 1, new Padding(24, 6, 17, 10));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 225));
        bar.Controls.Add(BuildLinks(), 0, 0);
        var feedback = Grid(1, 2, new Padding(18, 0, 13, 0));
        feedback.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        feedback.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _configState = Caption("", WorkspaceTheme.Strong);
        _status = Caption("", WorkspaceTheme.Small);
        feedback.Controls.Add(_configState, 0, 0);
        feedback.Controls.Add(_status, 0, 1);
        bar.Controls.Add(feedback, 1, 0);

        _save = new WorkspaceButton("&Save configuration", primary: true)
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Font = WorkspaceTheme.Transition,
        };
        _save.Click += SaveConfiguration;
        _toolTip.SetToolTip(_save, "Save configuration (Ctrl+S)");
        bar.Controls.Add(_save, 2, 0);
        panel.Controls.Add(bar);
        return panel;
    }

    private Control BuildLinks()
    {
        var links = Grid(5, 1);
        links.Anchor = AnchorStyles.Left;
        links.Size = new Size(515, 34);
        var column = 0;
        foreach (var link in ProjectLinks.All)
        {
            var website = link.Label == "Website";
            var label = website ? "Portfolio website" : link.Label;
            var width = website ? 222 : column == 0 ? 130 : 161;
            links.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
            var control = new WorkspaceButton(label, bare: true)
            {
                Symbol = website ? WorkspaceGlyph.Link : null,
                Font = WorkspaceTheme.Link, Dock = DockStyle.Fill,
                Margin = column == 0 ? Padding.Empty : new Padding(22, 0, 0, 0),
                Image = website ? null : link.Logo(Theme), AccessibleRole = AccessibleRole.Link,
                AccessibleName = $"Open {label} in your browser",
            };
            if (!website)
                _appearanceBindings.Add(() => control.Image = link.Logo(Theme));
            control.Click += (_, _) => OpenProjectLink(link);
            _toolTip.SetToolTip(control, link.Uri?.AbsoluteUri ?? label);
            links.Controls.Add(control, column++, 0);
            if (column < links.ColumnCount)
            {
                links.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1));
                links.Controls.Add(BindBackground(new Panel
                {
                    Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3),
                }, theme => theme.Text), column++, 0);
            }
        }
        return links;
    }

    private Control SectionHeading(string text, Font font, ContextMenuStrip? menu = null)
    {
        var heading = Grid(2, 1);
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 14));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.Controls.Add(BindBackground(new Panel
        {
            Size = new Size(7, 28), Anchor = AnchorStyles.Left, Margin = Padding.Empty,
        }, theme => theme.Pink), 0, 0);
        if (menu is null)
        {
            heading.Controls.Add(Caption(text, font), 1, 0);
        }
        else
        {
            var button = new WorkspaceButton(text, bare: true)
            {
                Size = new Size(94, 35), Anchor = AnchorStyles.Left, Margin = Padding.Empty,
                Font = font, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = $"{text} options",
            };
            button.Click += (_, _) => menu.Show(button, new Point(0, button.Height));
            _toolTip.SetToolTip(button, $"{text} options");
            heading.Controls.Add(button, 1, 0);
        }
        return heading;
    }

    private ContextMenuStrip CreateMenu(params ToolStripItem[] items)
    {
        var menu = BindBackground(new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true },
            theme => theme.Raised);
        BindForeground(menu, theme => theme.Text);
        menu.Items.AddRange(items);
        _menus.Add(menu);
        return menu;
    }

    private WorkspaceSurface Surface() =>
        BindBackground(new WorkspaceSurface
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(13, 11, 13, 11),
            CornerRadius = 8,
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
        var background = selected && _folderList.Focused ? Theme.Hover : Theme.Surface;
        var foreground = Theme.Text;
        using var fill = new SolidBrush(background);
        using var divider = new Pen(Theme.Line);
        e.Graphics.FillRectangle(fill, e.Bounds);
        e.Graphics.DrawLine(divider, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        var glyphSize = LogicalToDeviceUnits(36);
        var glyphColor = Theme.HighContrast ? foreground : Theme.Yellow;
        WorkspacePaint.Glyph(e.Graphics, WorkspaceGlyph.Folder, new Rectangle(
            e.Bounds.Left + LogicalToDeviceUnits(10), e.Bounds.Top + (e.Bounds.Height - glyphSize) / 2,
            glyphSize, glyphSize), glyphColor, Theme.HighContrast ? background : Theme.OnYellow);
        var textLeft = e.Bounds.Left + LogicalToDeviceUnits(67);
        var textRight = e.Bounds.Right - LogicalToDeviceUnits(60);
        var textTop = e.Bounds.Top + LogicalToDeviceUnits(5);
        var split = e.Bounds.Top + e.Bounds.Height / 2;
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, FolderName(path), WorkspaceTheme.Link,
            Rectangle.FromLTRB(textLeft, textTop, textRight, split), foreground, flags | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, path, WorkspaceTheme.Body,
            Rectangle.FromLTRB(textLeft, split, textRight, e.Bounds.Bottom - LogicalToDeviceUnits(5)),
            selected ? foreground : Theme.Muted, flags | TextFormatFlags.PathEllipsis);
        var menuBounds = new Rectangle(e.Bounds.Right - LogicalToDeviceUnits(50),
            e.Bounds.Top + (e.Bounds.Height - LogicalToDeviceUnits(26)) / 2, LogicalToDeviceUnits(45),
            LogicalToDeviceUnits(26));
        WorkspacePaint.ButtonFace(e.Graphics, menuBounds, LogicalToDeviceUnits(5), background, Theme.ControlLine);
        TextRenderer.DrawText(e.Graphics, "•••", WorkspaceTheme.Strong, menuBounds, foreground,
            flags | TextFormatFlags.HorizontalCenter);
        if ((e.State & DrawItemState.Focus) != 0)
            WorkspacePaint.FocusMark(e.Graphics, _folderList, e.Bounds, foreground);
    }
}
