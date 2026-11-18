using System.Security.AccessControl;
using System.Security.Principal;

namespace GameLocker.Common.Security;

/// <summary>
/// Locks and unlocks folders by replacing their access control list, and restores the exact
/// permissions the folder had beforehand.
/// </summary>
/// <remarks>
/// <para><b>This class never writes a Deny entry.</b></para>
/// Deny precedence was the root of every permanent-lockout report. In Windows access checks an
/// explicit Deny matching any group in the caller's token defeats every Allow, so a Deny against
/// <c>BUILTIN\Users</c> also stopped the LocalSystem service - whose token is a member of that
/// group - and a Deny of <c>FullControl</c> includes <c>WRITE_DAC</c>, the one right needed to undo
/// it. See ARCHITECTURE-REVIEW.md findings [P-1], [P-2] and [P-4].
/// <para>
/// Instead, locking installs a <em>protected</em> access control list granting only SYSTEM and
/// Administrators. Under User Account Control an administrator's ordinary logon token carries the
/// Administrators group as deny-only, so it cannot be used to gain access: Explorer, the game, and
/// any non-elevated process are refused. An elevated process, the service, and this tool all retain
/// full access, so recovery never requires taking ownership.
/// </para>
/// </remarks>
public static class FolderAclManager
{
    private const InheritanceFlags FolderInheritance =
        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

    /// <summary>
    /// Captures a folder's current access control state so it can be restored exactly.
    /// </summary>
    /// <param name="folderPath">The folder to capture.</param>
    /// <returns>A snapshot suitable for <see cref="Restore"/>.</returns>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    public static AclSnapshot Capture(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        var directoryInfo = new DirectoryInfo(folderPath);
        var security = directoryInfo.GetAccessControl(
            AccessControlSections.Access | AccessControlSections.Owner);

