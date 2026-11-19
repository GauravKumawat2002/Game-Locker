using System.Security.AccessControl;
using System.Security.Principal;
using GameLocker.Common.Configuration;
using GameLocker.Common.Encryption;
using GameLocker.Common.Models;
using GameLocker.Common.Security;
using GameLocker.Common.Services;
using GameLocker.Common.State;

namespace GameLocker.Tests;

public sealed class FolderLockerRegressionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"GameLocker_Regression_{Guid.NewGuid():N}");
    private readonly string _game;
    private readonly string _keys;
    private readonly FolderStateStore _store;
    private readonly FolderLocker _locker;
    private readonly AclSnapshot _initialAcl;

    public FolderLockerRegressionTests()
    {
        _game = Directory.CreateDirectory(Path.Combine(_root, "game")).FullName;
        _keys = Path.Combine(_root, "keys");
        _store = new FolderStateStore(Path.Combine(_root, "state"));
        _locker = new FolderLocker(_keys, _store);
        _initialAcl = FolderAclManager.Capture(_game);
    }

    public void Dispose()
    {
        if (!FolderAclManager.MatchesSnapshot(_game, _initialAcl))
            FolderAclManager.Restore(_game, _initialAcl);

        Directory.Delete(_root, recursive: true);
    }

    [RequiresAdminFact]
    [Trait("Category", "RequiresAdmin")]
    public async Task UnlockRestoresAnAclAppliedBeforeItsJournalWasUpdated()
    {
        _store.Save(new FolderStateRecord { FolderPath = _game, Acl = _initialAcl, AclApplied = false });
        FolderAclManager.ApplyLock(_game);

        Assert.Equal(FolderState.Unknown, _locker.GetFolderState(_game));
        Assert.Single(_locker.GetManagedFolders());
        Assert.Equal(FolderState.Unlocked, (await _locker.UnlockFolderAsync(_game)).State);
        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
        Assert.Null(_store.TryLoad(_game));
    }

    [RequiresAdminFact]
    [Trait("Category", "RequiresAdmin")]
    public async Task RelockRepairsLiveDriftWithoutReplacingTheOriginalSnapshot()
    {
        Assert.Equal(FolderState.Locked, (await _locker.LockFolderAsync(_game)).State);
        FolderAclManager.Restore(_game, _initialAcl);
        Assert.Equal(FolderState.Unknown, _locker.GetFolderState(_game));

        Assert.Equal(FolderState.Locked, (await _locker.LockFolderAsync(_game)).State);
        Assert.True(FolderAclManager.IsLocked(_game));
        Assert.Equal(_initialAcl.Sddl, _store.TryLoad(_game)!.Acl!.Sddl);
        Assert.Equal(FolderState.Unlocked, (await _locker.UnlockFolderAsync(_game)).State);
        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task PreviousLockSignatureCanBeRestoredFromItsJournal()
    {
        using var originalPermissions = new FolderOwnerRightsTests.SavedAcl(_game);
        _store.Save(new FolderStateRecord { FolderPath = _game, Acl = _initialAcl, AclApplied = true, AclTreeValidated = true });
        new DirectoryInfo(_game).SetAccessControl(FolderOwnerRightsTests.CreatePreviousLockSecurity());

        Assert.Equal(FolderState.Unknown, _locker.GetFolderState(_game));
        Assert.Equal(FolderState.Unlocked, (await _locker.UnlockFolderAsync(_game)).State);
        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
        Assert.Null(_store.TryLoad(_game));
    }

    [RequiresAdminFact]
    [Trait("Category", "RequiresAdmin")]
    public async Task RelockUpgradesThePreviousSignatureAndKeepsItsJournal()
    {
        using var originalPermissions = new FolderOwnerRightsTests.SavedAcl(_game);
        _store.Save(new FolderStateRecord { FolderPath = _game, Acl = _initialAcl, AclApplied = true, AclTreeValidated = true });
        new DirectoryInfo(_game).SetAccessControl(FolderOwnerRightsTests.CreatePreviousLockSecurity());

        Assert.Equal(FolderState.Unknown, _locker.GetFolderState(_game));
        Assert.Equal(FolderState.Locked, (await _locker.LockFolderAsync(_game)).State);
        Assert.True(FolderAclManager.IsLocked(_game));
        Assert.Equal(_initialAcl.Sddl, _store.TryLoad(_game)!.Acl!.Sddl);

        Assert.Equal(FolderState.Unlocked, (await _locker.UnlockFolderAsync(_game)).State);
        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task LockRefusesAProtectedChildBeforeChangingPermissions()
    {
        var child = Directory.CreateDirectory(Path.Combine(_game, "separate permissions"));
        var security = child.GetAccessControl(AccessControlSections.Access);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
        child.SetAccessControl(security);
        var before = FolderAclManager.Capture(child.FullName);

        var result = await _locker.LockFolderAsync(_game);

        Assert.Equal(FolderState.Unknown, result.State);
        Assert.Contains("separate permissions", result.LastError);
        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
        Assert.True(FolderAclManager.MatchesSnapshot(child.FullName, before));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task LockRefusesAFileWithItsOwnBroadAllowRule()
    {
        var child = new FileInfo(Path.Combine(_game, "fixture.sav"));
        File.WriteAllText(child.FullName, "fixture data");
        var security = child.GetAccessControl(AccessControlSections.Access);
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            FileSystemRights.Read, AccessControlType.Allow));
        child.SetAccessControl(security);
        var before = child.GetAccessControl(AccessControlSections.Access).GetSecurityDescriptorSddlForm(AccessControlSections.Access);

        var result = await _locker.LockFolderAsync(_game);

        Assert.Equal(FolderState.Unknown, result.State);
        Assert.Contains("fixture.sav", result.LastError);
        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
        Assert.Equal(before, child.GetAccessControl(AccessControlSections.Access).GetSecurityDescriptorSddlForm(AccessControlSections.Access));
        Assert.Equal("fixture data", File.ReadAllText(child.FullName));
    }

    [RequiresAdminFact]
    [Trait("Category", "RequiresAdmin")]
    public async Task LockSupportsAnInheritingChildAndRestoresItsAccess()
    {
        var child = Directory.CreateDirectory(Path.Combine(_game, "inheriting child"));
        var original = Path.Combine(child.FullName, "fixture.sav");
        File.WriteAllText(original, "fixture data");

        Assert.Equal(FolderState.Locked, (await _locker.LockFolderAsync(_game)).State);
        Assert.Equal(FolderState.Locked, _locker.GetFolderState(_game));
        Assert.Equal(FolderState.Unlocked, (await _locker.UnlockFolderAsync(_game)).State);
        Assert.Equal("fixture data", File.ReadAllText(original));
    }

    [RequiresAdminFact]
    [Trait("Category", "RequiresAdmin")]
    public void TestLockRestoresPermissionsWhenTheCallerThrows()
    {
        Assert.Throws<InvalidOperationException>(() => _locker.TestLockFolder(_game, () =>
        {
            Assert.True(FolderAclManager.IsLocked(_game));
            Assert.True(_store.TryLoad(_game)!.TestLock);
            throw new InvalidOperationException("Synthetic test interruption.");
        }));

        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
        Assert.Null(_store.TryLoad(_game));
    }

    [RequiresAdminFact]
    [Trait("Category", "RequiresAdmin")]
    public async Task ASecondOperationCannotReplaceAnActiveTestJournal()
    {
        Task<GameFolderInfo>? attempt = null;
        _locker.TestLockFolder(_game, () =>
        {
            var secondLocker = new FolderLocker(_keys, _store);
            attempt = secondLocker.UnlockFolderAsync(_game);
            Assert.True(attempt.IsCompletedSuccessfully);
            Assert.True(_store.TryLoad(_game)!.TestLock);
            Assert.True(FolderAclManager.IsLocked(_game));
        });

        Assert.NotNull(attempt);
        var result = await attempt;
        Assert.Equal(FolderState.Unknown, result.State);
        Assert.NotNull(result.LastError);
        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
        Assert.Null(_store.TryLoad(_game));
    }

    [RequiresAdminFact]
    [Trait("Category", "RequiresAdmin")]
    public async Task AclRecoveryIgnoresDamagedVaultKeys()
    {
        Directory.CreateDirectory(_keys);
        File.WriteAllText(Path.Combine(_keys, "folder_key.dat"), "damaged fixture");

        Assert.Equal(FolderState.Locked, (await _locker.LockFolderAsync(_game)).State);
        Assert.Equal(FolderState.Unlocked, (await _locker.UnlockFolderAsync(_game)).State);
        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
    }

    [RequiresAdminFact]
    [Trait("Category", "RequiresAdmin")]
    public async Task AclRecoveryRunsEvenWhenAPendingVaultFileCannotBeRestored()
    {
        _store.Save(new FolderStateRecord
        {
            FolderPath = _game,
            Acl = _initialAcl,
            AclApplied = true,
            VaultFiles = ["missing.sav"],
        });
        FolderAclManager.ApplyLock(_game);

        var result = await _locker.UnlockFolderAsync(_game);

        Assert.Equal(FolderState.Unknown, result.State);
        Assert.Contains("missing.sav", result.LastError);
        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
        Assert.Equal(["missing.sav"], _store.TryLoad(_game)!.VaultFiles);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task CorruptJournalIsNeverOverwrittenByALockAttempt()
    {
        var path = _store.GetRecordPath(_game);
        Directory.CreateDirectory(_store.StateDirectory);
        File.WriteAllText(path, "{ incomplete");

        var result = await _locker.LockFolderAsync(_game);

        Assert.Equal(FolderState.Unknown, result.State);
        Assert.Contains("journal", result.LastError);
        Assert.Equal("{ incomplete", File.ReadAllText(path));
        Assert.True(FolderAclManager.MatchesSnapshot(_game, _initialAcl));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task UnmanagedEncryptedExtensionDoesNotTriggerLegacyRecovery()
    {
        var path = Path.Combine(_game, "asset.enc");
        File.WriteAllText(path, "game-owned data");

        Assert.Equal(FolderState.Unlocked, _locker.GetFolderState(_game));
        Assert.Equal(FolderState.Unlocked, (await _locker.UnlockFolderAsync(_game)).State);
        Assert.Equal("game-owned data", File.ReadAllText(path));
        Assert.False(Directory.Exists(_keys));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task VaultCollisionKeepsBothCopies()
    {
        var original = Path.Combine(_game, "slot.sav");
        File.WriteAllText(original, "save");
        File.WriteAllText(original + ".enc", "existing file");

        var manifest = await _locker.EncryptFolderWithCustomExtensionsAsync(_game, SaveFiles());

        Assert.Empty(manifest);
        Assert.Equal("save", File.ReadAllText(original));
        Assert.Equal("existing file", File.ReadAllText(original + ".enc"));
        Assert.Contains("conflicting", _store.TryLoad(_game)!.LastError);
    }

    [Theory]
    [Trait("Category", "Basic")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingVaultKeysAreNotReplacedWhileAnotherFolderHasPendingFiles(bool cachedKeys)
    {
        if (cachedKeys)
        {
            await _locker.InitializeAsync();
            File.Delete(Path.Combine(_keys, "folder_key.dat"));
            File.Delete(Path.Combine(_keys, "folder_iv.dat"));
        }

        var earlierFolder = Directory.CreateDirectory(Path.Combine(_root, "earlier game")).FullName;
        _store.Save(new FolderStateRecord
        {
            FolderPath = earlierFolder,
            Mode = LockMode.AclAndVault,
            VaultFiles = ["earlier.sav"],
        });
        var earlierCiphertext = Path.Combine(earlierFolder, "earlier.sav.enc");
        File.WriteAllText(earlierCiphertext, "previous ciphertext fixture");
        var original = Path.Combine(_game, "new.sav");
        File.WriteAllText(original, "new save fixture");

        Assert.Empty(await _locker.EncryptFolderWithCustomExtensionsAsync(_game, SaveFiles()));

        Assert.False(File.Exists(Path.Combine(_keys, "folder_key.dat")));
        Assert.False(File.Exists(Path.Combine(_keys, "folder_iv.dat")));
        Assert.Equal("previous ciphertext fixture", File.ReadAllText(earlierCiphertext));
        Assert.Equal("new save fixture", File.ReadAllText(original));
        Assert.False(File.Exists(original + ".enc"));
        Assert.Contains("Restore the original vault keys", _store.TryLoad(_game)!.LastError);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task MissingVaultKeysAreNotReplacedWhileAnotherJournalIsUnreadable()
    {
        var earlierFolder = Directory.CreateDirectory(Path.Combine(_root, "earlier game")).FullName;
        Directory.CreateDirectory(_store.StateDirectory);
        var unreadableJournal = _store.GetRecordPath(earlierFolder);
        File.WriteAllText(unreadableJournal, "{ incomplete");
        var original = Path.Combine(_game, "new.sav");
        File.WriteAllText(original, "new save fixture");

        Assert.Empty(await _locker.EncryptFolderWithCustomExtensionsAsync(_game, SaveFiles()));

        Assert.False(Directory.Exists(_keys));
        Assert.Equal("{ incomplete", File.ReadAllText(unreadableJournal));
        Assert.Equal("new save fixture", File.ReadAllText(original));
        Assert.False(File.Exists(original + ".enc"));
        Assert.Contains("Restore the original vault keys", _store.TryLoad(_game)!.LastError);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task VaultRecoveryAfterRestartRestoresMatchingHashesAndKeepsUnselectedFiles()
    {
        var original = Path.Combine(_game, "slot.sav");
        var native = Path.Combine(_game, "game.dat");
        File.WriteAllText(original, "save");
        File.WriteAllText(native, "asset");

        var manifest = await _locker.EncryptFolderWithCustomExtensionsAsync(_game, SaveFiles());
        Assert.Equal(["slot.sav"], manifest);
        Assert.False(File.Exists(original));
        Assert.True(File.Exists(original + ".enc"));
        Assert.Equal("asset", File.ReadAllText(native));
        Assert.True(_store.TryLoad(_game)!.VaultCiphertextHashes.ContainsKey("slot.sav"));

        var restarted = new FolderLocker(_keys, _store);
        Assert.Equal(FolderState.Unlocked, (await restarted.UnlockFolderAsync(_game)).State);
        Assert.Equal("save", File.ReadAllText(original));
        Assert.False(File.Exists(original + ".enc"));
        Assert.Null(_store.TryLoad(_game));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task VaultKeepsOpenFilesUntilTheirReadersReleaseThem()
    {
        var original = Path.Combine(_game, "checkpoint.sav");
        var ciphertext = original + ".enc";
        var expected = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        File.WriteAllBytes(original, expected);

        using (File.Open(original, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            Assert.Empty(await _locker.EncryptFolderWithCustomExtensionsAsync(_game, SaveFiles()));
            Assert.Equal(expected, File.ReadAllBytes(original));
            Assert.False(File.Exists(ciphertext));
        }

        Assert.Equal(["checkpoint.sav"], await _locker.EncryptFolderWithCustomExtensionsAsync(_game, SaveFiles()));
        var encrypted = File.ReadAllBytes(ciphertext);

        using (File.Open(ciphertext, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            var busy = await _locker.UnlockFolderAsync(_game);
            Assert.NotEqual(FolderState.Unlocked, busy.State);
            Assert.Equal(encrypted, File.ReadAllBytes(ciphertext));
            Assert.False(File.Exists(original));
        }

        Assert.Equal(FolderState.Unlocked, (await _locker.UnlockFolderAsync(_game)).State);
        Assert.Equal(expected, File.ReadAllBytes(original));
        Assert.False(File.Exists(ciphertext));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task VaultRecoveryKeepsAnExistingDifferentOriginalAndItsCiphertext()
    {
        var original = Path.Combine(_game, "slot.sav");
        File.WriteAllText(original, "save");
        await _locker.EncryptFolderWithCustomExtensionsAsync(_game, SaveFiles());
        var encrypted = File.ReadAllBytes(original + ".enc");
        File.WriteAllText(original, "new original");

        var result = await _locker.UnlockFolderAsync(_game);

        Assert.Contains("differs", result.LastError);
        Assert.Equal("new original", File.ReadAllText(original));
        Assert.Equal(encrypted, File.ReadAllBytes(original + ".enc"));
        Assert.Equal(["slot.sav"], _store.TryLoad(_game)!.VaultFiles);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task VaultRetryRecognizesACrashAfterPublishingTheCiphertext()
    {
        var original = Path.Combine(_game, "slot.sav");
        File.WriteAllText(original, "save");
        await _locker.EncryptFolderWithCustomExtensionsAsync(_game, SaveFiles());
        File.WriteAllText(original, "save");

        Assert.Equal(["slot.sav"], await _locker.EncryptFolderWithCustomExtensionsAsync(_game, SaveFiles()));
        Assert.False(File.Exists(original));
        Assert.True(File.Exists(original + ".enc"));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task VaultRecoveryKeepsBothCopiesWhenIntegrityVerificationFails()
    {
        var original = Path.Combine(_game, "slot.sav");
        File.WriteAllText(original, "save");
        await _locker.EncryptFolderWithCustomExtensionsAsync(_game, SaveFiles());

        File.WriteAllText(original + ".enc", "corrupt ciphertext");
        var result = await _locker.UnlockFolderAsync(_game);

        Assert.Contains("integrity", result.LastError);
        Assert.False(File.Exists(original));
        Assert.Equal("corrupt ciphertext", File.ReadAllText(original + ".enc"));
        Assert.Equal(["slot.sav"], _store.TryLoad(_game)!.VaultFiles);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task LegacyRecoveryKeepsItsUnauthenticatedEncryptedSource()
    {
        await _locker.InitializeAsync();
        var key = await DpapiHelper.UnprotectFromFileAsync(Path.Combine(_keys, "folder_key.dat"));
        var iv = await DpapiHelper.UnprotectFromFileAsync(Path.Combine(_keys, "folder_iv.dat"));
        var original = Path.Combine(_game, "legacy.sav");
        var bytes = "legacy save"u8.ToArray();
        var encrypted = AesEncryptionHelper.Encrypt(bytes, key, iv);
        File.WriteAllBytes(original + ".enc", encrypted);

        await _locker.DecryptSingleFileAsync(original + ".enc");

        Assert.Equal(bytes, File.ReadAllBytes(original));
        Assert.Equal(encrypted, File.ReadAllBytes(original + ".enc"));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task MissingConfigSidecarsAreReportedAndTheOriginalFileIsPreserved()
    {
        var path = Directory.CreateDirectory(Path.Combine(_root, "config")).FullName;
        var manager = new ConfigManager(path);
        File.WriteAllText(manager.ConfigFilePath, "fixture config with missing keys");

        Assert.Equal(ConfigLoadStatus.Unreadable, (await manager.LoadConfigResultAsync()).Status);
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.SaveConfigAsync(new GameLockerConfig()));
        Assert.Equal("fixture config with missing keys", File.ReadAllText(manager.ConfigFilePath));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public async Task EmptyScheduleSavesAndRetainsThePreviousSettingsAsBackup()
    {
        var manager = new ConfigManager(Path.Combine(_root, "config"));
        await manager.SaveConfigAsync(new GameLockerConfig { GameFolderPaths = [_game] });
        await manager.SaveConfigAsync(new GameLockerConfig { FoldersPendingRestore = [_game] });

        var result = await manager.LoadConfigResultAsync();

        Assert.True(result.IsLoaded);
        Assert.Empty(result.Config!.GameFolderPaths);
        Assert.Equal([_game], result.Config.FoldersPendingRestore);
        Assert.True(File.Exists(manager.BackupFilePath));
    }

    private static FolderEncryptionSettings SaveFiles() => new()
    {
        SelectedExtensions = [".sav", ".dat"],
    };
}
