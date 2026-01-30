using GameLocker.Common.Configuration;
using GameLocker.Common.Models;
using GameLocker.Common.Security;

namespace GameLocker.ConfigUI;

public partial class Form1 : Form
{
    private readonly ConfigManager _configManager;
    private GameLockerConfig _config;

    public Form1()
    {
        InitializeComponent();
        _configManager = new ConfigManager();
        _config = new GameLockerConfig();
    }

    private async void Form1_Load(object sender, EventArgs e)
    {
        // Check for admin privileges
        if (!AclHelper.IsRunningAsAdmin())
        {
            MessageBox.Show(
                "GameLocker ConfigUI should be run as Administrator for full functionality.\n\n" +
                "Some features may not work correctly without admin privileges.",
                "Administrator Recommended",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        // Load existing configuration
        await LoadConfigurationAsync();
    }

    private async Task LoadConfigurationAsync()
    {
        try
        {
            var existingConfig = await _configManager.LoadConfigAsync();
            if (existingConfig != null)
            {
                _config = existingConfig;
                PopulateFormFromConfig();
                ShowStatus("Configuration loaded successfully.", false);
            }
            else
            {
                ShowStatus("No existing configuration found. Configure your settings.", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error loading configuration: {ex.Message}", true);
        }
    }

    private void PopulateFormFromConfig()
    {
        // Populate days checkboxes
        chkSunday.Checked = _config.AllowedDays.Contains(DayOfWeek.Sunday);
        chkMonday.Checked = _config.AllowedDays.Contains(DayOfWeek.Monday);
        chkTuesday.Checked = _config.AllowedDays.Contains(DayOfWeek.Tuesday);
        chkWednesday.Checked = _config.AllowedDays.Contains(DayOfWeek.Wednesday);
        chkThursday.Checked = _config.AllowedDays.Contains(DayOfWeek.Thursday);
        chkFriday.Checked = _config.AllowedDays.Contains(DayOfWeek.Friday);
        chkSaturday.Checked = _config.AllowedDays.Contains(DayOfWeek.Saturday);

        // Populate time settings
        dtpStartTime.Value = DateTime.Today.Add(_config.StartTime.ToTimeSpan());
        nudDuration.Value = _config.DurationHours;

        // Populate folder list
        lstFolders.Items.Clear();
        foreach (var folder in _config.GameFolderPaths)
        {
            lstFolders.Items.Add(folder);
        }

        // Populate other settings
        chkNotifications.Checked = _config.NotificationsEnabled;
        nudPollingInterval.Value = _config.PollingIntervalMinutes;
    }

    private void PopulateConfigFromForm()
    {
        _config.AllowedDays.Clear();
        
        if (chkSunday.Checked) _config.AllowedDays.Add(DayOfWeek.Sunday);
        if (chkMonday.Checked) _config.AllowedDays.Add(DayOfWeek.Monday);
        if (chkTuesday.Checked) _config.AllowedDays.Add(DayOfWeek.Tuesday);
        if (chkWednesday.Checked) _config.AllowedDays.Add(DayOfWeek.Wednesday);
        if (chkThursday.Checked) _config.AllowedDays.Add(DayOfWeek.Thursday);
        if (chkFriday.Checked) _config.AllowedDays.Add(DayOfWeek.Friday);
        if (chkSaturday.Checked) _config.AllowedDays.Add(DayOfWeek.Saturday);

        _config.StartTime = TimeOnly.FromDateTime(dtpStartTime.Value);
        _config.DurationHours = (int)nudDuration.Value;

        _config.GameFolderPaths.Clear();
        foreach (string folder in lstFolders.Items)
        {
            _config.GameFolderPaths.Add(folder);
        }

        _config.NotificationsEnabled = chkNotifications.Checked;
        _config.PollingIntervalMinutes = (int)nudPollingInterval.Value;
    }

    private void btnAddFolder_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a game folder to lock",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            if (!lstFolders.Items.Contains(dialog.SelectedPath))
            {
                lstFolders.Items.Add(dialog.SelectedPath);
                ShowStatus($"Added folder: {dialog.SelectedPath}", false);
            }
            else
            {
                ShowStatus("Folder already in list.", true);
            }
        }
    }

    private void btnRemoveFolder_Click(object sender, EventArgs e)
    {
        if (lstFolders.SelectedIndex >= 0)
        {
            var removed = lstFolders.SelectedItem?.ToString();
            lstFolders.Items.RemoveAt(lstFolders.SelectedIndex);
            ShowStatus($"Removed folder: {removed}", false);
        }
        else
        {
            ShowStatus("Select a folder to remove.", true);
        }
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        if (!ValidateInputs())
            return;

        try
        {
            PopulateConfigFromForm();
            await _configManager.SaveConfigAsync(_config);
            ShowStatus("Configuration saved successfully! The service will pick up changes automatically.", false);

            MessageBox.Show(
                "Configuration saved successfully!\n\n" +
                "The GameLocker service will automatically pick up the new settings.\n" +
                "Make sure the GameLocker Service is running.",
                "Configuration Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error saving configuration: {ex.Message}", true);
            MessageBox.Show(
                $"Failed to save configuration:\n\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private bool ValidateInputs()
    {
        // Check if at least one day is selected
        if (!chkSunday.Checked && !chkMonday.Checked && !chkTuesday.Checked &&
            !chkWednesday.Checked && !chkThursday.Checked && !chkFriday.Checked &&
            !chkSaturday.Checked)
        {
            ShowStatus("Please select at least one allowed gaming day.", true);
            return false;
        }

        // Check if duration is valid
        if (nudDuration.Value < 1)
        {
            ShowStatus("Duration must be at least 1 hour.", true);
            return false;
        }

        // Check if at least one folder is added
        if (lstFolders.Items.Count == 0)
        {
            ShowStatus("Please add at least one game folder.", true);
            return false;
        }

        // Validate all folders exist
        foreach (string folder in lstFolders.Items)
        {
            if (!Directory.Exists(folder))
            {
                ShowStatus($"Folder does not exist: {folder}", true);
                return false;
            }
        }

        return true;
    }

    private void btnTestLock_Click(object sender, EventArgs e)
    {
        if (lstFolders.SelectedIndex < 0)
        {
            ShowStatus("Select a folder to test lock.", true);
            return;
        }

        var folder = lstFolders.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(folder))
            return;

        var result = MessageBox.Show(
            $"This will temporarily lock the folder:\n{folder}\n\n" +
            "The folder will be unlocked immediately after the test.\n" +
            "Continue?",
            "Test Lock",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return;

        try
        {
            AclHelper.DenyAccess(folder);
            ShowStatus($"Folder locked successfully: {folder}", false);

            MessageBox.Show(
                "Folder locked successfully!\n\n" +
                "Try to access the folder now - you should see an access denied message.\n" +
                "Click OK to unlock the folder.",
                "Lock Test",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            AclHelper.AllowAccess(folder);
            ShowStatus($"Folder unlocked: {folder}", false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Test failed: {ex.Message}", true);
            
            // Try to unlock in case of partial success
            try { AclHelper.AllowAccess(folder); } catch { }
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        lblStatus.Text = message;
        lblStatus.ForeColor = isError ? Color.Red : Color.Green;
    }

    private void btnClear_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear the configuration?\n\n" +
            "This will delete all saved settings.",
            "Clear Configuration",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            _configManager.DeleteConfig();
            _config = new GameLockerConfig();
            PopulateFormFromConfig();
            ShowStatus("Configuration cleared.", false);
        }
    }
}

