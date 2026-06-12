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
        return new EngagementDbContext(options); // dispatcher defaults to null at this layer
    }

    public LeaguePersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static readonly DateTimeOffset Wed = new(2030, 1, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task League_standing_round_trips()
    {
        var id = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            var s = LeagueStanding.Create(id, LeagueWeek.Containing(Wed));
            s.RecordXp(42, Wed);
            await repo.AddAsync(s, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var reloaded = await new LeagueStandingRepository(ctx).GetAsync(id, CancellationToken.None);
            Assert.NotNull(reloaded);
            Assert.Equal(LeagueTier.Bronze, reloaded!.Tier);
            Assert.Equal(42, reloaded.WeeklyXp.Value);
            Assert.Equal(new DateOnly(2030, 1, 7), reloaded.Week.Start);
        }
    }

    [Fact]
    public async Task Cohort_query_filters_by_tier_and_week_and_orders_desc()
    {
        var week = LeagueWeek.Containing(Wed);
        var low = new LearnerId(Guid.NewGuid());
        var high = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            var a = LeagueStanding.Create(low, week); a.RecordXp(10, Wed);
            var b = LeagueStanding.Create(high, week); b.RecordXp(90, Wed);
            await repo.AddAsync(a, CancellationToken.None);
            await repo.AddAsync(b, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var cohort = await new LeagueStandingRepository(ctx)
                .GetCohortAsync(LeagueTier.Bronze, week, CancellationToken.None);
            Assert.Equal(2, cohort.Count);
            Assert.Equal(high.Value, cohort[0].Id.Value); // 90 ranks first
            Assert.Equal(low.Value, cohort[1].Id.Value);
        }
    }
}
