using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using GameLocker.Common.Security;
using Microsoft.Win32.SafeHandles;

namespace GameLocker.Service;

/// <summary>Requests an ordinary window close from the desktop where a matching game runs.</summary>
internal static class InteractiveGameCloser
{
    internal const string Command = "--close-session-apps";
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HelperExitAllowance = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ExitPollInterval = TimeSpan.FromMilliseconds(50);
    private const uint QueryAndSynchronize = 0x00101000;
    private const uint WaitTimeout = 258;
    private const uint CloseMessage = 0x0010;
    private const uint NoWindowWithUnicodeEnvironment = 0x08000400;
    private const int AccessDenied = 5;

    [Flags]
    private enum CloseResult
    {
        Finished = 0,
        UnavailableWindow = 1,
        MessageRefused = 2,
        StillRunning = 4,
        InvalidRequest = 8,
    }

    private readonly record struct Target(int Id, long CreationTime);

    internal static async Task RequestAsync(
        IEnumerable<string> folders, ILogger logger, CancellationToken cancellationToken)
    {
        var roots = folders.Select(ProcessImagePath.GetFolderPath).OfType<string>().ToArray();
        if (roots.Length == 0)
            return;

        var sessions = FindTargets(roots, cancellationToken);
        if (sessions.Count == 0)
            return;

        var executable = Environment.ProcessPath;
        if (executable is null || !Path.IsPathFullyQualified(executable) ||
            !Path.GetFileName(executable).Equals("GameLocker.Service.exe", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Game close requests require the installed GameLocker service executable.");
            return;
        }

        var elapsed = Stopwatch.StartNew();
        var deadline = DateTime.UtcNow + GracePeriod - HelperExitAllowance;
        var helpers = new List<(int Session, SafeProcessHandle Process, int[] TargetIds)>();
        try
        {
            foreach (var (sessionId, targets) in sessions)
            {
                foreach (var batch in targets.Chunk(512))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (elapsed.Elapsed >= GracePeriod)
                    {
                        logger.LogWarning("Game close request window expired before session {Session} could be reached.", sessionId);
                        break;
                    }

                    var targetIds = batch.Select(target => target.Id).ToArray();
                    try
                    {
                        helpers.Add((sessionId, StartHelper(sessionId, executable, deadline, batch), targetIds));
                    }
                    catch (Win32Exception ex)
                    {
                        logger.LogWarning(ex, "Could not request a graceful game exit in session {Session} for process IDs {Ids}.",
                            sessionId, targetIds);
                    }
                }
            }

            while (helpers.Any(helper => IsRunning(helper.Process)) && elapsed.Elapsed < GracePeriod)
                await Task.Delay(ExitPollInterval, cancellationToken);

            foreach (var helper in helpers)
            {
                if (IsRunning(helper.Process) || !GetExitCodeProcess(helper.Process, out var result))
                {
                    logger.LogWarning(
                        "Graceful game exit could not be confirmed within the grace period in session {Session} for process IDs {Ids}.",
                        helper.Session, helper.TargetIds);
                }
                else if (result != CloseResult.Finished)
                {
                    logger.LogWarning(
                        "Graceful game exit was incomplete in session {Session} for process IDs {Ids}: {Result}.",
                        helper.Session, helper.TargetIds, result);
                }
                else
                {
                    logger.LogInformation("Game processes exited in session {Session}: {Ids}.", helper.Session, helper.TargetIds);
                }
            }
        }
        finally
        {
            foreach (var helper in helpers)
                helper.Process.Dispose();
        }
    }

    private static Dictionary<int, List<Target>> FindTargets(string[] roots, CancellationToken cancellationToken)
    {
        var sessions = new Dictionary<int, List<Target>>();
        var imageBuffer = new StringBuilder(32768);
        var processes = Process.GetProcesses();
        try
        {
            foreach (var process in processes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = process.Id;
                if (id == Environment.ProcessId || !ProcessIdToSessionId(id, out var sessionId) || sessionId == 0)
                    continue;

                using var handle = OpenProcess(QueryAndSynchronize, false, id);
                if (handle.IsInvalid || !IsRunning(handle))
                    continue;

                var image = ProcessImagePath.GetImagePath(handle, imageBuffer);
                if (image is null || !roots.Any(root => ProcessImagePath.IsInsideFolder(image, root)) ||
                    !GetProcessTimes(handle, out var created, out _, out _, out _))
                    continue;

                if (!sessions.TryGetValue(sessionId, out var targets))
                    sessions.Add(sessionId, targets = []);
                targets.Add(new Target(id, created));
            }
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }

        return sessions;
    }

    internal static async Task<int> RunHelperAsync(string[] args)
    {
        if (args.Length < 3 || !long.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var deadlineTicks) ||
            deadlineTicks > DateTime.MaxValue.Ticks ||
            !ProcessIdToSessionId(Environment.ProcessId, out var sessionId) || sessionId == 0)
            return (int)CloseResult.InvalidRequest;

