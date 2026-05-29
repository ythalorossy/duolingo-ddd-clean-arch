using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class ValueObjectsTests
{
    [Fact]
    public void LearnerId_rejects_empty_guid()
    {
        Assert.Throws<ArgumentException>(() => new LearnerId(Guid.Empty));
    }

    [Fact]
    public void Xp_starts_at_zero_and_is_never_negative()
    {
        Assert.Equal(0, Xp.Zero.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Xp(-1));
    }

    [Fact]
    public void Xp_add_increases_by_award_amount()
    {
        var result = Xp.Zero.Add(new XpAward(10, "LessonCompleted", Guid.NewGuid()));
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void XpAward_rejects_non_positive_amounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new XpAward(0, "x", Guid.NewGuid()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new XpAward(-5, "x", Guid.NewGuid()));
    }

    [Fact]
    public void XpAward_rejects_empty_source_id()
    {
        Assert.Throws<ArgumentException>(() => new XpAward(10, "x", Guid.Empty));
    }
}
