using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LeagueStandingTests
{
    private static readonly LeagueWeek Wk = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)); // wk of Jan 7

    private static LeagueStanding NewBronze() =>
        LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Bronze);

    [Fact]
    public void Create_sets_identity_tier_and_zero_xp()
    {
        var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Gold);
        Assert.Equal(LeagueTier.Gold, s.Tier);
        Assert.Equal(new DateOnly(2030, 1, 7), s.Week.Start);
        Assert.Equal(0, s.WeeklyXp.Value);
    }

    [Fact]
    public void RecordXp_accumulates_on_this_weeks_row()
    {
        var s = NewBronze();
        s.RecordXp(15);
        s.RecordXp(10);
        Assert.Equal(25, s.WeeklyXp.Value);
    }

    [Fact]
    public void RecordXp_rejects_a_negative_amount()
    {
        var s = NewBronze();
        Assert.Throws<ArgumentOutOfRangeException>(() => s.RecordXp(-1));
    }
}
