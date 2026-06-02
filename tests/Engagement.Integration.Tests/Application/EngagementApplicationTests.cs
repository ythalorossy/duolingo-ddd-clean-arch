using BuildingBlocks.Contracts;
using Engagement.Application;
using Engagement.Domain;
using Xunit;

namespace Engagement.Integration.Tests.Application;

public class EngagementApplicationTests
{
    private sealed class InMemoryRepo : ILearnerEngagementRepository
    {
        private readonly Dictionary<Guid, LearnerEngagement> _store = new();
        public Task<LearnerEngagement?> GetAsync(LearnerId id, CancellationToken ct) =>
            Task.FromResult(_store.GetValueOrDefault(id.Value));
        public Task AddAsync(LearnerEngagement learner, CancellationToken ct)
        {
            _store[learner.Id.Value] = learner;
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task Completing_a_lesson_awards_flat_xp_to_a_new_learner()
    {
        var repo = new InMemoryRepo();
        var handler = new AwardXpForLessonCompletedHandler(repo, new LessonCompletionXpPolicy());
        var learnerId = Guid.NewGuid();

        await handler.HandleAsync(
            new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        var learner = await repo.GetAsync(new LearnerId(learnerId), CancellationToken.None);
        Assert.Equal(10, learner!.TotalXp.Value);
    }

    [Fact]
    public async Task Same_event_delivered_twice_awards_once()
    {
        var repo = new InMemoryRepo();
        var handler = new AwardXpForLessonCompletedHandler(repo, new LessonCompletionXpPolicy());
        var learnerId = Guid.NewGuid();
        var evt = new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, CancellationToken.None);
        await handler.HandleAsync(evt, CancellationToken.None);

        var learner = await repo.GetAsync(new LearnerId(learnerId), CancellationToken.None);
        Assert.Equal(10, learner!.TotalXp.Value);
    }

    [Fact]
    public async Task Query_returns_zero_for_an_unknown_learner()
    {
        var repo = new InMemoryRepo();
        var handler = new GetLearnerEngagementHandler(repo);

        var result = await handler.HandleAsync(new GetLearnerEngagement(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(0, result.TotalXp);
    }
}
