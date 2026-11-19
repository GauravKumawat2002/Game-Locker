using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using GameLocker.Common.Configuration;
using GameLocker.Common.Models;
using GameLocker.Common.Security;
using GameLocker.Common.Services;

namespace GameLocker.ConfigUI;

public partial class Form1 : Form
{
    private readonly ConfigManager _configManager;
    private readonly FileExtensionScanner _extensionScanner = new();
    private readonly Dictionary<string, FolderEncryptionSettings> _folderSettings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ExtensionScanResult> _scanResults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<ExtensionScanResult>> _activeScans = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _scheduleTimer = new() { Interval = 1000 };
    private GameLockerConfig _config = new();
    private bool _loaded;
    private bool _hasSavedConfig;
    private bool _canEdit;
    private bool _busy;
    private bool _populating;
    private bool _populatingExtensions;
    private bool _dirty;

    public Form1() : this(new ConfigManager())
    {
    }

    internal Form1(ConfigManager configManager)
    {
        _configManager = configManager;
        InitializeWorkspace();
        PopulateFromConfiguration();
        UpdateEditControls();
        Load += LoadConfiguration;
        _scheduleTimer.Tick += (_, _) => UpdatePreview();
        _scheduleTimer.Start();
        _durationInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode is not (Keys.Up or Keys.Down))
                return;
            AdjustDuration(e.KeyCode == Keys.Up ? 1 : -1);
            e.SuppressKeyPress = true;
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _scheduleTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private async void LoadConfiguration(object? sender, EventArgs e)
    {
        var result = await _configManager.LoadConfigResultAsync();
        if (IsDisposed)
            return;

        var isAdministrator = AclHelper.IsRunningAsAdmin();
        _canEdit = isAdministrator && !result.IsDamaged;
        _loaded = true;
        _hasSavedConfig = result.IsLoaded;
        if (result.IsLoaded)
            _config = result.Config!;

        PopulateFromConfiguration();
        UpdateEditControls();

        if (!isAdministrator)
            ShowStatus("Run as administrator to change settings.", StatusTone.Warning);
        else if (result.IsDamaged)
            ShowError("Configuration unavailable", $"Restore the saved configuration before making changes.\n\n{result.Error}");
        else if (result.IsLoaded)
            ShowStatus("Configuration loaded.");
        else
            ShowStatus("Choose your schedule and add a game folder.");
    }

    private void PopulateFromConfiguration()
    {
        _populating = true;
        try
        {
            for (var index = 0; index < Week.Length; index++)
                _dayOptions[index].Checked = _config.AllowedDays.Contains(Week[index]);

            _startInput.Text = FormatInputTime(_config.StartTime);
            _durationInput.Text = _config.DurationHours.ToString(CultureInfo.InvariantCulture);
            _notifications.Checked = _config.NotificationsEnabled;
            _vault.Checked = _config.LockMode == LockMode.AclAndVault;

            _folderSettings.Clear();
            foreach (var settings in _config.FolderEncryptionSettings)
                _folderSettings[settings.FolderPath] = CopyFolderSettings(settings);

            _scanResults.Clear();
            _activeScans.Clear();
            _folderList.Items.Clear();
            _folderList.Items.AddRange(_config.GameFolderPaths.ToArray());
            if (_folderList.Items.Count > 0)
                _folderList.SelectedIndex = 0;
            UpdateFolderCount();
        }
        finally
        {
            _populating = false;
        }

        RefreshVault();
        DraftChanged();
    }

    private static string FormatInputTime(TimeOnly time)
    {
        var format = time.Ticks % TimeSpan.TicksPerSecond != 0 ? "HH':'mm':'ss'.'fffffff"
            : time.Second != 0 ? "HH':'mm':'ss" : "HH':'mm";
        return time.ToString(format, CultureInfo.InvariantCulture);
    }

