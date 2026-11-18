using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using GameLocker.Common.Security;

namespace GameLocker.Common.State;

/// <summary>
/// Persists one <see cref="FolderStateRecord"/> per managed folder, outside the folder itself.
/// </summary>
/// <remarks>
/// Records live outside the folders they describe for two reasons: a locked folder is by definition
/// inaccessible, and a record kept inside it could be deleted by the person the lock is meant to
/// restrain.
/// <para>
/// The directory is given an explicit access control list granting only SYSTEM and Administrators.
/// The installer previously granted every user full control of GameLocker's data directory, which
/// made the encryption key deletable and the service's command channel writable by anyone.
/// See ARCHITECTURE-REVIEW.md findings [V-1], [V-2] and [H-3].
/// </para>
/// </remarks>
public sealed class FolderStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _stateDirectory;
    private readonly bool _hardenPermissions;

    /// <summary>
    /// Creates a store rooted at the default location under ProgramData, restricted to SYSTEM and
    /// Administrators.
    /// </summary>
    public FolderStateStore() : this(GetDefaultStateDirectory(), hardenPermissions: true)
    {
    }

    /// <summary>
    /// Creates a store rooted at a specific directory, without changing its permissions.
    /// </summary>
    /// <param name="stateDirectory">The directory to hold state records.</param>
    public FolderStateStore(string stateDirectory) : this(stateDirectory, hardenPermissions: false)
    {
    }

    /// <summary>
    /// Creates a store rooted at a specific directory.
    /// </summary>
    /// <param name="stateDirectory">The directory to hold state records.</param>
    /// <param name="hardenPermissions">
    /// Whether to restrict the directory to SYSTEM and Administrators. Only appropriate for the
    /// real store: applying it to an arbitrary directory would lock the calling process out of a
    /// folder it needs to read.
    /// </param>
    public FolderStateStore(string stateDirectory, bool hardenPermissions)
    {
        _stateDirectory = stateDirectory;
        _hardenPermissions = hardenPermissions;
    }

    /// <summary>
    /// Gets the directory holding the state records.
    /// </summary>
    public string StateDirectory => _stateDirectory;

    /// <summary>
    /// Gets the default state directory, <c>%ProgramData%\GameLocker\state</c>.
    /// </summary>
    /// <returns>The default state directory path.</returns>
    public static string GetDefaultStateDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "GameLocker",
            "state");

    /// <summary>
    /// Creates the state directory if needed and restricts it to SYSTEM and Administrators.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the directory exists and carries the restrictive access control list;
    /// <c>false</c> when it exists but could not be secured, which happens when the caller is not
    /// an Administrator.
    /// </returns>
    public bool EnsureSecured()
    {
        PathGuard.EnsureNoReparsePoints(_stateDirectory);
        Directory.CreateDirectory(_stateDirectory);

        if (!_hardenPermissions || FolderAclManager.IsLocked(_stateDirectory))
            return true;

        try
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            new DirectoryInfo(_stateDirectory).SetAccessControl(security);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PrivilegeNotHeldException)
        {
            return false;
        }
    }

    internal FileStream BeginOperation(string folderPath)
    {
        RequireSecured();
        var path = GetRecordPath(folderPath) + ".lock";
        PathGuard.EnsureNoReparsePoints(path);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private void RequireSecured()
    {
        if (!EnsureSecured())
            throw new UnauthorizedAccessException($"Could not secure the GameLocker state directory: {_stateDirectory}");
    }

    /// <summary>
    /// Loads the record for a folder.
    /// </summary>
    /// <param name="folderPath">The folder to look up. Canonicalised before lookup.</param>
    /// <returns>The record, or <c>null</c> when the folder has none.</returns>
    public FolderStateRecord? TryLoad(string folderPath)
    {
        var file = GetRecordPath(folderPath);

        try
        {
            return ReadRecord(file);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new IOException($"Could not read the GameLocker journal '{file}': {ex.Message}", ex);
        }
    }

    private FolderStateRecord ReadRecord(string file)
    {
        PathGuard.EnsureNoReparsePoints(file);
        var json = File.ReadAllText(file, Encoding.UTF8);
        var record = JsonSerializer.Deserialize<FolderStateRecord>(json, JsonOptions);

        if (record is null || record.SchemaVersion != FolderStateRecord.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(record.FolderPath) || !Path.IsPathFullyQualified(record.FolderPath) ||
            record.VaultFiles is null || record.VaultCiphertextHashes is null ||
            !string.Equals(GetRecordPath(record.FolderPath), file, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The journal has an unsupported schema or an invalid folder path.");
        }

        return record;
    }

    /// <summary>
    /// Writes a record, replacing any existing one for the same folder.
    /// </summary>
    /// <param name="record">The record to persist. Its folder path is canonicalised in place.</param>
    public void Save(FolderStateRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        RequireSecured();

        record.FolderPath = PathGuard.Normalize(record.FolderPath);
        record.SchemaVersion = FolderStateRecord.CurrentSchemaVersion;

        var file = GetRecordPath(record.FolderPath);
        PathGuard.EnsureNoReparsePoints(file);
        var temp = file + $".{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(record, JsonOptions);

        // Write to a temporary file, flush it to stable storage, then swap it into place. A crash
        // at any point leaves either the previous record or the new one - never a truncated file.
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, file, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    /// <summary>
    /// Removes the record for a folder, if one exists.
    /// </summary>
    /// <param name="folderPath">The folder whose record should be removed.</param>
    public void Delete(string folderPath)
    {
        RequireSecured();
        var file = GetRecordPath(folderPath);

        if (File.Exists(file))
            File.Delete(file);
    }

    /// <summary>
    /// Loads every record in the store.
    /// </summary>
    /// <returns>All readable records. Unreadable files are skipped.</returns>
    public IReadOnlyList<FolderStateRecord> LoadAll(Action<string>? reportError = null)
    {
        var records = new List<FolderStateRecord>();
        PathGuard.EnsureNoReparsePoints(_stateDirectory);

        try
        {
            foreach (var file in Directory.EnumerateFiles(_stateDirectory, "*.json"))
            {
                try
                {
                    records.Add(ReadRecord(file));
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
                {
                    reportError?.Invoke($"Could not read the GameLocker journal '{file}': {ex.Message}");
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            // The state directory has not been created yet.
        }

        return records;
    }

    /// <summary>
    /// Gets the file that holds a folder's record.
    /// </summary>
    /// <param name="folderPath">The folder path.</param>
    /// <returns>The full path of the record file.</returns>
    public string GetRecordPath(string folderPath)
    {
        var canonical = PathGuard.Normalize(folderPath);

        // Case-insensitive key: NTFS paths are case-insensitive, so two spellings of the same
        // folder must resolve to the same record.
        var bytes = Encoding.UTF8.GetBytes(canonical.ToUpperInvariant());
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        return Path.Combine(_stateDirectory, $"{hash}.json");
    }
}
