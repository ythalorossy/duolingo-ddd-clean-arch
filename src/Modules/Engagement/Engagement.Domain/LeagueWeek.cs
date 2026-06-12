using BuildingBlocks.Domain;

namespace Engagement.Domain;

// A league competition week: a fixed UTC calendar week anchored to its Monday.
// Shared by every learner in a cohort, so it uses ONE canonical clock (UTC) — unlike
// LearnerStreak, which is per-learner and uses the learner's own time zone.
public sealed class LeagueWeek : ValueObject
{
    public DateOnly Start { get; } // the Monday (UTC)

    public LeagueWeek(DateOnly start)
    {
        if (start.DayOfWeek != DayOfWeek.Monday)
            throw new ArgumentException("A league week must start on a Monday.", nameof(start));
        Start = start;
    }

    public static LeagueWeek Containing(DateTimeOffset instant)
    {
        var date = DateOnly.FromDateTime(instant.UtcDateTime);
        // DayOfWeek: Sunday=0..Saturday=6. Days since Monday: Mon→0 … Sun→6.
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return new LeagueWeek(date.AddDays(-daysSinceMonday));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
    }

    public override string ToString() => Start.ToString("yyyy-MM-dd");
}
