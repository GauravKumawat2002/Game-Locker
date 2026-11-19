using GameLocker.Common.Models;

namespace GameLocker.Tests;

/// <summary>
/// Tests for the gaming-window calculation in <see cref="GameLockerConfig"/>.
/// </summary>
/// <remarks>
/// The midnight-crossing cases here are the ones that produced the "sometimes it works, sometimes
/// it doesn't" reports: the previous implementation tested the allowed-days list against the
/// current day before looking at the time, so a Saturday 22:00 window locked the folder at 00:01 on
/// Sunday while the user was mid-session. See ARCHITECTURE-REVIEW.md finding [S-2].
/// </remarks>
public class ScheduleWindowTests
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private static GameLockerConfig Config(TimeOnly start, int durationHours, params DayOfWeek[] days) =>
        new()
        {
            StartTime = start,
            DurationHours = durationHours,
            AllowedDays = days.ToList(),
        };

    // 2026-07-25 is a Saturday, so 07-26 is a Sunday and 07-27 a Monday.
    private static DateTime Sat(int hour, int minute = 0) => new(2026, 7, 25, hour, minute, 0);
    private static DateTime Sun(int hour, int minute = 0) => new(2026, 7, 26, hour, minute, 0);
    private static DateTime Mon(int hour, int minute = 0) => new(2026, 7, 27, hour, minute, 0);

    [Fact]
    [Trait("Category", "Basic")]
    public void WindowWithinOneDay_IsAllowedInsideAndDeniedOutside()
    {
        var config = Config(new TimeOnly(18, 0), 2, DayOfWeek.Saturday);

        Assert.False(config.IsWithinAllowedTime(Sat(17, 59)));
        Assert.True(config.IsWithinAllowedTime(Sat(18, 0)));
        Assert.True(config.IsWithinAllowedTime(Sat(19, 59)));
        Assert.False(config.IsWithinAllowedTime(Sat(20, 0)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void WindowCrossingMidnight_StaysAllowedAfterMidnight()
    {
        // Saturday 22:00 for four hours runs until Sunday 02:00.
        var config = Config(new TimeOnly(22, 0), 4, DayOfWeek.Saturday);

        Assert.True(config.IsWithinAllowedTime(Sat(22, 0)));
        Assert.True(config.IsWithinAllowedTime(Sat(23, 59)));

        // The regression: Sunday is not an allowed day, but the window opened on Saturday and is
        // still running.
        Assert.True(config.IsWithinAllowedTime(Sun(0, 1)));
        Assert.True(config.IsWithinAllowedTime(Sun(1, 59)));

        Assert.False(config.IsWithinAllowedTime(Sun(2, 0)));
        Assert.False(config.IsWithinAllowedTime(Sun(22, 0)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void WindowCrossingMidnight_ReportsLockTimeAfterMidnight()
    {
        var config = Config(new TimeOnly(22, 0), 4, DayOfWeek.Saturday);

        Assert.Equal(Sun(2, 0), config.GetNextLockTime(Sat(23, 0)));
        Assert.Equal(Sun(2, 0), config.GetNextLockTime(Sun(0, 30)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void GetNextLockTime_IsNullOutsideAnyWindow()
    {
        var config = Config(new TimeOnly(18, 0), 2, DayOfWeek.Saturday);

        Assert.Null(config.GetNextLockTime(Sat(12, 0)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void GetNextUnlockTime_FindsTheNextWindowStart()
    {
        var config = Config(new TimeOnly(18, 0), 2, DayOfWeek.Saturday, DayOfWeek.Monday);

        Assert.Equal(Sat(18, 0), config.GetNextUnlockTime(Sat(9, 0)));
        Assert.Equal(Mon(18, 0), config.GetNextUnlockTime(Sat(20, 30)));
    }

    [Theory]
    [InlineData(18, false)]
    [InlineData(20, true)]
    [Trait("Category", "Basic")]
    public void RecheckDelay_WakesAtTheUpcomingTransition(int hour, bool allowedBeforeTransition)
    {
        var config = Config(new TimeOnly(18, 0), 2, DayOfWeek.Saturday);
        var transition = Sat(hour);
        var observedAt = transition.AddSeconds(-4);
        var schedule = config.GetScheduleState(observedAt);

        Assert.Equal(allowedBeforeTransition, schedule.IsAllowed);
        Assert.Equal(transition, schedule.NextTransition);
        Assert.Equal(TimeSpan.FromSeconds(4), schedule.GetRecheckDelay(observedAt, PollInterval));
        Assert.NotEqual(allowedBeforeTransition, config.GetScheduleState(transition).IsAllowed);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void RecheckDelay_IsImmediateWhenFolderWorkPassesTheObservedTransition()
    {
        var config = Config(new TimeOnly(22, 0), 4, DayOfWeek.Saturday);
        var observed = config.GetScheduleState(Sun(1, 59));
        var afterFolderWork = Sun(2, 0).AddSeconds(1);

        Assert.True(observed.IsAllowed);
        Assert.NotEqual(observed, config.GetScheduleState(afterFolderWork));
        Assert.Equal(TimeSpan.Zero, observed.GetRecheckDelay(afterFolderWork, PollInterval));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void RecheckDelay_KeepsPollingWhenTheTransitionIsDistantOrAbsent()
    {
        var now = Sat(12);
        var scheduled = Config(new TimeOnly(18, 0), 2, DayOfWeek.Saturday).GetScheduleState(now);
        var alwaysAllowed = Config(new TimeOnly(18, 0), 24, Enum.GetValues<DayOfWeek>()).GetScheduleState(now);
        var neverAllowed = Config(new TimeOnly(18, 0), 2).GetScheduleState(now);

        Assert.Equal(PollInterval, scheduled.GetRecheckDelay(now, PollInterval));
        Assert.Equal(Sat(18), scheduled.NextTransition);
        Assert.Equal(PollInterval, alwaysAllowed.GetRecheckDelay(now, PollInterval));
        Assert.Null(alwaysAllowed.NextTransition);
        Assert.Equal(PollInterval, neverAllowed.GetRecheckDelay(now, PollInterval));
        Assert.Null(neverAllowed.NextTransition);
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void ScheduleState_DoesNotChangeAtTheJoinBetweenConsecutiveFullDayWindows()
    {
        var config = Config(new TimeOnly(18, 0), 24, DayOfWeek.Saturday, DayOfWeek.Sunday);
        var join = Sun(18);
        var beforeJoin = config.GetScheduleState(join.AddSeconds(-1));

        Assert.True(beforeJoin.IsAllowed);
        Assert.Equal(Mon(18), beforeJoin.NextTransition);
        Assert.Equal(beforeJoin, config.GetScheduleState(join));
        Assert.Equal(PollInterval, beforeJoin.GetRecheckDelay(join.AddSeconds(-1), PollInterval));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void NoAllowedDays_IsNeverAllowed()
    {
        var config = Config(new TimeOnly(18, 0), 2);

        Assert.False(config.IsWithinAllowedTime(Sat(18, 30)));
        Assert.Null(config.GetNextUnlockTime(Sat(9, 0)));
        Assert.Null(config.GetNextLockTime(Sat(18, 30)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void ZeroDuration_IsNeverAllowed()
    {
        var config = Config(new TimeOnly(18, 0), 0, DayOfWeek.Saturday);

        Assert.False(config.IsWithinAllowedTime(Sat(18, 0)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void TwentyFourHourDuration_CoversTheWholeDay()
    {
        var config = Config(new TimeOnly(0, 0), 24, DayOfWeek.Saturday);

        Assert.True(config.IsWithinAllowedTime(Sat(0, 0)));
        Assert.True(config.IsWithinAllowedTime(Sat(23, 59)));
        Assert.False(config.IsWithinAllowedTime(Sun(0, 0)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void ConsecutiveFullDaysReportTheEndOfTheCombinedWindow()
    {
        var config = Config(new TimeOnly(18, 0), 24, DayOfWeek.Saturday, DayOfWeek.Sunday);

        Assert.Equal(Mon(18), config.GetNextLockTime(Sun(17, 55)));
        Assert.True(config.IsWithinAllowedTime(Sun(18)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void RoundTheClockScheduleHasNoUpcomingLock()
    {
        var config = Config(new TimeOnly(18, 0), 24, Enum.GetValues<DayOfWeek>());

        Assert.True(config.IsWithinAllowedTime(Sat(17, 55)));
        Assert.Null(config.GetNextLockTime(Sat(17, 55)));
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void ConsecutiveAllowedDays_ProduceOverlappingWindowsWithoutGaps()
    {
        var config = Config(new TimeOnly(20, 0), 8, DayOfWeek.Saturday, DayOfWeek.Sunday);

        Assert.True(config.IsWithinAllowedTime(Sun(2, 0)));   // Saturday's window, still running
        Assert.False(config.IsWithinAllowedTime(Sun(12, 0))); // between windows
        Assert.True(config.IsWithinAllowedTime(Sun(21, 0)));  // Sunday's window
        Assert.True(config.IsWithinAllowedTime(Mon(3, 0)));   // Sunday's window past midnight
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void LockModeDefaultsToAclOnly()
    {
        // A configuration written before this setting existed deserialises to the default, so
        // upgrading must not leave bulk encryption enabled.
        Assert.Equal(LockMode.AclOnly, new GameLockerConfig().LockMode);
    }
}
