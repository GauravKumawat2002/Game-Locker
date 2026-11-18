using System.Text.Json.Serialization;

namespace GameLocker.Common.Models;

/// <summary>
/// How a folder is locked.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LockMode
{
    /// <summary>
    /// Deny access using NTFS permissions only. No file is read, written, moved or deleted.
    /// </summary>
    /// <remarks>
    /// This is the default and the recommended mode. It completes in roughly the same time on a
    /// 200 MB folder as on a 200 GB one, is exactly reversible from the captured permission
    /// snapshot, and cannot corrupt data because it never touches file contents.
    /// <para>
    /// A folder that cannot be opened is already unusable to a game, so this achieves the entire
    /// product goal. See REMEDIATION-PLAN.md, "Why this plan looks the way it does".
    /// </para>
    /// </remarks>
    AclOnly = 0,

    /// <summary>
    /// Deny access using NTFS permissions, and additionally encrypt an explicitly selected set of
    /// save files.
    /// </summary>
    /// <remarks>
    /// Opt-in only, and never selected automatically. Encryption is restricted to a user-chosen
    /// allowlist, is subject to the denylist in
    /// <see cref="Services.FileExtensionScanner.NeverEncryptExtensions"/>, and is gated behind a
    /// verified key backup. The hardened container format lands in Phase 4 of REMEDIATION-PLAN.md;
    /// until then this mode should not be used on data that matters.
    /// </remarks>
    AclAndVault = 1,
}
