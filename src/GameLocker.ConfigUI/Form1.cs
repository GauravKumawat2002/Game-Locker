using GameLocker.Common.Configuration;
using GameLocker.Common.Models;
using GameLocker.Common.Security;
using GameLocker.Common.Services;

namespace GameLocker.ConfigUI;

public partial class Form1 : Form
{
    private readonly ConfigManager _configManager = new();
    private readonly FileExtensionScanner _extensionScanner = new();
    private readonly Dictionary<string, FolderEncryptionSettings> _folderSettings = new(StringComparer.OrdinalIgnoreCase);
    private GameLockerConfig _config = new();
    private bool _canEdit;
    private bool _saving;
    private bool _populatingExtensions;
    private int _scanVersion;

    public Form1()
    {
        InitializeComponent();
        UpdateEditControls();
        ResetExtensions();
    }

    private async void Form1_Load(object sender, EventArgs e)
    {
        var result = await _configManager.LoadConfigResultAsync();
        if (IsDisposed)
            return;

        var isAdministrator = AclHelper.IsRunningAsAdmin();
        _canEdit = isAdministrator && !result.IsDamaged;

        if (result.IsLoaded)
            _config = result.Config!;

        PopulateFormFromConfig();
        UpdateEditControls();

        if (!isAdministrator)
        {
            ShowStatus("Run as administrator to change settings.", true);
        }
        else if (result.IsDamaged)
        {
            ShowError("Configuration unavailable", $"Restore the saved configuration before making changes.\n\n{result.Error}");
        }
        else if (result.IsLoaded)
        {
            ShowStatus("Configuration loaded.");
        }
        else
        {
            ShowStatus("Choose your schedule and add a game folder.");
        }
    }

    private void PopulateFormFromConfig()
    {
        for (var index = 0; index < DayCheckboxes.Length; index++)
            DayCheckboxes[index].Checked = _config.AllowedDays.Contains((DayOfWeek)index);

        dtpStartTime.Value = DateTime.Today.Add(_config.StartTime.ToTimeSpan());
        nudDuration.Value = _config.DurationHours;
        chkNotifications.Checked = _config.NotificationsEnabled;
        chkVault.Checked = _config.LockMode == LockMode.AclAndVault;

        _folderSettings.Clear();
        foreach (var settings in _config.FolderEncryptionSettings)
            _folderSettings[settings.FolderPath] = settings;

        lstFolders.Items.Clear();
        foreach (var path in _config.GameFolderPaths)
            lstFolders.Items.Add(path);

        ResetExtensions();
    }

    private CheckBox[] DayCheckboxes =>
        [chkSunday, chkMonday, chkTuesday, chkWednesday, chkThursday, chkFriday, chkSaturday];

    private void UpdateEditControls()
    {
        var enabled = _canEdit && !_saving;
        grpDays.Enabled = enabled;
        grpTime.Enabled = enabled;
        grpFolders.Enabled = enabled;
        grpSettings.Enabled = enabled;
        grpExtensions.Enabled = enabled && chkVault.Checked;
        btnSave.Enabled = enabled;
        btnClear.Enabled = enabled;
    }

    private void btnAddFolder_Click(object sender, EventArgs e)
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

        if (lstFolders.Items.Cast<string>().Any(path =>
            PathGuard.IsAtOrUnder(path, guard.Path) || PathGuard.IsAtOrUnder(guard.Path, path)))
        {
            ShowStatus("Choose a folder outside existing monitored folders.", true);
            return;
        }

