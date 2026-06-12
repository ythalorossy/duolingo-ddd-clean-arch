using BuildingBlocks.Domain;

namespace Engagement.Domain;

// One small aggregate per learner. Accumulates XP for the current UTC week and rolls
// lazily on the next activity in a later week — the same "settle on activity, never on a
// timer" idea as LearnerStreak. Raises no domain event in Slice 1.
public sealed class LeagueStanding : AggregateRoot
{
    public LearnerId Id { get; private set; } = default!;
    public LeagueTier Tier { get; private set; }
    public LeagueWeek Week { get; private set; } = default!;
    public Xp WeeklyXp { get; private set; } = Xp.Zero;

    private LeagueStanding() { } // EF

    public static LeagueStanding Create(LearnerId id, LeagueWeek week) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        Tier = LeagueTier.Bronze,
        Week = week ?? throw new ArgumentNullException(nameof(week)),
        WeeklyXp = Xp.Zero
    };

    public void RecordXp(int amount, DateTimeOffset asOfUtc)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "XP amount cannot be negative.");

        var week = LeagueWeek.Containing(asOfUtc);

        if (week.Start > Week.Start)
        {
            // A later week began — roll over: this week starts fresh.
            Week = week;
            WeeklyXp = new Xp(amount);
            return;
        }

        if (week.Start < Week.Start)
            return; // clock moved back — defensive, cannot revive a past week

        WeeklyXp = new Xp(WeeklyXp.Value + amount); // same week — accumulate
    }
}
