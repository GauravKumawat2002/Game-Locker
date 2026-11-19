using System.Diagnostics;
using GameLocker.Common.Configuration;
using GameLocker.Common.Models;
using GameLocker.Common.Notifications;
using GameLocker.Common.Security;
using GameLocker.Common.Services;

namespace GameLocker.Service;

/// <summary>
/// The GameLocker Windows Service. Continuously reconciles each configured folder's actual state
/// against the state the schedule requires.
/// </summary>
/// <remarks>
/// This is a convergence loop, not a state machine. Earlier versions acted only when a boolean
/// flipped from "allowed" to "not allowed", and updated that flag whether or not the work had
/// succeeded - so one transient failure at the boundary minute meant the folders stayed unlocked
/// for the entire period, with nothing retried and nothing reported. That is the "sometimes it
/// works, sometimes it doesn't" behaviour.
/// <para>
/// Every cycle now asks the same two questions for every folder - what should it be, and what is it
/// - and acts on the difference. The result of an action is never trusted: the folder is re-observed
/// afterwards, and only a folder that genuinely reached the required state counts as done.
/// See ARCHITECTURE-REVIEW.md findings [S-1], [S-3], [S-4], [S-5] and [S-7].
/// </para>
/// </remarks>
public sealed class GameLockerService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ClockJumpThreshold = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryInitialDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RetryMaxDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ProblemLogInterval = TimeSpan.FromMinutes(10);

    private readonly ILogger<GameLockerService> _logger;
    private readonly ConfigManager _configManager;
    private readonly FolderLocker _folderLocker;

    private readonly RetryPolicy _retryPolicy = new(RetryInitialDelay, RetryMaxDelay);
    private readonly LockWarningTracker _warnings = new(15, 5);
    private readonly ClockDriftDetector _clockDrift = new(ClockJumpThreshold);
    private readonly Stopwatch _monotonic = Stopwatch.StartNew();
    private readonly HashSet<string> _gameCloseRequested = new(StringComparer.OrdinalIgnoreCase);

    private GameLockerConfig? _config;
    private bool? _lastAnnouncedAllowed;
    private DateTime? _nextUnlockAfterCloseRequest;
    private DateTime _lastProblemLogUtc = DateTime.MinValue;

    public GameLockerService(ILogger<GameLockerService> logger)
    {
        _logger = logger;
        _configManager = new ConfigManager();
        _folderLocker = new FolderLocker(_configManager.ConfigDirectory, logger);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "GameLocker Service starting. Reconciling at schedule transitions and polling every {Seconds} seconds.",
            TickInterval.TotalSeconds);

        // Restrict the state store on every start, not only at install time.
        if (!_folderLocker.StateStore.EnsureSecured())
            throw new UnauthorizedAccessException($"Could not secure {_folderLocker.StateStore.StateDirectory}.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TickInterval;
            try
            {
                delay = await ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                // A schedule transition interrupted work. Re-derive the desired state immediately.
                continue;
            }
            catch (Exception ex)
            {
                // A single bad cycle must never take the loop down; the next one re-observes
                // everything from scratch anyway.
                _logger.LogError(ex, "Reconciliation cycle failed: {Message}", ex.Message);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("GameLocker Service stopped.");
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GameLocker Service stopping...");

        if (_config?.NotificationsEnabled == true)
            TryNotify(NotificationHelper.ShowServiceStoppedNotification);

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Runs one reconciliation cycle.
    /// </summary>
    /// <param name="cancellationToken">Cancels the cycle when the service is stopping.</param>
    /// <returns>How long to wait before reconciling again.</returns>
    private async Task<TimeSpan> ReconcileAsync(CancellationToken cancellationToken)
    {
        DetectClockJump();

        await LoadConfigurationAsync();
        await ProcessReloadSignalAsync();
        await ProcessImmediateCommandsAsync();

        // Captured after every step that can replace it, so the rest of the cycle works from one
        // consistent view.
        var config = _config;
        if (config is null || config.GameFolderPaths.Count == 0)
        {
            await RestoreRemovedFoldersAsync(config, cancellationToken.ThrowIfCancellationRequested);
            return TickInterval;
        }

        var observedAt = DateTime.Now;
        var schedule = config.GetScheduleState(observedAt);
        var desired = schedule.IsAllowed ? FolderState.Unlocked : FolderState.Locked;
        await RequestGameClosuresAsync(config, schedule, cancellationToken);
        CheckForInterruption();

        var deferVault = desired == FolderState.Locked && config.LockMode == LockMode.AclAndVault;
        var pendingVault = new List<(string FolderPath, string RetryKey)>();
        var total = config.GameFolderPaths.Count;
        var correct = 0;
        var changed = 0;
        var failed = 0;
        var deferred = 0;

        foreach (var folderPath in config.GameFolderPaths)
        {
            CheckForInterruption();
            var retryKey = $"{desired}|{folderPath}";

            var observed = _folderLocker.GetFolderState(folderPath);
            CheckForInterruption();

            if (observed == desired)
            {
                _retryPolicy.RecordSuccess(retryKey);
                correct++;
                continue;
            }

            if (!_retryPolicy.ShouldAttempt(retryKey, DateTime.UtcNow))
            {
                deferred++;
                continue;
            }

            _logger.LogInformation(
                "{Folder} is {Observed} but should be {Desired}. Converging.",
                folderPath, observed, desired);

            var error = desired == FolderState.Locked
                ? await TryLockAsync(folderPath, CheckForInterruption, deferVault)
                : await TryUnlockAsync(folderPath, CheckForInterruption);

            CheckForInterruption();
            if (deferVault && error is null)
                pendingVault.Add((folderPath, retryKey));
            else
                ObserveResult(folderPath, retryKey, error);
        }

        // Apply all folder restrictions before the first potentially slow vault transfer.
        foreach (var (folderPath, retryKey) in pendingVault)
        {
            CheckForInterruption();
            var error = await TryLockAsync(folderPath, CheckForInterruption);
            ObserveResult(folderPath, retryKey, error);
        }

        if (changed > 0 || failed > 0 || deferred > 0)
        {
            _logger.LogInformation(
                "Reconciled: {Correct}/{Total} correct, {Changed} changed, {Failed} failed, {Deferred} deferred.",
                correct, total, changed, failed, deferred);
        }

        CheckForInterruption();
        var now = DateTime.Now;
        AnnounceTransition(schedule.IsAllowed, correct, total, now);
        ShowWarningIfDue(schedule.IsAllowed, now);
        await RestoreRemovedFoldersAsync(config, CheckForInterruption);
        return schedule.GetRecheckDelay(DateTime.Now, TickInterval);

        void CheckForInterruption()
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTime.Now;
            if ((now < observedAt || (schedule.NextTransition is { } transition && now >= transition)) &&
                config.GetScheduleState(now) != schedule)
            {
                throw new OperationCanceledException("The gaming schedule changed during reconciliation.");
            }
        }

        void ObserveResult(string folderPath, string retryKey, string? error)
        {
            CheckForInterruption();
            // Re-observe. The return value of the operation is not evidence: the whole class of
            // "reported success while the folder was untouched" bugs came from believing it.
            var after = _folderLocker.GetFolderState(folderPath);
            CheckForInterruption();

            if (after == desired)
            {
                _retryPolicy.RecordSuccess(retryKey);
                correct++;
                changed++;
                _logger.LogInformation("{Folder} is now {State}.", folderPath, after);
            }
            else
            {
                failed++;
                var delay = _retryPolicy.RecordFailure(retryKey, DateTime.UtcNow);

                _logger.LogError(
                    "{Folder} is still {Observed} after attempting to make it {Desired} " +
                    "(attempt {Attempt}). {Error} Retrying in {Delay}.",
                    folderPath, after, desired, _retryPolicy.FailureCount(retryKey),
                    error ?? "No error was reported.", delay);
            }
        }
    }

    private async Task RequestGameClosuresAsync(GameLockerConfig config, ScheduleState schedule, CancellationToken cancellationToken)
    {
        if (schedule.IsAllowed || (_nextUnlockAfterCloseRequest is { } nextUnlock && DateTime.Now >= nextUnlock))
            _gameCloseRequested.Clear();

        _nextUnlockAfterCloseRequest = schedule.IsAllowed ? null : schedule.NextTransition;
        if (schedule.IsAllowed)
            return;

        var folders = config.GameFolderPaths.Select(NormalizeConfiguredPath).Where(_gameCloseRequested.Add).ToArray();
        if (folders.Length == 0)
            return;

        try
        {
            await InteractiveGameCloser.RequestAsync(folders, _logger, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not request graceful game exit before applying the scheduled folder lock.");
        }
    }

    private async Task RestoreRemovedFoldersAsync(GameLockerConfig? config, Action checkForInterruption)
    {
        var configured = (config?.GameFolderPaths ?? []).Select(NormalizeConfiguredPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var restore = _folderLocker.StateStore.LoadAll(LogProblemThrottled)
            .Where(record => record.HasOutstandingChanges &&
                (record.TestLock || (config is not null && !configured.Contains(record.FolderPath))))
            .Select(record => record.FolderPath)
            .Concat((config?.FoldersPendingRestore ?? []).Select(NormalizeConfiguredPath)
                .Where(path => !configured.Contains(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in restore)
        {
            checkForInterruption();
            var retryKey = $"restore|{folder}";
            var now = DateTime.UtcNow;
            if (!_retryPolicy.ShouldAttempt(retryKey, now))
                continue;

            if (_folderLocker.GetFolderState(folder) == FolderState.Unlocked)
            {
                _retryPolicy.RecordSuccess(retryKey);
                continue;
            }

            var error = await TryUnlockAsync(folder, checkForInterruption);
            if (error is null)
            {
                _retryPolicy.RecordSuccess(retryKey);
                _logger.LogInformation("Restored permissions and vault data for {Folder}.", folder);
            }
            else
            {
                var delay = _retryPolicy.RecordFailure(retryKey, now);
                _logger.LogWarning("Could not restore {Folder}: {Error}. Retrying in {Delay}.", folder, error, delay);
            }
        }
    }

    private static string NormalizeConfiguredPath(string path)
    {
        try { return PathGuard.Normalize(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    /// <summary>
    /// Notices that the wall clock has moved independently of elapsed time and forces a clean slate.
    /// </summary>
    private void DetectClockJump()
    {
        if (!_clockDrift.Sample(DateTime.UtcNow, _monotonic.Elapsed, out var drift))
            return;

        _logger.LogWarning(
            "The system clock moved by {Drift} relative to elapsed real time. " +
            "This indicates sleep, resume, a daylight-saving transition, or a clock change. " +
            "Retry backoff and pending warnings have been reset.",
            drift);

        // Every deadline held in memory was computed against a wall clock that is no longer
        // comparable, so all of them are discarded and this cycle re-derives the truth.
        _retryPolicy.Reset();
        _warnings.Reset();
        _lastAnnouncedAllowed = null;
    }

    /// <summary>
    /// Announces a lock or unlock, but only once every folder has actually reached the new state.
    /// </summary>
    /// <param name="allowed">Whether gaming is currently allowed.</param>
    /// <param name="correct">How many folders are in the required state.</param>
    /// <param name="total">How many folders are configured.</param>
    /// <param name="now">The current local time.</param>
    private void AnnounceTransition(bool allowed, int correct, int total, DateTime now)
    {
        if (_lastAnnouncedAllowed == allowed || correct != total)
            return;

        var isFirstObservation = _lastAnnouncedAllowed is null;
        _lastAnnouncedAllowed = allowed;

        // Nothing to announce on the first pass after startup - the folders were already in the
        // right state and the user did not just experience a transition.
        if (isFirstObservation)
            return;

        var config = _config;

        if (config?.NotificationsEnabled != true)
            return;

        if (allowed)
        {
            var lockTime = config.GetNextLockTime(now);
            TryNotify(() => NotificationHelper.ShowUnlockNotification(total, lockTime));
        }
        else
        {
            var nextUnlock = config.GetNextUnlockTime(now);
            TryNotify(() => NotificationHelper.ShowLockNotification(total, nextUnlock));
        }
    }

    /// <summary>
    /// Shows the "your session ends soon" warning when a threshold has just been crossed.
    /// </summary>
    /// <param name="allowed">Whether gaming is currently allowed.</param>
    /// <param name="now">The current local time.</param>
    private void ShowWarningIfDue(bool allowed, DateTime now)
    {
        var config = _config;

        if (config is null)
            return;

        // Passing null when no window is open is what re-arms the thresholds for the next one.
        var lockTime = allowed ? config.GetNextLockTime(now) : null;
        var threshold = _warnings.GetWarningToShow(lockTime, now);

        if (threshold is null || !config.NotificationsEnabled)
            return;

        TryNotify(() => NotificationHelper.ShowWarningNotification(threshold.Value));
    }

    /// <summary>
    /// Locks one folder and returns the reported error, if any.
    /// </summary>
    /// <param name="folderPath">The folder to lock.</param>
    /// <returns>The error message, or <c>null</c>.</returns>
    private async Task<string?> TryLockAsync(
        string folderPath, Action? checkForInterruption = null, bool deferVault = false)
    {
        try
        {
            var config = _config;
            var mode = config?.LockMode ?? LockMode.AclOnly;

            // The vault settings are only consulted when the user has explicitly opted in. In the
            // default AclOnly mode nothing reads or writes a single file.
            var vaultSettings = mode == LockMode.AclAndVault && config is not null
                ? config.GetFolderEncryptionSettings(folderPath)
                : null;

            var result = await _folderLocker.LockFolderAsync(folderPath, mode, vaultSettings, checkForInterruption, deferVault);

            return result.LastError;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Unlocks one folder and returns the reported error, if any.
    /// </summary>
    /// <param name="folderPath">The folder to unlock.</param>
    /// <returns>The error message, or <c>null</c>.</returns>
    private async Task<string?> TryUnlockAsync(string folderPath, Action? checkForInterruption = null)
    {
        try
        {
            var result = await _folderLocker.UnlockFolderAsync(folderPath, checkForInterruption);
            return result.LastError;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Reloads the configuration, distinguishing "not saved yet" from "damaged".
    /// </summary>
    private async Task LoadConfigurationAsync()
    {
        var result = await _configManager.LoadConfigResultAsync();

        switch (result.Status)
        {
            case ConfigLoadStatus.Loaded:
                var isFirstLoad = _config is null;
                var loaded = result.Config!;
                _config = loaded;

                if (isFirstLoad)
                {
                    _logger.LogInformation(
                        "Configuration loaded. Monitoring {Count} folder(s) in {Mode} mode.",
                        loaded.GameFolderPaths.Count, loaded.LockMode);

                    if (loaded.NotificationsEnabled)
                        TryNotify(NotificationHelper.ShowServiceStartedNotification);
                }

                break;

            case ConfigLoadStatus.NotFound when _config is not null:
                // The file was there a moment ago. Keep running on what we have rather than
                // silently stopping enforcement.
                LogProblemThrottled(
                    "The configuration file has disappeared. Continuing with the last known " +
                    "configuration; folders are still being enforced.");
                break;

            case ConfigLoadStatus.NotFound:
                LogProblemThrottled("No configuration has been saved yet. Waiting for the ConfigUI.");
                break;

            case ConfigLoadStatus.Unreadable:
                // Never treated as a first run. A damaged configuration is an error the user has to
                // know about, not an empty one to be quietly adopted.
                LogProblemThrottled(
                    $"The configuration exists but could not be read: {result.Error} " +
                    (_config is null
                        ? "No folders are being enforced. Restore config.dat.bak or reconfigure."
                        : "Continuing with the last known configuration."));
                break;
        }
    }

    /// <summary>
    /// Logs a persistent problem at most once per <see cref="ProblemLogInterval"/>.
    /// </summary>
    /// <param name="message">The message to log.</param>
    private void LogProblemThrottled(string message)
    {
        var utcNow = DateTime.UtcNow;

        if (utcNow - _lastProblemLogUtc < ProblemLogInterval)
            return;

        _lastProblemLogUtc = utcNow;
        _logger.LogWarning("{Message}", message);
    }

    /// <summary>
    /// Runs a notification without letting it affect the caller.
    /// </summary>
    /// <param name="show">The notification to show.</param>
    /// <remarks>
    /// A LocalSystem service runs in session 0 with no registered application identity, so a toast
    /// can fail. It previously did so on the path that recorded the state transition, which left
    /// the service re-attempting the whole lock every cycle.
    /// See ARCHITECTURE-REVIEW.md finding [S-6].
    /// </remarks>
    private void TryNotify(Action show)
    {
        try
        {
            show();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not show notification: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Consumes the reload signal written by the ConfigUI on save.
    /// </summary>
    private async Task ProcessReloadSignalAsync()
    {
        var signalFile = Path.Combine(_configManager.ConfigDirectory, "reload_signal");

        if (!File.Exists(signalFile))
            return;

        try
        {
            File.Delete(signalFile);
            _logger.LogInformation("Reload signal received.");

            await LoadConfigurationAsync();

            // The user has just changed something, so give every folder an immediate attempt
            // rather than waiting out any backoff from before the change.
            _retryPolicy.Reset();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing reload signal: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Consumes a one-shot command written by the ConfigUI.
    /// </summary>
    /// <remarks>
    /// This file-based channel is replaced by an access-controlled named pipe in Phase 3 of
    /// REMEDIATION-PLAN.md. Until then every path is checked against the folders GameLocker
    /// actually manages, because the directory has historically been writable by any user and this
    /// service runs as LocalSystem. See ARCHITECTURE-REVIEW.md findings [V-1] and [S-9].
    /// <para>
    /// Note that a manual lock or unlock does not survive the schedule: the next reconciliation
    /// cycle restores whatever the schedule requires. That is deliberate.
    /// </para>
    /// </remarks>
    private async Task ProcessImmediateCommandsAsync()
    {
        var commandFile = Path.Combine(_configManager.ConfigDirectory, "immediate_action");

        if (!File.Exists(commandFile))
            return;

        try
        {
            var commandText = await File.ReadAllTextAsync(commandFile);
            File.Delete(commandFile);

            var parts = commandText.Split('|');

            if (parts.Length < 3)
            {
                _logger.LogWarning("Ignoring malformed command: {Command}", commandText);
                return;
            }

            var action = parts[0];
            var folderPath = parts[1];

            if (!IsManagedFolder(folderPath, allowRecoveryOnly: !action.Equals("lock", StringComparison.OrdinalIgnoreCase), out var resolvedPath))
            {
                _logger.LogWarning(
                    "Rejected command '{Action}' for '{Folder}': it is not a folder GameLocker manages.",
                    action, folderPath);
                return;
            }

            _logger.LogInformation("Command received: {Action} for {Folder}", action, resolvedPath);

            var error = action.ToLowerInvariant() switch
            {
                "lock" => await TryLockAsync(resolvedPath),
                "unlock" or "remove" => await TryUnlockAsync(resolvedPath),
                _ => $"Unknown action '{action}'.",
            };

            if (error is not null)
                _logger.LogWarning("Command '{Action}' for {Folder} reported: {Error}", action, resolvedPath, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command file: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Determines whether an externally supplied path refers to a folder GameLocker manages.
    /// </summary>
    /// <param name="requestedPath">The raw path from the command file.</param>
    /// <param name="resolvedPath">The canonical path, when the check succeeds.</param>
    /// <returns><c>true</c> when the path is managed.</returns>
    /// <remarks>
    /// Two sources count as authorisation, both of them writable only by an administrator: the
    /// saved configuration, and the state store. The second matters because a folder being removed
    /// from the configuration still needs to be unlocked afterwards.
    /// </remarks>
    private bool IsManagedFolder(string requestedPath, bool allowRecoveryOnly, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(requestedPath))
            return false;

        string candidate;
        try
        {
            candidate = Normalize(requestedPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _logger.LogWarning("Rejected malformed command path '{Folder}': {Message}", requestedPath, ex.Message);
            return false;
        }

        foreach (var configured in _config?.GameFolderPaths ?? Enumerable.Empty<string>())
        {
            string known;
            try
            {
                known = Normalize(configured);
            }
            catch
            {
                continue; // A malformed entry in the configuration cannot authorise anything.
            }

            if (string.Equals(candidate, known, StringComparison.OrdinalIgnoreCase))
            {
                resolvedPath = configured;
                return true;
            }
        }

        // Not in the configuration, but GameLocker has an outstanding record for it - so it locked
        // this folder and is responsible for being able to unlock it again.
        if (allowRecoveryOnly && _folderLocker.StateStore.TryLoad(candidate) is not null)
        {
            resolvedPath = candidate;
            return true;
        }

        return false;

        static string Normalize(string path) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
