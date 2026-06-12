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

    [Fact]
    public async Task Leaderboard_ranks_by_weekly_xp_desc_with_my_rank()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9); // week of Jan 7
        var record = new RecordLeagueXpOnXpAwarded(repo, clock);

        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();
        await record.HandleAsync(new XpAwarded(ana, 30, 30, DateTimeOffset.UtcNow), CancellationToken.None);
        await record.HandleAsync(new XpAwarded(bruno, 50, 50, DateTimeOffset.UtcNow), CancellationToken.None);

        var dto = await new GetLeagueLeaderboardHandler(repo, clock)
            .HandleAsync(new GetLeagueLeaderboard(ana), CancellationToken.None);

        Assert.Equal("Bronze", dto.Tier);
        Assert.Equal(new DateOnly(2030, 1, 7), dto.WeekStart);
        Assert.Equal(2, dto.Rows.Count);
        Assert.Equal(bruno, dto.Rows[0].LearnerId); // 50 ranks first
        Assert.Equal(ana, dto.Rows[1].LearnerId);
        Assert.Equal(2, dto.MyRank);                // ana is rank 2
    }

    [Fact]
    public async Task Unknown_learner_gets_the_bronze_board_with_null_rank()
    {
        var dto = await new GetLeagueLeaderboardHandler(new InMemoryStandings(), ClockAt(2030, 1, 9))
            .HandleAsync(new GetLeagueLeaderboard(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("Bronze", dto.Tier);
        Assert.Empty(dto.Rows);
        Assert.Null(dto.MyRank);
        Assert.Equal(new DateOnly(2030, 1, 7), dto.WeekStart);
    }

    [Fact]
    public async Task A_stale_week_standing_is_absent_from_the_current_board()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9); // week of Jan 7
        var learner = Guid.NewGuid();
        await new RecordLeagueXpOnXpAwarded(repo, clock)
            .HandleAsync(new XpAwarded(learner, 20, 20, DateTimeOffset.UtcNow), CancellationToken.None);

        clock.SetUtcNow(new DateTimeOffset(2030, 1, 16, 12, 0, 0, TimeSpan.Zero)); // next week (Jan 14)
        var dto = await new GetLeagueLeaderboardHandler(repo, clock)
            .HandleAsync(new GetLeagueLeaderboard(learner), CancellationToken.None);

        Assert.Empty(dto.Rows);
        Assert.Null(dto.MyRank);
        Assert.Equal(new DateOnly(2030, 1, 14), dto.WeekStart);
    }
}