    private bool TryReadStart(out TimeOnly time) => TimeOnly.TryParseExact(_startInput.Text.Trim(),
        ["H':'mm", "HH':'mm", "H':'mm':'ss", "HH':'mm':'ss", "HH':'mm':'ss'.'FFFFFFF"],
        CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private bool TryReadDuration(out int hours) => int.TryParse(_durationInput.Text.Trim(),
        NumberStyles.None, CultureInfo.InvariantCulture, out hours) && hours is >= 1 and <= 24;

    private List<DayOfWeek> ReadDays() => Week.Where((_, index) => _dayOptions[index].Checked).ToList();

    private void AdjustDuration(int change)
    {
        if (_durationInput.ReadOnly)
            return;

        var hours = TryReadDuration(out var value) ? value : _config.DurationHours;
        _durationInput.Text = Math.Clamp(hours + change, 1, 24).ToString(CultureInfo.InvariantCulture);
    }

    private void DraftChanged()
    {
        if (_populating)
            return;

        var folders = _folderList.Items.Cast<string>();
        var savedFolders = _config.FolderEncryptionSettings
            .GroupBy(settings => settings.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        _dirty = !TryReadStart(out var start) || start != _config.StartTime ||
            !TryReadDuration(out var duration) || duration != _config.DurationHours ||
            !_config.AllowedDays.ToHashSet().SetEquals(ReadDays()) ||
            !_config.GameFolderPaths.SequenceEqual(folders, StringComparer.OrdinalIgnoreCase) ||
            _config.NotificationsEnabled != _notifications.Checked ||
            (_config.LockMode == LockMode.AclAndVault) != _vault.Checked ||
            _folderSettings.Count != savedFolders.Count ||
            _folderSettings.Any(pair => !savedFolders.TryGetValue(pair.Key, out var saved) ||
                saved.UseCustomSelection != pair.Value.UseCustomSelection ||
                !saved.SelectedExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(pair.Value.SelectedExtensions));

        SetCaption(_configState, !_loaded ? "LOADING CONFIGURATION"
            : !_canEdit ? "READ ONLY"
            : _dirty ? "UNSAVED CHANGES"
            : _hasSavedConfig ? "SAVED CONFIGURATION" : "NEW CONFIGURATION");
        _configState.ForeColor = _dirty && _canEdit ? WorkspaceTheme.Warn : WorkspaceTheme.Muted;
        UpdateEditControls();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_populating)
            return;

        SetCaption(_previewMode, _dirty ? "Draft preview" : _hasSavedConfig ? "Saved schedule" : "Window preview");
        if (!TryReadStart(out var start) || !TryReadDuration(out var duration))
        {
            _timeline.SetWindow(default, 0, false);
            SetCaption(_windowState, "");
            SetCaption(_windowSummary, "Incomplete time window");
            SetCaption(_transition, "Use a 24-hour start time and a duration of 1–24 hours.");
            return;
        }

        var days = ReadDays();
        var crossesMidnight = start.ToTimeSpan().Add(TimeSpan.FromHours(duration)).TotalHours >= 24;
        var schedule = new GameLockerConfig { AllowedDays = days, StartTime = start, DurationHours = duration };
        _timeline.SetWindow(start, duration, days.Count > 0);
        _timeline.AccessibleDescription = crossesMidnight
            ? "Highlights play time from a selected day’s start into the following day."
            : "Highlights play time on the selected days.";
        var state = schedule.GetScheduleState(DateTime.Now);
        SetCaption(_windowState, days.Count == 0 ? "" : state.IsAllowed ? "PLAY OPEN" : "PLAY CLOSED");
        _windowState.ForeColor = state.IsAllowed ? WorkspaceTheme.Accent : WorkspaceTheme.Muted;
        var range = $"{start:HH':'mm}–{start.AddHours(duration):HH':'mm}";
        SetCaption(_windowSummary, crossesMidnight ? $"{range}  ·  ends next day" : $"{range}  ·  local time");
        SetCaption(_transition, days.Count == 0 ? "Select the days when this window starts."
            : state.NextTransition is { } next
                ? $"Next {(state.IsAllowed ? "lock" : "unlock")}  {next:ddd, d MMM} at {FormatInputTime(TimeOnly.FromDateTime(next))}"
                : "Play is allowed all week.");
    }

    private static void SetCaption(Label label, string value)
    {
        if (label.Text != value)
            label.Text = value;
    }

    private void UpdateEditControls()
    {
        var enabled = _canEdit && !_busy;
        foreach (var day in _dayOptions)
            day.Enabled = enabled;

        _startInput.ReadOnly = !enabled;
        _durationInput.ReadOnly = !enabled;
        var validDuration = TryReadDuration(out var hours);
        _shorter.Enabled = enabled && (!validDuration || hours > 1);
        _longer.Enabled = enabled && (!validDuration || hours < 24);
        _notifications.Enabled = enabled;
        _vault.Enabled = enabled;
        _folderList.Enabled = !_busy;
        _addFolder.Enabled = enabled;
        _removeFolder.Enabled = enabled && _folderList.SelectedIndex >= 0;
        _testLock.Enabled = _removeFolder.Enabled;
        _save.Enabled = enabled;
        _clear.Enabled = enabled && (_hasSavedConfig || _dirty);

        var folder = _folderList.SelectedItem as string;
        var canScan = enabled && _vault.Checked && folder is not null;
        _scan.Enabled = canScan && !_activeScans.ContainsKey(folder!);
        var canSelect = canScan && _extensions.Rows.Count > 0;
        _extensions.Enabled = canSelect;
        _selectSaves.Enabled = canSelect;
        _selectNone.Enabled = canSelect;
    }

    private void AddFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a game folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        if (!PathGuard.TryValidate(dialog.SelectedPath, out var guard))
        {
            ShowError("Folder unavailable", guard.Message);
            return;
        }

        if (_folderList.Items.Cast<string>().Any(path =>
            PathGuard.IsAtOrUnder(path, guard.Path) || PathGuard.IsAtOrUnder(guard.Path, path)))
        {
            ShowStatus("Choose a folder outside existing monitored folders.", StatusTone.Warning);
            return;
        }

        _folderList.Items.Add(guard.Path);
        _folderList.SelectedItem = guard.Path;
        UpdateFolderCount();
        DraftChanged();
        ShowStatus("Folder added. Save to apply your changes.");
    }

