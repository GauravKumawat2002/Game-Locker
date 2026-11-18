using System.Security.AccessControl;
using System.Security.Principal;

namespace GameLocker.Common.Security;

/// <summary>
/// Provides NTFS ACL (Access Control List) management for folder access control.
/// </summary>
public static class AclHelper
{
    private const InheritanceFlags LockInheritanceFlags =
        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

    /// <summary>
    /// The rights denied to standard users while a folder is locked.
    /// </summary>
    /// <remarks>
    /// This mask deliberately EXCLUDES <see cref="FileSystemRights.ChangePermissions"/> (WRITE_DAC),
    /// <see cref="FileSystemRights.TakeOwnership"/> (WRITE_OWNER) and
    /// <see cref="FileSystemRights.ReadPermissions"/> (READ_CONTROL).
    /// <para>
    /// Denying <see cref="FileSystemRights.FullControl"/> also denies those three rights - which are
    /// precisely the rights required to remove the deny rule again. The result was a folder that
    /// neither the service nor an Administrator could recover without taking ownership first.
    /// Because a LocalSystem token is itself a member of BUILTIN\Users, denying FullControl to that
    /// group locked the service out of the folders it is responsible for unlocking.
    /// See ARCHITECTURE-REVIEW.md findings [P-1] and [P-2].
    /// </para>
    /// Content is unreadable and unexecutable while locked, so a game cannot launch, but the ACL
    /// can always be repaired.
    /// </remarks>
    private const FileSystemRights LockedDenyRights =
        FileSystemRights.ReadData |                  // == ListDirectory
        FileSystemRights.WriteData |                 // == CreateFiles
        FileSystemRights.AppendData |                // == CreateDirectories
        FileSystemRights.ReadExtendedAttributes |
        FileSystemRights.WriteExtendedAttributes |
        FileSystemRights.ExecuteFile |               // == Traverse
        FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.Delete;

    /// <summary>
    /// Denies content access to a folder for all interactive users, while leaving the ability to
    /// repair the ACL intact for both SYSTEM and Administrators.
    /// </summary>
    /// <param name="folderPath">The path to the folder.</param>
    public static void DenyAccess(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        var directoryInfo = new DirectoryInfo(folderPath);
        var security = directoryInfo.GetAccessControl();

        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var usersGroup = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        // Ensure SYSTEM and Administrators always hold an explicit Allow entry. A Deny still wins
        // over these for the rights it covers; these exist so the DACL is never left with no Allow
        // entry at all, and so ownership/permission repair is always attributable.
        security.SetAccessRule(new FileSystemAccessRule(
            systemSid,
            FileSystemRights.FullControl,
            LockInheritanceFlags,
            PropagationFlags.None,
            AccessControlType.Allow));

        security.SetAccessRule(new FileSystemAccessRule(
            adminsSid,
            FileSystemRights.FullControl,
            LockInheritanceFlags,
            PropagationFlags.None,
            AccessControlType.Allow));

        // A single deny against BUILTIN\Users covers every interactive account, administrators
        // included. Deny rules for AuthenticatedUsers and for the calling process's own SID were
        // removed: the latter resolved to S-1-5-18 when called from the service, so the service
        // denied itself. See LockedDenyRights.
        security.SetAccessRule(new FileSystemAccessRule(
            usersGroup,
            LockedDenyRights,
            LockInheritanceFlags,
            PropagationFlags.None,
            AccessControlType.Deny));

        directoryInfo.SetAccessControl(security);
    }

