using GameLocker.Common.Configuration;
using GameLocker.Common.Models;
using GameLocker.Common.Notifications;
using GameLocker.Common.Services;

namespace GameLocker.Service;

/// <summary>
/// Main Windows Service for GameLocker.
/// Monitors schedule and enforces game folder lock/unlock.
/// </summary>
public sealed class GameLockerService : BackgroundService
{
    private readonly ILogger<GameLockerService> _logger;
    private readonly ConfigManager _configManager;
    private readonly FolderLocker _folderLocker;
    
    private GameLockerConfig? _config;
    private bool _wasWithinAllowedTime = false;
    private bool _isFirstRun = true;
    private DateTime? _lastWarningTime = null;

    public GameLockerService(ILogger<GameLockerService> logger)
    {
        _logger = logger;
        _configManager = new ConfigManager();
        _folderLocker = new FolderLocker(_configManager.ConfigDirectory);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameLocker Service starting...");

        try
        {
            // Initialize the folder locker
            await _folderLocker.InitializeAsync();

            // Load initial configuration
            await LoadConfigurationAsync();

            if (_config == null)
            {
                _logger.LogWarning("No configuration found. Service will wait for configuration.");
            }
            else
            {
                _logger.LogInformation("Configuration loaded. Monitoring {count} folder(s).", 
                    _config.GameFolderPaths.Count);
                
                // Notify service started
                if (_config.NotificationsEnabled)
                {
                    NotificationHelper.ShowServiceStartedNotification();
                }
            }

            // Main service loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check for immediate commands first
                    await ProcessImmediateCommandsAsync();
                    
                    // Check for reload signals
                    await ProcessReloadSignalAsync();
                    
                    // Process regular schedule
                    await ProcessScheduleAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during service processing: {Message}", ex.Message);
                }

                // Wait for a short interval then check for immediate commands again
                // Use 5 second polling for responsive command handling
                for (int i = 0; i < 12; i++) // 12 x 5 seconds = 1 minute
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    
                    // Check for immediate commands frequently
                    await ProcessImmediateCommandsAsync();
                    await ProcessReloadSignalAsync();
                }

                // Reload configuration periodically (in case it changed)
                await LoadConfigurationAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Service is stopping, this is expected
            _logger.LogInformation("GameLocker Service received stop signal.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in GameLocker Service: {Message}", ex.Message);
            
            // Exit with error code so Windows Service Manager can restart
            Environment.Exit(1);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GameLocker Service stopping...");

