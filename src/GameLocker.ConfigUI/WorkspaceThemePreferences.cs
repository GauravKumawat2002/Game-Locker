namespace GameLocker.ConfigUI;

internal sealed class WorkspaceThemePreferences
{
    private readonly string _settingsFile;

    public WorkspaceThemePreferences() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLocker", "appearance.txt"))
    {
    }

    internal WorkspaceThemePreferences(string settingsFile)
    {
        _settingsFile = Path.GetFullPath(settingsFile);
    }

    public WorkspaceThemeMode Read()
    {
        try
        {
            return File.ReadAllText(_settingsFile).Trim() switch
            {
                nameof(WorkspaceThemeMode.Dark) => WorkspaceThemeMode.Dark,
                nameof(WorkspaceThemeMode.Light) => WorkspaceThemeMode.Light,
                _ => WorkspaceThemeMode.System,
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return WorkspaceThemeMode.System;
        }
    }

    public bool TrySave(WorkspaceThemeMode mode)
    {
        var temporary = $"{_settingsFile}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            File.WriteAllText(temporary, mode.ToString());
            File.Move(temporary, _settingsFile, overwrite: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException)
            {
                // Preserve the current preference and report the original failure.
            }
            return false;
        }
    }
}
