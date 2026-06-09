using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class LearnerStreak : AggregateRoot
{
    public LearnerId Id { get; private set; } = default!;
    public LearnerTimeZone TimeZone { get; private set; } = LearnerTimeZone.Utc;
    public int CurrentStreak { get; private set; }
    public int LongestStreak { get; private set; }
    public DateOnly? LastQualifyingDate { get; private set; }

    public int FreezeBalance { get; private set; }

    public const int MaxFreezes = 2;

    private LearnerStreak() { } // EF

    public static LearnerStreak Create(LearnerId id) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        TimeZone = LearnerTimeZone.Utc
    };

    public void ChangeTimeZone(LearnerTimeZone timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        TimeZone = timeZone;
    }

    public void GrantFreeze() =>
        FreezeBalance = Math.Min(FreezeBalance + 1, MaxFreezes);

    public void RegisterQualifyingActivity(DateTimeOffset occurredOnUtc)
    {
        var day = TimeZone.LocalDateOf(occurredOnUtc);

        if (LastQualifyingDate is { } last)
        {
            if (day <= last)
                return; // same day (idempotent) or late/out-of-order

            // One freeze is burned per missed day, up to what's held.
            // The streak survives only if freezes cover the WHOLE gap.
            var gap = GapBetween(last, day);
            var consumed = Math.Min(gap, FreezeBalance);
            FreezeBalance -= consumed;

            CurrentStreak = consumed == gap ? CurrentStreak + 1 : 1;
        }
        else
        {
            CurrentStreak = 1;
        }

        LastQualifyingDate = day;
        if (CurrentStreak > LongestStreak)
            LongestStreak = CurrentStreak;

        RaiseDomainEvent(new StreakAdvanced(Id.Value, CurrentStreak, day, occurredOnUtc));
    }

    // Whole days missed strictly between two local dates (0 when consecutive).
    // Only meaningful for to > from, which is the only caller context.
    private static int GapBetween(DateOnly from, DateOnly to) =>
        to.DayNumber - from.DayNumber - 1;

    public StreakReport Report(DateOnly today)
    {
        if (LastQualifyingDate is not { } last)
            return new StreakReport(StreakStatus.None, 0, LongestStreak, FreezeBalance);

        if (today <= last)
            return new StreakReport(StreakStatus.Active, CurrentStreak, LongestStreak, FreezeBalance);

        // Project the same rule the write path applies — without mutating.
        var gap = GapBetween(last, today);
        var consumed = Math.Min(gap, FreezeBalance);

        return consumed == gap
            ? new StreakReport(StreakStatus.AtRisk, CurrentStreak, LongestStreak, FreezeBalance - consumed)
            : new StreakReport(StreakStatus.Broken, 0, LongestStreak, FreezeBalance - consumed);
    }
}
