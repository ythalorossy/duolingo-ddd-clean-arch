using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LearnerTimeZoneTests
{
    [Fact]
    public void Utc_is_valid_and_default()
    {
        Assert.Equal("UTC", LearnerTimeZone.Utc.IanaId);
    }

    [Fact]
    public void Rejects_unknown_time_zone()
    {
        Assert.Throws<ArgumentException>(() => new LearnerTimeZone("Mars/Olympus_Mons"));
    }

    [Fact]
    public void Rejects_empty()
    {
        Assert.Throws<ArgumentException>(() => new LearnerTimeZone(" "));
    }

    [Fact]
    public void Converts_late_night_instant_to_correct_local_date()
    {
        // New York is UTC-5 (Jan, no DST). Mon 11:30 PM local = Tue 04:30 UTC.
        var ny = new LearnerTimeZone("America/New_York");
        var utcInstant = new DateTimeOffset(2030, 1, 8, 4, 30, 0, TimeSpan.Zero); // Tue 04:30 UTC

        var localDate = ny.LocalDateOf(utcInstant);

        Assert.Equal(new DateOnly(2030, 1, 7), localDate); // still Monday in New York
    }

    [Fact]
    public void Utc_zone_keeps_the_utc_date()
    {
        var utc = LearnerTimeZone.Utc;
        var instant = new DateTimeOffset(2030, 1, 8, 4, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2030, 1, 8), utc.LocalDateOf(instant));
    }
}
