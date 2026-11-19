using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using GameLocker.Common.Security;
using Microsoft.Win32.SafeHandles;

namespace GameLocker.Tests;

public sealed class FolderOwnerRightsTests : IDisposable
{
    private const uint ReadControl = (uint)FileSystemRights.ReadPermissions;
    private const uint WriteDac = (uint)FileSystemRights.ChangePermissions;
    private const uint ReadData = (uint)FileSystemRights.ReadData;
    private const uint FileObject = 1;
    private const uint DaclSecurityInformation = 4;
    private const uint ProtectedDacl = 0x80000000;
    private const uint UnprotectedDacl = 0x20000000;
    private const uint BackupSemantics = 0x02000000;
    private const int AccessDenied = 5;

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("GameLocker_OwnerRights_");

    [Fact]
    [Trait("Category", "Basic")]
    public void OwnerRightsRestrictsNewHandlesAndPreservesOriginalPermissions()
    {
        var folder = _root.CreateSubdirectory("game");
        var child = folder.CreateSubdirectory("child");
        var file = new FileInfo(Path.Combine(child.FullName, "fixture.sav"));
        File.WriteAllText(file.FullName, "fixture");

        var paths = new[] { folder.FullName, child.FullName, file.FullName };
        var snapshots = paths.Select(ReadFilePermissions).ToArray();
        var original = FolderAclManager.Capture(folder.FullName);
        var isElevated = AclHelper.IsRunningAsAdmin();

        if (!isElevated)
        {
            using var identity = WindowsIdentity.GetCurrent();
            Assert.Equal(identity.User?.Value, original.OwnerSid);
            var error = Assert.Throws<UnauthorizedAccessException>(() => FolderAclManager.ApplyLock(folder.FullName));
            Assert.Contains("elevated administrator", error.Message);
            Assert.True(FolderAclManager.MatchesSnapshot(folder.FullName, original));
        }

        // Establish and test all restoration handles before writing the first lock ACL.
        using (new SavedAcl(file.FullName))
        using (new SavedAcl(child.FullName))
        using (new SavedAcl(folder.FullName))
        {
            if (isElevated)
            {
                FolderAclManager.ApplyLock(folder.FullName);
            }
            else
            {
                // Exercise the shared descriptor with disposable data and the saved ACL handles.
                folder.SetAccessControl(FolderAclManager.CreateLockSecurity());
            }

            Assert.True(FolderAclManager.IsLocked(folder.FullName));
            Assert.Equal(original.OwnerSid, FolderAclManager.Capture(folder.FullName).OwnerSid);
            Assert.All(paths, path =>
            {
                Assert.True(CanReadPermissions(path));
                Assert.Equal(isElevated, CanOpen(path, ReadData));
                var permissions = ReadFilePermissions(path);
                Assert.True(isElevated == CanChangePermissions(path), $"Unexpected permission change access for '{path}': {permissions}");
            });
        }

        Assert.True(FolderAclManager.MatchesSnapshot(folder.FullName, original));
        Assert.Equal(snapshots, paths.Select(ReadFilePermissions).ToArray());
        Assert.Equal("fixture", File.ReadAllText(file.FullName));
    }

    internal static DirectorySecurity CreatePreviousLockSecurity()
    {
        var security = new DirectorySecurity();
        security.SetSecurityDescriptorSddlForm("D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)", AccessControlSections.Access);
        return security;
    }

    public void Dispose() => _root.Delete(recursive: true);

