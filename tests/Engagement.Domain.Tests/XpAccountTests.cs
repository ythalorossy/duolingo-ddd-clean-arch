using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class XpAccountTests
{
    private static XpAccount NewLearner() => XpAccount.Create(new LearnerId(Guid.NewGuid()));
    private static XpAward Award(int amount = 10) => new(amount, "LessonCompleted", Guid.NewGuid());

    [Fact]
    public void New_learner_starts_with_zero_xp()
    {
        Assert.Equal(0, NewLearner().TotalXp.Value);
    }

    [Fact]
    public void Awarding_xp_increases_the_total()
    {
        var learner = NewLearner();
        learner.AwardXp(Award(10));
        Assert.Equal(10, learner.TotalXp.Value);
    }

    [Fact]
    public void Awarding_xp_raises_an_XpAwarded_event()
    {
        var learner = NewLearner();
        learner.AwardXp(Award(10));

        var evt = Assert.IsType<XpAwarded>(Assert.Single(learner.DomainEvents));
        Assert.Equal(learner.Id.Value, evt.LearnerId);
        Assert.Equal(10, evt.Amount);
        Assert.Equal(10, evt.NewTotal);
    }

    [Fact]
    public void Awarding_the_same_source_twice_is_idempotent()
    {
        var learner = NewLearner();
        var award = Award(10);

        learner.AwardXp(award);
        learner.AwardXp(award); // same SourceId

        Assert.Equal(10, learner.TotalXp.Value);              // awarded once
        Assert.Single(learner.DomainEvents);                  // event raised once
    }

    [Fact]
    public void Policy_returns_flat_ten_for_a_completed_lesson()
    {
        Assert.Equal(10, new LessonCompletionXpPolicy().XpForCompletedLesson());
    }
}
