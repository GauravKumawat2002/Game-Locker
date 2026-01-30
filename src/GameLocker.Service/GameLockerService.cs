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
                    await ProcessScheduleAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during schedule processing: {Message}", ex.Message);
                }

                // Wait for the polling interval
                var intervalMinutes = _config?.PollingIntervalMinutes ?? 1;
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);

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

        // Check if state changed
        if (isWithinAllowedTime != _wasWithinAllowedTime)
        {
            if (isWithinAllowedTime)
            {
                // Time to unlock
                await UnlockAllFoldersAsync();
            }
            else
            {
                // Time to lock
                await LockAllFoldersAsync();
            }

            _wasWithinAllowedTime = isWithinAllowedTime;
        }
        else if (isWithinAllowedTime)
        {
            // Check if we should show a warning
            await CheckAndShowWarningAsync(now);
        }
    }

    private async Task LockAllFoldersAsync()
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

        if (_config.NotificationsEnabled && successCount > 0)
        {
            var nextUnlock = _config.GetNextUnlockTime(DateTime.Now);
            NotificationHelper.ShowLockNotification(successCount, nextUnlock);
        }
    }

    private async Task UnlockAllFoldersAsync()
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
            var lockTime = _config.GetNextLockTime(DateTime.Now);
            NotificationHelper.ShowUnlockNotification(successCount, lockTime);
        }

        // Reset warning time
        _lastWarningTime = null;
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
}
