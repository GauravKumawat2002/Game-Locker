using System.Text.Json.Serialization;

namespace GameLocker.Common.Security;

/// <summary>
/// A record of a folder's access control state, captured immediately before it was locked.
/// </summary>
/// <remarks>
/// The previous implementation captured nothing. Unlocking "restored" access by granting
/// <c>Users</c> and <c>Authenticated Users</c> FullControl, which is not the same thing as putting
/// the original permissions back - it left every folder GameLocker had ever touched permanently
/// more permissive than it started. See ARCHITECTURE-REVIEW.md findings [P-3] and [P-4].
/// </remarks>
public sealed class AclSnapshot
{
    /// <summary>
    /// The folder's discretionary access control list in SDDL form, as it was before locking.
    /// </summary>
    [JsonPropertyName("sddl")]
    public string Sddl { get; set; } = string.Empty;

    /// <summary>
    /// The SID of the folder's owner at capture time, in SDDL form.
    /// </summary>
    [JsonPropertyName("ownerSid")]
    public string? OwnerSid { get; set; }

    /// <summary>
    /// Whether the folder's access rules were protected from inheritance at capture time.
    /// </summary>
    [JsonPropertyName("wasProtected")]
    public bool WasProtected { get; set; }

    /// <summary>
    /// When the snapshot was taken, in UTC.
    /// </summary>
    [JsonPropertyName("capturedAtUtc")]
    public DateTime CapturedAtUtc { get; set; }

    /// <summary>
    /// Gets a value indicating whether the snapshot carries enough information to restore from.
    /// </summary>
    [JsonIgnore]
    public bool IsRestorable => !string.IsNullOrWhiteSpace(Sddl);
}
