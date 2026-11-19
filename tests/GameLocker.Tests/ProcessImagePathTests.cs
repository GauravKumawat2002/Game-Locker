using System.Diagnostics;
using System.Text;
using GameLocker.Common.Security;

namespace GameLocker.Tests;

public class ProcessImagePathTests : IDisposable
{
    private readonly string _fixture = Directory.CreateTempSubdirectory("GameLocker_Images_").FullName;

    [Fact]
    [Trait("Category", "Basic")]
    public void OnlyImagesInsideTheSelectedNtfsFolderAreEligible()
    {
        var selected = Directory.CreateDirectory(Path.Combine(_fixture, "Game")).FullName;
        var adjacent = Directory.CreateDirectory(Path.Combine(_fixture, "GameBackup")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(selected, "bin")).FullName;
        var root = ProcessImagePath.GetFolderPath(selected);
        Assert.NotNull(root);

        var eligible = CopyCommandProcessor(nested);
        var unrelated = CopyCommandProcessor(adjacent);
        Assert.True(ProcessImagePath.IsInsideFolder(ReadRunningImagePath(eligible), root));
        Assert.True(ProcessImagePath.IsInsideFolder(ReadRunningImagePath(eligible.ToLowerInvariant()), root));
        Assert.False(ProcessImagePath.IsInsideFolder(ReadRunningImagePath(unrelated), root));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void AnImageLaunchedThroughAJunctionUsesItsRealDirectory()
    {
        var selected = Directory.CreateDirectory(Path.Combine(_fixture, "Game")).FullName;
        var other = Directory.CreateDirectory(Path.Combine(_fixture, "Other")).FullName;
        var alias = Path.Combine(selected, "alias");
        var outside = CopyCommandProcessor(other);

        var junction = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            Arguments = "/d /c mklink /J \"%GAMELOCKER_IMAGE_TEST_ALIAS%\" \"%GAMELOCKER_IMAGE_TEST_TARGET%\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        junction.Environment["GAMELOCKER_IMAGE_TEST_ALIAS"] = alias;
        junction.Environment["GAMELOCKER_IMAGE_TEST_TARGET"] = other;
        using var command = Process.Start(junction)!;
        Assert.True(command.WaitForExit(5000));
        Assert.Equal(0, command.ExitCode);

        try
        {
            var selectedRoot = ProcessImagePath.GetFolderPath(selected);
            var otherRoot = ProcessImagePath.GetFolderPath(other);
            Assert.NotNull(selectedRoot);
            Assert.NotNull(otherRoot);
            Assert.Null(ProcessImagePath.GetFolderPath(alias));
            var image = ReadRunningImagePath(Path.Combine(alias, Path.GetFileName(outside)));

            Assert.False(ProcessImagePath.IsInsideFolder(image, selectedRoot));
            Assert.True(ProcessImagePath.IsInsideFolder(image, otherRoot));
        }
        finally
        {
            Directory.Delete(alias);
        }
    }

    private static string CopyCommandProcessor(string directory)
    {
        var executable = Path.Combine(directory, "fixture.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), executable);
        return executable;
    }

    private static string ReadRunningImagePath(string executable)
    {
        var start = new ProcessStartInfo(executable)
        {
            Arguments = "/d /q",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        using var process = Process.Start(start)!;
        try
        {
            var path = ProcessImagePath.GetImagePath(process.SafeHandle, new StringBuilder(32768));
            Assert.NotNull(path);
            return path;
        }
        finally
        {
            if (!process.HasExited)
                process.StandardInput.WriteLine("exit");
            Assert.True(process.WaitForExit(5000));
        }
    }

    public void Dispose()
    {
        // Delete the two known fixture levels without traversing any directory links.
        foreach (var directory in new[] { @"Game\bin", "Game", "GameBackup", "Other" })
        {
            var path = Path.Combine(_fixture, directory);
            if (Directory.Exists(path))
            {
                File.Delete(Path.Combine(path, "fixture.exe"));
                Directory.Delete(path);
            }
        }

        Directory.Delete(_fixture);
    }
}