        if (_config?.NotificationsEnabled == true)
        {
            NotificationHelper.ShowServiceStoppedNotification();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task LoadConfigurationAsync()
    {
        var newConfig = await _configManager.LoadConfigAsync();
        
        if (newConfig != null)
        {
            _config = newConfig;
            _logger.LogDebug("Configuration reloaded.");
        }
    }

    private async Task ProcessScheduleAsync()
    {
        if (_config == null || _config.GameFolderPaths.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        var isWithinAllowedTime = _config.IsWithinAllowedTime(now);

        _logger.LogDebug("Schedule check at {Time}: AllowedTime={IsAllowed}", 
            now, isWithinAllowedTime);

        // Check if state changed or if this is the first run
        if (_isFirstRun || isWithinAllowedTime != _wasWithinAllowedTime)
        {
            var total = _config.GameFolderPaths.Count;

            var succeeded = isWithinAllowedTime
                ? await UnlockAllFoldersAsync()
                : await LockAllFoldersAsync();

            // Only record the transition once every folder actually reached the required state.
            // This was previously assigned unconditionally, so a single transient failure at the
            // boundary minute - a file held open by the game, an antivirus scan, a busy disk -
            // was recorded as success and never retried until the next boundary, potentially a
            // week later. See ARCHITECTURE-REVIEW.md finding [S-1].
            if (succeeded == total)
            {
                _wasWithinAllowedTime = isWithinAllowedTime;
                _isFirstRun = false;
            }
            else
            {
                _logger.LogError(
                    "Only {Succeeded} of {Total} folder(s) reached the required state ({Desired}). " +
                    "The transition has NOT been recorded and will be retried on the next cycle.",
                    succeeded, total, isWithinAllowedTime ? "unlocked" : "locked");
            }
        }
        else if (isWithinAllowedTime)
        {
            // Check if we should show a warning
            await CheckAndShowWarningAsync(now);
        }
    }

    /// <summary>
    /// Locks every configured folder.
    /// </summary>
    /// <returns>The number of folders that reached the locked state.</returns>
    private async Task<int> LockAllFoldersAsync()
    {
        _logger.LogInformation("Locking all game folders...");

        var successCount = 0;

        foreach (var folderPath in _config!.GameFolderPaths)
        {
            try
            {
                var result = await _folderLocker.LockFolderAsync(folderPath);
                
                if (result.State == FolderState.Locked)
                {
                    successCount++;
                    _logger.LogInformation("Locked folder: {Path}", folderPath);
                }
                else
                {
                    _logger.LogWarning("Failed to lock folder: {Path} - {Error}", 
                        folderPath, result.LastError);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking folder: {Path}", folderPath);
            }
        }

        // Notifications must never affect the outcome. A LocalSystem service runs in session 0
        // with no registered AUMID, so ToastContentBuilder.Show() can throw - which previously
        // unwound past the state assignment and caused the whole lock to repeat every cycle.
        // See ARCHITECTURE-REVIEW.md finding [S-6].
        if (_config.NotificationsEnabled && successCount > 0)
        {
            try
            {
                var nextUnlock = _config.GetNextUnlockTime(DateTime.Now);
                NotificationHelper.ShowLockNotification(successCount, nextUnlock);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not show lock notification: {Message}", ex.Message);
            }
        }

        return successCount;
    }

    /// <summary>
    /// Unlocks every configured folder.
    /// </summary>
    /// <returns>The number of folders that reached the unlocked state.</returns>
    private async Task<int> UnlockAllFoldersAsync()
    {
        _logger.LogInformation("Unlocking all game folders...");

        var successCount = 0;

        foreach (var folderPath in _config!.GameFolderPaths)
        {
            try
            {
                var result = await _folderLocker.UnlockFolderAsync(folderPath);
                
                if (result.State == FolderState.Unlocked)
                {
                    successCount++;
                    _logger.LogInformation("Unlocked folder: {Path}", folderPath);
                }
                else
                {
                    _logger.LogWarning("Failed to unlock folder: {Path} - {Error}", 
                        folderPath, result.LastError);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking folder: {Path}", folderPath);
            }
        }

        if (_config.NotificationsEnabled && successCount > 0)
        {
            try
            {
                var lockTime = _config.GetNextLockTime(DateTime.Now);
                NotificationHelper.ShowUnlockNotification(successCount, lockTime);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not show unlock notification: {Message}", ex.Message);
            }
        }

        // Reset warning time
        _lastWarningTime = null;

        return successCount;
    }

    private Task CheckAndShowWarningAsync(DateTime now)
    {
        if (_config == null || !_config.NotificationsEnabled)
            return Task.CompletedTask;

        var lockTime = _config.GetNextLockTime(now);
        if (!lockTime.HasValue)
            return Task.CompletedTask;

        var timeRemaining = lockTime.Value - now;
        
        // Show warnings at 15 minutes and 5 minutes before lock
        if (timeRemaining.TotalMinutes <= 15 && timeRemaining.TotalMinutes > 14 && 
            _lastWarningTime != lockTime.Value.AddMinutes(-15))
        {
            NotificationHelper.ShowWarningNotification(15);
            _lastWarningTime = lockTime.Value.AddMinutes(-15);
        }
        else if (timeRemaining.TotalMinutes <= 5 && timeRemaining.TotalMinutes > 4 && 
                 _lastWarningTime != lockTime.Value.AddMinutes(-5))
        {
            NotificationHelper.ShowWarningNotification(5);
            _lastWarningTime = lockTime.Value.AddMinutes(-5);
        }

        return Task.CompletedTask;
    }

    private async Task ProcessReloadSignalAsync()
    {
        try
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GameLocker");
            var signalFile = Path.Combine(configDir, "reload_signal");

            if (File.Exists(signalFile))
            {
                _logger.LogInformation("Reload signal received. Reloading configuration...");
                
                // Delete signal file
                File.Delete(signalFile);
                
                // Force reload configuration
                await LoadConfigurationAsync();
                
                // Apply current state based on new config
                if (_config != null)
                {
                    var now = DateTime.Now;
                    var isWithinAllowedTime = _config.IsWithinAllowedTime(now);
                    var total = _config.GameFolderPaths.Count;

                    var succeeded = isWithinAllowedTime
                        ? await UnlockAllFoldersAsync()
                        : await LockAllFoldersAsync();

                    if (succeeded == total)
                    {
                        _wasWithinAllowedTime = isWithinAllowedTime;
                        _logger.LogInformation("Configuration reloaded and applied immediately.");
                    }
                    else
                    {
                        _logger.LogError(
                            "Configuration reloaded, but only {Succeeded} of {Total} folder(s) reached " +
                            "the required state. The transition will be retried on the next cycle.",
                            succeeded, total);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing reload signal: {Message}", ex.Message);
        }
    }

    private async Task ProcessImmediateCommandsAsync()
    {
        try
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GameLocker");
            var commandFile = Path.Combine(configDir, "immediate_action");

            if (File.Exists(commandFile))
            {
                var commandText = await File.ReadAllTextAsync(commandFile);
                File.Delete(commandFile);

                var parts = commandText.Split('|');
                if (parts.Length >= 3)
                {
                    var action = parts[0];
                    var folderPath = parts[1];
                    var timestamp = parts[2];

                    _logger.LogInformation("Immediate command received: {Action} for {Folder}", action, folderPath);

                    // The command file lives in a directory that has historically been writable by
                    // every user on the machine, and this service runs as LocalSystem. Without this
                    // check, any unprivileged user could make SYSTEM grant them access to - or
                    // encrypt - an arbitrary path such as C:\Windows.
                    // See ARCHITECTURE-REVIEW.md findings [V-1] and [V-4].
                    if (!IsManagedFolder(folderPath, out var resolvedPath))
                    {
                        _logger.LogWarning(
                            "Rejected immediate command {Action}: '{Folder}' is not a configured game folder. " +
                            "Commands are only honoured for folders present in the saved configuration.",
                            action, folderPath);
                        return;
                    }

                    switch (action.ToLowerInvariant())
                    {
                        case "lock":
                            await LockSingleFolderAsync(resolvedPath);
                            break;
                        case "unlock":
                            await UnlockSingleFolderAsync(resolvedPath);
                            break;
                        case "remove":
                            await RemoveFolderAsync(resolvedPath);
                            break;
                        default:
                            _logger.LogWarning("Unknown immediate action: {Action}", action);
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing immediate commands: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Determines whether a path supplied by an external command refers to a folder in the saved
    /// configuration, and resolves it to the configured spelling.
    /// </summary>
    /// <param name="requestedPath">The raw path taken from the command file.</param>
    /// <param name="resolvedPath">The matching configured path, when the check succeeds.</param>
    /// <returns><c>true</c> when the path is managed by GameLocker; otherwise <c>false</c>.</returns>
    private bool IsManagedFolder(string requestedPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(requestedPath) || _config == null)
            return false;

        string candidate;
        try
        {
            candidate = Normalize(requestedPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _logger.LogWarning("Rejected malformed command path '{Folder}': {Message}", requestedPath, ex.Message);
            return false;
        }

        foreach (var configured in _config.GameFolderPaths)
        {
            string known;
            try
            {
                known = Normalize(configured);
            }
            catch
            {
                continue; // A malformed entry in config cannot authorise anything.
            }

            if (string.Equals(candidate, known, StringComparison.OrdinalIgnoreCase))
            {
                resolvedPath = configured;
                return true;
            }
        }

        return false;

        static string Normalize(string path) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private async Task LockSingleFolderAsync(string folderPath)
    {
        try
        {
            var result = await _folderLocker.LockFolderAsync(folderPath);
            
            if (result.State == FolderState.Locked)
            {
                _logger.LogInformation("Successfully locked folder: {Path}", folderPath);
            }
            else
            {
                _logger.LogWarning("Failed to lock folder: {Path} - {Error}", folderPath, result.LastError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking single folder: {Path}", folderPath);
        }
    }

    private async Task UnlockSingleFolderAsync(string folderPath)
    {
        try
        {
            var result = await _folderLocker.UnlockFolderAsync(folderPath);
            
            if (result.State == FolderState.Unlocked)
            {
                _logger.LogInformation("Successfully unlocked folder: {Path}", folderPath);
            }
            else
            {
                _logger.LogWarning("Failed to unlock folder: {Path} - {Error}", folderPath, result.LastError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking single folder: {Path}", folderPath);
        }
    }

    private async Task RemoveFolderAsync(string folderPath)
    {
        try
        {
            // Always attempt to unlock the folder when removing
            var result = await _folderLocker.UnlockFolderAsync(folderPath);
            
            if (result.State == FolderState.Unlocked)
            {
                _logger.LogInformation("Successfully removed and unlocked folder: {Path}", folderPath);
            }
            else
            {
                _logger.LogWarning("Failed to fully unlock removed folder: {Path} - {Error}", folderPath, result.LastError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing folder: {Path}", folderPath);
        }
    }
}
