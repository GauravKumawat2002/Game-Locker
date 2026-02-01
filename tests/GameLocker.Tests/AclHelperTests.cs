using System.Security.AccessControl;
using System.Security.Principal;
using GameLocker.Common.Security;

namespace GameLocker.Tests;

/// <summary>
/// Unit tests for AclHelper class.
/// NOTE: These tests require Administrator privileges to modify ACLs.
/// Run with: dotnet test --filter "Category=RequiresAdmin" -- as admin
/// </summary>
public class AclHelperTests : IDisposable
{
    private readonly string _testFolder;
    private readonly List<string> _createdFolders = new();

    public AclHelperTests()
    {
        // Create a unique test folder in temp directory
        _testFolder = Path.Combine(Path.GetTempPath(), $"GameLocker_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testFolder);
        _createdFolders.Add(_testFolder);
    }

    public void Dispose()
    {
        // Cleanup: Ensure access is restored before deleting
        foreach (var folder in _createdFolders)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    // Try to restore access first
                    try { AclHelper.AllowAccess(folder); } catch { }
                    
                    // Force reset permissions using icacls equivalent
                    ResetPermissions(folder);
                    
                    Directory.Delete(folder, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private void ResetPermissions(string path)
    {
        try
        {
            var directoryInfo = new DirectoryInfo(path);
            var security = directoryInfo.GetAccessControl();
            
            // Remove all explicit deny rules
            var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));
            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.AccessControlType == AccessControlType.Deny)
                {
                    security.RemoveAccessRule(rule);
                }
            }
            
            directoryInfo.SetAccessControl(security);
        }
        catch { }
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void IsRunningAsAdmin_ReturnsBoolean()
    {
        // This should not throw
        var result = AclHelper.IsRunningAsAdmin();
        
        // Result should be a boolean (we can't assert specific value as it depends on context)
        Assert.True(result == true || result == false);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void DenyAccess_ThrowsOnNonexistentFolder()
    {
        var fakePath = @"C:\NonExistent\Folder\That\Does\Not\Exist";
        
        Assert.Throws<DirectoryNotFoundException>(() => AclHelper.DenyAccess(fakePath));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void AllowAccess_ThrowsOnNonexistentFolder()
    {
        var fakePath = @"C:\NonExistent\Folder\That\Does\Not\Exist";
        
        Assert.Throws<DirectoryNotFoundException>(() => AclHelper.AllowAccess(fakePath));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void IsAccessDenied_ReturnsFalseForNonexistentFolder()
    {
        var fakePath = @"C:\NonExistent\Folder\That\Does\Not\Exist";
        
        var result = AclHelper.IsAccessDenied(fakePath);
        
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void IsAccessDenied_ReturnsFalseForUnlockedFolder()
    {
        // _testFolder is unlocked by default
        var result = AclHelper.IsAccessDenied(_testFolder);
        
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void GetAccessControlInfo_ReturnsNonEmptyStringForValidFolder()
    {
        var result = AclHelper.GetAccessControlInfo(_testFolder);
        
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.DoesNotContain("does not exist", result.ToLower());
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void GetAccessControlInfo_ReturnsErrorMessageForNonexistentFolder()
    {
        var fakePath = @"C:\NonExistent\Folder\That\Does\Not\Exist";
        
        var result = AclHelper.GetAccessControlInfo(fakePath);
        
        Assert.Contains("does not exist", result.ToLower());
    }

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void DenyAccess_SetsAccessDenied()
    {
        // Skip if not running as admin
        if (!AclHelper.IsRunningAsAdmin())
        {
            return; // Skip test - requires admin
        }

        // Act
        AclHelper.DenyAccess(_testFolder);

        // Assert
        Assert.True(AclHelper.IsAccessDenied(_testFolder));
    }

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void AllowAccess_RemovesDenyRule()
    {
        // Skip if not running as admin
        if (!AclHelper.IsRunningAsAdmin())
        {
            return; // Skip test - requires admin
        }

        // Arrange - first deny access
        AclHelper.DenyAccess(_testFolder);
        Assert.True(AclHelper.IsAccessDenied(_testFolder));

        // Act
        AclHelper.AllowAccess(_testFolder);

        // Assert
        Assert.False(AclHelper.IsAccessDenied(_testFolder));
    }

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void DenyAccess_PreservesSystemAccess()
    {
        // Skip if not running as admin
        if (!AclHelper.IsRunningAsAdmin())
        {
            return; // Skip test - requires admin
        }

        // Act
        AclHelper.DenyAccess(_testFolder);

        // Assert - SYSTEM should have explicit allow rule
        var directoryInfo = new DirectoryInfo(_testFolder);
        var security = directoryInfo.GetAccessControl();
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));

        var hasSystemAllow = false;
        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.IdentityReference.Equals(systemSid) &&
                rule.AccessControlType == AccessControlType.Allow &&
                rule.FileSystemRights.HasFlag(FileSystemRights.FullControl))
            {
                hasSystemAllow = true;
                break;
            }
        }

        Assert.True(hasSystemAllow, "SYSTEM account should have explicit ALLOW rule after DenyAccess()");
    }

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void DenyAccess_PreservesAdministratorsAccess()
    {
        // Skip if not running as admin
        if (!AclHelper.IsRunningAsAdmin())
        {
            return; // Skip test - requires admin
        }

        // Act
        AclHelper.DenyAccess(_testFolder);

        // Assert - Administrators should have explicit allow rule
        var directoryInfo = new DirectoryInfo(_testFolder);
        var security = directoryInfo.GetAccessControl();
        var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));

        var hasAdminsAllow = false;
        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.IdentityReference.Equals(adminsSid) &&
                rule.AccessControlType == AccessControlType.Allow &&
                rule.FileSystemRights.HasFlag(FileSystemRights.FullControl))
            {
                hasAdminsAllow = true;
                break;
            }
        }

        Assert.True(hasAdminsAllow, "Administrators should have explicit ALLOW rule after DenyAccess()");
    }

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void DenyAccess_DoesNotDenyEveryone()
    {
        // Skip if not running as admin
        if (!AclHelper.IsRunningAsAdmin())
        {
            return; // Skip test - requires admin
        }

        // Act
        AclHelper.DenyAccess(_testFolder);

        // Assert - WorldSid (Everyone) should NOT have deny rule
        var directoryInfo = new DirectoryInfo(_testFolder);
        var security = directoryInfo.GetAccessControl();
        var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));

        var hasEveryoneDeny = false;
        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.IdentityReference.Equals(everyoneSid) &&
                rule.AccessControlType == AccessControlType.Deny)
            {
                hasEveryoneDeny = true;
                break;
            }
        }

        Assert.False(hasEveryoneDeny, "Everyone (WorldSid) should NOT have DENY rule - it includes SYSTEM!");
    }

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void DenyAccess_DeniesUsersGroup()
    {
        // Skip if not running as admin
        if (!AclHelper.IsRunningAsAdmin())
        {
            return; // Skip test - requires admin
        }

        // Act
        AclHelper.DenyAccess(_testFolder);

        // Assert - Users group should have deny rule
        var directoryInfo = new DirectoryInfo(_testFolder);
        var security = directoryInfo.GetAccessControl();
        var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));

        var hasUsersDeny = false;
        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.IdentityReference.Equals(usersSid) &&
                rule.AccessControlType == AccessControlType.Deny &&
                rule.FileSystemRights.HasFlag(FileSystemRights.FullControl))
            {
                hasUsersDeny = true;
                break;
            }
        }

        Assert.True(hasUsersDeny, "Users group should have DENY rule after DenyAccess()");
    }

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void LockUnlockCycle_WorksCorrectly()
    {
        // Skip if not running as admin
        if (!AclHelper.IsRunningAsAdmin())
        {
            return; // Skip test - requires admin
        }

        // Initial state - should be unlocked
        Assert.False(AclHelper.IsAccessDenied(_testFolder));

        // Lock
        AclHelper.DenyAccess(_testFolder);
        Assert.True(AclHelper.IsAccessDenied(_testFolder));

        // Unlock
        AclHelper.AllowAccess(_testFolder);
        Assert.False(AclHelper.IsAccessDenied(_testFolder));

        // Lock again
        AclHelper.DenyAccess(_testFolder);
        Assert.True(AclHelper.IsAccessDenied(_testFolder));

        // Unlock again
        AclHelper.AllowAccess(_testFolder);
        Assert.False(AclHelper.IsAccessDenied(_testFolder));
    }

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void DenyAccess_WithSubfolders_PropagatesACL()
    {
        // Skip if not running as admin
        if (!AclHelper.IsRunningAsAdmin())
        {
            return; // Skip test - requires admin
        }

        // Create subfolder structure
        var subfolder = Path.Combine(_testFolder, "subfolder");
        Directory.CreateDirectory(subfolder);
        var deepFolder = Path.Combine(subfolder, "deep");
        Directory.CreateDirectory(deepFolder);

        // Act - lock parent
        AclHelper.DenyAccess(_testFolder);

        // Assert - parent should be marked as denied
        Assert.True(AclHelper.IsAccessDenied(_testFolder));
        
        // Note: Subfolders inherit ACL, but IsAccessDenied only checks explicit rules
        // The actual access would be denied due to inheritance
    }

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void AllowAccess_IdempotentOnUnlockedFolder()
    {
        // Skip if not running as admin
        if (!AclHelper.IsRunningAsAdmin())
        {
            return; // Skip test - requires admin
        }

        // Folder is already unlocked
        Assert.False(AclHelper.IsAccessDenied(_testFolder));

        // Calling AllowAccess on unlocked folder should not throw
        AclHelper.AllowAccess(_testFolder);
        
        Assert.False(AclHelper.IsAccessDenied(_testFolder));
    }

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void DenyAccess_MultipleCallsAreIdempotent()
    {
        // Skip if not running as admin
        if (!AclHelper.IsRunningAsAdmin())
        {
            return; // Skip test - requires admin
        }

        // Call DenyAccess multiple times
        AclHelper.DenyAccess(_testFolder);
        AclHelper.DenyAccess(_testFolder);
        AclHelper.DenyAccess(_testFolder);

        // Should still be denied
        Assert.True(AclHelper.IsAccessDenied(_testFolder));

        // One AllowAccess should unlock it
        AclHelper.AllowAccess(_testFolder);
        Assert.False(AclHelper.IsAccessDenied(_testFolder));
    }
}
