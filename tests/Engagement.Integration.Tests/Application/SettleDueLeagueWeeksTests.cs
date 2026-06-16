using BuildingBlocks.Mediator;
using Engagement.Application;
using Engagement.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Application;

public class SettleDueLeagueWeeksTests
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
        public Task<IReadOnlyList<LeagueWeek>> GetDistinctEndedWeeksAsync(LeagueWeek currentWeek, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LeagueWeek>>(
                Store.Values.Select(s => s.Week).Distinct()
                    .Where(w => w.Start < currentWeek.Start).OrderBy(w => w.Start).ToList());
    }

    private sealed class InMemorySettlements : ILeagueWeekSettlementRepository
    {
        public readonly HashSet<DateOnly> Settled = new();
        public Task<bool> ExistsAsync(LeagueWeek week, CancellationToken ct) => Task.FromResult(Settled.Contains(week.Start));
        public Task AddAsync(LeagueWeekSettlement marker, CancellationToken ct) { Settled.Add(marker.Week.Start); return Task.CompletedTask; }
    }

    // Routes SettleLeagueWeek to a REAL handler over the same repos, and records the order weeks
    // were settled in — so we can assert both the policy (which/what order) and the real outcome.
    private sealed class DispatchingMediator(
        ILeagueStandingRepository st, ILeagueWeekSettlementRepository se, TimeProvider clock) : IMediator
    {
        public readonly List<DateOnly> SettledOrder = new();
        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is SettleLeagueWeek cmd)
            {
                SettledOrder.Add(cmd.WeekStart);
                await new SettleLeagueWeekHandler(st, se, clock).HandleAsync(cmd, ct);
                return (TResponse)(object)Unit.Value;
            }
            throw new NotSupportedException(request.GetType().Name);
        }
        public Task PublishAsync(INotification notification, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static readonly LeagueWeek W1 = new(new DateOnly(2030, 1, 7));
    private static readonly LeagueWeek W2 = new(new DateOnly(2030, 1, 14));
    private static readonly DateOnly W3Start = new(2030, 1, 21); // the current week
    private static FakeTimeProvider ClockInW3() => new(new DateTimeOffset(2030, 1, 21, 12, 0, 0, TimeSpan.Zero));

    private static void Seed(InMemoryStandings st, LeagueWeek week, int n, LeagueTier tier)
    {
        for (var i = 0; i < n; i++)
        {
            var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), week, tier);
            s.RecordXp((n - i) * 10); // descending XP: first seeded is the top
            st.Store[(s.Id.Value, week.Start)] = s;
        }
    }

    [Fact]
    public async Task Settles_ended_weeks_oldest_first_excluding_current()
    {
        var st = new InMemoryStandings();
        var se = new InMemorySettlements();
        Seed(st, W2, 5, LeagueTier.Bronze);                       // seeded out of order on purpose
        Seed(st, W1, 5, LeagueTier.Bronze);
        Seed(st, new LeagueWeek(W3Start), 5, LeagueTier.Bronze);  // current week — must NOT settle
        var mediator = new DispatchingMediator(st, se, ClockInW3());
        var handler = new SettleDueLeagueWeeksHandler(st, mediator, ClockInW3());

        await handler.HandleAsync(new SettleDueLeagueWeeks(), CancellationToken.None);

        Assert.Equal(new[] { W1.Start, W2.Start }, mediator.SettledOrder.ToArray());
    }

    [Fact]
    public async Task Nothing_due_when_only_the_current_week_has_standings()
    {
        var st = new InMemoryStandings();
        var se = new InMemorySettlements();
        Seed(st, new LeagueWeek(W3Start), 5, LeagueTier.Bronze);
        var mediator = new DispatchingMediator(st, se, ClockInW3());
        var handler = new SettleDueLeagueWeeksHandler(st, mediator, ClockInW3());

        await handler.HandleAsync(new SettleDueLeagueWeeks(), CancellationToken.None);

        Assert.Empty(mediator.SettledOrder);
    }

    [Fact]
    public async Task Catch_up_settles_in_order_so_w1_promotion_lands_in_w2()
    {
        var st = new InMemoryStandings();
        var se = new InMemorySettlements();
        Seed(st, W1, 5, LeagueTier.Bronze); // floor(0.2*5)=1 → the top learner promotes Bronze→Silver
        var topW1 = st.Store.Values.Where(s => s.Week == W1)
            .OrderByDescending(s => s.WeeklyXp.Value).First().Id.Value;
        // The same top learner also earned early in W2 at the carried-forward Bronze tier.
        var early = LeagueStanding.Create(new LearnerId(topW1), W2, LeagueTier.Bronze);
        early.RecordXp(5);
        st.Store[(topW1, W2.Start)] = early;
        var mediator = new DispatchingMediator(st, se, ClockInW3()); // W1 and W2 both ended
        var handler = new SettleDueLeagueWeeksHandler(st, mediator, ClockInW3());

        await handler.HandleAsync(new SettleDueLeagueWeeks(), CancellationToken.None);

        Assert.Equal(new[] { W1.Start, W2.Start }, mediator.SettledOrder.ToArray());  // oldest first
        Assert.Equal(LeagueTier.Silver, st.Store[(topW1, W2.Start)].Tier);            // W1 settled first → W2 row reconciled up
    }
}
