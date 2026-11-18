using GameLocker.Common.Services;

namespace GameLocker.Tests;

/// <summary>
/// Tests for the pure decision-making pieces the reconciliation loop is built from.
/// </summary>
/// <remarks>
/// These are extracted from the service precisely so they can be tested. The scheduler defects in
/// ARCHITECTURE-REVIEW.md - failures recorded as successes, one-minute-wide warning windows, and no
/// handling of sleep or clock changes - were all in code that could not be exercised without
/// installing a service and waiting for a schedule boundary.
/// </remarks>
public class RetryPolicyTests
{
    private static readonly DateTime T0 = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    private static RetryPolicy Policy() =>
        new(TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(15));

    [Fact]
    [Trait("Category", "Basic")]
    public void AnUnknownKey_IsAlwaysDue()
    {
        Assert.True(Policy().ShouldAttempt("D:\\Games\\Title", T0));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void AfterAFailure_TheKeyIsDeferredUntilTheDelayElapses()
    {
        var policy = Policy();

        var delay = policy.RecordFailure("folder", T0);

        Assert.Equal(TimeSpan.FromSeconds(15), delay);
        Assert.False(policy.ShouldAttempt("folder", T0));
        Assert.False(policy.ShouldAttempt("folder", T0.AddSeconds(14)));
        Assert.True(policy.ShouldAttempt("folder", T0.AddSeconds(15)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void ConsecutiveFailures_DoubleTheDelay()
    {
        var policy = Policy();

        Assert.Equal(TimeSpan.FromSeconds(15), policy.RecordFailure("folder", T0));
        Assert.Equal(TimeSpan.FromSeconds(30), policy.RecordFailure("folder", T0));
        Assert.Equal(TimeSpan.FromSeconds(60), policy.RecordFailure("folder", T0));
        Assert.Equal(3, policy.FailureCount("folder"));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void TheDelayIsCapped()
    {
        var policy = Policy();

        for (var i = 0; i < 40; i++)
            policy.RecordFailure("folder", T0);

        Assert.Equal(TimeSpan.FromMinutes(15), policy.RecordFailure("folder", T0));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void Success_ClearsTheHistory()
    {
        var policy = Policy();

        policy.RecordFailure("folder", T0);
        policy.RecordSuccess("folder");

        Assert.True(policy.ShouldAttempt("folder", T0));
        Assert.Equal(0, policy.FailureCount("folder"));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void KeysAreTrackedIndependentlyAndCaseInsensitively()
    {
        var policy = Policy();

        policy.RecordFailure(@"D:\Games\One", T0);

        Assert.False(policy.ShouldAttempt(@"d:\games\one", T0));
        Assert.True(policy.ShouldAttempt(@"D:\Games\Two", T0));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void Reset_MakesEveryKeyDueImmediately()
    {
        var policy = Policy();

        policy.RecordFailure("folder", T0);
        policy.Reset();

        // Recorded deadlines were computed against a clock that has since jumped.
        Assert.True(policy.ShouldAttempt("folder", T0));
    }
}

/// <summary>
/// Tests for <see cref="LockWarningTracker"/>.
/// </summary>
public class LockWarningTrackerTests
{
    private static readonly DateTime LockAt = new(2026, 7, 27, 22, 0, 0);

    [Fact]
    [Trait("Category", "Basic")]
    public void EachThresholdFiresOnceAsItIsCrossed()
    {
        var tracker = new LockWarningTracker(15, 5);

        Assert.Null(tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-20)));
        Assert.Equal(15, tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-15)));
        Assert.Null(tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-14)));
        Assert.Null(tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-6)));
        Assert.Equal(5, tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-5)));
        Assert.Null(tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-1)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void FrequentSampling_DoesNotProduceRepeatedWarnings()
    {
        // The previous implementation depended on catching a one-minute-wide window exactly once.
        var tracker = new LockWarningTracker(15, 5);
        var fired = new List<int>();

        for (var second = 0; second < 20 * 60; second += 15)
        {
            var warning = tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-20).AddSeconds(second));
            if (warning is not null)
                fired.Add(warning.Value);
        }

        Assert.Equal(new[] { 15, 5 }, fired);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void CrossingSeveralThresholdsAtOnce_ShowsOnlyTheMostUrgent()
    {
        // What happens when the service was stopped, or the machine asleep, through both.
        var tracker = new LockWarningTracker(15, 5);

        Assert.Equal(5, tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-3)));
        Assert.Null(tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-2)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void NoOpenWindow_ShowsNothingAndClearsHistory()
    {
        var tracker = new LockWarningTracker(15, 5);

        Assert.Equal(15, tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-15)));
        Assert.Null(tracker.GetWarningToShow(null, LockAt.AddMinutes(5)));

        // A later window warns again.
        var nextLock = LockAt.AddDays(7);
        Assert.Equal(15, tracker.GetWarningToShow(nextLock, nextLock.AddMinutes(-15)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void AfterTheLockTime_NothingIsShown()
    {
        var tracker = new LockWarningTracker(15, 5);

        Assert.Null(tracker.GetWarningToShow(LockAt, LockAt));
        Assert.Null(tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(1)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void ChangingTheWindow_RearmsEveryThreshold()
    {
        var tracker = new LockWarningTracker(15, 5);

        Assert.Equal(15, tracker.GetWarningToShow(LockAt, LockAt.AddMinutes(-15)));

        var extended = LockAt.AddHours(2);
        Assert.Equal(15, tracker.GetWarningToShow(extended, extended.AddMinutes(-15)));
    }
}

/// <summary>
/// Tests for <see cref="ClockDriftDetector"/>.
/// </summary>
public class ClockDriftDetectorTests
{
    private static readonly DateTime T0 = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Category", "Basic")]
    public void TheFirstSampleNeverReportsAJump()
    {
        var detector = new ClockDriftDetector(TimeSpan.FromSeconds(30));

        Assert.False(detector.Sample(T0, TimeSpan.Zero, out var drift));
        Assert.Equal(TimeSpan.Zero, drift);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void ClocksAdvancingTogether_IsNotAJump()
    {
        var detector = new ClockDriftDetector(TimeSpan.FromSeconds(30));

        detector.Sample(T0, TimeSpan.Zero, out _);

        Assert.False(detector.Sample(T0.AddSeconds(15), TimeSpan.FromSeconds(15), out _));
        Assert.False(detector.Sample(T0.AddSeconds(30), TimeSpan.FromSeconds(30), out _));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void SmallJitter_IsToleratedWithoutReporting()
    {
        var detector = new ClockDriftDetector(TimeSpan.FromSeconds(30));

        detector.Sample(T0, TimeSpan.Zero, out _);

        Assert.False(detector.Sample(T0.AddSeconds(17), TimeSpan.FromSeconds(15), out var drift));
        Assert.Equal(TimeSpan.FromSeconds(2), drift);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void SleepingAcrossABoundary_IsReported()
    {
        // Two hours of wall clock, fifteen seconds of monotonic time: the machine was asleep.
        var detector = new ClockDriftDetector(TimeSpan.FromSeconds(30));

        detector.Sample(T0, TimeSpan.Zero, out _);

        Assert.True(detector.Sample(T0.AddHours(2), TimeSpan.FromSeconds(15), out var drift));
        Assert.True(drift > TimeSpan.FromMinutes(100));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void WindingTheClockBack_IsReported()
    {
        // The simplest way to defeat a schedule enforced from the wall clock.
        var detector = new ClockDriftDetector(TimeSpan.FromSeconds(30));

        detector.Sample(T0, TimeSpan.Zero, out _);

        Assert.True(detector.Sample(T0.AddHours(-3), TimeSpan.FromSeconds(15), out var drift));
        Assert.True(drift > TimeSpan.FromMinutes(170));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void Reset_MakesTheNextSampleRePrime()
    {
        var detector = new ClockDriftDetector(TimeSpan.FromSeconds(30));

        detector.Sample(T0, TimeSpan.Zero, out _);
        detector.Reset();

        Assert.False(detector.Sample(T0.AddHours(5), TimeSpan.FromSeconds(15), out _));
    }
}
