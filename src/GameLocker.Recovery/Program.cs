using GameLocker.Common.Configuration;
using GameLocker.Common.Models;
using GameLocker.Common.Security;
using GameLocker.Common.Services;
using GameLocker.Common.State;

namespace GameLocker.Recovery;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var command = args.Length == 0 ? "list" : args[0].ToLowerInvariant();
        if (command is "help" or "--help" or "-h" or "/?")
            return Help();

        if (!AclHelper.IsRunningAsAdmin())
        {
            Console.Error.WriteLine("Run GameLocker Recovery from an elevated prompt.");
            return 3;
        }

        var store = new FolderStateStore();
        var locker = new FolderLocker(ConfigManager.GetDefaultConfigDirectory(), store);

        try
        {
            return command switch
            {
                "list" or "--list" or "-l" when args.Length <= 1 => List(store),
                "restore" or "--restore" when args.Length == 2 => await RestoreAsync(locker, args[1]) ? 0 : 1,
                "restore-all" or "--restore-all" when args.Length == 1 => await RestoreAllAsync(locker),
                "restore-file" when args.Length == 2 => await RestoreFileAsync(locker, args[1]),
                _ => Help(exitCode: 2),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 4;
        }
    }

    private static int Help(int exitCode = 0)
    {
        Console.WriteLine("GameLocker Recovery");
        Console.WriteLine("Run from an elevated prompt and stop GameLocker Service before restoring scheduled folders.");
        Console.WriteLine("  GameLocker.Recovery list                 Show recorded folders and pending changes");
        Console.WriteLine("  GameLocker.Recovery restore <folder>     Restore one folder from its lock journal");
        Console.WriteLine("  GameLocker.Recovery restore-all          Restore folders with outstanding changes");
        Console.WriteLine("  GameLocker.Recovery restore-file <path>  Recover an explicitly selected legacy .enc file");
        Console.WriteLine("Legacy file recovery keeps the encrypted source. After verifying the restored file,");
        Console.WriteLine("move its encrypted copy to a backup folder before retrying the folder restore.");
        return exitCode;
    }

    private static int List(FolderStateStore store)
    {
        var errors = 0;
        var records = store.LoadAll(error =>
        {
            Console.Error.WriteLine(error);
            errors++;
        });

        Console.WriteLine($"State store: {store.StateDirectory}");
        foreach (var record in records.OrderBy(record => record.FolderPath))
        {
            Console.WriteLine(record.FolderPath);
            Console.WriteLine($"  Mode:             {record.Mode}");
            Console.WriteLine($"  Locked at (UTC):  {record.LockedAtUtc:u}");
            Console.WriteLine($"  ACL on disk:      {(FolderAclManager.IsLocked(record.FolderPath) ? "locked" : "lock ACL unavailable")}");
            Console.WriteLine($"  ACL snapshot:     {(record.Acl is { IsRestorable: true } ? "present" : "none")}");
            Console.WriteLine($"  Pending files:    {record.VaultFiles.Count}");

            if (record.TestLock)
                Console.WriteLine("  Temporary lock test");
            if (!string.IsNullOrWhiteSpace(record.LastError))
                Console.WriteLine($"  Last error:       {record.LastError}");
        }

        Console.WriteLine($"{records.Count(record => record.HasOutstandingChanges)} folder(s) have outstanding changes.");
        return errors == 0 ? 0 : 1;
    }

    private static async Task<int> RestoreAllAsync(FolderLocker locker)
    {
        var failed = 0;
        var records = locker.StateStore.LoadAll(error =>
        {
            Console.Error.WriteLine(error);
            failed++;
        });

        var configResult = await new ConfigManager().LoadConfigResultAsync();
        if (configResult.IsDamaged)
        {
            Console.Error.WriteLine($"Could not check the configuration for legacy lock markers. {configResult.Error}");
            failed++;
        }

        var config = configResult.Config;
        var legacyPaths = (config?.GameFolderPaths ?? []).Concat(config?.FoldersPendingRestore ?? [])
            .Where(path => PathGuard.TryValidate(path, out _) && locker.HasLockMarker(path));
        var folderPaths = records.Where(record => record.HasOutstandingChanges)
            .Select(record => record.FolderPath)
            .Concat(legacyPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in folderPaths)
        {
            if (!await RestoreAsync(locker, path))
                failed++;
        }

        Console.WriteLine($"{folderPaths.Count} folder(s) checked. {failed} recovery error(s).");
        return failed == 0 ? 0 : 1;
    }

    private static async Task<bool> RestoreAsync(FolderLocker locker, string folderPath)
    {
        try
        {
            var path = PathGuard.Validate(folderPath);
            if (locker.StateStore.TryLoad(path) is null && !locker.HasLockMarker(path))
            {
                Console.Error.WriteLine($"No GameLocker journal or legacy marker exists for '{path}'.");
                return false;
            }

            var result = await locker.UnlockFolderAsync(path);
            if (result.State == FolderState.Unlocked)
            {
                Console.WriteLine($"Restored: {path}");
                return true;
            }

            Console.Error.WriteLine($"Could not fully restore '{path}': {result.LastError}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not fully restore '{folderPath}': {ex.Message}");
        }

        return false;
    }

    private static async Task<int> RestoreFileAsync(FolderLocker locker, string encryptedPath)
    {
        await locker.DecryptSingleFileAsync(encryptedPath);
        Console.WriteLine($"Restored a copy of '{encryptedPath}'. The encrypted source was kept.");
        return 0;
    }
}

