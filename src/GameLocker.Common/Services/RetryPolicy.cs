namespace GameLocker.Common.Services;

/// <summary>
/// Tracks repeated failures per key and spaces out retries with exponential backoff.
/// </summary>
/// <remarks>
/// The scheduler previously recorded a failed lock as though it had succeeded and never retried it
/// until the next schedule boundary, which could be a week away. Retrying is now the default, so
/// this exists to stop a permanently failing folder from being hammered every cycle and flooding
/// the log. See ARCHITECTURE-REVIEW.md finding [S-1].
/// </remarks>
public sealed class RetryPolicy
{
    private sealed class Attempt
    {
        public int Failures { get; set; }
        public DateTime NextAttemptUtc { get; set; }
    }

    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly Dictionary<string, Attempt> _attempts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a policy.
    /// </summary>
    /// <param name="initialDelay">The delay applied after the first failure.</param>
    /// <param name="maxDelay">The ceiling the delay doubles towards.</param>
    public RetryPolicy(TimeSpan initialDelay, TimeSpan maxDelay)
    {
        if (initialDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(initialDelay), "The initial delay must be positive.");

        if (maxDelay < initialDelay)
            throw new ArgumentOutOfRangeException(nameof(maxDelay), "The maximum delay must not be less than the initial delay.");

        _initialDelay = initialDelay;
        _maxDelay = maxDelay;
    }

    /// <summary>
    /// Determines whether a key is due for another attempt.
    /// </summary>
    /// <param name="key">The key, typically a folder path.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns><c>true</c> when the key has never failed, or its backoff has elapsed.</returns>
    public bool ShouldAttempt(string key, DateTime utcNow) =>
        !_attempts.TryGetValue(key, out var attempt) || utcNow >= attempt.NextAttemptUtc;

    /// <summary>
    /// Clears the failure history for a key.
    /// </summary>
    /// <param name="key">The key to clear.</param>
    public void RecordSuccess(string key) => _attempts.Remove(key);

    /// <summary>
    /// Records a failure and returns how long the key will now be skipped for.
    /// </summary>
    /// <param name="key">The key that failed.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>The delay before the next attempt.</returns>
    public TimeSpan RecordFailure(string key, DateTime utcNow)
    {
        if (!_attempts.TryGetValue(key, out var attempt))
        {
            attempt = new Attempt();
            _attempts[key] = attempt;
        }

        attempt.Failures++;

        // Double from the initial delay, capped. The shift is bounded so it cannot overflow on a
        // key that has been failing for a very long time.
        var exponent = Math.Min(attempt.Failures - 1, 20);
        var ticks = _initialDelay.Ticks * (1L << exponent);
        var delay = ticks > _maxDelay.Ticks || ticks < 0
            ? _maxDelay
            : TimeSpan.FromTicks(ticks);

        attempt.NextAttemptUtc = utcNow + delay;
        return delay;
    }

    /// <summary>
    /// Gets how many consecutive failures a key has recorded.
    /// </summary>
    /// <param name="key">The key to query.</param>
    /// <returns>The consecutive failure count, or zero.</returns>
    public int FailureCount(string key) =>
        _attempts.TryGetValue(key, out var attempt) ? attempt.Failures : 0;

    /// <summary>
    /// Forgets all failure history, so every key is due immediately.
    /// </summary>
    /// <remarks>
    /// Called when the machine's clock jumps or it resumes from sleep: the recorded deadlines were
    /// computed against a wall clock that is no longer comparable.
    /// </remarks>
    public void Reset() => _attempts.Clear();
}