    private void RemoveFolder(object? sender, EventArgs e)
    {
        if (_folderList.SelectedItem is not string folder)
            return;

        var index = _folderList.SelectedIndex;
        _folderSettings.Remove(folder);
        _scanResults.Remove(folder);
        _activeScans.Remove(folder);
        _folderList.Items.RemoveAt(index);
        if (_folderList.Items.Count > 0)
            _folderList.SelectedIndex = Math.Min(index, _folderList.Items.Count - 1);
        else
            _addFolder.Focus();
        UpdateFolderCount();
        RefreshVault();
        DraftChanged();
        ShowStatus("Folder removed from the list. Save to restore its access.");
    }

    private void UpdateFolderCount()
    {
        var count = _folderList.Items.Count;
        _folderCount.Text = count == 1 ? "1 folder" : $"{count} folders";
        _folderList.Visible = count > 0;
        _folderEmpty.Visible = count == 0;
    }

    private static string FolderName(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return string.IsNullOrEmpty(name) ? path : name;
    }

    private void FolderSelectionChanged()
    {
        if (!_populating)
            RefreshVault();
    }

    private void RefreshVault()
    {
        var folder = _folderList.SelectedItem as string;
        _vaultFolder.Text = folder is null ? "No folder selected" : FolderName(folder);
        _toolTip.SetToolTip(_vaultFolder, folder);
        _toolTip.SetToolTip(_vaultEmptyBody, null);
        _scan.Text = "Scan file types";
        _extensions.Rows.Clear();
        if (!_vault.Checked)
        {
            ShowVaultState("Folder permissions only", "Vault encryption is off",
                "Enable encryption to choose file types for the vault.");
        }
        else if (folder is null)
        {
            ShowVaultState("Per-folder settings", "Choose a game folder", "Select a folder from the list to get started.");
        }
        else if (_activeScans.ContainsKey(folder))
        {
            _scan.Text = "Scanning…";
            ShowVaultState("Scanning this folder", "Looking for file types", "You can continue editing while the scan runs.");
        }
        else if (!_scanResults.TryGetValue(folder, out var result))
        {
            var details = _folderSettings.TryGetValue(folder, out var settings)
                ? settings.UseCustomSelection ? CountSelected(settings.SelectedExtensions.Count) : "Saved preset"
                : "Ready to scan";
            ShowVaultState(details, "Scan this folder", "Review eligible file types before selecting them.");
        }
        else if (result.ErrorMessage is { Length: > 0 } error)
        {
            ShowVaultState("Scan failed", "Could not scan this folder", error, StatusTone.Warning);
            _toolTip.SetToolTip(_vaultEmptyBody, error);
        }
        else
        {
            PopulateScan(folder, result);
        }
        UpdateEditControls();
    }

