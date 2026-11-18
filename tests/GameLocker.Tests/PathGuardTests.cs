using System.Diagnostics;
using GameLocker.Common.Security;
using GameLocker.Common.Services;

namespace GameLocker.Tests;

/// <summary>
/// Tests for <see cref="PathGuard"/>, which decides what may be used as a lock target.
/// </summary>
/// <remarks>
/// Before this existed, any path reaching FolderLocker was acted on. Because the service runs as
/// LocalSystem and read its commands from a world-writable file, an unprivileged user could name
/// C:\Windows and have it operated on. See ARCHITECTURE-REVIEW.md findings [V-1] and [V-4].
/// </remarks>
public class PathGuardTests : IDisposable
{
    private readonly string _testFolder;

    public PathGuardTests()
    {
        _testFolder = Path.Combine(Path.GetTempPath(), $"GameLocker_Guard_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testFolder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testFolder))
                Directory.Delete(_testFolder, recursive: true);
        }
        catch
        {
            // Ignore cleanup failures.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void OrdinaryFolder_IsEligible()
    {
        Assert.True(PathGuard.TryValidate(_testFolder, out var result));
        Assert.Equal(PathRejectionReason.None, result.Reason);
        Assert.True(result.IsEligible);
    }

    [Theory]
    [Trait("Category", "Basic")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPath_IsRejected(string? path)
    {
        Assert.False(PathGuard.TryValidate(path, out var result));
        Assert.Equal(PathRejectionReason.Empty, result.Reason);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void MissingFolder_IsRejected()
    {
        var missing = Path.Combine(_testFolder, "does-not-exist");

        Assert.False(PathGuard.TryValidate(missing, out var result));
        Assert.Equal(PathRejectionReason.NotFound, result.Reason);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void DriveRoot_IsRejected()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory);

        Assert.False(PathGuard.TryValidate(root, out var result));
        Assert.Equal(PathRejectionReason.DriveRoot, result.Reason);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void WindowsDirectory_IsRejected()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        Assert.False(PathGuard.TryValidate(windows, out var result));
        Assert.Equal(PathRejectionReason.SystemLocation, result.Reason);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void System32_IsRejected()
    {
        // The exact exploit payload: an unprivileged user naming System32 in the command channel.
        Assert.False(PathGuard.TryValidate(Environment.SystemDirectory, out var result));
        Assert.Equal(PathRejectionReason.SystemLocation, result.Reason);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void ProgramDataTree_IsRejected()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        Assert.False(PathGuard.TryValidate(programData, out var result));
        Assert.Equal(PathRejectionReason.SystemLocation, result.Reason);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void ProgramFilesItself_IsRejectedButSubfoldersAreNot()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        Assert.False(PathGuard.TryValidate(programFiles, out var result));
        Assert.Equal(PathRejectionReason.SystemLocation, result.Reason);

        // Games are routinely installed under Program Files, so the tree cannot be forbidden
        // wholesale - only the container itself.
        var game = Path.Combine(programFiles, "SomePublisher", "SomeGame");
        Assert.False(PathGuard.TryValidate(game, out var missing));
        Assert.Equal(PathRejectionReason.NotFound, missing.Reason); // rejected for absence, not location
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void UsersRoot_IsRejected()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var usersRoot = Path.GetDirectoryName(profile);

        Assert.NotNull(usersRoot);
        Assert.False(PathGuard.TryValidate(usersRoot, out var result));
        Assert.Equal(PathRejectionReason.SystemLocation, result.Reason);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void TrailingSeparatorAndCasing_NormaliseToTheSamePath()
    {
        Assert.True(PathGuard.TryValidate(_testFolder + Path.DirectorySeparatorChar, out var withSlash));
        Assert.True(PathGuard.TryValidate(_testFolder, out var plain));

        Assert.Equal(plain.Path, withSlash.Path);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void RelativeSegments_AreResolvedBeforeTheSystemCheck()
    {
        // A traversal dressed up to look like an ordinary folder must not slip past the checks.
        var escape = Path.Combine(_testFolder, "..", "..", "..", "Windows");

        Assert.False(PathGuard.TryValidate(escape, out var result));
        Assert.NotEqual(PathRejectionReason.None, result.Reason);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void Validate_ThrowsForIneligiblePaths()
    {
        Assert.Throws<InvalidOperationException>(() => PathGuard.Validate(Environment.SystemDirectory));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void JunctionAncestryIsRejectedAndScansSkipTheJunction()
    {
        var selected = Directory.CreateDirectory(Path.Combine(_testFolder, "selected")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(_testFolder, "target", "child")).FullName;
        var junction = Path.Combine(selected, "linked");
        File.WriteAllText(Path.Combine(target, "outside.sav"), "fixture data");
        File.WriteAllText(Path.Combine(selected, "inside.cfg"), "fixture data");

        var command = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
        {
            Arguments = "/d /c mklink /J \"%GAMELOCKER_TEST_JUNCTION%\" \"%GAMELOCKER_TEST_TARGET%\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        command.Environment["GAMELOCKER_TEST_JUNCTION"] = junction;
        command.Environment["GAMELOCKER_TEST_TARGET"] = Path.GetDirectoryName(target);
        using var process = Process.Start(command)!;
        Assert.True(process.WaitForExit(5000));
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());

        try
        {
            Assert.False(PathGuard.TryValidate(Path.Combine(junction, "child"), out var rejected));
            Assert.Equal(PathRejectionReason.ReparsePoint, rejected.Reason);

            var scan = new FileExtensionScanner().ScanFolderExtensions(selected);
            Assert.Null(scan.ErrorMessage);
            Assert.Single(scan.Extensions);
            Assert.True(scan.Extensions.ContainsKey(".cfg"));
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    [Theory]
    [Trait("Category", "Basic")]
    [InlineData(@"C:\Games\Title", @"C:\Games", true)]
    [InlineData(@"C:\Games", @"C:\Games", true)]
    [InlineData(@"C:\GamesOther", @"C:\Games", false)]
    [InlineData(@"C:\games\title", @"C:\Games", true)]
    public void IsAtOrUnder_DoesNotTreatSiblingPrefixesAsChildren(string candidate, string ancestor, bool expected)
    {
        Assert.Equal(expected, PathGuard.IsAtOrUnder(candidate, ancestor));
    }
}