    /// <summary>
    /// Removes every explicit deny rule from a folder, restoring the access it had before it was
    /// locked.
    /// </summary>
    /// <remarks>
    /// This method no longer grants FullControl to Users and Authenticated Users. Doing so did not
    /// restore the folder's original permissions - it replaced them with a permanently more
    /// permissive set, so a single lock/unlock cycle on a folder under Program Files left it
    /// writable by every account on the machine. See ARCHITECTURE-REVIEW.md finding [P-3].
    /// <para>
    /// Because <see cref="DenyAccess"/> only ever adds deny entries, removing them restores the
    /// prior state. Explicit deny entries added by anything other than GameLocker are also removed,
    /// which is deliberate: it lets this method recover folders left inaccessible by earlier
    /// versions.
    /// </para>
    /// </remarks>
    /// <param name="folderPath">The path to the folder.</param>
    public static void AllowAccess(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        var directoryInfo = new DirectoryInfo(folderPath);
        var security = directoryInfo.GetAccessControl();

        // GetAccessRules returns a snapshot, so mutating `security` while iterating it is safe.
        // includeInherited is false: an inherited deny belongs to a parent folder and cannot be
        // removed here.
        var existingRules = security.GetAccessRules(
            includeExplicit: true,
            includeInherited: false,
            targetType: typeof(SecurityIdentifier));

        var removed = 0;
        foreach (FileSystemAccessRule rule in existingRules)
        {
            if (rule.AccessControlType != AccessControlType.Deny)
                continue;

            security.RemoveAccessRuleAll(new FileSystemAccessRule(
                rule.IdentityReference,
                rule.FileSystemRights,
                rule.InheritanceFlags,
                rule.PropagationFlags,
                AccessControlType.Deny));

            removed++;
        }

        if (removed == 0)
            return; // Nothing to undo; leave the DACL untouched.

        directoryInfo.SetAccessControl(security);
    }

    /// <summary>
    /// Checks if access to a folder is denied for the current user.
    /// </summary>
    /// <param name="folderPath">The path to the folder.</param>
    /// <returns>True if access is denied, false otherwise.</returns>
    public static bool IsAccessDenied(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return false;

        try
        {
            var directoryInfo = new DirectoryInfo(folderPath);
            var security = directoryInfo.GetAccessControl();
            var usersGroup = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));

            foreach (FileSystemAccessRule rule in rules)
            {
                // Match on ReadData rather than FullControl: DenyAccess deliberately denies a
                // reduced right set so that the ACL stays repairable, so a FullControl test would
                // never fire. ReadData is present in every lock this code produces.
                if (rule.IdentityReference.Equals(usersGroup) &&
                    rule.AccessControlType == AccessControlType.Deny &&
                    rule.FileSystemRights.HasFlag(FileSystemRights.ReadData))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // If we can't determine the access, assume it's accessible
            return false;
        }
    }

    internal static void RemoveLegacyGameLockerDenies(string folderPath)
    {
        var folder = new DirectoryInfo(folderPath);
        var security = folder.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var authenticated = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var owner = security.GetOwner(typeof(SecurityIdentifier));
        var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Where(rule => rule.AccessControlType == AccessControlType.Deny &&
                rule.InheritanceFlags == LockInheritanceFlags && rule.PropagationFlags == PropagationFlags.None)
            .ToList();

        if (!rules.Any(rule => rule.IdentityReference.Equals(users) &&
            (rule.FileSystemRights == LockedDenyRights || rule.FileSystemRights == FileSystemRights.FullControl)))
        {
            return;
        }

        foreach (var rule in rules)
        {
            var sid = rule.IdentityReference;
            if ((sid.Equals(users) && rule.FileSystemRights == LockedDenyRights) ||
                (rule.FileSystemRights == FileSystemRights.FullControl &&
                    (sid.Equals(users) || sid.Equals(authenticated) || sid.Equals(system) || sid.Equals(owner))))
            {
                security.RemoveAccessRuleSpecific(rule);
            }
        }

        folder.SetAccessControl(security);
    }

    /// <summary>
    /// Gets the current access control list for a folder.
    /// </summary>
    /// <param name="folderPath">The path to the folder.</param>
    /// <returns>The access control list as a string.</returns>
    public static string GetAccessControlInfo(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return "Directory does not exist.";

        try
        {
            var directoryInfo = new DirectoryInfo(folderPath);
            var security = directoryInfo.GetAccessControl();
            return security.GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        }
        catch (Exception ex)
        {
            return $"Error retrieving access control: {ex.Message}";
        }
    }

    /// <summary>
    /// Checks if the current process is running as Administrator.
    /// </summary>
    /// <returns>True if running as admin, false otherwise.</returns>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
