namespace GameLocker.Common.Models;

/// <summary>
/// The schedule observed at a specific time and its next transition, if any.
/// </summary>
public readonly record struct ScheduleState(bool IsAllowed, DateTime? NextTransition)
{
    /// <summary>
    /// Gets how long to wait before checking this state again.
    /// </summary>
    public TimeSpan GetRecheckDelay(DateTime currentTime, TimeSpan pollInterval)
    {
        if (NextTransition is not { } transition)
            return pollInterval;

        var untilTransition = transition - currentTime;
        if (untilTransition <= TimeSpan.Zero)
            return TimeSpan.Zero;

        return untilTransition < pollInterval ? untilTransition : pollInterval;
    }
}
