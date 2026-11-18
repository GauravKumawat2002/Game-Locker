namespace GameLocker.Common.Services;

/// <summary>
/// Detects that the wall clock has moved independently of elapsed real time.
/// </summary>
/// <remarks>
/// Compares the change in wall-clock time against the change in a monotonic timer between two
/// samples. A meaningful difference means the machine slept or hibernated, a daylight-saving
/// transition occurred, the clock was synchronised, or the user changed it deliberately - which is
/// the simplest way to defeat a schedule enforced from <c>DateTime.Now</c>.
/// <para>
/// This is used instead of <c>SystemEvents.TimeChanged</c> and <c>SystemEvents.PowerModeChanged</c>
/// because those depend on a window receiving <c>WM_TIMECHANGE</c> and <c>WM_POWERBROADCAST</c>. A
/// Windows Service runs in session 0 with no message pump, so those events are not dependable there,
/// whereas comparing two clocks always is. It also catches all four causes with one mechanism.
/// See ARCHITECTURE-REVIEW.md finding [S-7].
/// </para>
/// </remarks>
public sealed class ClockDriftDetector
{
    private readonly TimeSpan _threshold;
    private DateTime _lastWallUtc;
    private TimeSpan _lastMonotonic;
    private bool _primed;

    /// <summary>
    /// Creates a detector.
    /// </summary>
    /// <param name="threshold">
    /// How far the two clocks may diverge between samples before it counts as a jump. This must
    /// comfortably exceed normal scheduling jitter.
    /// </param>
    public ClockDriftDetector(TimeSpan threshold)
    {
        if (threshold <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(threshold), "The threshold must be positive.");

        _threshold = threshold;
    }

    /// <summary>
    /// Records a sample and reports whether the clock jumped since the previous one.
    /// </summary>
    /// <param name="wallUtc">The current wall-clock time, in UTC.</param>
    /// <param name="monotonic">A monotonically increasing elapsed time, such as
    /// <see cref="System.Diagnostics.Stopwatch.Elapsed"/>.</param>
    /// <param name="drift">How far the two clocks diverged since the previous sample.</param>
    /// <returns>
    /// <c>true</c> when the divergence exceeded the threshold. The first call always returns
    /// <c>false</c>, because there is nothing to compare against yet.
    /// </returns>
    public bool Sample(DateTime wallUtc, TimeSpan monotonic, out TimeSpan drift)
    {
        if (!_primed)
        {
            _primed = true;
            _lastWallUtc = wallUtc;
            _lastMonotonic = monotonic;
            drift = TimeSpan.Zero;
            return false;
        }

        var wallDelta = wallUtc - _lastWallUtc;
        var monotonicDelta = monotonic - _lastMonotonic;

        _lastWallUtc = wallUtc;
        _lastMonotonic = monotonic;

        // Duration() so that both a jump forwards and a jump backwards are detected.
        drift = (wallDelta - monotonicDelta).Duration();
        return drift > _threshold;
    }

    /// <summary>
    /// Discards the previous sample, so the next call re-primes instead of comparing.
    /// </summary>
    public void Reset() => _primed = false;
}
