namespace GameLocker.Common.Services;

/// <summary>
/// Decides when to warn that a gaming window is about to close, firing each threshold at most once
/// per window.
/// </summary>
/// <remarks>
/// The previous logic tested <c>remaining &lt;= 15 &amp;&amp; remaining &gt; 14</c> - a one-minute
/// window sampled by a loop whose period drifted with however long the previous lock had taken - so
/// warnings frequently never fired at all. Anchoring to the window's end instead means a threshold
/// fires as soon as it is crossed, at any sampling rate.
/// See ARCHITECTURE-REVIEW.md finding [S-3].
/// </remarks>
public sealed class LockWarningTracker
{
    private readonly int[] _thresholdsMinutes;
    private readonly HashSet<int> _fired = new();
    private DateTime? _windowEnd;

    /// <summary>
    /// Creates a tracker.
    /// </summary>
    /// <param name="thresholdsMinutes">
    /// Minutes-before-lock at which to warn. Order does not matter.
    /// </param>
    public LockWarningTracker(params int[] thresholdsMinutes)
    {
        _thresholdsMinutes = (thresholdsMinutes ?? Array.Empty<int>())
            .Where(t => t > 0)
            .Distinct()
            .OrderBy(t => t)
            .ToArray();
    }

    /// <summary>
    /// Determines which warning, if any, should be shown now.
    /// </summary>
    /// <param name="lockTime">When the current window closes, or <c>null</c> when no window is open.</param>
    /// <param name="now">The current time.</param>
    /// <returns>
    /// The threshold in minutes to warn about, or <c>null</c> when nothing should be shown.
    /// </returns>
    /// <remarks>
    /// When several thresholds have been crossed at once - which happens if the service was stopped
    /// through them, or the machine was asleep - only the most urgent is returned and the rest are
    /// marked as spent, so the user gets one notification rather than a burst.
    /// </remarks>
    public int? GetWarningToShow(DateTime? lockTime, DateTime now)
    {
        if (lockTime is null)
        {
            // No window open: forget what has been shown so the next window starts clean.
            _windowEnd = null;
            _fired.Clear();
            return null;
        }

        if (_windowEnd != lockTime)
        {
            _windowEnd = lockTime;
            _fired.Clear();
        }

        var remaining = lockTime.Value - now;

        if (remaining <= TimeSpan.Zero)
            return null;

        int? mostUrgent = null;

        foreach (var threshold in _thresholdsMinutes)
        {
            if (remaining > TimeSpan.FromMinutes(threshold))
                continue;

            if (_fired.Add(threshold))
                mostUrgent ??= threshold;
        }

        return mostUrgent;
    }

    /// <summary>
    /// Forgets which warnings have been shown.
    /// </summary>
    public void Reset()
    {
        _windowEnd = null;
        _fired.Clear();
    }
}
