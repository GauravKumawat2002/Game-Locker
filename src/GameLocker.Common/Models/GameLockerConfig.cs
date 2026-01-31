using System.Text.Json.Serialization;

namespace GameLocker.Common.Models;

/// <summary>
/// Configuration model for GameLocker settings.
/// Stores the user's gaming schedule and folder paths.
/// </summary>
public class GameLockerConfig
{
    /// <summary>
    /// List of allowed gaming days (e.g., "Saturday", "Sunday").
    /// </summary>
    [JsonPropertyName("allowedDays")]
    public List<DayOfWeek> AllowedDays { get; set; } = new();

    /// <summary>
    /// Start time for gaming on allowed days (hour and minute).
    /// </summary>
    [JsonPropertyName("startTime")]
    public TimeOnly StartTime { get; set; } = new(18, 0); // Default: 6 PM

    /// <summary>
    /// Duration of gaming window in hours.
    /// </summary>
    [JsonPropertyName("durationHours")]
    public int DurationHours { get; set; } = 2; // Default: 2 hours

    /// <summary>
    /// Full paths to game folders to lock/unlock.
    /// </summary>
    [JsonPropertyName("gameFolderPaths")]
    public List<string> GameFolderPaths { get; set; } = new();

    /// <summary>
    /// Polling interval in minutes for the service to check schedule.
    /// </summary>
    [JsonPropertyName("pollingIntervalMinutes")]
    public int PollingIntervalMinutes { get; set; } = 1;

    /// <summary>
    /// Indicates if notifications are enabled.
    /// </summary>
    [JsonPropertyName("notificationsEnabled")]
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Selective encryption settings - only encrypt specific file types instead of everything
    /// </summary>
    [JsonPropertyName("encryptionSettings")]
    public SelectiveEncryptionSettings EncryptionSettings { get; set; } = new SelectiveEncryptionSettings();

    /// <summary>
    /// Per-folder encryption settings with user-selected file extensions.
    /// Each game folder can have its own custom extension selection.
    /// </summary>
    [JsonPropertyName("folderEncryptionSettings")]
    public List<FolderEncryptionSettings> FolderEncryptionSettings { get; set; } = new();

    /// <summary>
    /// Checks if the current time is within the allowed gaming window.
    /// </summary>
    /// <param name="currentTime">The current date and time to check.</param>
    /// <returns>True if gaming is currently allowed, false otherwise.</returns>
    public bool IsWithinAllowedTime(DateTime currentTime)
    {
        // Check if today is an allowed day
        if (!AllowedDays.Contains(currentTime.DayOfWeek))
        {
            return false;
        }

        // Calculate the time window
        var currentTimeOnly = TimeOnly.FromDateTime(currentTime);
        var endTime = StartTime.AddHours(DurationHours);

        // Handle case where gaming window crosses midnight
        if (endTime < StartTime)
        {
            // Gaming window crosses midnight
            return currentTimeOnly >= StartTime || currentTimeOnly < endTime;
        }

        return currentTimeOnly >= StartTime && currentTimeOnly < endTime;
    }

    /// <summary>
    /// Gets the next unlock time from the current time.
    /// </summary>
    /// <param name="currentTime">The current date and time.</param>
    /// <returns>The next DateTime when gaming will be unlocked, or null if no schedule.</returns>
    public DateTime? GetNextUnlockTime(DateTime currentTime)
    {
        if (AllowedDays.Count == 0)
            return null;

        var currentDate = currentTime.Date;
        
        // Check the next 7 days
        for (int i = 0; i < 7; i++)
        {
            var checkDate = currentDate.AddDays(i);
            if (AllowedDays.Contains(checkDate.DayOfWeek))
            {
                var unlockDateTime = checkDate.Add(StartTime.ToTimeSpan());
                if (unlockDateTime > currentTime)
                {
                    return unlockDateTime;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the next lock time from the current time.
    /// </summary>
    /// <param name="currentTime">The current date and time.</param>
    /// <returns>The next DateTime when gaming will be locked, or null if no schedule.</returns>
    public DateTime? GetNextLockTime(DateTime currentTime)
    {
        if (!IsWithinAllowedTime(currentTime))
            return null;

        var endTime = StartTime.AddHours(DurationHours);
        var lockDateTime = currentTime.Date.Add(endTime.ToTimeSpan());
        
        // If end time is before start time, it means we're crossing midnight
        if (endTime < StartTime)
        {
            lockDateTime = lockDateTime.AddDays(1);
        }

        return lockDateTime;
    }

    /// <summary>
    /// Gets the encryption settings for a specific folder path.
    /// </summary>
    /// <param name="folderPath">Path to the game folder</param>
    /// <returns>Folder encryption settings or null if not configured</returns>
    public FolderEncryptionSettings? GetFolderEncryptionSettings(string folderPath)
    {
        return FolderEncryptionSettings.FirstOrDefault(f => 
            string.Equals(f.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sets or updates encryption settings for a specific folder.
    /// </summary>
    /// <param name="settings">The folder encryption settings to save</param>
    public void SetFolderEncryptionSettings(FolderEncryptionSettings settings)
    {
        // Remove existing settings for this folder
        FolderEncryptionSettings.RemoveAll(f => 
            string.Equals(f.FolderPath, settings.FolderPath, StringComparison.OrdinalIgnoreCase));
        
        // Add the new settings
        FolderEncryptionSettings.Add(settings);
    }

    /// <summary>
    /// Gets the effective encryption settings for a folder (custom or fallback to global preset).
    /// </summary>
    /// <param name="folderPath">Path to the game folder</param>
    /// <returns>Settings to use for encryption, never null</returns>
    public FolderEncryptionSettings GetEffectiveFolderSettings(string folderPath)
    {
        var existing = GetFolderEncryptionSettings(folderPath);
        if (existing != null)
        {
            return existing;
        }

        // Create default settings with global preset as fallback
        return new FolderEncryptionSettings
        {
            FolderPath = folderPath,
            UseCustomSelection = false,
            FallbackPreset = EncryptionSettings
        };
    }

    /// <summary>
    /// Removes encryption settings for a folder (when folder is removed from the list).
    /// </summary>
    /// <param name="folderPath">Path to the folder to remove settings for</param>
    public bool RemoveFolderEncryptionSettings(string folderPath)
    {
        return FolderEncryptionSettings.RemoveAll(f => 
            string.Equals(f.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase)) > 0;
    }
}