        lstFolders.Items.Add(guard.Path);
        lstFolders.SelectedItem = guard.Path;
        ShowStatus("Folder added. Save to apply your changes.");
    }

    private void btnRemoveFolder_Click(object sender, EventArgs e)
    {
        if (lstFolders.SelectedItem is not string folder)
        {
            ShowStatus("Select a folder to remove.", true);
            return;
        }

        _folderSettings.Remove(folder);
        lstFolders.Items.Remove(folder);
        ShowStatus("Folder removed from the list. Save to restore its access.");
    }

    private void lstFolders_SelectedIndexChanged(object sender, EventArgs e) => ResetExtensions();

    private void chkVault_CheckedChanged(object sender, EventArgs e)
    {
        UpdateEditControls();
        ResetExtensions();
    }

    private void ResetExtensions()
    {
        _scanVersion++;
        clbExtensions.Tag = null;
        clbExtensions.Items.Clear();
        lblExtensionInfo.Text = !chkVault.Checked
            ? "Vault encryption is off. GameLocker uses folder permissions."
            : lstFolders.SelectedItem is string
                ? "Click Scan file types to choose files for the vault."
                : "Select a folder to choose files for the vault.";
        UpdateScanControls();
    }

    private void UpdateScanControls()
    {
        btnRescan.Enabled = lstFolders.SelectedItem is string;
        var scanned = clbExtensions.Tag is List<ExtensionInfo>;
        clbExtensions.Enabled = scanned;
        btnSelectSafe.Enabled = scanned;
        btnSelectNone.Enabled = scanned;
    }

    private async void btnRescan_Click(object sender, EventArgs e)
    {
        if (!chkVault.Checked || lstFolders.SelectedItem is not string folder)
            return;

        ResetExtensions();
        var version = _scanVersion;
        lblExtensionInfo.Text = "Scanning file types…";
        btnRescan.Enabled = false;
        var result = await Task.Run(() => _extensionScanner.ScanFolderExtensions(folder));

        if (IsDisposed || version != _scanVersion)
            return;

        btnRescan.Enabled = true;
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            lblExtensionInfo.Text = "Scan unavailable. Check the folder is unlocked and try again.";
            ShowError("Scan failed", result.ErrorMessage);
            return;
        }

        var extensions = result.GetExtensionsByRisk()
            .Where(extension => !FileExtensionScanner.IsNeverEncryptable("file" + extension.Extension) &&
                extension.Extension != ".enc")
            .ToList();
        var settings = GetFolderSettings(folder);
        _populatingExtensions = true;
        clbExtensions.BeginUpdate();
        try
        {
            clbExtensions.Tag = extensions;
            foreach (var extension in extensions)
            {
                var selected = settings.UseCustomSelection &&
                    settings.SelectedExtensions.Contains(extension.Extension, StringComparer.OrdinalIgnoreCase);
                clbExtensions.Items.Add(
                    $"{extension.Extension} ({extension.FileCount} files, {extension.FormattedSize}) · {extension.RiskLevel}",
                    selected);
            }
        }
        finally
        {
            clbExtensions.EndUpdate();
            _populatingExtensions = false;
        }

        lblExtensionInfo.Text = extensions.Count == 0
            ? "This folder has no file types eligible for the vault."
            : "Check the file types to encrypt during a lock.";
        UpdateScanControls();
    }

    private FolderEncryptionSettings GetFolderSettings(string path)
    {
        if (!_folderSettings.TryGetValue(path, out var settings))
        {
            settings = new FolderEncryptionSettings { FolderPath = path };
            _folderSettings.Add(path, settings);
        }

        return settings;
    }

    private void clbExtensions_ItemCheck(object sender, ItemCheckEventArgs e)
    {
        if (_populatingExtensions || lstFolders.SelectedItem is not string path ||
            clbExtensions.Tag is not List<ExtensionInfo> extensions)
        {
            return;
        }

        var settings = GetFolderSettings(path);
        var extension = extensions[e.Index].Extension;
        settings.UseCustomSelection = true;
        settings.SelectedExtensions.RemoveAll(selected => selected.Equals(extension, StringComparison.OrdinalIgnoreCase));
        if (e.NewValue == CheckState.Checked)
            settings.SelectedExtensions.Add(extension);
    }

    private void btnSelectSafe_Click(object sender, EventArgs e)
    {
        if (lstFolders.SelectedItem is not string path || clbExtensions.Tag is not List<ExtensionInfo> extensions)
            return;

        SelectExtensions(path, extensions, extension => extension.RiskLevel == RiskLevel.Safe);
        ShowStatus("Selected eligible save-file types.");
    }

    private void btnSelectNone_Click(object sender, EventArgs e)
    {
        if (lstFolders.SelectedItem is not string path || clbExtensions.Tag is not List<ExtensionInfo> extensions)
            return;

        SelectExtensions(path, extensions, _ => false);
        ShowStatus("File type selections cleared.");
    }

    private void SelectExtensions(string path, List<ExtensionInfo> extensions, Func<ExtensionInfo, bool> select)
    {
        var settings = GetFolderSettings(path);
        settings.UseCustomSelection = true;
        settings.SelectedExtensions.Clear();

        for (var index = 0; index < extensions.Count; index++)
        {
            var extension = extensions[index];
            var selected = select(extension);
            clbExtensions.SetItemChecked(index, selected);
            if (selected && !settings.SelectedExtensions.Contains(extension.Extension))
                settings.SelectedExtensions.Add(extension.Extension);
        }
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        var days = DayCheckboxes.Select((checkbox, index) => (checkbox, index))
            .Where(day => day.checkbox.Checked)
            .Select(day => (DayOfWeek)day.index)
            .ToList();
        var folders = lstFolders.Items.Cast<string>().ToList();

        if (folders.Count > 0 && days.Count == 0)
        {
            ShowStatus("Choose at least one day for the gaming schedule.", true);
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

        var config = new GameLockerConfig
        {
            AllowedDays = days,
            GameFolderPaths = folders,
            StartTime = TimeOnly.FromDateTime(dtpStartTime.Value),
            DurationHours = (int)nudDuration.Value,
            NotificationsEnabled = chkNotifications.Checked,
            PollingIntervalMinutes = _config.PollingIntervalMinutes,
            LockMode = chkVault.Checked ? LockMode.AclAndVault : LockMode.AclOnly,
            EncryptionSettings = _config.EncryptionSettings,
            FolderEncryptionSettings = _folderSettings.Values.ToList(),
        };
        await SaveAsync(config);
    }

    private async void btnClear_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show(this,
            "Clear the saved schedule and restore access to folders GameLocker has locked?",
            "Clear configuration", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        if (await SaveAsync(new GameLockerConfig()))
            PopulateFormFromConfig();
    }

    private async Task<bool> SaveAsync(GameLockerConfig config)
    {
        _saving = true;
        UpdateEditControls();
        try
        {
            var locker = new FolderLocker(_configManager.ConfigDirectory);
            config.FoldersPendingRestore = _config.GameFolderPaths
                .Concat(_config.FoldersPendingRestore)
                .Except(config.GameFolderPaths, StringComparer.OrdinalIgnoreCase)
                .Where(path => locker.GetFolderState(path) != FolderState.Unlocked)
                .ToList();

            await _configManager.SaveConfigAsync(config);
            _config = config;

            if (ServiceManager.IsServiceRunning())
            {
                await ServiceManager.SendConfigReloadCommandAsync();
                ShowStatus(config.FoldersPendingRestore.Count == 0
                    ? "Configuration saved."
                    : "Configuration saved. The service will restore removed folders.");
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
                    ShowStatus("Configuration saved. GameLocker Service is stopped.", true);
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowError("Could not save the configuration", ex.Message);
            return false;
        }
        finally
        {
            _saving = false;
            UpdateEditControls();
        }
    }

    private async void btnTestLock_Click(object sender, EventArgs e)
    {
        if (lstFolders.SelectedItem is not string folder)
        {
            ShowStatus("Select a folder to test.", true);
            return;
        }

        if (MessageBox.Show(this, $"Temporarily test the lock on this folder?\n\n{folder}",
            "Test lock", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _saving = true;
        UpdateEditControls();
        ShowStatus("Checking folder permissions…");
        try
        {
            var locker = new FolderLocker(_configManager.ConfigDirectory);
            Action explainActiveLock = () => MessageBox.Show(this,
                "The lock is active. Try opening the folder in File Explorer, then click OK to restore access.",
                "Test lock", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await Task.Run(() => locker.TestLockFolder(folder, () => Invoke(explainActiveLock)));

            if (!IsDisposed)
                ShowStatus("Test finished. Original permissions restored.");
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
                ShowError("Test lock could not finish", $"{ex.Message}\n\nGameLocker Recovery can restore pending test locks.");
        }
        finally
        {
            _saving = false;
            if (!IsDisposed)
                UpdateEditControls();
        }
    }

    private void ShowStatus(string message, bool isError = false)
    {
        lblStatus.Text = message;
        lblStatus.ForeColor = isError ? Color.DarkRed : SystemColors.ControlText;
    }

    private void ShowError(string title, string message)
    {
        ShowStatus(title, true);
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

