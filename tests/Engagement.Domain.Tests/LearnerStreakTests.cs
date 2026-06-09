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
        Assert.Empty(s.DomainEvents.OfType<StreakFrozen>());
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

    [Fact]
    public void One_freeze_bridges_a_single_missed_day_and_streak_continues()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1)); // current 1
        s.RegisterQualifyingActivity(Noon(2030, 1, 3)); // Jan 2 missed → freeze bridges it
        Assert.Equal(2, s.CurrentStreak);   // continued, NOT reset
        Assert.Equal(0, s.FreezeBalance);   // the freeze was consumed
    }

    [Fact]
    public void Two_freezes_bridge_a_two_day_gap()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();
        s.GrantFreeze();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        s.RegisterQualifyingActivity(Noon(2030, 1, 4)); // Jan 2 + Jan 3 missed → both bridged
        Assert.Equal(2, s.CurrentStreak);
        Assert.Equal(0, s.FreezeBalance);
    }

    [Fact]
    public void Gap_larger_than_balance_resets_and_burns_all_freezes()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();                                 // only 1 freeze
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        s.RegisterQualifyingActivity(Noon(2030, 1, 4)); // 2-day gap, only 1 freeze → reset
        Assert.Equal(1, s.CurrentStreak);
        Assert.Equal(0, s.FreezeBalance);                // the freeze was burned trying
    }

    [Fact]
    public void Same_day_does_not_consume_a_freeze()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        s.RegisterQualifyingActivity(new DateTimeOffset(2030, 1, 1, 20, 0, 0, TimeSpan.Zero));
        Assert.Equal(1, s.FreezeBalance);                // untouched
    }

    [Fact]
    public void Longest_survives_a_freeze_bridge()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        s.RegisterQualifyingActivity(Noon(2030, 1, 2)); // current 2, longest 2
        s.RegisterQualifyingActivity(Noon(2030, 1, 4)); // Jan 3 bridged → current 3
        Assert.Equal(3, s.CurrentStreak);
        Assert.Equal(3, s.LongestStreak);
    }

    [Fact]
    public void Report_projects_freeze_coverage_without_consuming_it()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();                                // balance 1
        s.RegisterQualifyingActivity(Noon(2030, 1, 1)); // current 1, last Jan 1
        var r = s.Report(new DateOnly(2030, 1, 3));     // Jan 2 missed, freeze would cover it
        Assert.Equal(StreakStatus.AtRisk, r.Status);    // protected, not broken
        Assert.Equal(1, r.CurrentStreak);
        Assert.Equal(0, r.FreezesAvailable);            // projected 1 - 1
        Assert.Equal(1, s.FreezeBalance);               // STORED balance unchanged — read is pure
    }

    [Fact]
    public void Report_is_broken_when_gap_exceeds_freezes()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();                                // 1 freeze
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        var r = s.Report(new DateOnly(2030, 1, 4));     // 2-day gap, only 1 freeze
        Assert.Equal(StreakStatus.Broken, r.Status);
        Assert.Equal(0, r.CurrentStreak);
        Assert.Equal(0, r.FreezesAvailable);
    }

    [Fact]
    public void Report_exposes_balance_while_active()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        var r = s.Report(new DateOnly(2030, 1, 1));     // active today
        Assert.Equal(StreakStatus.Active, r.Status);
        Assert.Equal(1, r.FreezesAvailable);
    }

    [Fact]
    public void Bridging_a_gap_raises_StreakFrozen_with_days_frozen()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        s.RegisterQualifyingActivity(Noon(2030, 1, 3)); // Jan 2 bridged

        var frozen = Assert.Single(s.DomainEvents.OfType<StreakFrozen>());
        Assert.Equal(1, frozen.FreezesConsumed);
        Assert.Equal(new DateOnly(2030, 1, 3), frozen.Date);
    }

    [Fact]
    public void A_normal_consecutive_advance_raises_no_StreakFrozen()
    {
        var s = NewUtcLearner();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        s.RegisterQualifyingActivity(Noon(2030, 1, 2)); // consecutive, no freeze used
        Assert.Empty(s.DomainEvents.OfType<StreakFrozen>());
    }

    [Fact]
    public void A_reset_that_burns_freezes_raises_no_StreakFrozen()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();                                // 1 freeze, gap will be 2
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));
        s.RegisterQualifyingActivity(Noon(2030, 1, 4)); // burns the freeze but still resets
        Assert.Empty(s.DomainEvents.OfType<StreakFrozen>());
    }

    [Fact]
    public void Re_delivered_bridge_event_does_not_double_consume_a_freeze()
    {
        var s = NewUtcLearner();
        s.GrantFreeze();
        s.RegisterQualifyingActivity(Noon(2030, 1, 1));   // current 1
        s.RegisterQualifyingActivity(Noon(2030, 1, 3));   // Jan 2 missed → freeze bridges it: current 2, balance 0

        // Re-deliver the SAME bridging completion (same local day) — must be an idempotent no-op,
        // not a second consumption.
        s.RegisterQualifyingActivity(Noon(2030, 1, 3));

        Assert.Equal(2, s.CurrentStreak);
        Assert.Equal(0, s.FreezeBalance);
    }
}
