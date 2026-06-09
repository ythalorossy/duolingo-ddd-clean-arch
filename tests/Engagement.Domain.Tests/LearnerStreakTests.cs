using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LearnerStreakTests
{
    private static LearnerStreak NewUtcLearner() =>
        LearnerStreak.Create(new LearnerId(Guid.NewGuid()));

    // Noon UTC on a given day → unambiguous local date under UTC zone.
    private static DateTimeOffset Noon(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void First_activity_starts_streak_at_one()
    {
        var s = NewUtcLearner();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        Assert.Equal(1, s.CurrentStreak);
        Assert.Equal(1, s.LongestStreak);
    }

    [Fact]
    public void Consecutive_days_increment()
    {
        var s = NewUtcLearner();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        s.RegisterQualifyingActivity(Noon(2030, 1, 2));
        s.RegisterQualifyingActivity(Noon(2030, 1, 3));
        Assert.Equal(3, s.CurrentStreak);
        Assert.Equal(3, s.LongestStreak);
    }

    [Fact]
    public void Same_day_is_idempotent()
    {
        var s = NewUtcLearner();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        s.RegisterQualifyingActivity(new DateTimeOffset(2030, 1, 1, 20, 0, 0, TimeSpan.Zero));
        Assert.Equal(1, s.CurrentStreak);
    }

    [Fact]
    public void Gap_resets_to_one_but_longest_is_retained()
    {
        var s = NewUtcLearner();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        s.RegisterQualifyingActivity(Noon(2030, 1, 2)); // current 2, longest 2
        s.RegisterQualifyingActivity(Noon(2030, 1, 5)); // gap → reset to 1
        Assert.Equal(1, s.CurrentStreak);
        Assert.Equal(2, s.LongestStreak);
    }

    [Fact]
    public void Out_of_order_earlier_activity_is_ignored()
    {
        var s = NewUtcLearner();
        s.RegisterQualifyingActivity(Noon(2030, 1, 5));
        s.RegisterQualifyingActivity(Noon(2030, 1, 1)); // earlier → ignored
        Assert.Equal(1, s.CurrentStreak);
        Assert.Equal(new DateOnly(2030, 1, 5), s.LastQualifyingDate);
    }

    [Fact]
    public void Report_is_active_on_the_qualifying_day()
    {
        var s = NewUtcLearner();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        var r = s.Report(new DateOnly(2030, 1, 1));
        Assert.Equal(StreakStatus.Active, r.Status);
        Assert.Equal(1, r.CurrentStreak);
    }

    [Fact]
    public void Report_is_at_risk_the_next_day()
    {
        var s = NewUtcLearner();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        var r = s.Report(new DateOnly(2030, 1, 2));
        Assert.Equal(StreakStatus.AtRisk, r.Status);
        Assert.Equal(1, r.CurrentStreak); // still shows the count while at risk
    }

    [Fact]
    public void Report_is_broken_after_a_missed_day_with_effective_zero()
    {
        var s = NewUtcLearner();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1)); // stored current = 1
        var r = s.Report(new DateOnly(2030, 1, 3));     // skipped the 2nd
        Assert.Equal(StreakStatus.Broken, r.Status);
        Assert.Equal(0, r.CurrentStreak);               // displayed current is 0
    }

    [Fact]
    public void Report_is_none_when_never_active()
    {
        var r = NewUtcLearner().Report(new DateOnly(2030, 1, 1));
        Assert.Equal(StreakStatus.None, r.Status);
        Assert.Equal(0, r.CurrentStreak);
    }

    [Fact]
    public void Granting_a_freeze_increments_the_balance()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();
        Assert.Equal(1, s.FreezeBalance);
    }

    [Fact]
    public void Freeze_balance_is_clamped_at_the_cap()
    {
        var s = NewUtcLearner();
        for (var i = 0; i < LearnerStreak.MaxFreezes * 2 + 1; i++) s.GrantFreeze();
        Assert.Equal(LearnerStreak.MaxFreezes, s.FreezeBalance);
    }

    [Fact]
    public void Freeze_cap_is_two_by_policy()
    {
        Assert.Equal(2, LearnerStreak.MaxFreezes);
    }

    [Fact]
    public void New_learner_starts_with_zero_freezes()
    {
        Assert.Equal(0, NewUtcLearner().FreezeBalance);
    }
}
