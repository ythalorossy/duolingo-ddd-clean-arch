using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LeagueStandingTests
{
    private static readonly DateTimeOffset Wed = new(2030, 1, 9, 12, 0, 0, TimeSpan.Zero);      // week of Jan 7
    private static readonly DateTimeOffset Thu = new(2030, 1, 10, 12, 0, 0, TimeSpan.Zero);     // same week
    private static readonly DateTimeOffset NextWed = new(2030, 1, 16, 12, 0, 0, TimeSpan.Zero); // week of Jan 14

    private static LeagueStanding NewBronze(DateTimeOffset asOf) =>
        LeagueStanding.Create(new LearnerId(Guid.NewGuid()), LeagueWeek.Containing(asOf));

    [Fact]
    public void Create_starts_in_bronze_with_zero_xp()
    {
        var s = NewBronze(Wed);
        Assert.Equal(LeagueTier.Bronze, s.Tier);
        Assert.Equal(0, s.WeeklyXp.Value);
        Assert.Equal(new DateOnly(2030, 1, 7), s.Week.Start);
    }

    [Fact]
    public void RecordXp_accumulates_within_the_same_week()
    {
        var s = NewBronze(Wed);
        s.RecordXp(15, Wed);
        s.RecordXp(10, Thu);
        Assert.Equal(25, s.WeeklyXp.Value);
        Assert.Equal(new DateOnly(2030, 1, 7), s.Week.Start);
    }

    [Fact]
    public void RecordXp_in_a_later_week_resets_the_total()
    {
        var s = NewBronze(Wed);
        s.RecordXp(40, Wed);
        s.RecordXp(15, NextWed);
        Assert.Equal(15, s.WeeklyXp.Value);
        Assert.Equal(new DateOnly(2030, 1, 14), s.Week.Start);
    }

    [Fact]
    public void RecordXp_in_an_earlier_week_is_ignored()
    {
        var s = NewBronze(NextWed);
        s.RecordXp(20, NextWed);
        s.RecordXp(99, Wed); // earlier week — defensive guard
        Assert.Equal(20, s.WeeklyXp.Value);
        Assert.Equal(new DateOnly(2030, 1, 14), s.Week.Start);
    }

    [Fact]
    public void RecordXp_rejects_a_negative_amount()
    {
        var s = NewBronze(Wed);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.RecordXp(-5, Wed));
    }
}
