using Engagement.Application;
using Engagement.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Application;

public class LeagueApplicationTests
{
    private sealed class InMemoryStandings : ILeagueStandingRepository
    {
        // keyed by (learnerId, weekStart)
        private readonly Dictionary<(Guid, DateOnly), LeagueStanding> _store = new();

        public Task<LeagueStanding?> GetAsync(LearnerId id, LeagueWeek week, CancellationToken ct) =>
            Task.FromResult(_store.GetValueOrDefault((id.Value, week.Start)));

        public Task<LeagueStanding?> GetMostRecentAsync(LearnerId id, CancellationToken ct) =>
            Task.FromResult(_store.Values
                .Where(s => s.Id.Value == id.Value)
                .OrderByDescending(s => s.Week.Start)
                .FirstOrDefault());

        public Task AddAsync(LeagueStanding s, CancellationToken ct)
        {
            _store[(s.Id.Value, s.Week.Start)] = s;
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
        public Task<IReadOnlyList<LeagueWeek>> GetDistinctEndedWeeksAsync(LeagueWeek currentWeek, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LeagueWeek>>(
                _store.Values.Select(s => s.Week).Distinct()
                    .Where(w => w.Start < currentWeek.Start).OrderBy(w => w.Start).ToList());
    }

    private static FakeTimeProvider ClockAt(int y, int m, int d) =>
        new(new DateTimeOffset(y, m, d, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task XpAwarded_creates_a_bronze_row_for_the_current_week()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9);
        var learner = Guid.NewGuid();

        await new RecordLeagueXpOnXpAwarded(repo, clock)
            .HandleAsync(new XpAwarded(learner, 15, 15, DateTimeOffset.UtcNow), CancellationToken.None);

        var s = await repo.GetAsync(new LearnerId(learner), LeagueWeek.Containing(clock.GetUtcNow()), CancellationToken.None);
        Assert.NotNull(s);
        Assert.Equal(LeagueTier.Bronze, s!.Tier);
        Assert.Equal(15, s.WeeklyXp.Value);
    }

    [Fact]
    public async Task A_new_week_creates_a_new_row_carrying_the_prior_tier_forward()
    {
        var repo = new InMemoryStandings();
        var learner = new LearnerId(Guid.NewGuid());
        // Seed a week-1 Gold row directly.
        await repo.AddAsync(LeagueStanding.Create(learner, ClockAtWeek(2030, 1, 9), LeagueTier.Gold), CancellationToken.None);

        var clock = ClockAt(2030, 1, 16); // next week
        await new RecordLeagueXpOnXpAwarded(repo, clock)
            .HandleAsync(new XpAwarded(learner.Value, 5, 5, DateTimeOffset.UtcNow), CancellationToken.None);

        var s = await repo.GetAsync(learner, LeagueWeek.Containing(clock.GetUtcNow()), CancellationToken.None);
        Assert.Equal(LeagueTier.Gold, s!.Tier); // carried forward
        Assert.Equal(5, s.WeeklyXp.Value);

        static LeagueWeek ClockAtWeek(int y, int m, int d) =>
            LeagueWeek.Containing(new DateTimeOffset(y, m, d, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Leaderboard_ranks_by_weekly_xp_desc_with_my_rank()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9);
        var record = new RecordLeagueXpOnXpAwarded(repo, clock);
        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();
        await record.HandleAsync(new XpAwarded(ana, 30, 30, DateTimeOffset.UtcNow), CancellationToken.None);
        await record.HandleAsync(new XpAwarded(bruno, 50, 50, DateTimeOffset.UtcNow), CancellationToken.None);

        var dto = await new GetLeagueLeaderboardHandler(repo, clock)
            .HandleAsync(new GetLeagueLeaderboard(ana), CancellationToken.None);

        Assert.Equal("Bronze", dto.Tier);
        Assert.Equal(new DateOnly(2030, 1, 7), dto.WeekStart);
        Assert.Equal(bruno, dto.Rows[0].LearnerId);
        Assert.Equal(2, dto.MyRank);
    }

    [Fact]
    public async Task Unknown_learner_gets_the_bronze_board_with_null_rank()
    {
        var dto = await new GetLeagueLeaderboardHandler(new InMemoryStandings(), ClockAt(2030, 1, 9))
            .HandleAsync(new GetLeagueLeaderboard(Guid.NewGuid()), CancellationToken.None);
        Assert.Equal("Bronze", dto.Tier);
        Assert.Empty(dto.Rows);
        Assert.Null(dto.MyRank);
    }
}
