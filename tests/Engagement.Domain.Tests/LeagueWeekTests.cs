using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LeagueWeekTests
{
    // Jan 7 2030 is a Monday; Jan 9 = Wed, Jan 13 = Sun, Jan 14 = next Monday.
    [Fact]
    public void Containing_returns_the_monday_of_the_week()
    {
        var w = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 15, 0, 0, TimeSpan.Zero)); // Wed
        Assert.Equal(new DateOnly(2030, 1, 7), w.Start);
    }

    [Fact]
    public void Sunday_and_the_following_monday_are_different_weeks()
    {
        var sun = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 13, 23, 59, 59, TimeSpan.Zero));
        var mon = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 14, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2030, 1, 7), sun.Start);
        Assert.Equal(new DateOnly(2030, 1, 14), mon.Start);
        Assert.NotEqual(sun, mon);
    }

    [Fact]
    public void Keys_off_the_utc_instant_not_the_offset()
    {
        // 2030-01-14 13:00 +14:00 == 2030-01-13 23:00 UTC (a Sunday) → week of Jan 7.
        var w = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 14, 13, 0, 0, TimeSpan.FromHours(14)));
        Assert.Equal(new DateOnly(2030, 1, 7), w.Start);
    }

    [Fact]
    public void Constructor_rejects_a_non_monday()
    {
        Assert.Throws<ArgumentException>(() => new LeagueWeek(new DateOnly(2030, 1, 9))); // Wednesday
    }

    [Fact]
    public void Same_week_instances_are_equal()
    {
        var a = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 1, 0, 0, TimeSpan.Zero));
        var b = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 11, 22, 0, 0, TimeSpan.Zero));
        Assert.Equal(a, b);
    }
}
