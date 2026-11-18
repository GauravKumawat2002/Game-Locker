using System.Text.Json.Serialization;
using GameLocker.Common.Models;
using GameLocker.Common.Security;

namespace GameLocker.Common.State;

/// <summary>
/// What GameLocker did to a folder, and everything needed to undo it.
/// </summary>
/// <remarks>
/// This record replaces the <c>.gamelocker</c> marker file as the source of truth. The marker was
/// an empty signal stored inside the folder it described: it carried no information about what had
/// been done, deleting it made an encrypted folder report itself as unlocked, and creating an empty
/// file of that name was enough to convince the tool a folder was locked.
/// See ARCHITECTURE-REVIEW.md findings [F-1] and [F-4].
/// </remarks>
public sealed class FolderStateRecord
{
    /// <summary>The current schema version written by this build.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The schema version this record was written with.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// The canonical path of the folder this record describes.
    /// </summary>
    [JsonPropertyName("folderPath")]
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>
    /// The mechanism used to lock the folder.
    /// </summary>
    [JsonPropertyName("mode")]
    public LockMode Mode { get; set; } = LockMode.AclOnly;

    /// <summary>
    /// When the folder was locked, in UTC.
    /// </summary>
    [JsonPropertyName("lockedAtUtc")]
    public DateTime LockedAtUtc { get; set; }

    /// <summary>
    /// Whether the restrictive access control list is currently applied.
    /// </summary>
    [JsonPropertyName("aclApplied")]
    public bool AclApplied { get; set; }

    [JsonPropertyName("aclTreeValidated")]
    public bool AclTreeValidated { get; set; }

    /// <summary>
    /// The folder's access control state before it was locked.
    /// </summary>
    [JsonPropertyName("acl")]
    public AclSnapshot? Acl { get; set; }

    [JsonPropertyName("lockInProgress")]
    public bool LockInProgress { get; set; }

    [JsonPropertyName("testLock")]
    public bool TestLock { get; set; }

    /// <summary>
    /// The files this folder currently holds in encrypted form, relative to
    /// <see cref="FolderPath"/>, excluding the <c>.enc</c> suffix.
    /// </summary>
    /// <remarks>
    /// An explicit manifest means a partially failed operation is always enumerable. Previously
    /// there was no record of which files had been encrypted, so after a partial failure there was
    /// no way - in the product or by hand - to determine what had been done.
    /// </remarks>
    [JsonPropertyName("vaultFiles")]
    public List<string> VaultFiles { get; set; } = new();

    [JsonPropertyName("vaultCiphertextHashes")]
    public Dictionary<string, string> VaultCiphertextHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A description of the most recent failure, when the last operation did not fully succeed.
    /// </summary>
    [JsonPropertyName("lastError")]
    public string? LastError { get; set; }

    /// <summary>
    /// Gets a value indicating whether anything remains to be undone for this folder.
    /// </summary>
    [JsonIgnore]
    public bool HasOutstandingChanges => Acl is not null || AclApplied || VaultFiles.Count > 0;
}
