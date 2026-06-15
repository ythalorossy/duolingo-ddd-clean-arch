using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LeagueTierTests
{
    [Fact]
    public void Next_steps_up_one_tier()
    {
        Assert.Equal(LeagueTier.Gold, LeagueTier.Silver.Next());
    }

    [Fact]
    public void Previous_steps_down_one_tier()
    {
        Assert.Equal(LeagueTier.Silver, LeagueTier.Gold.Previous());
    }

    [Fact]
    public void Diamond_does_not_promote_above_itself()
    {
        Assert.Equal(LeagueTier.Diamond, LeagueTier.Diamond.Next());
    }

    [Fact]
    public void Bronze_does_not_demote_below_itself()
    {
        Assert.Equal(LeagueTier.Bronze, LeagueTier.Bronze.Previous());
    }
}
