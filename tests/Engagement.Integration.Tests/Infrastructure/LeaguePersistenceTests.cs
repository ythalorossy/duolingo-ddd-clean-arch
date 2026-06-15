using Engagement.Domain;
using Engagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Engagement.Integration.Tests.Infrastructure;

public class LeaguePersistenceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_LeagueTest;Trusted_Connection=True;TrustServerCertificate=True";

    private static EngagementDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>().UseSqlServer(ConnectionString).Options;
        return new EngagementDbContext(options);
    }

    public LeaguePersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static readonly LeagueWeek Wk1 = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero));  // Jan 7
    private static readonly LeagueWeek Wk2 = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 16, 12, 0, 0, TimeSpan.Zero)); // Jan 14

    [Fact]
    public async Task League_standing_round_trips()
    {
        var id = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            var s = LeagueStanding.Create(id, Wk1, LeagueTier.Gold);
            s.RecordXp(42);
            await repo.AddAsync(s, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var reloaded = await new LeagueStandingRepository(ctx).GetAsync(id, Wk1, CancellationToken.None);
            Assert.NotNull(reloaded);
            Assert.Equal(LeagueTier.Gold, reloaded!.Tier);
            Assert.Equal(42, reloaded.WeeklyXp.Value);
        }
    }

    [Fact]
    public async Task Same_learner_has_a_row_per_week_and_most_recent_wins()
    {
        var id = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            await repo.AddAsync(LeagueStanding.Create(id, Wk1, LeagueTier.Bronze), CancellationToken.None);
            await repo.AddAsync(LeagueStanding.Create(id, Wk2, LeagueTier.Silver), CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            Assert.Equal(LeagueTier.Bronze, (await repo.GetAsync(id, Wk1, CancellationToken.None))!.Tier);
            Assert.Equal(LeagueTier.Silver, (await repo.GetMostRecentAsync(id, CancellationToken.None))!.Tier);
        }
    }

    [Fact]
    public async Task Cohort_query_filters_by_tier_and_week_and_orders_desc()
    {
        var low = new LearnerId(Guid.NewGuid());
        var high = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            var a = LeagueStanding.Create(low, Wk1, LeagueTier.Bronze); a.RecordXp(10);
            var b = LeagueStanding.Create(high, Wk1, LeagueTier.Bronze); b.RecordXp(90);
            await repo.AddAsync(a, CancellationToken.None);
            await repo.AddAsync(b, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var cohort = await new LeagueStandingRepository(ctx)
                .GetCohortAsync(LeagueTier.Bronze, Wk1, CancellationToken.None);
            Assert.Equal(high.Value, cohort[0].Id.Value);
            Assert.Equal(low.Value, cohort[1].Id.Value);
        }
    }
}
