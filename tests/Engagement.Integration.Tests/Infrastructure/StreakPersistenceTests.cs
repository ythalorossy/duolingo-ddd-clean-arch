using Engagement.Domain;
using Engagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Engagement.Integration.Tests.Infrastructure;

public class StreakPersistenceTests
{
    // Distinct DB from slice-1's EngagementPersistenceTests so the two test classes
    // don't race each other's EnsureDeleted under xUnit's parallel execution.
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_StreakTest;Trusted_Connection=True;TrustServerCertificate=True";

    private static EngagementDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>().UseSqlServer(ConnectionString).Options;
        return new EngagementDbContext(options);
    }

    public StreakPersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task Streak_round_trips_with_timezone_and_dateonly()
    {
        var id = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LearnerStreakRepository(ctx);
            var s = LearnerStreak.Create(id);
            s.ChangeTimeZone(new LearnerTimeZone("America/New_York"));
            s.RegisterQualifyingActivity(new DateTimeOffset(2030, 1, 1, 17, 0, 0, TimeSpan.Zero)); // noon NY
            await repo.AddAsync(s, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var reloaded = await new LearnerStreakRepository(ctx).GetAsync(id, CancellationToken.None);
            Assert.NotNull(reloaded);
            Assert.Equal("America/New_York", reloaded!.TimeZone.IanaId);
            Assert.Equal(1, reloaded.CurrentStreak);
            Assert.Equal(new DateOnly(2030, 1, 1), reloaded.LastQualifyingDate);
        }
    }
}