    private void ShowVaultState(string details, string title, string body, StatusTone tone = StatusTone.Normal)
    {
        _vaultDetails.Text = details;
        _vaultEmptyTitle.Text = title;
        _vaultEmptyTitle.ForeColor = tone == StatusTone.Warning ? WorkspaceTheme.Warn : WorkspaceTheme.Text;
        _vaultEmptyBody.Text = body;
        _extensions.Visible = false;
        _vaultEmpty.Visible = true;
    }

    private async void ScanFolder(object? sender, EventArgs e)
    {
        if (!_vault.Checked || _folderList.SelectedItem is not string folder || _activeScans.ContainsKey(folder))
            return;

        var scan = Task.Run(() => _extensionScanner.ScanFolderExtensions(folder));
        _activeScans.Add(folder, scan);
        RefreshVault();
        ExtensionScanResult result;
        try
        {
            result = await scan;
        }
        catch (Exception ex)
        {
            result = new ExtensionScanResult { FolderPath = folder, ErrorMessage = ex.Message };
        }

        if (IsDisposed || !_activeScans.TryGetValue(folder, out var current) || current != scan)
            return;

        _activeScans.Remove(folder);
        _scanResults[folder] = result;
        if (string.Equals(_folderList.SelectedItem as string, folder, StringComparison.OrdinalIgnoreCase) && _vault.Checked)
        {
            RefreshVault();
            ShowStatus(string.IsNullOrEmpty(result.ErrorMessage) ? "Folder scan finished." : "Folder scan failed. Review the vault panel.",
                string.IsNullOrEmpty(result.ErrorMessage) ? StatusTone.Normal : StatusTone.Warning);
        }
    }

    private void PopulateScan(string folder, ExtensionScanResult result)
    {
        var eligible = result.GetExtensionsByRisk()
            .Where(extension => extension.Extension.StartsWith('.') &&
                !FileExtensionScanner.IsNeverEncryptable("file" + extension.Extension) &&
                !extension.Extension.Equals(".enc", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (eligible.Count == 0)
        {
            ShowVaultState("0 eligible file types", "No eligible file types",
                "Install and vault files are excluded.");
            return;
        }

        _folderSettings.TryGetValue(folder, out var settings);
        _populatingExtensions = true;
        _extensions.SuspendLayout();
        try
        {
            foreach (var extension in eligible)
            {
                var selected = settings?.ShouldEncryptFile("file" + extension.Extension) == true;
                var risk = extension.RiskLevel == RiskLevel.Safe ? "Save file" : extension.RiskLevel.ToString();
                var index = _extensions.Rows.Add(selected, extension.Extension, extension.FileCount, extension.FormattedSize, risk);
                var row = _extensions.Rows[index];
                row.Tag = extension;
                row.Cells[1].ToolTipText = extension.Extension;
                row.Cells[4].Style.ForeColor = extension.RiskLevel == RiskLevel.Safe ? WorkspaceTheme.Accent
                    : extension.RiskLevel is RiskLevel.High or RiskLevel.Dangerous ? WorkspaceTheme.Warn : WorkspaceTheme.Muted;
            }
            _extensions.ClearSelection();
        }
        finally
        {
            _extensions.ResumeLayout();
            _populatingExtensions = false;
        }
        UpdateVaultCount();
        _vaultEmpty.Visible = false;
        _extensions.Visible = true;
    }

    private static string CountSelected(int count) => count == 1 ? "1 type selected" : $"{count} types selected";

    private void UpdateVaultCount()
    {
        var rows = _extensions.Rows.Cast<DataGridViewRow>();
        _vaultDetails.Text = $"{CountSelected(rows.Count(row => row.Cells[0].Value is true))}  /  {_extensions.Rows.Count} eligible";
    }

    private FolderEncryptionSettings GetDraftFolderSettings(string folder)
    {
        if (!_folderSettings.TryGetValue(folder, out var settings))
        {
            settings = new FolderEncryptionSettings { FolderPath = folder };
            _folderSettings.Add(folder, settings);
        }
        return settings;
    }

    private void ExtensionSelectionChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_populatingExtensions || e.ColumnIndex != 0 || e.RowIndex < 0 || e.RowIndex >= _extensions.Rows.Count ||
            _folderList.SelectedItem is not string folder ||
            _extensions.Rows[e.RowIndex].Tag is not ExtensionInfo extension)
            return;

        var settings = GetDraftFolderSettings(folder);
        if (!settings.UseCustomSelection)
        {
            settings.SelectedExtensions = _extensions.Rows.Cast<DataGridViewRow>()
                .Where(row => row.Cells[0].Value is true)
                .Select(row => ((ExtensionInfo)row.Tag!).Extension)
                .ToList();
            settings.UseCustomSelection = true;
        }
        else
        {
            settings.SelectedExtensions.RemoveAll(selected => selected.Equals(extension.Extension, StringComparison.OrdinalIgnoreCase));
            if (_extensions.Rows[e.RowIndex].Cells[0].Value is true)
                settings.SelectedExtensions.Add(extension.Extension);
        }
        UpdateVaultCount();
        DraftChanged();
    }