        var deadline = new DateTime(deadlineTicks, DateTimeKind.Utc);
        var elapsed = Stopwatch.StartNew();
        var processes = new List<SafeProcessHandle>();
        var result = CloseResult.Finished;
        try
        {
            foreach (var argument in args.Skip(2).Distinct(StringComparer.Ordinal))
            {
                if (DateTime.UtcNow >= deadline || elapsed.Elapsed >= GracePeriod)
                    return (int)(result | CloseResult.StillRunning);

                var parts = argument.Split(':', 2);
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var processId) ||
                    !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var created) ||
                    processId <= 0 || processId == Environment.ProcessId)
                {
                    result |= CloseResult.InvalidRequest;
                    continue;
                }

                var handle = OpenProcess(QueryAndSynchronize, false, processId);
                if (handle.IsInvalid)
                {
                    if (Marshal.GetLastWin32Error() == AccessDenied)
                        result |= CloseResult.MessageRefused;
                    handle.Dispose();
                    continue;
                }

                if (!IsRunning(handle))
                {
                    handle.Dispose();
                    continue;
                }

                if (!GetProcessTimes(handle, out var observedCreationTime, out _, out _, out _) ||
                    observedCreationTime != created ||
                    !ProcessIdToSessionId(processId, out var targetSession) || targetSession != sessionId)
                {
                    handle.Dispose();
                    result |= CloseResult.InvalidRequest;
                    continue;
                }

                processes.Add(handle);
                IntPtr window;
                try
                {
                    using var process = Process.GetProcessById(processId);
                    window = process.MainWindowHandle;
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    continue;
                }

                if (window == IntPtr.Zero || !IsWindowEnabled(window) ||
                    GetWindowThreadProcessId(window, out var windowProcess) == 0 || windowProcess != processId)
                {
                    result |= CloseResult.UnavailableWindow;
                    continue;
                }

                if (DateTime.UtcNow >= deadline || elapsed.Elapsed >= GracePeriod)
                    return (int)(result | CloseResult.StillRunning);

                if (IsRunning(handle) && !PostMessageW(window, CloseMessage, IntPtr.Zero, IntPtr.Zero))
                    result |= CloseResult.MessageRefused;
            }

            while (processes.Any(IsRunning))
            {
                if (DateTime.UtcNow >= deadline || elapsed.Elapsed >= GracePeriod)
                    return (int)(result | CloseResult.StillRunning);

                await Task.Delay(ExitPollInterval);
            }

            return (int)result;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    private static SafeProcessHandle StartHelper(int sessionId, string executable, DateTime deadline, Target[] targets)
    {
        if (!WTSQueryUserToken(sessionId, out var token))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not obtain the interactive session token.");

        using (token)
        {
            if (!CreateEnvironmentBlock(out var environment, token, false))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not build the interactive session environment.");

            try
            {
                var commandLine = new StringBuilder();
                commandLine.Append(CultureInfo.InvariantCulture, $"\"{executable}\" {Command} {deadline.Ticks}");
                foreach (var target in targets)
                    commandLine.Append(CultureInfo.InvariantCulture, $" {target.Id}:{target.CreationTime}");

                var startup = new StartupInfo
                {
                    Size = (uint)Marshal.SizeOf<StartupInfo>(),
                    Desktop = @"winsta0\default",
                };
                if (!CreateProcessAsUserW(token, executable, commandLine, IntPtr.Zero, IntPtr.Zero,
                    false, NoWindowWithUnicodeEnvironment, environment, Path.GetDirectoryName(executable)!,
                    ref startup, out var child))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not start the game close helper on the interactive desktop.");

                using var thread = new SafeWaitHandle(child.Thread, ownsHandle: true);
                return new SafeProcessHandle(child.Process, ownsHandle: true);
            }
            finally
            {
                DestroyEnvironmentBlock(environment);
            }
        }
    }

    private static bool IsRunning(SafeProcessHandle handle) => WaitForSingleObject(handle, 0) == WaitTimeout;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public uint Size;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public ushort ShowWindow, ReservedSize;
        public IntPtr ReservedBytes, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public int ProcessId, ThreadId;
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQueryUserToken(int sessionId, out SafeAccessTokenHandle token);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, SafeAccessTokenHandle token,
        [MarshalAs(UnmanagedType.Bool)] bool inherit);

    [DllImport("userenv.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessAsUserW(SafeAccessTokenHandle token, string application, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags, IntPtr environment, string currentDirectory, ref StartupInfo startup, out ProcessInformation child);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProcessIdToSessionId(int processId, out int sessionId);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long creationTime, out long exitTime,
        out long kernelTime, out long userTime);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(SafeProcessHandle process, out CloseResult exitCode);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
