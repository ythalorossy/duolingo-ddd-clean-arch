using BuildingBlocks.Contracts;
using Engagement.Application;
using Engagement.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Application;

public class StreakApplicationTests
{
    private sealed class InMemoryStreaks : ILearnerStreakRepository
    {
        private readonly Dictionary<Guid, LearnerStreak> _store = new();
        public Task<LearnerStreak?> GetAsync(LearnerId id, CancellationToken ct) =>
            Task.FromResult(_store.GetValueOrDefault(id.Value));
        public Task AddAsync(LearnerStreak s, CancellationToken ct) { _store[s.Id.Value] = s; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static DateTimeOffset Noon(int y, int m, int d) => new(y, m, d, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Lesson_completed_advances_the_streak()
    {
        var repo = new InMemoryStreaks();
        var handler = new RegisterStreakForLessonCompletedHandler(repo);
        var learnerId = Guid.NewGuid();

        await handler.HandleAsync(new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), Noon(2030, 1, 1)), CancellationToken.None);
        await handler.HandleAsync(new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), Noon(2030, 1, 2)), CancellationToken.None);

        var s = await repo.GetAsync(new LearnerId(learnerId), CancellationToken.None);
        Assert.Equal(2, s!.CurrentStreak);
    }

    [Fact]
    public async Task Set_time_zone_persists_on_the_streak()
    {
        var repo = new InMemoryStreaks();
        var handler = new SetLearnerTimeZoneHandler(repo);
        var learnerId = Guid.NewGuid();

        await handler.HandleAsync(new SetLearnerTimeZone(learnerId, "America/New_York"), CancellationToken.None);

        var s = await repo.GetAsync(new LearnerId(learnerId), CancellationToken.None);
        Assert.Equal("America/New_York", s!.TimeZone.IanaId);
    }

    [Fact]
    public async Task Query_reports_active_on_the_local_today()
    {
        var repo = new InMemoryStreaks();
        var learnerId = Guid.NewGuid();
        await new RegisterStreakForLessonCompletedHandler(repo)
            .HandleAsync(new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), Noon(2030, 1, 1)), CancellationToken.None);

        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero));
        var dto = await new GetLearnerStreakHandler(repo, clock)
            .HandleAsync(new GetLearnerStreak(learnerId), CancellationToken.None);

        Assert.Equal(1, dto.CurrentStreak);
        Assert.Equal("Active", dto.Status);
    }

    [Fact]
    public async Task Query_returns_none_for_unknown_learner()
    {
        var dto = await new GetLearnerStreakHandler(new InMemoryStreaks(), new FakeTimeProvider())
            .HandleAsync(new GetLearnerStreak(Guid.NewGuid()), CancellationToken.None);
        Assert.Equal("None", dto.Status);
        Assert.Equal(0, dto.CurrentStreak);
    }

    [Fact]
    public async Task Grant_creates_streak_and_adds_a_freeze()
    {
        var repo = new InMemoryStreaks();
        var handler = new GrantStreakFreezeHandler(repo);
        var learnerId = Guid.NewGuid();

        await handler.HandleAsync(new GrantStreakFreeze(learnerId), CancellationToken.None);

        var s = await repo.GetAsync(new LearnerId(learnerId), CancellationToken.None);
        Assert.Equal(1, s!.FreezeBalance);
    }

    [Fact]
    public async Task Grant_respects_the_cap()
    {
        var repo = new InMemoryStreaks();
        var handler = new GrantStreakFreezeHandler(repo);
        var learnerId = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
            await handler.HandleAsync(new GrantStreakFreeze(learnerId), CancellationToken.None);

        var s = await repo.GetAsync(new LearnerId(learnerId), CancellationToken.None);
        Assert.Equal(LearnerStreak.MaxFreezes, s!.FreezeBalance);
    }

    [Fact]
    public async Task Query_exposes_projected_freezes_available()
    {
        var repo = new InMemoryStreaks();
        var learnerId = Guid.NewGuid();
        await new GrantStreakFreezeHandler(repo).HandleAsync(new GrantStreakFreeze(learnerId), CancellationToken.None);
        await new RegisterStreakForLessonCompletedHandler(repo)
            .HandleAsync(new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), Noon(2030, 1, 1)), CancellationToken.None);

        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero));
        var dto = await new GetLearnerStreakHandler(repo, clock)
            .HandleAsync(new GetLearnerStreak(learnerId), CancellationToken.None);

        Assert.Equal(1, dto.FreezesAvailable);
    }

    [Fact]
    public async Task Unknown_learner_reports_zero_freezes()
    {
        var dto = await new GetLearnerStreakHandler(new InMemoryStreaks(), new FakeTimeProvider())
            .HandleAsync(new GetLearnerStreak(Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(0, dto.FreezesAvailable);
    }
}
