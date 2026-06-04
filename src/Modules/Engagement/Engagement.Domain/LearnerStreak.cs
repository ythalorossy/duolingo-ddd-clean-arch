using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class LearnerStreak : AggregateRoot
{
    public LearnerId Id { get; private set; } = default!;
    public LearnerTimeZone TimeZone { get; private set; } = LearnerTimeZone.Utc;
    public int CurrentStreak { get; private set; }
    public int LongestStreak { get; private set; }
    public DateOnly? LastQualifyingDate { get; private set; }

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

    public void RegisterQualifyingActivity(DateTimeOffset occurredOnUtc)
    {
        var day = TimeZone.LocalDateOf(occurredOnUtc);

        if (LastQualifyingDate is { } last)
        {
            if (day <= last)
                return; // same day (idempotent) or late/out-of-order

            CurrentStreak = day == last.AddDays(1) ? CurrentStreak + 1 : 1; // continue or reset
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

    public StreakReport Report(DateOnly today)
    {
        if (LastQualifyingDate is not { } last)
            return new StreakReport(StreakStatus.None, 0, LongestStreak);
        if (last == today)
            return new StreakReport(StreakStatus.Active, CurrentStreak, LongestStreak);
        if (last == today.AddDays(-1))
            return new StreakReport(StreakStatus.AtRisk, CurrentStreak, LongestStreak);
        return new StreakReport(StreakStatus.Broken, 0, LongestStreak);
    }
}