        // Only explicit entries are captured. Inherited entries belong to the parent folder and are
        // recomputed by Windows when the access rules are unprotected, so writing them back
        // literally would duplicate them and leave the folder drifting further from its original
        // state on every cycle.
        var explicitOnly = new DirectorySecurity();
        explicitOnly.SetAccessRuleProtection(
            isProtected: security.AreAccessRulesProtected,
            preserveInheritance: false);

        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true,
                     includeInherited: false,
                     targetType: typeof(SecurityIdentifier)))
        {
            explicitOnly.AddAccessRule(rule);
        }

        return new AclSnapshot
        {
            Sddl = explicitOnly.GetSecurityDescriptorSddlForm(AccessControlSections.Access),
            OwnerSid = TryGetOwnerSid(security),
            WasProtected = security.AreAccessRulesProtected,
            CapturedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Applies the restrictive access control list that constitutes a lock.
    /// </summary>
    /// <param name="folderPath">The folder to lock.</param>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    /// <exception cref="InvalidOperationException">The lock could not be verified afterwards.</exception>
    public static void ApplyLock(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        VerifyCompatibleDescendants(folderPath);
        var security = new DirectorySecurity();

        // Protected: stop inheriting the permissive entries from the parent, and do not copy them
        // down. Everything the folder had is in the snapshot taken before this call.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            FolderInheritance,
            PropagationFlags.None,
            AccessControlType.Allow));

        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            FolderInheritance,
            PropagationFlags.None,
            AccessControlType.Allow));

        new DirectoryInfo(folderPath).SetAccessControl(security);

        VerifyLocked(folderPath);
    }

    /// <summary>
    /// Checks whether existing descendants can safely inherit the folder lock. The lock never
    /// overwrites a descendant's explicit rules or changes a descendant's inheritance policy.
    /// </summary>
    internal static void VerifyCompatibleDescendants(string folderPath)
    {
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var directories = new Stack<string>();
        directories.Push(folderPath);

        while (directories.TryPop(out var directory))
        {
            PathGuard.EnsureNoReparsePoints(directory);
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                PathGuard.EnsureNoReparsePoints(path);
                var isDirectory = File.GetAttributes(path).HasFlag(FileAttributes.Directory);
                FileSystemSecurity security = isDirectory
                    ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access)
                    : new FileInfo(path).GetAccessControl(AccessControlSections.Access);

                var incompatible = security.AreAccessRulesProtected || security
                    .GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
                    .Cast<FileSystemAccessRule>()
                    .Any(rule => rule.AccessControlType != AccessControlType.Allow ||
                        (!rule.IdentityReference.Equals(system) && !rule.IdentityReference.Equals(admins)));

                if (incompatible)
                    throw new InvalidOperationException($"Folder cannot be locked safely because '{path}' has separate permissions.");

                if (isDirectory)
                    directories.Push(path);
            }
        }
    }

    /// <summary>
    /// Restores a folder's access control list from a snapshot.
    /// </summary>
    /// <param name="folderPath">The folder to restore.</param>
    /// <param name="snapshot">The snapshot captured before locking.</param>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    /// <exception cref="ArgumentException">The snapshot carries nothing to restore.</exception>
    public static void Restore(string folderPath, AclSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        if (!snapshot.IsRestorable)
            throw new ArgumentException("The snapshot does not contain a permission descriptor.", nameof(snapshot));

        var security = new DirectorySecurity();
        security.SetSecurityDescriptorSddlForm(snapshot.Sddl, AccessControlSections.Access);

        new DirectoryInfo(folderPath).SetAccessControl(security);
    }

    /// <summary>
    /// Clears every explicit permission entry and re-enables inheritance from the parent folder.
    /// </summary>
    /// <param name="folderPath">The folder to reset.</param>
    /// <remarks>
    /// The equivalent of <c>icacls &lt;folder&gt; /reset</c>. This is a fallback for folders locked
    /// by a build that kept no snapshot: it produces a sane, accessible folder, but it is not the
    /// same as restoring the permissions the folder originally had. Prefer <see cref="Restore"/>
    /// whenever a snapshot exists.
    /// <para>
    /// The resulting descriptor has an empty explicit list and is <em>unprotected</em>, so Windows
    /// recomputes the inherited entries. An empty <em>protected</em> list would instead deny
    /// everyone, which is why the protection flag matters more than the entries here.
    /// </para>
    /// </remarks>
    public static void ResetToInherited(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);

        new DirectoryInfo(folderPath).SetAccessControl(security);
    }

    /// <summary>
    /// Determines whether a folder currently carries the lock access control list.
    /// </summary>
    /// <param name="folderPath">The folder to inspect.</param>
    /// <returns><c>true</c> when the folder is locked; <c>false</c> when it is not, or when its
    /// permissions could not be read.</returns>
    public static bool IsLocked(string folderPath)
    {
        try
        {
            return DescribeLockState(folderPath) is null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Confirms that a folder carries the lock access control list.
    /// </summary>
    /// <param name="folderPath">The folder to verify.</param>
    /// <exception cref="InvalidOperationException">The folder is not correctly locked.</exception>
    public static void VerifyLocked(string folderPath)
    {
        var problem = DescribeLockState(folderPath);

        if (problem is not null)
            throw new InvalidOperationException($"Lock verification failed for '{folderPath}': {problem}");
    }

    /// <summary>
    /// Confirms that a folder's permissions match a snapshot.
    /// </summary>
    /// <param name="folderPath">The folder to verify.</param>
    /// <param name="snapshot">The snapshot that should have been restored.</param>
    /// <returns><c>true</c> when the folder's explicit permissions match the snapshot exactly.</returns>
    public static bool MatchesSnapshot(string folderPath, AclSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            var current = Capture(folderPath);
            return string.Equals(current.Sddl, snapshot.Sddl, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks the lock invariants and describes the first violation.
    /// </summary>
    /// <param name="folderPath">The folder to inspect.</param>
    /// <returns><c>null</c> when the folder is correctly locked; otherwise a description of what
    /// is wrong.</returns>
    private static string? DescribeLockState(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return "the folder does not exist";

        var security = new DirectoryInfo(folderPath).GetAccessControl(AccessControlSections.Access);

        if (!security.AreAccessRulesProtected)
            return "the access rules are not protected, so permissions are still inherited from the parent folder";

        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var expected = new HashSet<IdentityReference> { systemSid, adminsSid };

        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true,
                     includeInherited: true,
                     targetType: typeof(SecurityIdentifier)))
        {
            if (rule.IsInherited || rule.AccessControlType != AccessControlType.Allow ||
                rule.FileSystemRights != FileSystemRights.FullControl ||
                rule.InheritanceFlags != FolderInheritance || rule.PropagationFlags != PropagationFlags.None ||
                !expected.Remove(rule.IdentityReference))
            {
                return $"an unexpected access rule is present for '{rule.IdentityReference.Value}'";
            }
        }

        // Without this the service could not unlock the folder it just locked - the exact failure
        // that made folders unrecoverable in v1.2.0.
        if (expected.Count != 0)
            return "SYSTEM and Administrators must both hold full control";

        return null;
    }

    private static string? TryGetOwnerSid(DirectorySecurity security)
    {
        try
        {
            return security.GetOwner(typeof(SecurityIdentifier))?.Value;
        }
        catch
        {
            return null;
        }
    }
}
