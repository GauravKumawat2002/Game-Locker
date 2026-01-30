using GameLocker.Common.Encryption;
using GameLocker.Common.Models;
using GameLocker.Common.Security;

namespace GameLocker.Common.Services;

/// <summary>
/// Manages locking and unlocking of game folders.
/// Combines encryption and ACL controls for comprehensive protection.
/// </summary>
public class FolderLocker
{
    private readonly string _keyStorePath;
    private byte[]? _masterKey;
    private byte[]? _masterIV;

    /// <summary>
    /// Creates a new FolderLocker with the specified key storage path.
    /// </summary>
    /// <param name="keyStorePath">Path to store encryption keys.</param>
    public FolderLocker(string keyStorePath)
    {
        _keyStorePath = keyStorePath;
    }

    /// <summary>
    /// Initializes the folder locker by loading or generating encryption keys.
    /// </summary>
    public async Task InitializeAsync()
    {
        var keyPath = Path.Combine(_keyStorePath, "folder_key.dat");
        var ivPath = Path.Combine(_keyStorePath, "folder_iv.dat");

        if (File.Exists(keyPath) && File.Exists(ivPath))
        {
            _masterKey = await DpapiHelper.UnprotectFromFileAsync(keyPath);
            _masterIV = await DpapiHelper.UnprotectFromFileAsync(ivPath);
        }
        else
        {
            // Ensure directory exists
            Directory.CreateDirectory(_keyStorePath);

            _masterKey = AesEncryptionHelper.GenerateKey();
            _masterIV = AesEncryptionHelper.GenerateIV();

            await DpapiHelper.ProtectToFileAsync(_masterKey, keyPath);
            await DpapiHelper.ProtectToFileAsync(_masterIV, ivPath);
        }
    }

    /// <summary>
    /// Locks a folder by applying ACL restrictions.
    /// Note: Full file encryption is resource-intensive for large game folders.
    /// ACL-based locking provides fast, effective protection.
    /// </summary>
    /// <param name="folderPath">Path to the folder to lock.</param>
    /// <returns>Information about the locked folder.</returns>
    public async Task<GameFolderInfo> LockFolderAsync(string folderPath)
    {
        var info = new GameFolderInfo
        {
            Path = folderPath,
            State = FolderState.Unknown
        };

        try
        {
            if (!Directory.Exists(folderPath))
            {
                info.LastError = "Folder does not exist.";
                return info;
            }

            // Check if already locked
            if (AclHelper.IsAccessDenied(folderPath))
            {
                info.State = FolderState.Locked;
                return info;
            }

            // Create a lock marker file with encrypted content
            await CreateLockMarkerAsync(folderPath);

            // Apply ACL to deny access
            AclHelper.DenyAccess(folderPath);

            info.State = FolderState.Locked;
            info.LastStateChange = DateTime.Now;
        }
        catch (Exception ex)
        {
            info.LastError = ex.Message;
        }

        return info;
    }

    /// <summary>
    /// Unlocks a folder by removing ACL restrictions.
    /// </summary>
    /// <param name="folderPath">Path to the folder to unlock.</param>
    /// <returns>Information about the unlocked folder.</returns>
    public async Task<GameFolderInfo> UnlockFolderAsync(string folderPath)
    {
        var info = new GameFolderInfo
        {
            Path = folderPath,
            State = FolderState.Unknown
        };

        try
        {
            if (!Directory.Exists(folderPath))
            {
                info.LastError = "Folder does not exist.";
                return info;
            }

            // Check if already unlocked
            if (!AclHelper.IsAccessDenied(folderPath))
            {
                info.State = FolderState.Unlocked;
                return info;
            }

            // Remove ACL restrictions
            AclHelper.AllowAccess(folderPath);

            // Remove lock marker
            await RemoveLockMarkerAsync(folderPath);

            info.State = FolderState.Unlocked;
            info.LastStateChange = DateTime.Now;
        }
        catch (Exception ex)
        {
            info.LastError = ex.Message;
        }

        return info;
    }

    /// <summary>
    /// Gets the current state of a folder.
    /// </summary>
    /// <param name="folderPath">Path to the folder.</param>
    /// <returns>The folder's current state.</returns>
    public FolderState GetFolderState(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return FolderState.Unknown;

        return AclHelper.IsAccessDenied(folderPath) ? FolderState.Locked : FolderState.Unlocked;
    }

    /// <summary>
    /// Checks if a lock marker file exists in the folder.
    /// </summary>
    public bool HasLockMarker(string folderPath)
    {
        var markerPath = Path.Combine(folderPath, ".gamelocker");
        return File.Exists(markerPath);
    }

    private async Task CreateLockMarkerAsync(string folderPath)
    {
        if (_masterKey == null || _masterIV == null)
            await InitializeAsync();

        var markerPath = Path.Combine(folderPath, ".gamelocker");
        var timestamp = DateTime.Now.ToString("O");
        var markerData = System.Text.Encoding.UTF8.GetBytes($"GameLocker|Locked|{timestamp}");
        var encryptedMarker = AesEncryptionHelper.Encrypt(markerData, _masterKey!, _masterIV!);
        await File.WriteAllBytesAsync(markerPath, encryptedMarker);

        // Set the marker file as hidden
        File.SetAttributes(markerPath, FileAttributes.Hidden);
    }

    private Task RemoveLockMarkerAsync(string folderPath)
    {
        var markerPath = Path.Combine(folderPath, ".gamelocker");
        if (File.Exists(markerPath))
        {
            File.Delete(markerPath);
        }
        return Task.CompletedTask;
    }
}
