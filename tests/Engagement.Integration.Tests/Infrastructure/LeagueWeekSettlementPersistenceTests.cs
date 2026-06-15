using Engagement.Domain;
using Engagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Engagement.Integration.Tests.Infrastructure;

public class LeagueWeekSettlementPersistenceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_LeagueSettleTest;Trusted_Connection=True;TrustServerCertificate=True";

    private static EngagementDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>().UseSqlServer(ConnectionString).Options;
        return new EngagementDbContext(options);
    }

    public LeagueWeekSettlementPersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static readonly LeagueWeek Wk = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Marker_round_trips_and_exists_reports_true()
    {
        await using (var ctx = NewContext())
        {
            var repo = new LeagueWeekSettlementRepository(ctx);
            Assert.False(await repo.ExistsAsync(Wk, CancellationToken.None));
            await repo.AddAsync(new LeagueWeekSettlement(Wk, new DateTimeOffset(2030, 1, 14, 0, 0, 0, TimeSpan.Zero)), CancellationToken.None);
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            Assert.True(await new LeagueWeekSettlementRepository(ctx).ExistsAsync(Wk, CancellationToken.None));
        }
    }

    [Fact]
    public void LeagueWeek_Next_advances_seven_days()
    {
        Assert.Equal(new DateOnly(2030, 1, 14), Wk.Next().Start);
    }
}