    private void SelectExtensions(bool saveFilesOnly)
    {
        if (_folderList.SelectedItem is not string folder || _extensions.Rows.Count == 0)
            return;

        var settings = GetDraftFolderSettings(folder);
        settings.UseCustomSelection = true;
        settings.SelectedExtensions.Clear();
        _populatingExtensions = true;
        try
        {
            foreach (DataGridViewRow row in _extensions.Rows)
            {
                var extension = (ExtensionInfo)row.Tag!;
                var selected = saveFilesOnly && extension.RiskLevel == RiskLevel.Safe;
                row.Cells[0].Value = selected;
                if (selected)
                    settings.SelectedExtensions.Add(extension.Extension);
            }
        }
        finally
        {
            _populatingExtensions = false;
        }
        UpdateVaultCount();
        DraftChanged();
        ShowStatus(saveFilesOnly ? "Selected known save-file types." : "File type selections cleared.");
    }

    private static FolderEncryptionSettings CopyFolderSettings(FolderEncryptionSettings settings) => new()
    {
        FolderPath = settings.FolderPath,
        UseCustomSelection = settings.UseCustomSelection,
        SelectedExtensions = [.. settings.SelectedExtensions],
        LastScanned = settings.LastScanned,
        TotalFiles = settings.TotalFiles,
        UniqueExtensions = settings.UniqueExtensions,
        FallbackPreset = settings.FallbackPreset,
        UserNotes = settings.UserNotes,
    };

    private async void SaveConfiguration(object? sender, EventArgs e)
    {
        if (!TryReadStart(out var start))
        {
            ShowStatus("Enter a start time from 00:00 to 23:59, such as 18:30.", StatusTone.Warning);
            _startInput.Focus();
            return;
        }
        if (!TryReadDuration(out var duration))
        {
            ShowStatus("Enter a duration between 1 and 24 hours.", StatusTone.Warning);
            _durationInput.Focus();
            return;
        }

        var days = ReadDays();
        var folders = _folderList.Items.Cast<string>().ToList();
        if (folders.Count > 0 && days.Count == 0)
        {
            ShowStatus("Choose at least one day for the gaming schedule.", StatusTone.Warning);
            _dayOptions[0].Focus();
            return;
        }
        foreach (var folder in folders)
        {
            if (!PathGuard.TryValidate(folder, out var guard))
            {
                ShowError("Folder unavailable", guard.Message);
                return;
            }
        }

        await SaveAsync(new GameLockerConfig
        {
            AllowedDays = days, StartTime = start, DurationHours = duration, GameFolderPaths = folders,
            NotificationsEnabled = _notifications.Checked, PollingIntervalMinutes = _config.PollingIntervalMinutes,
            LockMode = _vault.Checked ? LockMode.AclAndVault : LockMode.AclOnly,
            EncryptionSettings = _config.EncryptionSettings,
            FolderEncryptionSettings = _folderSettings.Values.Select(CopyFolderSettings).ToList(),
        });
    }

    private async void ClearConfiguration(object? sender, EventArgs e)
    {
        if (!WorkspaceDialog.Confirm(this, "Clear configuration",
            "Clear the saved schedule, remove all monitored folders, and restore access to folders GameLocker has locked?",
            "Clear configuration", danger: true))
            return;

        if (await SaveAsync(new GameLockerConfig(), clearing: true))
            PopulateFromConfiguration();
    }

