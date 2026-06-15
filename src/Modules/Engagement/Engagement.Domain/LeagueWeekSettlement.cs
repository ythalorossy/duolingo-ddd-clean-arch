using BuildingBlocks.Domain;

namespace Engagement.Domain;

// One row per settled week — the idempotency marker. Its existence means "week already settled."
public sealed class LeagueWeekSettlement : AggregateRoot
{
    public LeagueWeek Week { get; private set; } = default!;
    public DateTimeOffset SettledAt { get; private set; }

    private LeagueWeekSettlement() { } // EF

    public LeagueWeekSettlement(LeagueWeek week, DateTimeOffset settledAt)
    {
        Week = week ?? throw new ArgumentNullException(nameof(week));
        SettledAt = settledAt;
    }
}
