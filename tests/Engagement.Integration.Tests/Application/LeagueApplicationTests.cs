using Engagement.Application;
using Engagement.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Application;

public class LeagueApplicationTests
{
    private sealed class InMemoryStandings : ILeagueStandingRepository
    {
        private readonly Dictionary<Guid, LeagueStanding> _store = new();

        public Task<LeagueStanding?> GetAsync(LearnerId id, CancellationToken ct) =>
            Task.FromResult(_store.GetValueOrDefault(id.Value));

        public Task AddAsync(LeagueStanding s, CancellationToken ct)
        {
            _store[s.Id.Value] = s;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(LeagueTier tier, LeagueWeek week, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LeagueStanding>>(
                _store.Values
                    .Where(s => s.Tier == tier && s.Week == week)
                    .OrderByDescending(s => s.WeeklyXp.Value)
                    .ThenBy(s => s.Id.Value)
                    .ToList());
    }

    private static FakeTimeProvider ClockAt(int y, int m, int d) =>
        new(new DateTimeOffset(y, m, d, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task XpAwarded_creates_a_bronze_standing_and_records_xp()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9); // Wed, week of Jan 7
        var handler = new RecordLeagueXpOnXpAwarded(repo, clock);
        var learner = Guid.NewGuid();

        await handler.HandleAsync(new XpAwarded(learner, 15, 15, DateTimeOffset.UtcNow), CancellationToken.None);

        var s = await repo.GetAsync(new LearnerId(learner), CancellationToken.None);
        Assert.NotNull(s);
        Assert.Equal(LeagueTier.Bronze, s!.Tier);
        Assert.Equal(15, s.WeeklyXp.Value);
        Assert.Equal(new DateOnly(2030, 1, 7), s.Week.Start);
    }

    [Fact]
    public async Task Two_awards_in_the_same_week_accumulate()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9);
        var handler = new RecordLeagueXpOnXpAwarded(repo, clock);
        var learner = Guid.NewGuid();

        await handler.HandleAsync(new XpAwarded(learner, 15, 15, DateTimeOffset.UtcNow), CancellationToken.None);
        clock.SetUtcNow(new DateTimeOffset(2030, 1, 10, 12, 0, 0, TimeSpan.Zero)); // Thu, same week
        await handler.HandleAsync(new XpAwarded(learner, 10, 25, DateTimeOffset.UtcNow), CancellationToken.None);

        var s = await repo.GetAsync(new LearnerId(learner), CancellationToken.None);
        Assert.Equal(25, s!.WeeklyXp.Value);
    }
}
