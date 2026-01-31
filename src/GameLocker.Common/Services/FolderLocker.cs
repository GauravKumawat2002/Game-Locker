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
    /// Locks a folder by encrypting all files and applying ACL restrictions.
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
            if (HasLockMarker(folderPath))
            {
                info.State = FolderState.Locked;
                return info;
            }

            // Create a lock marker file first
            await CreateLockMarkerAsync(folderPath);

            // Encrypt all files in the folder recursively
            await EncryptFolderContentsAsync(folderPath);

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
    /// Unlocks a folder by decrypting all files and removing ACL restrictions.
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
            if (!HasLockMarker(folderPath))
            {
                info.State = FolderState.Unlocked;
                return info;
            }

            // Remove ACL restrictions first
            AclHelper.AllowAccess(folderPath);

            // Decrypt all files in the folder recursively
            await DecryptFolderContentsAsync(folderPath);

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

        return HasLockMarker(folderPath) ? FolderState.Locked : FolderState.Unlocked;
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

    /// <summary>
    /// Encrypts all files in a folder recursively.
    /// </summary>
    private async Task EncryptFolderContentsAsync(string folderPath)
    {
        if (_masterKey == null || _masterIV == null)
            await InitializeAsync();

        // Get all files recursively, excluding the lock marker
        var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".gamelocker") && !f.EndsWith(".enc"))
            .ToArray();

        foreach (var filePath in files)
        {
            try
            {
                await EncryptFileAsync(filePath);
            }
            catch (Exception ex)
            {
                // Log but continue with other files
                Console.WriteLine($"Failed to encrypt {filePath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Decrypts all encrypted files in a folder recursively.
    /// </summary>
    private async Task DecryptFolderContentsAsync(string folderPath)
    {
        if (_masterKey == null || _masterIV == null)
            await InitializeAsync();

        // Get all encrypted files recursively
        var encryptedFiles = Directory.GetFiles(folderPath, "*.enc", SearchOption.AllDirectories);

        foreach (var encryptedPath in encryptedFiles)
        {
            try
            {
                await DecryptFileAsync(encryptedPath);
            }
            catch (Exception ex)
            {
                // Log but continue with other files
                Console.WriteLine($"Failed to decrypt {encryptedPath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Encrypts a single file by replacing it with an encrypted version.
    /// Uses streaming for large files to handle files over 2GB.
    /// </summary>
    private Task EncryptFileAsync(string filePath)
    {
        if (_masterKey == null || _masterIV == null)
            throw new InvalidOperationException("Encryption keys not initialized");

        var fileInfo = new FileInfo(filePath);
        var encryptedPath = filePath + ".enc";

        try
        {
            // Use streaming encryption for large files
            using var sourceStream = File.OpenRead(filePath);
            using var destStream = File.Create(encryptedPath);
            
            AesEncryptionHelper.EncryptStream(sourceStream, destStream, _masterKey, _masterIV);
            
            // Delete original file after successful encryption
            File.Delete(filePath);
            
            // Set encrypted file as hidden
            File.SetAttributes(encryptedPath, FileAttributes.Hidden);
        }
        catch (Exception ex)
        {
            // Clean up partial encrypted file on failure
            if (File.Exists(encryptedPath))
                File.Delete(encryptedPath);
            throw new Exception($"Failed to encrypt {filePath}: {ex.Message}", ex);
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Decrypts a single .enc file by replacing it with the decrypted version.
    /// Uses streaming for large files to handle files over 2GB.
    /// </summary>
    private Task DecryptFileAsync(string encryptedPath)
    {
        if (_masterKey == null || _masterIV == null)
            throw new InvalidOperationException("Encryption keys not initialized");

        // Determine original file path (remove .enc extension)
        var originalPath = encryptedPath.Substring(0, encryptedPath.Length - 4);

        try
        {
            // Use streaming decryption for large files
            using var sourceStream = File.OpenRead(encryptedPath);
            using var destStream = File.Create(originalPath);
            
            AesEncryptionHelper.DecryptStream(sourceStream, destStream, _masterKey, _masterIV);
            
            // Delete encrypted file after successful decryption
            File.Delete(encryptedPath);
        }
        catch (Exception ex)
        {
            // Clean up partial decrypted file on failure
            if (File.Exists(originalPath))
                File.Delete(originalPath);
            throw new Exception($"Failed to decrypt {encryptedPath}: {ex.Message}", ex);
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Locks a folder using per-folder extension settings with dynamic file type discovery.
    /// </summary>
    /// <param name="folderPath">Path to the folder to lock.</param>
    /// <param name="folderSettings">Per-folder encryption settings with user-selected extensions.</param>
    /// <returns>Information about the locked folder.</returns>
    public async Task<GameFolderInfo> LockFolderWithCustomExtensionsAsync(string folderPath, FolderEncryptionSettings folderSettings)
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
            if (HasLockMarker(folderPath))
            {
                info.State = FolderState.Locked;
                return info;
            }

            // Create a lock marker file first
            await CreateLockMarkerAsync(folderPath);

            // Use per-folder extension settings for precise encryption control
            await EncryptFolderWithCustomExtensionsAsync(folderPath, folderSettings);

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
    /// Encrypts files in a folder using per-folder custom extension selection.
    /// Only encrypts the specific file extensions the user selected for this folder.
    /// </summary>
    /// <param name="folderPath">Path to the folder to encrypt</param>
    /// <param name="folderSettings">Per-folder settings with user-selected extensions</param>
    public async Task EncryptFolderWithCustomExtensionsAsync(string folderPath, FolderEncryptionSettings folderSettings)
    {
        if (_masterKey == null || _masterIV == null)
            await InitializeAsync();

        // Get all files recursively, excluding the lock marker and already encrypted files
        var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".gamelocker") && !f.EndsWith(".enc"))
            .ToArray();

        var filesToEncrypt = files.Where(f => folderSettings.ShouldEncryptFile(Path.GetFileName(f))).ToArray();
        var skippedFiles = files.Except(filesToEncrypt).ToArray();

        Console.WriteLine($"Per-Folder Encryption Summary for {Path.GetFileName(folderPath)}:");
        Console.WriteLine($"- Total files found: {files.Length}");
        Console.WriteLine($"- Files to encrypt: {filesToEncrypt.Length}");
        Console.WriteLine($"- Files to skip: {skippedFiles.Length}");
        Console.WriteLine($"- Configuration: {folderSettings.GetEncryptionSummary()}");

        // Show which extensions are being encrypted
        if (folderSettings.UseCustomSelection && folderSettings.SelectedExtensions.Count > 0)
        {
            Console.WriteLine($"- Selected extensions: {string.Join(", ", folderSettings.SelectedExtensions)}");
        }

        foreach (var filePath in filesToEncrypt)
        {
            try
            {
                await EncryptFileAsync(filePath);
            }
            catch (Exception ex)
            {
                // Log but continue with other files
                Console.WriteLine($"Failed to encrypt {filePath}: {ex.Message}");
            }
        }

        // Log some examples of files that were skipped
        if (skippedFiles.Length > 0)
        {
            Console.WriteLine($"\nSkipped files (user did not select these extensions):");
            var skippedByExtension = skippedFiles
                .GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
                .OrderByDescending(g => g.Count())
                .Take(5);

            foreach (var group in skippedByExtension)
            {
                var examples = string.Join(", ", group.Take(2).Select(Path.GetFileName));
                Console.WriteLine($"- {group.Key} ({group.Count()} files): {examples}");
            }

            if (skippedByExtension.Count() > 5)
            {
                Console.WriteLine($"- ... and more extension types");
            }
        }
    }
}