    private async Task<bool> SaveAsync(GameLockerConfig config, bool clearing = false)
    {
        _busy = true;
        UpdateEditControls();
        ShowStatus(clearing ? "Clearing configuration…" : "Saving configuration…");
        var success = clearing ? "Configuration cleared." : "Configuration saved.";
        try
        {
            var locker = new FolderLocker(_configManager.ConfigDirectory);
            try
            {
                config.FoldersPendingRestore = await Task.Run(() => _config.GameFolderPaths
                    .Concat(_config.FoldersPendingRestore)
                    .Except(config.GameFolderPaths, StringComparer.OrdinalIgnoreCase)
                    .Where(path => locker.GetFolderState(path) != FolderState.Unlocked)
                    .ToList());
                await _configManager.SaveConfigAsync(config);
            }
            catch (Exception ex)
            {
                ShowError("Could not save the configuration", ex.Message);
                return false;
            }
            _config = config;
            _hasSavedConfig = true;
            DraftChanged();

            if (ServiceManager.IsServiceRunning())
            {
                var signaled = await ServiceManager.SendConfigReloadCommandAsync();
                if (!signaled)
                    ShowStatus($"{success} Could not notify GameLocker Service.", StatusTone.Warning);
                else
                    ShowStatus(config.FoldersPendingRestore.Count == 0
                        ? success : $"{success} The service will restore removed folders.", StatusTone.Success);
            }
            else
            {
                var errors = new List<string>();
                foreach (var path in config.FoldersPendingRestore)
                {
                    var restored = await Task.Run(() => locker.UnlockFolderAsync(path));
                    if (restored.State != FolderState.Unlocked)
                        errors.Add($"{path}\n{restored.LastError}");
                }
                if (errors.Count > 0)
                    ShowError("Some folders still need recovery", string.Join("\n\n", errors));
                else
                    ShowStatus($"{success} GameLocker Service is stopped.", StatusTone.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowError("Configuration saved with a follow-up error", ex.Message);
        }
        finally
        {
            _busy = false;
            DraftChanged();
        }
        return true;
    }

    private async void TestLock(object? sender, EventArgs e)
    {
        if (_folderList.SelectedItem is not string folder ||
            !WorkspaceDialog.Confirm(this, "Test folder lock",
              $"Temporarily lock this folder using Windows permissions? GameLocker will restore access after the test.\n\n{folder}",
              "Start test"))
            return;

        _busy = true;
        UpdateEditControls();
        ShowStatus("Testing folder permissions…");
        try
        {
            var locker = new FolderLocker(_configManager.ConfigDirectory);
            Action explainActiveLock = () => WorkspaceDialog.ShowMessage(this, "Test lock is active",
                "Try opening the folder in File Explorer. When you’re finished, restore access here.",
                "Restore access");
            await Task.Run(() => locker.TestLockFolder(folder, () => Invoke(explainActiveLock)));
            ShowStatus("Test finished. Original permissions restored.", StatusTone.Success);
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
                ShowError("Test lock could not finish", $"{ex.Message}\n\nGameLocker Recovery can restore pending test locks.");
        }
        finally
        {
            _busy = false;
            if (!IsDisposed)
                UpdateEditControls();
        }
    }

    private void OpenProjectLink(ProjectLink link)
    {
        if (link.Uri is null)
        {
            ShowStatus($"{link.Label} is unavailable. Its link must be a valid HTTPS address.", StatusTone.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(link.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ShowStatus($"Could not open {link.Label} in your default browser.", StatusTone.Warning);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S))
        {
            _save.PerformClick();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            if (_busy)
            {
                e.Cancel = true;
            }
            else if (_dirty && _canEdit && !WorkspaceDialog.Confirm(this, "Discard unsaved changes?",
                         "The changes in this window will be lost.", "Discard changes", "Keep editing", danger: true))
            {
                e.Cancel = true;
            }
        }
        base.OnFormClosing(e);
    }

    private enum StatusTone { Normal, Success, Warning, Error }

    private void ShowStatus(string message, StatusTone tone = StatusTone.Normal)
    {
        _status.Text = message;
        _status.ForeColor = tone switch
        {
            StatusTone.Success => WorkspaceTheme.Accent,
            StatusTone.Warning => WorkspaceTheme.Warn,
            StatusTone.Error => WorkspaceTheme.Error,
            _ => WorkspaceTheme.Muted,
        };
        _toolTip.SetToolTip(_status, message);
    }

    private void ShowError(string title, string message)
    {
        ShowStatus(title, StatusTone.Error);
        WorkspaceDialog.ShowMessage(this, title, message, "Close", error: true);
    }
}