    private static FileSystemSecurity ReadSecurity(string path) =>
        Directory.Exists(path)
            ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access)
            : new FileInfo(path).GetAccessControl(AccessControlSections.Access);

    private static string ReadFilePermissions(string path)
    {
        var descriptor = new RawSecurityDescriptor(ReadSecurity(path).GetSecurityDescriptorBinaryForm(), 0);
        // Windows can mark a restored DACL as auto-inherited when its entries are propagated.
        descriptor.SetFlags(descriptor.ControlFlags & ~ControlFlags.DiscretionaryAclAutoInherited);
        return descriptor.GetSddlForm(AccessControlSections.Access);
    }

    private static bool CanReadPermissions(string path) => AccessPermissions(path, write: false);

    private static bool CanChangePermissions(string path) => AccessPermissions(path, write: true);

    private static bool AccessPermissions(string path, bool write)
    {
        // Use the security APIs so this probes READ_CONTROL and WRITE_DAC without the extra
        // access needed to open a regular synchronous file handle.
        var result = GetNamedSecurityInfo(path, FileObject, DaclSecurityInformation,
            out _, out _, out var dacl, out _, out var descriptor);
        try
        {
            if (result == 0 && write)
            {
                result = SetNamedSecurityInfo(path, FileObject, DaclSecurityInformation,
                    IntPtr.Zero, IntPtr.Zero, dacl, IntPtr.Zero);
            }

            return result switch
            {
                0 => true,
                AccessDenied => false,
                _ => throw new Win32Exception((int)result),
            };
        }
        finally
        {
            if (descriptor != IntPtr.Zero)
                LocalFree(descriptor);
        }
    }

    private static bool CanOpen(string path, uint rights)
    {
        using var handle = Open(path, rights);
        if (!handle.IsInvalid)
            return true;

        var error = Marshal.GetLastWin32Error();
        if (error != AccessDenied)
            throw new Win32Exception(error);

        return false;
    }

    private static SafeFileHandle Open(string path, uint rights) =>
        CreateFile(path, rights, FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero,
            FileMode.Open, BackupSemantics, IntPtr.Zero);

    internal sealed class SavedAcl : IDisposable
    {
        private readonly SafeFileHandle _handle;
        private readonly uint _flags;
        private IntPtr _descriptor;
        private IntPtr _dacl;

        internal SavedAcl(string path)
        {
            var wasProtected = ReadSecurity(path).AreAccessRulesProtected;
            _flags = DaclSecurityInformation | (wasProtected ? ProtectedDacl : UnprotectedDacl);
            _handle = Open(path, ReadControl | WriteDac);
            if (_handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                _handle.Dispose();
                throw new Win32Exception(error);
            }

            try
            {
                ThrowOnError(GetSecurityInfo(_handle, FileObject, DaclSecurityInformation,
                    out _, out _, out _dacl, out _, out _descriptor));
                if (_dacl == IntPtr.Zero)
                    throw new InvalidOperationException("The test fixture must have a DACL.");

                Restore();
            }
            catch
            {
                Release();
                throw;
            }
        }

        public void Dispose()
        {
            try { Restore(); }
            finally { Release(); }
        }

        private void Restore() =>
            ThrowOnError(SetSecurityInfo(_handle, FileObject, _flags, IntPtr.Zero, IntPtr.Zero, _dacl, IntPtr.Zero));

        private void Release()
        {
            _handle.Dispose();
            if (_descriptor != IntPtr.Zero)
                LocalFree(_descriptor);
        }

        private static void ThrowOnError(uint error)
        {
            if (error != 0)
                throw new Win32Exception((int)error);
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, FileShare share, IntPtr attributes,
        FileMode creation, uint flags, IntPtr template);

    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityInfo(SafeFileHandle handle, uint objectType, uint securityInformation,
        out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr descriptor);

    [DllImport("advapi32.dll", EntryPoint = "GetNamedSecurityInfoW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetNamedSecurityInfo(string path, uint objectType, uint securityInformation,
        out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr descriptor);

    [DllImport("advapi32.dll", EntryPoint = "SetNamedSecurityInfoW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint SetNamedSecurityInfo(string path, uint objectType, uint securityInformation,
        IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);

    [DllImport("advapi32.dll")]
    private static extern uint SetSecurityInfo(SafeFileHandle handle, uint objectType, uint securityInformation,
        IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
