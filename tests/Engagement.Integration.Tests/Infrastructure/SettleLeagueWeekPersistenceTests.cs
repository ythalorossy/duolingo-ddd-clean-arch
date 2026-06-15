using Engagement.Application;
using Engagement.Domain;
using Engagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Infrastructure;

public class SettleLeagueWeekPersistenceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_SettleTest;Trusted_Connection=True;TrustServerCertificate=True";

    private static EngagementDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>().UseSqlServer(ConnectionString).Options;
        return new EngagementDbContext(options);
    }

    public SettleLeagueWeekPersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static readonly LeagueWeek Wk = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)); // week of Mon Jan 7
    private static readonly LeagueWeek NextWk = Wk.Next();

    private async Task Settle()
    {
        await using var ctx = NewContext();
        var handler = new SettleLeagueWeekHandler(
            new LeagueStandingRepository(ctx),
            new LeagueWeekSettlementRepository(ctx),
            new FakeTimeProvider(new DateTimeOffset(2030, 1, 14, 0, 0, 0, TimeSpan.Zero)));
        await handler.HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);
    }

    private static async Task<int> SilverCountNextWeek()
    {
        await using var ctx = NewContext();
        var cohort = await new LeagueStandingRepository(ctx).GetCohortAsync(LeagueTier.Silver, NextWk, CancellationToken.None);
        return cohort.Count;
    }

    [Fact]
    public async Task Settling_twice_against_the_db_does_not_double_move()
    {
        // Seed a Bronze cohort of 10 (k = floor(0.2*10) = 2) for week Jan 7, descending XP.
        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            for (var i = 0; i < 10; i++)
            {
                var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Bronze);
                s.RecordXp((10 - i) * 10);
                await repo.AddAsync(s, CancellationToken.None);
            }
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await Settle();
        Assert.Equal(2, await SilverCountNextWeek()); // top 2 promoted Bronze→Silver

        await Settle(); // marker exists → no-op

        Assert.Equal(2, await SilverCountNextWeek()); // unchanged, no double-move
        await using var verify = NewContext();
        Assert.True(await new LeagueWeekSettlementRepository(verify).ExistsAsync(Wk, CancellationToken.None));
    }
}
