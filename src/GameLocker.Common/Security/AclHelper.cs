using System.Security.AccessControl;
using System.Security.Principal;

namespace GameLocker.Common.Security;

/// <summary>
/// Provides NTFS ACL (Access Control List) management for folder access control.
/// </summary>
public static class AclHelper
{
    /// <summary>
    /// Denies access to a folder for all non-admin users while preserving SYSTEM access.
    /// IMPORTANT: SYSTEM must retain access so the service can manage locked folders.
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

        // CRITICAL: Get SYSTEM account SID - the service runs as SYSTEM and must retain access
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        // Get various user groups to deny
        var usersGroup = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

        // FIRST: Add explicit ALLOW rule for SYSTEM so it can still manage the folder
        // This MUST be added BEFORE deny rules because deny rules take precedence by default
        var systemAllowRule = new FileSystemAccessRule(
            systemSid,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow);
        security.SetAccessRule(systemAllowRule);

        // Also ensure Administrators can access
        var adminsAllowRule = new FileSystemAccessRule(
            adminsSid,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow);
        security.SetAccessRule(adminsAllowRule);

        // Add deny rules for regular users - but NOT Everyone (which includes SYSTEM)
        var denyRules = new[]
        {
            // Deny all access to Users group
            new FileSystemAccessRule(
                usersGroup,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny),
            // Deny all access to Authenticated Users
            new FileSystemAccessRule(
                authenticatedUsers,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny),
            // Deny access to current user specifically
            new FileSystemAccessRule(
                userSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny)
        };

        foreach (var rule in denyRules)
        {
            security.SetAccessRule(rule); // Use SetAccessRule instead of AddAccessRule to ensure it takes precedence
        }

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

        // Get the current user's identity
        var currentUser = WindowsIdentity.GetCurrent();
        var userSid = currentUser.User;

        if (userSid == null)
            throw new InvalidOperationException("Could not get current user SID.");

        // Get various user groups
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var usersGroup = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

        // First, get all existing rules and remove ALL deny rules
        var existingRules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));
        foreach (FileSystemAccessRule rule in existingRules)
        {
            if (rule.AccessControlType == AccessControlType.Deny)
            {
                // Remove the deny rule - use PurgeAccessRules approach for reliability
                security.RemoveAccessRuleAll(new FileSystemAccessRule(
                    rule.IdentityReference,
                    rule.FileSystemRights,
                    rule.InheritanceFlags,
                    rule.PropagationFlags,
                    AccessControlType.Deny));
            }
        }

        // Also try to remove specific deny rules we typically add
        var denyRulesToRemove = new[]
        {
            new FileSystemAccessRule(
                usersGroup,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny),
            new FileSystemAccessRule(
                authenticatedUsers,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny),
            new FileSystemAccessRule(
                userSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny),
            new FileSystemAccessRule(
                systemSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny)
        };

        foreach (var rule in denyRulesToRemove)
        {
            try
            {
                security.RemoveAccessRuleAll(rule);
            }
            catch { /* Ignore if rule doesn't exist */ }
        }

        // Restore default full control permissions for Users group
        var allowRuleUsers = new FileSystemAccessRule(
            usersGroup,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow);
        security.SetAccessRule(allowRuleUsers);

        // Ensure Authenticated Users also have access
        var allowRuleAuth = new FileSystemAccessRule(
            authenticatedUsers,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow);
        security.SetAccessRule(allowRuleAuth);

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
            // If we can't determine the access, assume it's accessible
            return false;
        }
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
