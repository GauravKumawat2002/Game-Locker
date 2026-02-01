using GameLocker.Common.Configuration;
using GameLocker.Common.Models;
using GameLocker.Common.Security;
using GameLocker.Common.Services;
using System.ServiceProcess;

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

    private async void btnAddFolder_Click(object sender, EventArgs e)
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
                
                ShowStatus($"Added folder: {dialog.SelectedPath}. Checking service status...", false);
                
                // Check if service is running and apply configuration immediately
                await TriggerImmediateFolderProcessingAsync("add", dialog.SelectedPath);
            }
            else
            {
                ShowStatus("Folder already in list.", true);
            }
        }
    }

    private async void btnRemoveFolder_Click(object sender, EventArgs e)
    {
        if (lstFolders.SelectedIndex >= 0)
        {
            var removed = lstFolders.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(removed))
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to remove '{removed}' from GameLocker?\n\n" +
                    "This will decrypt all files in the folder and restore normal access permissions.\n" +
                    "The decryption process may take some time depending on the number and size of files.",
                    "Remove Folder",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    await DecryptAndRemoveFolderAsync(removed);
                }
            }
        }
        else
        {
            ShowStatus("Select a folder to remove.", true);
        }
    }
    
    /// <summary>
    /// Decrypts all files in a folder and removes it from the configuration.
    /// Shows a progress dialog with live updates.
    /// </summary>
    private async Task DecryptAndRemoveFolderAsync(string folderPath)
    {
        // Create a progress form
        var progressForm = new Form
        {
            Text = "Decrypting Files",
            Width = 550,
            Height = 220,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ControlBox = false,
            TopMost = true
        };
        
        var lblProgress = new Label
        {
            Text = "Initializing decryption...",
            Location = new Point(20, 20),
            Size = new Size(500, 25),
            AutoSize = false,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold)
        };
        
        var progressBar = new ProgressBar
        {
            Location = new Point(20, 50),
            Size = new Size(500, 30),
            Style = ProgressBarStyle.Continuous
        };
        
        var lblFile = new Label
        {
            Text = "Please wait...",
            Location = new Point(20, 90),
            Size = new Size(500, 45),
            AutoSize = false
        };
        
        var btnClose = new Button
        {
            Text = "Close",
            Location = new Point(225, 145),
            Size = new Size(100, 35),
            Enabled = false
        };
        
        bool completed = false;
        string folderToRemove = folderPath;
        
        btnClose.Click += (s, e) => { 
            if (completed) 
            {
                progressForm.DialogResult = DialogResult.OK;
                progressForm.Close();
            }
        };
        
        progressForm.Controls.AddRange(new Control[] { lblProgress, progressBar, lblFile, btnClose });
        
        // Run the decryption in a background task while showing the form
        _ = Task.Run(async () =>
        {
            try
            {
                // Initialize FolderLocker
                string keyStorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GameLocker");
                var folderLocker = new FolderLocker(keyStorePath);
                
                progressForm.Invoke(() => {
                    lblProgress.Text = "Loading encryption keys...";
                    lblFile.Text = "Initializing...";
                });
                
                await folderLocker.InitializeAsync();
                
                // First, restore ACL permissions
                progressForm.Invoke(() => {
                    lblProgress.Text = "Restoring folder permissions...";
                    lblFile.Text = "Removing access restrictions...";
                });
                
                try
                {
                    AclHelper.AllowAccess(folderPath);
                }
                catch (Exception ex)
                {
                    progressForm.Invoke(() => lblFile.Text = $"ACL warning: {ex.Message}");
                }
                
                // Find all encrypted files (including hidden files)
                progressForm.Invoke(() => {
                    lblProgress.Text = "Scanning for encrypted files...";
                    lblFile.Text = "Looking for .enc files...";
                });
                
                // Use DirectoryInfo to get hidden files too
                var dirInfo = new DirectoryInfo(folderPath);
                var encryptedFiles = dirInfo.GetFiles("*.enc", SearchOption.AllDirectories)
                    .Select(f => f.FullName)
                    .ToArray();
                int totalFiles = encryptedFiles.Length;
                
                if (totalFiles == 0)
                {
                    progressForm.Invoke(() => {
                        lblProgress.Text = "No encrypted files found.";
                        lblFile.Text = "Folder was not encrypted or already decrypted.";
                        progressBar.Style = ProgressBarStyle.Continuous;
                        progressBar.Value = 100;
                    });
                }
                else
                {
                    progressForm.Invoke(() => {
                        progressBar.Maximum = totalFiles;
                        progressBar.Value = 0;
                    });
                    
                    int decrypted = 0;
                    int failed = 0;
                    
                    foreach (var encFile in encryptedFiles)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(encFile);
                            progressForm.Invoke(() => {
                                lblProgress.Text = $"Decrypting file {decrypted + failed + 1} of {totalFiles}...";
                                lblFile.Text = fileName.Length > 60 ? fileName.Substring(0, 57) + "..." : fileName;
                            });
                            
                            // Unhide the file first if it's hidden
                            var fileInfo = new FileInfo(encFile);
                            if ((fileInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                            {
                                fileInfo.Attributes &= ~FileAttributes.Hidden;
                            }
                            
                            // Decrypt using FolderLocker's public method
                            await folderLocker.DecryptSingleFileAsync(encFile);
                            decrypted++;
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            Console.WriteLine($"Failed to decrypt {encFile}: {ex.Message}");
                        }
                        
                        int currentProgress = decrypted + failed;
                        progressForm.Invoke(() => progressBar.Value = currentProgress);
                    }
                    
                    progressForm.Invoke(() => {
                        if (failed == 0)
                            lblProgress.Text = $"✓ Successfully decrypted {decrypted} files!";
                        else
                            lblProgress.Text = $"Decryption complete: {decrypted} OK, {failed} failed.";
                    });
                }
                
                // Remove lock marker
                var markerPath = Path.Combine(folderPath, ".gamelocker");
                if (File.Exists(markerPath))
                {
                    try 
                    { 
                        var markerInfo = new FileInfo(markerPath);
                        markerInfo.Attributes &= ~FileAttributes.Hidden;
                        File.Delete(markerPath); 
                    } 
                    catch { }
                }
                
                progressForm.Invoke(() => {
                    lblFile.Text = "✓ Folder removed from GameLocker successfully!";
                    completed = true;
                    btnClose.Enabled = true;
                    btnClose.Focus();
                });
            }
            catch (Exception ex)
            {
                progressForm.Invoke(() => {
                    lblProgress.Text = "Error during decryption";
                    lblFile.Text = ex.Message;
                    completed = true;
                    btnClose.Enabled = true;
                });
            }
        });
        
        // Show the form modally
        progressForm.ShowDialog(this);
        progressForm.Dispose();
        
        // After dialog closes, update UI
        _folderSettings.Remove(folderToRemove);
        if (lstFolders.Items.Contains(folderToRemove))
            lstFolders.Items.Remove(folderToRemove);
        clbExtensions.Items.Clear();
        lblExtensionInfo.Text = "Select a folder to view file types";
        ShowStatus($"Folder removed and files decrypted: {folderToRemove}", false);
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
            
            // Immediately notify the service of configuration changes
            var serviceNotified = await ServiceManager.SendConfigReloadCommandAsync();
            var serviceStatus = ServiceManager.GetServiceStatus();
            
            ShowStatus($"Configuration saved successfully! Service notification: {(serviceNotified ? "SUCCESS" : "FAILED")}", false);

            MessageBox.Show(
                "Configuration saved successfully!\n\n" +
                $"Service Status: {serviceStatus?.ToString() ?? "Not Found"}\n" +
                $"Service Notified: {(serviceNotified ? "Yes" : "No")}\n" +
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

    private async Task TriggerImmediateFolderProcessingAsync(string action, string folderPath)
    {
        try
        {
            // Check service status
            var serviceStatus = ServiceManager.GetServiceStatus();
            
            if (serviceStatus == null)
            {
                ShowStatus("GameLocker Service not found. Please ensure the service is installed.", true);
                return;
            }
            
            if (serviceStatus != ServiceControllerStatus.Running)
            {
                ShowStatus($"GameLocker Service is {serviceStatus}. Attempting to start...", false);
                
                var started = await ServiceManager.StartServiceAsync();
                if (!started)
                {
                    ShowStatus("Failed to start GameLocker Service. Please start it manually.", true);
                    return;
                }
                
                ShowStatus("GameLocker Service started successfully.", false);
            }
            
            // Send immediate command to the service
            string command = action switch
            {
                "add" => "lock", // When adding a folder, we want to lock it if we're outside gaming hours
                "remove" => "unlock", // When removing a folder, we always want to unlock it
                _ => action
            };
            
            var commandSent = await ServiceManager.SendImmediateActionCommandAsync(command, folderPath);
            
            if (commandSent)
            {
                ShowStatus($"Command sent to service: {command} folder '{folderPath}'", false);
                
                // For remove operations, give a bit more time for processing
                if (action == "remove")
                {
                    await Task.Delay(2000); // Wait 2 seconds for the service to process
                }
            }
            else
            {
                ShowStatus("Failed to send command to service.", true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error communicating with service: {ex.Message}", true);
        }
    }
}

