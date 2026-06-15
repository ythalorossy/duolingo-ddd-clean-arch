using BuildingBlocks.Domain;

namespace Engagement.Domain;

// One aggregate per (learner, week). A row is a single fixed week, so RecordXp just adds —
// the lazy week-roll moved to the handler (which find-or-creates the current week's row).
// Identity is (Id, Week); both are value objects with value-equality (via ValueObject), which
// is what makes the EF composite key safe.
public sealed class LeagueStanding : AggregateRoot
{
    public LearnerId Id { get; private set; } = default!;
    public LeagueWeek Week { get; private set; } = default!;
    public LeagueTier Tier { get; private set; }
    public Xp WeeklyXp { get; private set; } = Xp.Zero;

    private LeagueStanding() { } // EF

    public static LeagueStanding Create(LearnerId id, LeagueWeek week, LeagueTier tier) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        Week = week ?? throw new ArgumentNullException(nameof(week)),
        Tier = tier,
        WeeklyXp = Xp.Zero
    };

    public void RecordXp(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "XP amount cannot be negative.");
        WeeklyXp = new Xp(WeeklyXp.Value + amount);
    }
}
