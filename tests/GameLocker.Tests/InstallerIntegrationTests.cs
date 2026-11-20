using System.Diagnostics;
using GameLocker.Common.Configuration;
using GameLocker.Common.Models;
using GameLocker.Common.Security;
using GameLocker.Common.Services;
using GameLocker.Common.State;
using Microsoft.Win32;
using ServiceController = System.ServiceProcess.ServiceController;

namespace GameLocker.Tests;

public sealed class InstallerIntegrationTests
{
    private const string PackageVariable = "GAMELOCKER_TEST_INSTALLER";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    [InstallerFact]
    [Trait("Category", "InstallerIntegration")]
    public async Task UninstallRequiresRecoveryAndRestoresScheduledFolder()
    {
        Assert.True(AclHelper.IsRunningAsAdmin());

        var config = new ConfigManager();
        var store = new FolderStateStore();
        Assert.False(Path.Exists(config.ConfigDirectory), "Use a fresh disposable Windows machine.");
        Assert.DoesNotContain(ServiceController.GetServices(), service =>
            service.ServiceName.Equals("GameLockerService", StringComparison.OrdinalIgnoreCase) ||
            service.ServiceName.Equals("GameLocker Service", StringComparison.OrdinalIgnoreCase));

        var package = Path.GetFullPath(Environment.GetEnvironmentVariable(PackageVariable)!);
        Assert.True(File.Exists(package), $"Installer package was not found at {package}");
        var fixture = Directory.CreateTempSubdirectory("GameLocker-Installer-");
        var gameFolder = fixture.CreateSubdirectory("game").FullName;
        var installFolder = Path.Combine(fixture.FullName, "app");
        var configUiFolder = Directory.CreateDirectory(Path.Combine(installFolder, "ConfigUI"));
        var legacyUi = Path.Combine(configUiFolder.FullName, "GameLocker.ConfigUI.exe");
        await File.WriteAllTextAsync(legacyUi, "Legacy Configuration executable fixture");
        var originalAcl = FolderAclManager.Capture(gameFolder);
        var saveFile = Path.Combine(gameFolder, "example.sav");
        await File.WriteAllTextAsync(saveFile, "GameLocker installer test fixture");

        string? invalidJournal = null;
        try
        {
            await InstallAsync(package, fixture, installFolder);
            Assert.True(File.Exists(Path.Combine(configUiFolder.FullName, "GameLocker.exe")));
            Assert.True(File.Exists(Path.Combine(configUiFolder.FullName, "GameLocker.ico")));
            Assert.False(File.Exists(legacyUi), "The installer did not replace the legacy Configuration executable.");
            await EventuallyAsync(ServiceManager.IsServiceRunning, "The installed Windows service did not start.");
            var executable = Path.Combine(installFolder, "Service", "GameLocker.Service.exe");
            var imagePath = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\GameLockerService", "ImagePath", null);
            Assert.Equal($"\"{executable}\"", imagePath as string, ignoreCase: true);

            await config.SaveConfigAsync(new GameLockerConfig
            {
                GameFolderPaths = [gameFolder],
                AllowedDays = [],
                NotificationsEnabled = false,
            });
            await EventuallyAsync(() => FolderAclManager.IsLocked(gameFolder), "The Windows service did not lock the scheduled fixture.");

            invalidJournal = store.GetRecordPath(Path.Combine(fixture.FullName, "invalid-record-fixture"));
            await File.WriteAllTextAsync(invalidJournal, "{invalid-json");

            var recovery = Path.Combine(installFolder, "Recovery", "GameLocker.Recovery.exe");
            Assert.True(File.Exists(recovery), "The installed package is missing Recovery.");

            Assert.NotEqual(0, await UninstallAsync(package, installFolder));
            Assert.True(File.Exists(recovery), "Recovery failure removed the recovery executable.");
            Assert.True(ServiceManager.IsServiceInstalled(), "Recovery failure removed the service registration.");
            Assert.False(ServiceManager.IsServiceRunning(), "The service remained running after recovery failed.");
            Assert.True(File.Exists(invalidJournal), "The uninstaller discarded an unreadable recovery journal.");

            File.Delete(invalidJournal);
            invalidJournal = null;
            Assert.True(await ServiceManager.StartServiceAsync());
            await EventuallyAsync(() => FolderAclManager.IsLocked(gameFolder), "The restarted service did not relock the fixture.");

            Assert.Equal(0, await UninstallAsync(package, installFolder));
            await EventuallyAsync(() => !ServiceManager.IsServiceInstalled(), "Successful uninstall left the service registered.");
            Assert.False(File.Exists(recovery), "Successful uninstall left the recovery executable installed.");
            Assert.True(FolderAclManager.MatchesSnapshot(gameFolder, originalAcl));
            Assert.Equal("GameLocker installer test fixture", await File.ReadAllTextAsync(saveFile));
            Assert.True(File.Exists(config.ConfigFilePath), "Successful uninstall removed the saved configuration.");
        }
        finally
        {
            if (ServiceManager.IsServiceRunning())
                await ServiceManager.StopServiceAsync();

            if (invalidJournal is not null)
                File.Delete(invalidJournal);

            if (!FolderAclManager.MatchesSnapshot(gameFolder, originalAcl))
                FolderAclManager.Restore(gameFolder, originalAcl);
        }
    }

    private static async Task InstallAsync(string package, DirectoryInfo fixture, string installFolder)
    {
        if (Path.GetExtension(package).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var payload = fixture.CreateSubdirectory("payload");
            System.IO.Compression.ZipFile.ExtractToDirectory(package, payload.FullName);
            var script = Path.Combine(payload.FullName, "GameLocker-Setup.ps1");
            Assert.Equal(0, await RunAsync("powershell.exe", "-NoProfile", "-NonInteractive", "-File", script, "-InstallPath", installFolder, "-Silent"));
        }
        else
        {
            Assert.Equal(".exe", Path.GetExtension(package), ignoreCase: true);
            Assert.Equal(0, await RunAsync(package, "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/TASKS=startservice", $"/DIR={installFolder}"));
        }
    }

    private static Task<int> UninstallAsync(string package, string installFolder)
    {
        if (Path.GetExtension(package).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var script = Path.Combine(installFolder, "GameLocker-Uninstall.ps1");
            Assert.True(File.Exists(script));
            return RunAsync("powershell.exe", "-NoProfile", "-NonInteractive", "-File", script);
        }

        var uninstaller = Assert.Single(Directory.EnumerateFiles(installFolder, "unins*.exe"));
        return RunAsync(uninstaller, "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART");
    }

    private static async Task<int> RunAsync(string executable, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {executable}.");
        using var timeout = new CancellationTokenSource(Timeout);
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errors = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            Console.WriteLine(await output);
            Console.Error.WriteLine(await errors);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            throw new TimeoutException($"{Path.GetFileName(executable)} exceeded {Timeout.TotalSeconds} seconds.");
        }
    }

    private static async Task EventuallyAsync(Func<bool> condition, string message)
    {
        var started = Stopwatch.GetTimestamp();
        while (!condition())
        {
            Assert.True(Stopwatch.GetElapsedTime(started) < Timeout, message);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    private sealed class InstallerFactAttribute : FactAttribute
    {
        public InstallerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PackageVariable)))
                Skip = $"Set {PackageVariable} only on a fresh disposable Windows machine.";
        }
    }
}
