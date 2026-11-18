using System.Security.Cryptography;
using GameLocker.Common.Encryption;
using GameLocker.Common.Models;
using GameLocker.Common.Security;
using GameLocker.Common.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameLocker.Common.Services;

/// <summary>Locks and restores folders using a durable ACL and vault journal.</summary>
public class FolderLocker
{
    private readonly string _keyStorePath;
    private readonly FolderStateStore _stateStore;
    private readonly ILogger _logger;
    private byte[]? _masterKey;
    private byte[]? _masterIV;

    public FolderLocker(string keyStorePath, ILogger? logger = null)
        : this(keyStorePath, new FolderStateStore(), logger)
    {
    }

    public FolderLocker(string keyStorePath, FolderStateStore stateStore, ILogger? logger = null)
    {
        _keyStorePath = keyStorePath;
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _logger = logger ?? NullLogger.Instance;
    }

    public FolderStateStore StateStore => _stateStore;

    /// <summary>Loads existing vault keys, or creates them before encrypting a first file.</summary>
    public Task InitializeAsync() => LoadVaultKeysAsync(create: true);

    private async Task LoadVaultKeysAsync(bool create)
    {
        if (_masterKey is not null && _masterIV is not null)
            return;

        // Multiple folder operations can start with a single, shared key directory.
        using var operation = _stateStore.BeginOperation(_keyStorePath);
        var keyPath = Path.Combine(_keyStorePath, "folder_key.dat");
        var ivPath = Path.Combine(_keyStorePath, "folder_iv.dat");
        PathGuard.EnsureNoReparsePoints(keyPath);
        PathGuard.EnsureNoReparsePoints(ivPath);
        var hasKey = File.Exists(keyPath);
        var hasIv = File.Exists(ivPath);

        if (!hasKey && !hasIv && create)
        {
            Directory.CreateDirectory(_keyStorePath);
            await DpapiHelper.ProtectToFileAsync(AesEncryptionHelper.GenerateKey(), keyPath);
            await DpapiHelper.ProtectToFileAsync(AesEncryptionHelper.GenerateIV(), ivPath);
        }
        else if (!hasKey || !hasIv)
        {
            throw new IOException("The vault key or IV is missing. Restore the original key files before retrying.");
        }

        var key = await DpapiHelper.UnprotectFromFileAsync(keyPath);
        var iv = await DpapiHelper.UnprotectFromFileAsync(ivPath);
        if (key.Length != 32 || iv.Length != 16)
            throw new InvalidDataException("The vault key or IV has an invalid length.");

        _masterKey = key;
        _masterIV = iv;
    }

    public async Task<GameFolderInfo> LockFolderAsync(
        string folderPath,
        LockMode mode = LockMode.AclOnly,
        FolderEncryptionSettings? vaultSettings = null)
    {
        var info = new GameFolderInfo { Path = folderPath, State = FolderState.Unknown };
        FolderStateRecord? record = null;
        FileStream? operation = null;

        try
        {
            var path = info.Path = PathGuard.Validate(folderPath);
            operation = _stateStore.BeginOperation(path);
            record = _stateStore.TryLoad(path);
            if (record is { TestLock: true })
            {
                RestorePermissions(record);
                _stateStore.Delete(path);
                record = null;
            }

            if (record is { AclApplied: true } && record.Acl is not { IsRestorable: true })
                throw new InvalidDataException("The original permissions are missing from the lock journal.");

            if (record is null)
            {
                if (HasLockMarker(path))
                    throw new InvalidOperationException("Restore the legacy GameLocker lock before starting a new lock.");

                record = new FolderStateRecord { FolderPath = path, LockedAtUtc = DateTime.UtcNow };
            }

            record.Acl ??= FolderAclManager.Capture(path);
            record.Mode = record.HasOutstandingChanges && record.LockInProgress ? record.Mode : mode;
            record.LockInProgress = true;
            _stateStore.Save(record);

            if (!FolderAclManager.IsLocked(path))
                FolderAclManager.ApplyLock(path);

            record.AclApplied = true;
            if (record.Mode == LockMode.AclAndVault && vaultSettings is not null)
                await EncryptSelectedFilesAsync(record, vaultSettings);

            record.LockInProgress = false;
            record.LastError = null;
            _stateStore.Save(record);
            SetFinished(info, FolderState.Locked);
        }
        catch (Exception ex)
        {
            info.LastError = ex.Message;
            RecordError(record, ex.Message);
        }
        finally
        {
            operation?.Dispose();
        }

        return info;
    }

