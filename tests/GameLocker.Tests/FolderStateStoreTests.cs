using GameLocker.Common.Models;
using GameLocker.Common.Security;
using GameLocker.Common.State;

namespace GameLocker.Tests;

/// <summary>
/// Tests for <see cref="FolderStateStore"/>, the record of what GameLocker changed and how to
/// undo it.
/// </summary>
/// <remarks>
/// This replaces the <c>.gamelocker</c> marker file, which carried no information, lived inside the
/// folder it described, and could be created or deleted by anyone.
/// See ARCHITECTURE-REVIEW.md findings [F-1] and [F-4].
/// </remarks>
public class FolderStateStoreTests : IDisposable
{
    private readonly string _root;
    private readonly FolderStateStore _store;

    public FolderStateStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"GameLocker_State_{Guid.NewGuid():N}");
        _store = new FolderStateStore(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Ignore cleanup failures.
        }

        GC.SuppressFinalize(this);
    }

    private static FolderStateRecord SampleRecord(string folderPath) => new()
    {
        FolderPath = folderPath,
        Mode = LockMode.AclOnly,
        LockedAtUtc = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc),
        AclApplied = true,
        Acl = new AclSnapshot
        {
            Sddl = "D:(A;OICI;FA;;;SY)",
            OwnerSid = "S-1-5-32-544",
            WasProtected = false,
            CapturedAtUtc = new DateTime(2026, 7, 26, 11, 59, 0, DateTimeKind.Utc),
        },
    };

    [Fact]
    [Trait("Category", "Basic")]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        var original = SampleRecord(@"C:\Games\Title");

        _store.Save(original);
        var loaded = _store.TryLoad(@"C:\Games\Title");

        Assert.NotNull(loaded);
        Assert.Equal(original.Mode, loaded.Mode);
        Assert.Equal(original.LockedAtUtc, loaded.LockedAtUtc);
        Assert.True(loaded.AclApplied);
        Assert.NotNull(loaded.Acl);
        Assert.Equal(original.Acl!.Sddl, loaded.Acl!.Sddl);
        Assert.Equal(original.Acl.OwnerSid, loaded.Acl.OwnerSid);
        Assert.Equal(FolderStateRecord.CurrentSchemaVersion, loaded.SchemaVersion);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void TryLoad_ReturnsNullWhenNoRecordExists()
    {
        Assert.Null(_store.TryLoad(@"C:\Games\NeverLocked"));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void PathSpellings_ResolveToTheSameRecord()
    {
        // NTFS paths are case-insensitive, and a trailing separator is not a different folder.
        _store.Save(SampleRecord(@"C:\Games\Title"));

        Assert.NotNull(_store.TryLoad(@"c:\games\title"));
        Assert.NotNull(_store.TryLoad(@"C:\Games\Title\"));
        Assert.NotNull(_store.TryLoad(@"C:\Games\Other\..\Title"));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void Save_CanonicalisesTheStoredPath()
    {
        var record = SampleRecord(@"C:\Games\Title\");

        _store.Save(record);

        Assert.Equal(@"C:\Games\Title", record.FolderPath);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void Save_OverwritesAnExistingRecord()
    {
        _store.Save(SampleRecord(@"C:\Games\Title"));

        var updated = SampleRecord(@"C:\Games\Title");
        updated.AclApplied = false;
        updated.LastError = "something went wrong";
        _store.Save(updated);

        var loaded = _store.TryLoad(@"C:\Games\Title");

        Assert.NotNull(loaded);
        Assert.False(loaded.AclApplied);
        Assert.Equal("something went wrong", loaded.LastError);
        Assert.Single(_store.LoadAll());
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void Save_LeavesNoTemporaryFileBehind()
    {
        _store.Save(SampleRecord(@"C:\Games\Title"));

        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void Delete_RemovesTheRecord()
    {
        _store.Save(SampleRecord(@"C:\Games\Title"));
        _store.Delete(@"C:\Games\Title");

        Assert.Null(_store.TryLoad(@"C:\Games\Title"));
        Assert.Empty(_store.LoadAll());
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void Delete_IsSafeWhenNoRecordExists()
    {
        _store.Delete(@"C:\Games\NeverLocked");
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void LoadAll_ReturnsEveryRecordAndSkipsUnreadableFiles()
    {
        _store.Save(SampleRecord(@"C:\Games\One"));
        _store.Save(SampleRecord(@"C:\Games\Two"));

        File.WriteAllText(Path.Combine(_root, "corrupt.json"), "{ this is not json");

        var all = _store.LoadAll();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, r => r.FolderPath == @"C:\Games\One");
        Assert.Contains(all, r => r.FolderPath == @"C:\Games\Two");
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void TryLoad_ReportsACorruptRecordAndKeepsTheFile()
    {
        var recordPath = _store.GetRecordPath(@"C:\Games\Title");
        Directory.CreateDirectory(_root);
        File.WriteAllText(recordPath, "{ truncated");

        Assert.Throws<IOException>(() => _store.TryLoad(@"C:\Games\Title"));

        // The file is the only description of what was done to the folder, so it must survive.
        Assert.True(File.Exists(recordPath));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void HasOutstandingChanges_ReflectsWhatIsLeftToUndo()
    {
        var record = SampleRecord(@"C:\Games\Title");
        Assert.True(record.HasOutstandingChanges);

        record.AclApplied = false;
        Assert.True(record.HasOutstandingChanges);

        record.Acl = null;
        Assert.False(record.HasOutstandingChanges);

        record.VaultFiles.Add(@"saves\slot1.sav");
        Assert.True(record.HasOutstandingChanges);
    }
}
