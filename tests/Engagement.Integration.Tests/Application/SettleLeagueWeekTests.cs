using Engagement.Application;
using Engagement.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Application;

public class SettleLeagueWeekTests
{
    private sealed class InMemoryStandings : ILeagueStandingRepository
    {
        public readonly Dictionary<(Guid, DateOnly), LeagueStanding> Store = new();
        public Task<LeagueStanding?> GetAsync(LearnerId id, LeagueWeek week, CancellationToken ct) =>
            Task.FromResult(Store.GetValueOrDefault((id.Value, week.Start)));
        public Task<LeagueStanding?> GetMostRecentAsync(LearnerId id, CancellationToken ct) =>
            Task.FromResult(Store.Values.Where(s => s.Id.Value == id.Value).OrderByDescending(s => s.Week.Start).FirstOrDefault());
        public Task AddAsync(LeagueStanding s, CancellationToken ct) { Store[(s.Id.Value, s.Week.Start)] = s; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(LeagueTier tier, LeagueWeek week, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LeagueStanding>>(
                Store.Values.Where(s => s.Tier == tier && s.Week == week)
                    .OrderByDescending(s => s.WeeklyXp.Value).ThenBy(s => s.Id.Value).ToList());
    }

    private sealed class InMemorySettlements : ILeagueWeekSettlementRepository
    {
        public readonly HashSet<DateOnly> Settled = new();
        public Task<bool> ExistsAsync(LeagueWeek week, CancellationToken ct) => Task.FromResult(Settled.Contains(week.Start));
        public Task AddAsync(LeagueWeekSettlement marker, CancellationToken ct) { Settled.Add(marker.Week.Start); return Task.CompletedTask; }
    }

    private static readonly LeagueWeek Wk = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)); // input Wed Jan 9 2030; week starts Mon Jan 7
    private static readonly DateOnly NextStart = new(2030, 1, 14);
    private static FakeTimeProvider Clock => new(new DateTimeOffset(2030, 1, 14, 0, 0, 0, TimeSpan.Zero));

    // Build a Bronze cohort of `n` with descending XP (so rank i has XP = n-i).
    private static (InMemoryStandings, List<Guid>) BronzeCohort(int n)
    {
        var repo = new InMemoryStandings();
        var ids = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            var id = new LearnerId(Guid.NewGuid());
            ids.Add(id.Value);
            var s = LeagueStanding.Create(id, Wk, LeagueTier.Bronze);
            s.RecordXp((n - i) * 10);
            repo.Store[(id.Value, Wk.Start)] = s;
        }
        return (repo, ids); // ids[0] is the top, ids[n-1] the bottom
    }

    private static SettleLeagueWeekHandler Handler(InMemoryStandings st, InMemorySettlements se) =>
        new(st, se, Clock);

    [Fact]
    public async Task Top_and_bottom_floor20pct_move_others_stay()
    {
        var (st, ids) = BronzeCohort(12); // floor(0.2*12)=2
        var se = new InMemorySettlements();

        await Handler(st, se).HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);

        // Bronze never demotes, so only the top 2 move (to Silver) for next week.
        Assert.Equal(LeagueTier.Silver, st.Store[(ids[0], NextStart)].Tier);
        Assert.Equal(LeagueTier.Silver, st.Store[(ids[1], NextStart)].Tier);
        // A middle learner has no next-week placement row.
        Assert.False(st.Store.ContainsKey((ids[5], NextStart)));
    }

    [Fact]
    public async Task Gold_cohort_promotes_top_and_demotes_bottom()
    {
        var (st, ids) = BronzeCohort(12);
        foreach (var key in st.Store.Keys.ToList()) // relabel the seeded cohort as Gold
            st.Store[key].PlaceInto(LeagueTier.Gold);
        var se = new InMemorySettlements();

        await Handler(st, se).HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);

        Assert.Equal(LeagueTier.Sapphire, st.Store[(ids[0], NextStart)].Tier);  // top up
        Assert.Equal(LeagueTier.Silver, st.Store[(ids[11], NextStart)].Tier);   // bottom down
    }

    [Fact]
    public async Task Tiny_cohort_of_four_does_not_move()
    {
        var (st, ids) = BronzeCohort(4); // floor(0.2*4)=0
        var se = new InMemorySettlements();
        await Handler(st, se).HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);
        Assert.All(ids, id => Assert.False(st.Store.ContainsKey((id, NextStart))));
    }

    [Fact]
    public async Task Re_settling_the_same_week_is_a_no_op()
    {
        var (st, ids) = BronzeCohort(12);
        var se = new InMemorySettlements();
        var handler = Handler(st, se);

        await handler.HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);
        var tierAfterFirst = st.Store[(ids[0], NextStart)].Tier;
        await handler.HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);

        Assert.Equal(tierAfterFirst, st.Store[(ids[0], NextStart)].Tier); // unchanged
        Assert.Single(se.Settled);
    }

    [Fact]
    public async Task Existing_next_week_row_keeps_its_xp_when_reconciled()
    {
        var (st, ids) = BronzeCohort(12);
        foreach (var key in st.Store.Keys.ToList()) st.Store[key].PlaceInto(LeagueTier.Gold);
        // Top learner already earned in next week at carried-forward Gold.
        var top = ids[0];
        var early = LeagueStanding.Create(new LearnerId(top), Wk.Next(), LeagueTier.Gold);
        early.RecordXp(99);
        st.Store[(top, NextStart)] = early;
        var se = new InMemorySettlements();

        await Handler(st, se).HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);

        Assert.Equal(LeagueTier.Sapphire, st.Store[(top, NextStart)].Tier); // moved
        Assert.Equal(99, st.Store[(top, NextStart)].WeeklyXp.Value);        // XP preserved
    }

    [Fact]
    public async Task Diamond_cohort_top_slice_stays_at_Diamond()
    {
        var (st, ids) = BronzeCohort(10); // k = floor(0.2 * 10) = 2
        foreach (var key in st.Store.Keys.ToList())
            st.Store[key].PlaceInto(LeagueTier.Diamond); // relabel the cohort as Diamond
        var se = new InMemorySettlements();

        await Handler(st, se).HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);

        // Top 2 would promote past Diamond → clamped to Diamond → no placement row created.
        Assert.False(st.Store.ContainsKey((ids[0], NextStart)));
        Assert.False(st.Store.ContainsKey((ids[1], NextStart)));
        // Bottom 2 demote to Obsidian.
        Assert.Equal(LeagueTier.Obsidian, st.Store[(ids[8], NextStart)].Tier);
        Assert.Equal(LeagueTier.Obsidian, st.Store[(ids[9], NextStart)].Tier);
    }
}