    /// <summary>Runs a temporary ACL test while holding the same journal as scheduled operations.</summary>
    public void TestLockFolder(string folderPath, Action onLocked)
    {
        ArgumentNullException.ThrowIfNull(onLocked);
        var path = PathGuard.Validate(folderPath);
        using var operation = _stateStore.BeginOperation(path);

        if (_stateStore.TryLoad(path) is { HasOutstandingChanges: true } || HasLockMarker(path))
        {
            throw new InvalidOperationException("This folder already has an outstanding GameLocker lock.");
        }

        var record = new FolderStateRecord
        {
            FolderPath = path,
            Acl = FolderAclManager.Capture(path),
            LockedAtUtc = DateTime.UtcNow,
            LockInProgress = true,
            TestLock = true,
        };
        _stateStore.Save(record);

        try
        {
            FolderAclManager.ApplyLock(path);
            onLocked();
        }
        finally
        {
            try
            {
                RestorePermissions(record);
                _stateStore.Delete(path);
            }
            catch (Exception ex)
            {
                RecordError(record, ex.Message);
                throw;
            }
        }
    }

    public async Task<GameFolderInfo> UnlockFolderAsync(string folderPath)
    {
        var info = new GameFolderInfo { Path = folderPath };

        try
        {
            var path = info.Path = PathGuard.Validate(folderPath);
            using var operation = _stateStore.BeginOperation(path);
            var record = _stateStore.TryLoad(path);
            if (record is null)
            {
                UnlockLegacyFolder(path);
                SetFinished(info, FolderState.Unlocked);
                return info;
            }

            var failures = new List<string>();
            try
            {
                if (record.VaultFiles.Count > 0)
                    await RestoreVaultFilesAsync(record, failures);
            }
            catch (Exception ex)
            {
                failures.Add(ex.Message);
            }

            try
            {
                RestorePermissions(record);
            }
            catch (Exception ex)
            {
                failures.Add($"Could not restore permissions: {ex.Message}");
            }

            if (failures.Count == 0 && !record.HasOutstandingChanges)
            {
                _stateStore.Delete(path);
                SetFinished(info, FolderState.Unlocked);
            }
            else
            {
                info.LastError = record.LastError = string.Join(" ", failures);
                _stateStore.Save(record);
            }
        }
        catch (Exception ex)
        {
            info.LastError = ex.Message;
        }

        return info;
    }

    private static void RestorePermissions(FolderStateRecord record)
    {
        if (record.Acl is null && !record.AclApplied)
            return;

        if (record.Acl is not { IsRestorable: true } snapshot)
            throw new InvalidDataException("The original permissions are missing from the lock journal.");

        if (!FolderAclManager.MatchesSnapshot(record.FolderPath, snapshot))
            FolderAclManager.Restore(record.FolderPath, snapshot);

        if (!FolderAclManager.MatchesSnapshot(record.FolderPath, snapshot))
            throw new IOException("The folder permissions do not match the original journal snapshot.");

        record.Acl = null;
        record.AclApplied = false;
    }

    public FolderState GetFolderState(string folderPath)
    {
        try
        {
            if (!PathGuard.TryValidate(folderPath, out var guard))
                return FolderState.Unknown;

            var path = guard.Path;
            var record = _stateStore.TryLoad(path);
            if (record is { HasOutstandingChanges: true })
            {
                return !record.LockInProgress && !record.TestLock &&
                    record.AclApplied && FolderAclManager.IsLocked(path)
                    ? FolderState.Locked
                    : FolderState.Unknown;
            }

            if (HasLockMarker(path))
                return AclHelper.IsAccessDenied(path) ? FolderState.Locked : FolderState.Unknown;

            return FolderState.Unlocked;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not inspect lock state for {Folder}", folderPath);
            return FolderState.Unknown;
        }
    }

    public IReadOnlyList<FolderStateRecord> GetManagedFolders() =>
        _stateStore.LoadAll(error => _logger.LogWarning("{JournalError}", error))
            .Where(record => record.HasOutstandingChanges)
            .OrderByDescending(record => record.LockedAtUtc)
            .ToList();

