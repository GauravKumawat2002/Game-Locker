using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace GameLocker.Common.Security;

/// <summary>Resolves image and folder paths in the same local Windows device namespace.</summary>
public static class ProcessImagePath
{
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint VolumeNameNt = 2;
    private const uint ProcessNameNative = 1;

    /// <summary>Returns the physical device path of an eligible folder on NTFS.</summary>
    public static string? GetFolderPath(string path)
    {
        if (!PathGuard.TryValidate(path, out var folder))
            return null;

        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(folder.Path)!);
            return drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase)
                ? GetFinalFolderPath(folder.Path)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Reads the running image's device path from Windows without reopening that path.</summary>
    public static string? GetImagePath(SafeProcessHandle process, StringBuilder buffer)
    {
        var size = buffer.Capacity;
        return QueryFullProcessImageNameW(process, ProcessNameNative, buffer, ref size)
            ? buffer.ToString()
            : null;
    }

    /// <summary>Checks whether the normalized image path is a descendant of the normalized folder.</summary>
    public static bool IsInsideFolder(string imagePath, string folderPath) =>
        imagePath.StartsWith(folderPath + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static string? GetFinalFolderPath(string path)
    {
        using var handle = CreateFileW(path, 0, FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero, OpenExisting, FileFlagBackupSemantics, IntPtr.Zero);
        if (handle.IsInvalid)
            return null;

        var buffer = new StringBuilder(512);
        var size = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, VolumeNameNt);
        if (size >= buffer.Capacity)
        {
            buffer.EnsureCapacity((int)size + 1);
            size = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, VolumeNameNt);
        }

        return size > 0 && size < buffer.Capacity ? buffer.ToString() : null;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint desiredAccess, FileShare shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path,
        uint pathLength, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, StringBuilder path, ref int length);
}
