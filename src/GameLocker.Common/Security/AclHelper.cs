using System.Security.AccessControl;
using System.Security.Principal;

namespace GameLocker.Common.Security;

/// <summary>
/// Provides NTFS ACL (Access Control List) management for folder access control.
/// </summary>
public static class AclHelper
{
    /// <summary>
    /// Denies access to a folder for all non-admin users.
    /// </summary>
    /// <param name="folderPath">The path to the folder.</param>
    public static void DenyAccess(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        var directoryInfo = new DirectoryInfo(folderPath);
        var security = directoryInfo.GetAccessControl();

        // Get the current user's identity
        var currentUser = WindowsIdentity.GetCurrent();
        var userSid = currentUser.User;

        if (userSid == null)
            throw new InvalidOperationException("Could not get current user SID.");

        // Get the Users group
        var usersGroup = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        // Add deny rule for the Users group (this will block normal users)
        var denyRule = new FileSystemAccessRule(
            usersGroup,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Deny);

        security.AddAccessRule(denyRule);

        // Apply the modified security settings
        directoryInfo.SetAccessControl(security);
    }

    /// <summary>
    /// Removes the deny access rule from a folder, restoring normal access.
    /// </summary>
    /// <param name="folderPath">The path to the folder.</param>
    public static void AllowAccess(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        var directoryInfo = new DirectoryInfo(folderPath);
        var security = directoryInfo.GetAccessControl();

        // Get the Users group
        var usersGroup = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        // Remove deny rules for the Users group
        var denyRule = new FileSystemAccessRule(
            usersGroup,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Deny);

        security.RemoveAccessRule(denyRule);

        // Apply the modified security settings
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
                if (rule.IdentityReference.Equals(usersGroup) &&
                    rule.AccessControlType == AccessControlType.Deny &&
                    rule.FileSystemRights.HasFlag(FileSystemRights.FullControl))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    /// <returns>True if running as administrator, false otherwise.</returns>
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Gets the owner of a folder.
    /// </summary>
    /// <param name="folderPath">The path to the folder.</param>
    /// <returns>The owner's identity reference.</returns>
    public static IdentityReference GetOwner(string folderPath)
    {
        var directoryInfo = new DirectoryInfo(folderPath);
        var security = directoryInfo.GetAccessControl();
        return security.GetOwner(typeof(NTAccount))!;
    }
}
