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

    [Fact]
    public void PlaceInto_a_higher_tier_raises_Promoted()
    {
        var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Gold);
        s.PlaceInto(LeagueTier.Sapphire);
        Assert.Equal(LeagueTier.Sapphire, s.Tier);
        var evt = Assert.Single(s.DomainEvents.OfType<Promoted>());
        Assert.Equal(LeagueTier.Gold, evt.From);
        Assert.Equal(LeagueTier.Sapphire, evt.To);
    }

    [Fact]
    public void PlaceInto_a_lower_tier_raises_Demoted()
    {
        var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Gold);
        s.PlaceInto(LeagueTier.Silver);
        Assert.Equal(LeagueTier.Silver, s.Tier);
        Assert.Single(s.DomainEvents.OfType<Demoted>());
    }

    [Fact]
    public void PlaceInto_the_same_tier_is_a_no_op()
    {
        var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Gold);
        s.PlaceInto(LeagueTier.Gold);
        Assert.Empty(s.DomainEvents);
    }
}