    private void RecordError(FolderStateRecord? record, string message)
    {
        if (record is null)
            return;

        record.LastError = message;
        try
        {
            _stateStore.Save(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not update the lock journal for {Folder}", record.FolderPath);
        }
    }

    public bool HasLockMarker(string folderPath) =>
        File.Exists(Path.Combine(folderPath, ".gamelocker"));

    private void UnlockLegacyFolder(string folderPath)
    {
        if (!HasLockMarker(folderPath))
            return;

        var marker = Path.Combine(folderPath, ".gamelocker");
        PathGuard.EnsureNoReparsePoints(marker);
        AclHelper.RemoveLegacyGameLockerDenies(folderPath);

        if (Directory.EnumerateFiles(folderPath, "*.enc", PathGuard.FileEnumerationOptions(recursive: true)).Any())
        {
            throw new InvalidOperationException(
                "The folder has legacy .enc files with no vault journal. Use GameLocker.Recovery restore-file to recover each known GameLocker file.");
        }

        File.Delete(marker);
    }

    /// <summary>Recovers an explicitly selected legacy file and keeps its unauthenticated source.</summary>
    public async Task DecryptSingleFileAsync(string encryptedPath)
    {
        if (!encryptedPath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select a .enc file.", nameof(encryptedPath));

        var path = Path.GetFullPath(encryptedPath);
        PathGuard.Validate(Path.GetDirectoryName(path));
        await LoadVaultKeysAsync(create: false);
        RestoreFile(path, null);
    }

    public Task<GameFolderInfo> LockFolderWithCustomExtensionsAsync(
        string folderPath, FolderEncryptionSettings folderSettings) =>
        LockFolderAsync(folderPath, LockMode.AclAndVault, folderSettings);

    /// <summary>Encrypts explicit selections and journals them without applying an ACL lock.</summary>
    public async Task<List<string>> EncryptFolderWithCustomExtensionsAsync(
        string folderPath, FolderEncryptionSettings folderSettings)
    {
        ArgumentNullException.ThrowIfNull(folderSettings);
        var path = PathGuard.Validate(folderPath);
        using var operation = _stateStore.BeginOperation(path);
        var record = _stateStore.TryLoad(path) ?? new FolderStateRecord
        {
            FolderPath = path,
            Mode = LockMode.AclAndVault,
            LockedAtUtc = DateTime.UtcNow,
        };

        try
        {
            await EncryptSelectedFilesAsync(record, folderSettings);
        }
        catch (Exception ex)
        {
            RecordError(record, ex.Message);
        }

        return record.VaultFiles.ToList();
    }

    private async Task EncryptSelectedFilesAsync(FolderStateRecord record, FolderEncryptionSettings settings)
    {
        var folderPath = record.FolderPath;
        var files = Directory.EnumerateFiles(folderPath, "*", PathGuard.FileEnumerationOptions(recursive: true))
            .Where(file =>
            {
                var name = Path.GetFileName(file);
                return !string.Equals(name, ".gamelocker", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith(".enc", StringComparison.OrdinalIgnoreCase) && settings.ShouldEncryptFile(name);
            })
            .ToArray();

        if (files.Length == 0)
            return;

        await LoadVaultKeysAsync(create: record.VaultFiles.Count == 0);
        var failures = new List<string>();
        foreach (var filePath in files)
        {
            try
            {
                EncryptFile(record, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not encrypt {File}", filePath);
                failures.Add($"{Path.GetRelativePath(folderPath, filePath)}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
            throw new IOException(string.Join(" ", failures));
    }

    private void EncryptFile(FolderStateRecord record, string originalPath)
    {
        var encryptedPath = originalPath + ".enc";
        var relativePath = Path.GetRelativePath(record.FolderPath, originalPath);
        PathGuard.EnsureNoReparsePoints(originalPath);
        PathGuard.EnsureNoReparsePoints(encryptedPath);

        using var original = new FileStream(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (File.Exists(encryptedPath))
        {
            if (record.VaultCiphertextHashes.TryGetValue(relativePath, out var expectedHash) &&
                CiphertextMatchesOriginal(encryptedPath, expectedHash, original))
            {
                File.Delete(originalPath);
                return;
            }

            throw new IOException("A conflicting .enc file already exists. Both copies were kept.");
        }

        var tempPath = encryptedPath + $".{Guid.NewGuid():N}.gamelocker-tmp";
        try
        {
            string hash;
            using (var destination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                AesEncryptionHelper.EncryptStream(original, destination, _masterKey!, _masterIV!, leaveOpen: true);
                destination.Flush(flushToDisk: true);
                destination.Position = 0;
                hash = Convert.ToHexString(SHA256.HashData(destination));
            }

            if (!record.VaultFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
                record.VaultFiles.Add(relativePath);

            record.VaultCiphertextHashes[relativePath] = hash;
            _stateStore.Save(record);

            File.Move(tempPath, encryptedPath, overwrite: false);
            File.Delete(originalPath);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private bool CiphertextMatchesOriginal(string encryptedPath, string expectedHash, Stream original)
    {
        using var ciphertext = File.OpenRead(encryptedPath);
        VerifyCiphertext(ciphertext, expectedHash);
        using var plainHash = SHA256.Create();
        using var hashStream = new CryptoStream(Stream.Null, plainHash, CryptoStreamMode.Write);
        AesEncryptionHelper.DecryptStream(ciphertext, hashStream, _masterKey!, _masterIV!, leaveOpen: true);
        hashStream.FlushFinalBlock();
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(original), plainHash.Hash!);
    }

    private async Task RestoreVaultFilesAsync(FolderStateRecord record, List<string> failures)
    {
        foreach (var relativePath in record.VaultFiles.ToArray())
        {
            try
            {
                var originalPath = ResolveVaultPath(record.FolderPath, relativePath);
                var encryptedPath = originalPath + ".enc";
                PathGuard.EnsureNoReparsePoints(encryptedPath);

                if (File.Exists(encryptedPath))
                {
                    await LoadVaultKeysAsync(create: false);
                    record.VaultCiphertextHashes.TryGetValue(relativePath, out var expectedHash);
                    RestoreFile(encryptedPath, expectedHash);
                }
                else if (!File.Exists(originalPath))
                {
                    throw new FileNotFoundException("Both the original and encrypted copy are missing.");
                }

                record.VaultFiles.Remove(relativePath);
                record.VaultCiphertextHashes.Remove(relativePath);
            }
            catch (Exception ex)
            {
                failures.Add($"Could not restore '{relativePath}': {ex.Message}");
            }
        }
    }

    private static string ResolveVaultPath(string folderPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Contains(':'))
        {
            throw new InvalidDataException("The vault journal entry is not a relative file path.");
        }

        var path = PathGuard.Normalize(Path.Combine(folderPath, relativePath));
        if (!PathGuard.IsAtOrUnder(path, folderPath) || path.Equals(folderPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The vault journal entry is outside the selected folder.");

        PathGuard.EnsureNoReparsePoints(path);
        return path;
    }

    private void RestoreFile(string encryptedPath, string? expectedHash)
    {
        var originalPath = encryptedPath[..^4];
        PathGuard.EnsureNoReparsePoints(originalPath);
        PathGuard.EnsureNoReparsePoints(encryptedPath);
        var tempPath = originalPath + $".{Guid.NewGuid():N}.gamelocker-tmp";

        try
        {
            using var source = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (expectedHash is not null)
                VerifyCiphertext(source, expectedHash);

            using var existing = File.Exists(originalPath) ? File.OpenRead(originalPath) : null;
            using (var destination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                AesEncryptionHelper.DecryptStream(source, destination, _masterKey!, _masterIV!, leaveOpen: true);
                destination.Flush(flushToDisk: true);

                if (existing is not null)
                {
                    if (expectedHash is null)
                        throw new IOException("An original file already exists. Both copies were kept.");

                    destination.Position = 0;
                    if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), SHA256.HashData(destination)))
                        throw new IOException("The restored data differs from an existing original. Both copies were kept.");
                }
            }

            if (existing is null)
                File.Move(tempPath, originalPath, overwrite: false);

            // Legacy ciphertext lacks an integrity digest, so it remains available for recovery.
            if (expectedHash is not null)
                File.Delete(encryptedPath);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static void VerifyCiphertext(Stream source, string expectedHash)
    {
        var actualHash = Convert.ToHexString(SHA256.HashData(source));
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The encrypted copy does not match its journal integrity digest.");

        source.Position = 0;
    }

    private static void SetFinished(GameFolderInfo info, FolderState state)
    {
        info.State = state;
        info.LastStateChange = DateTime.Now;
    }
}

