namespace GameLocker.Common.Models;

/// <summary>
/// Represents the current state of a game folder.
/// </summary>
public enum FolderState
{
    /// <summary>
    /// Folder is locked (encrypted and access denied).
    /// </summary>
    Locked,

    /// <summary>
    /// Folder is unlocked (decrypted and accessible).
    /// </summary>
    Unlocked,

    /// <summary>
    /// Folder state is unknown or not yet initialized.
    /// </summary>
    Unknown
}

/// <summary>
/// Represents information about a managed game folder.
/// </summary>
public class GameFolderInfo
{
    /// <summary>
    /// Full path to the game folder.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Current state of the folder.
    /// </summary>
    public FolderState State { get; set; } = FolderState.Unknown;

    /// <summary>
    /// Last time the folder state was changed.
    /// </summary>
    public DateTime? LastStateChange { get; set; }

    /// <summary>
    /// Any error message from the last operation.
    /// </summary>
    public string? LastError { get; set; }
}
