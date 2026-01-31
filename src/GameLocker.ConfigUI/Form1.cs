using GameLocker.Common.Configuration;
using GameLocker.Common.Models;
using GameLocker.Common.Security;
using GameLocker.Common.Services;

namespace GameLocker.ConfigUI;

public partial class Form1 : Form
{
    private readonly ConfigManager _configManager;
    private readonly FileExtensionScanner _extensionScanner;
    private GameLockerConfig _config;
    
    // Extension selection for each folder
    private Dictionary<string, FolderEncryptionSettings> _folderSettings = new();

    public Form1()
    {
        InitializeComponent();
        _configManager = new ConfigManager();
        _extensionScanner = new FileExtensionScanner();
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
                
                // Load folder settings into local dictionary
                _folderSettings.Clear();
                foreach (var settings in _config.FolderEncryptionSettings)
                {
                    _folderSettings[settings.FolderPath] = settings;
                }
                
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
        
        // Clear extension list until a folder is selected
        clbExtensions.Items.Clear();
        lblExtensionInfo.Text = "Select a folder to view file types";
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
        
        // Save folder encryption settings
        _config.FolderEncryptionSettings.Clear();
        foreach (var kvp in _folderSettings)
        {
            _config.FolderEncryptionSettings.Add(kvp.Value);
        }
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
                
                // Create default settings for this folder
                var settings = new FolderEncryptionSettings
                {
                    FolderPath = dialog.SelectedPath,
                    SelectedExtensions = new List<string>()
                };
                _folderSettings[dialog.SelectedPath] = settings;
                
                // Auto-select the newly added folder
                lstFolders.SelectedItem = dialog.SelectedPath;
                
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
            if (!string.IsNullOrEmpty(removed))
            {
                _folderSettings.Remove(removed);
            }
            lstFolders.Items.RemoveAt(lstFolders.SelectedIndex);
            clbExtensions.Items.Clear();
            lblExtensionInfo.Text = "Select a folder to view file types";
            ShowStatus($"Removed folder: {removed}", false);
        }
        else
        {
            ShowStatus("Select a folder to remove.", true);
        }
    }
    
    private void lstFolders_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstFolders.SelectedIndex >= 0)
        {
            var selectedFolder = lstFolders.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                ScanAndDisplayExtensions(selectedFolder);
            }
        }
        else
        {
            clbExtensions.Items.Clear();
            lblExtensionInfo.Text = "Select a folder to view file types";
        }
    }
    
    private void ScanAndDisplayExtensions(string folderPath)
    {
        try
        {
            clbExtensions.Items.Clear();
            lblExtensionInfo.Text = "Scanning folder...";
            Application.DoEvents();
            
            var result = _extensionScanner.ScanFolderExtensions(folderPath);
            
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                lblExtensionInfo.Text = $"Error: {result.ErrorMessage}";
                return;
            }
            
            // Get or create settings for this folder
            if (!_folderSettings.ContainsKey(folderPath))
            {
                _folderSettings[folderPath] = new FolderEncryptionSettings
                {
                    FolderPath = folderPath,
                    SelectedExtensions = new List<string>()
                };
            }
            var settings = _folderSettings[folderPath];
            
            // Get extensions sorted by risk level
            var extensions = result.GetExtensionsByRisk();
            
            foreach (var ext in extensions)
            {
                // Format: ".ext (10 files, 1.5 MB) - SAFE"
                string riskText = ext.RiskLevel.ToString().ToUpper();
                string displayText = $"{ext.Extension} ({ext.FileCount} files, {ext.FormattedSize}) - {riskText}";
                
                int index = clbExtensions.Items.Add(displayText);
                
                // Check if this extension is already selected in settings
                // Default to checking SAFE extensions
                bool shouldCheck = settings.SelectedExtensions.Contains(ext.Extension) ||
                    (settings.SelectedExtensions.Count == 0 && ext.RiskLevel == RiskLevel.Safe);
                    
                clbExtensions.SetItemChecked(index, shouldCheck);
            }
            
            lblExtensionInfo.Text = $"Found {result.TotalFilesFound} files, {result.UniqueExtensions} types. Check extensions to encrypt.";
            
            // Store extension info for later use
            clbExtensions.Tag = extensions;
        }
        catch (Exception ex)
        {
            lblExtensionInfo.Text = $"Error: {ex.Message}";
        }
    }
    
    private void clbExtensions_ItemCheck(object sender, ItemCheckEventArgs e)
    {
        // Get the selected folder
        if (lstFolders.SelectedIndex < 0) return;
        var folderPath = lstFolders.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(folderPath)) return;
        
        // Get extension info from tag
        var extensions = clbExtensions.Tag as List<ExtensionInfo>;
        if (extensions == null || e.Index >= extensions.Count) return;
        
        var ext = extensions[e.Index];
        
        // Warn about dangerous extensions
        if (e.NewValue == CheckState.Checked && ext.RiskLevel == RiskLevel.Dangerous)
        {
            var result = MessageBox.Show(
                $"⚠️ WARNING: Encrypting '{ext.Extension}' files is DANGEROUS!\n\n" +
                "This can cause:\n" +
                "• Game crashes on startup\n" +
                "• Corrupted game files\n" +
                "• Inability to play until decrypted\n\n" +
                $"Examples: {ext.ExampleFilesList}\n\n" +
                "Are you SURE you want to encrypt these files?",
                "Dangerous File Type",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
                
            if (result != DialogResult.Yes)
            {
                e.NewValue = CheckState.Unchecked;
                return;
            }
        }
        
        // Update folder settings - use BeginInvoke to update after the check state changes
        BeginInvoke(new Action(() => UpdateFolderSettings(folderPath)));
    }
    
    private void UpdateFolderSettings(string folderPath)
    {
        if (!_folderSettings.ContainsKey(folderPath)) return;
        
        var settings = _folderSettings[folderPath];
        var extensions = clbExtensions.Tag as List<ExtensionInfo>;
        if (extensions == null) return;
        
        settings.SelectedExtensions.Clear();
        
        for (int i = 0; i < clbExtensions.Items.Count && i < extensions.Count; i++)
        {
            if (clbExtensions.GetItemChecked(i))
            {
                settings.SelectedExtensions.Add(extensions[i].Extension);
            }
        }
    }
    
    private void btnSelectSafe_Click(object sender, EventArgs e)
    {
        var extensions = clbExtensions.Tag as List<ExtensionInfo>;
        if (extensions == null) return;
        
        for (int i = 0; i < clbExtensions.Items.Count && i < extensions.Count; i++)
        {
            clbExtensions.SetItemChecked(i, extensions[i].RiskLevel == RiskLevel.Safe);
        }
        
        if (lstFolders.SelectedIndex >= 0)
        {
            var folderPath = lstFolders.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(folderPath))
            {
                UpdateFolderSettings(folderPath);
            }
        }
        
        ShowStatus("Selected only SAFE file types (saves, configs, text files)", false);
    }
    
    private void btnSelectNone_Click(object sender, EventArgs e)
    {
        for (int i = 0; i < clbExtensions.Items.Count; i++)
        {
            clbExtensions.SetItemChecked(i, false);
        }
        
        if (lstFolders.SelectedIndex >= 0)
        {
            var folderPath = lstFolders.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(folderPath))
            {
                UpdateFolderSettings(folderPath);
            }
        }
        
        ShowStatus("Cleared all selections", false);
    }
    
    private void btnRescan_Click(object sender, EventArgs e)
    {
        if (lstFolders.SelectedIndex >= 0)
        {
            var selectedFolder = lstFolders.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                ScanAndDisplayExtensions(selectedFolder);
                ShowStatus("Folder rescanned", false);
            }
        }
        else
        {
            ShowStatus("Select a folder to scan", true);
        }
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        if (!ValidateInputs())
            return;

        try
        {
            // Make sure current folder settings are saved
            if (lstFolders.SelectedIndex >= 0)
            {
                var folderPath = lstFolders.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(folderPath))
                {
                    UpdateFolderSettings(folderPath);
                }
            }
            
            PopulateConfigFromForm();
            await _configManager.SaveConfigAsync(_config);
            ShowStatus("Configuration saved successfully! The service will pick up changes automatically.", false);

            MessageBox.Show(
                "Configuration saved successfully!\n\n" +
                "The GameLocker service will automatically pick up the new settings.\n" +
                "Make sure the GameLocker Service is running.\n\n" +
                $"Folders configured: {lstFolders.Items.Count}\n" +
                $"Total extension settings: {_folderSettings.Count}",
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
            _folderSettings.Clear();
            PopulateFormFromConfig();
            ShowStatus("Configuration cleared.", false);
        }
    }
}

