using Microsoft.Toolkit.Uwp.Notifications;

namespace GameLocker.Common.Notifications;

/// <summary>
/// Provides Windows toast notification functionality for GameLocker.
/// </summary>
public static class NotificationHelper
{
    private const string AppId = "GameLocker";

    /// <summary>
    /// Shows a notification when folders are locked.
    /// </summary>
    /// <param name="folderCount">Number of folders that were locked.</param>
    /// <param name="nextUnlockTime">The next time folders will be unlocked.</param>
    public static void ShowLockNotification(int folderCount, DateTime? nextUnlockTime)
    {
        var builder = new ToastContentBuilder()
            .AddText("🔒 GameLocker - Folders Locked")
            .AddText($"{folderCount} game folder(s) have been locked.");

        if (nextUnlockTime.HasValue)
        {
            builder.AddText($"Next unlock: {nextUnlockTime.Value:dddd, h:mm tt}");
        }

        builder.Show();
    }

    /// <summary>
    /// Shows a notification when folders are unlocked.
    /// </summary>
    /// <param name="folderCount">Number of folders that were unlocked.</param>
    /// <param name="lockTime">The time when folders will be locked again.</param>
    public static void ShowUnlockNotification(int folderCount, DateTime? lockTime)
    {
        var builder = new ToastContentBuilder()
            .AddText("🎮 GameLocker - Game Time!")
            .AddText($"{folderCount} game folder(s) are now unlocked.");

        if (lockTime.HasValue)
        {
            builder.AddText($"Locks again at: {lockTime.Value:h:mm tt}");
        }

        builder.Show();
    }

    /// <summary>
    /// Shows a warning notification before lock time.
    /// </summary>
    /// <param name="minutesRemaining">Minutes remaining before lock.</param>
    public static void ShowWarningNotification(int minutesRemaining)
    {
        new ToastContentBuilder()
            .AddText("⚠️ GameLocker - Warning")
            .AddText($"Game folders will lock in {minutesRemaining} minute(s)!")
            .AddText("Save your progress now.")
            .Show();
    }

    /// <summary>
    /// Shows an error notification.
    /// </summary>
    /// <param name="message">The error message.</param>
    public static void ShowErrorNotification(string message)
    {
        new ToastContentBuilder()
            .AddText("❌ GameLocker - Error")
            .AddText(message)
            .Show();
    }

    /// <summary>
    /// Shows a generic notification.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    public static void ShowNotification(string title, string message)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .Show();
    }

    /// <summary>
    /// Shows service started notification.
    /// </summary>
    public static void ShowServiceStartedNotification()
    {
        new ToastContentBuilder()
            .AddText("✅ GameLocker Service Started")
            .AddText("Game folders are now being monitored.")
            .Show();
    }

    /// <summary>
    /// Shows service stopped notification.
    /// </summary>
    public static void ShowServiceStoppedNotification()
    {
        new ToastContentBuilder()
            .AddText("⏹️ GameLocker Service Stopped")
            .AddText("Game folders are no longer being monitored.")
            .Show();
    }
}
