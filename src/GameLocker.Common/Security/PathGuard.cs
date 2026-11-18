namespace GameLocker.Common.Security;

/// <summary>
/// Why a path was rejected as a lock target.
/// </summary>
public enum PathRejectionReason
{
    /// <summary>The path is eligible.</summary>
    None = 0,

    /// <summary>The path was null, empty, or whitespace.</summary>
    Empty,

    /// <summary>The path could not be canonicalised.</summary>
    Malformed,

    /// <summary>The path does not exist.</summary>
    NotFound,

    /// <summary>The path is a junction, symbolic link, or other reparse point.</summary>
    ReparsePoint,

    /// <summary>The path is the root of a drive.</summary>
    DriveRoot,

    /// <summary>The path is, or is inside, a location the operating system depends on.</summary>
    SystemLocation,

    /// <summary>The path is, or contains, GameLocker's own data directory.</summary>
    GameLockerData,
}

/// <summary>
/// Decides whether a folder may be used as a GameLocker lock target.
/// </summary>
/// <remarks>
/// Before this existed, any path reaching <c>FolderLocker</c> was operated on directly - including
/// paths supplied by the file-based command channel, which was writable by unprivileged users.
/// A command naming <c>C:\Windows\System32</c> would have been executed by a LocalSystem service.
/// See ARCHITECTURE-REVIEW.md findings [V-1], [V-4] and [D-8].
/// </remarks>
public static class PathGuard
{
    /// <summary>
    /// Determines whether a folder may be locked, and canonicalises it.
    /// </summary>
    /// <param name="path">The candidate folder path.</param>
    /// <param name="result">The canonicalised path and the outcome of the check.</param>
    /// <returns><c>true</c> when the folder is an eligible lock target.</returns>
    public static bool TryValidate(string? path, out PathGuardResult result)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            result = new PathGuardResult(string.Empty, PathRejectionReason.Empty,
                "No folder path was supplied.");
            return false;
        }

        string full;
        try
        {
            full = Normalize(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            result = new PathGuardResult(path, PathRejectionReason.Malformed,
                $"The path could not be interpreted: {ex.Message}");
            return false;
        }

        if (full.StartsWith(@"\\", StringComparison.Ordinal))
        {
            result = new PathGuardResult(full, PathRejectionReason.Malformed,
                "Select a folder on a local drive using its drive-letter path.");
            return false;
        }

        var root = Path.GetPathRoot(full);
        if (!string.IsNullOrEmpty(root) &&
            string.Equals(Normalize(root), full, StringComparison.OrdinalIgnoreCase))
        {
            result = new PathGuardResult(full, PathRejectionReason.DriveRoot,
                $"An entire drive cannot be locked: {full}");
            return false;
        }

        foreach (var forbiddenTree in ForbiddenTrees())
        {
            if (IsAtOrUnder(full, forbiddenTree))
            {
                result = new PathGuardResult(full, PathRejectionReason.SystemLocation,
                    $"'{full}' is inside a protected system location ('{forbiddenTree}') and cannot be locked.");
                return false;
            }
        }

        foreach (var container in ForbiddenExactFolders())
        {
            if (string.Equals(full, container, StringComparison.OrdinalIgnoreCase))
            {
                result = new PathGuardResult(full, PathRejectionReason.SystemLocation,
                    $"'{full}' is a top-level system container. Select an individual game folder inside it instead.");
                return false;
            }
        }

        // Locking GameLocker's own data would destroy the keys and state needed to unlock anything.
        var dataDirectory = GameLockerDataDirectory();
        if (IsAtOrUnder(full, dataDirectory) || IsAtOrUnder(dataDirectory, full))
        {
            result = new PathGuardResult(full, PathRejectionReason.GameLockerData,
                $"'{full}' overlaps GameLocker's own data directory ('{dataDirectory}').");
            return false;
        }

        try
        {
            EnsureNoReparsePoints(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            result = new PathGuardResult(full, PathRejectionReason.ReparsePoint, ex.Message);
            return false;
        }

        if (!Directory.Exists(full))
        {
            result = new PathGuardResult(full, PathRejectionReason.NotFound,
                $"The folder does not exist: {full}");
            return false;
        }

        result = new PathGuardResult(full, PathRejectionReason.None, string.Empty);
        return true;
    }

    /// <summary>
    /// Validates a folder and throws when it is not an eligible lock target.
    /// </summary>
    /// <param name="path">The candidate folder path.</param>
    /// <returns>The canonicalised path.</returns>
    /// <exception cref="InvalidOperationException">The folder may not be locked.</exception>
    public static string Validate(string? path)
    {
        if (TryValidate(path, out var result))
            return result.Path;

        throw new InvalidOperationException(result.Message);
    }

    /// <summary>
    /// Canonicalises a path: absolute, with any trailing separator removed.
    /// </summary>
    /// <param name="path">The path to canonicalise.</param>
    /// <returns>The canonical form, suitable for ordinal comparison.</returns>
    public static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    /// <summary>
    /// Determines whether <paramref name="candidate"/> is <paramref name="ancestor"/> or sits
    /// beneath it.
    /// </summary>
    /// <param name="candidate">The path to test.</param>
    /// <param name="ancestor">The containing path.</param>
    /// <returns><c>true</c> when candidate is at or under ancestor.</returns>
    public static bool IsAtOrUnder(string candidate, string ancestor)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(ancestor))
            return false;

        if (string.Equals(candidate, ancestor, StringComparison.OrdinalIgnoreCase))
            return true;

        // Compare against the ancestor plus a separator so that "C:\WindowsApps" is not treated as
        // being inside "C:\Windows".
        var prefix = ancestor.EndsWith(Path.DirectorySeparatorChar)
            ? ancestor
            : ancestor + Path.DirectorySeparatorChar;

        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static void EnsureNoReparsePoints(string path)
    {
        var current = Normalize(path);

        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException($"Junctions and symbolic links are unsupported: {current}");
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                // A destination may not exist yet. Its ancestors still need checking.
            }

            current = Path.GetDirectoryName(current);
        }
    }

    internal static EnumerationOptions FileEnumerationOptions(bool recursive) => new()
    {
        RecurseSubdirectories = recursive,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = false,
    };

    /// <summary>
    /// Locations that may never be locked, including everything beneath them.
    /// </summary>
    private static IEnumerable<string> ForbiddenTrees()
    {
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.Windows,
                     Environment.SpecialFolder.System,
                     Environment.SpecialFolder.SystemX86,
                     Environment.SpecialFolder.CommonApplicationData,
                 })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path))
                yield return Normalize(path);
        }
    }

    /// <summary>
    /// Containers that may not be locked themselves, though folders inside them may be.
    /// </summary>
    /// <remarks>
    /// Games are routinely installed under Program Files, so those trees cannot be forbidden
    /// wholesale - only the container directories themselves.
    /// </remarks>
    private static IEnumerable<string> ForbiddenExactFolders()
    {
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86,
                     Environment.SpecialFolder.CommonProgramFiles,
                     Environment.SpecialFolder.CommonProgramFilesX86,
                     Environment.SpecialFolder.UserProfile,
                 })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path))
                yield return Normalize(path);
        }

        // The parent of the current user profile is normally C:\Users.
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var usersRoot = string.IsNullOrEmpty(profile) ? null : Path.GetDirectoryName(profile);
        if (!string.IsNullOrEmpty(usersRoot))
            yield return Normalize(usersRoot);
    }

    private static string GameLockerDataDirectory() =>
        Normalize(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "GameLocker"));
}

/// <summary>
/// The outcome of a <see cref="PathGuard"/> check.
/// </summary>
/// <param name="Path">The canonicalised path, when it could be determined.</param>
/// <param name="Reason">Why the path was rejected, or <see cref="PathRejectionReason.None"/>.</param>
/// <param name="Message">A human-readable explanation, empty when the path is eligible.</param>
public readonly record struct PathGuardResult(string Path, PathRejectionReason Reason, string Message)
{
    /// <summary>Gets a value indicating whether the path may be locked.</summary>
    public bool IsEligible => Reason == PathRejectionReason.None;
}
