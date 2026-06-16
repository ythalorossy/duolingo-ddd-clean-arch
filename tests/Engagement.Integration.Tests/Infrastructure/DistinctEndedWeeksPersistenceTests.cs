using Engagement.Domain;
using Engagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Engagement.Integration.Tests.Infrastructure;

public class DistinctEndedWeeksPersistenceTests
{
    // Unique DB name for this class — xUnit runs classes in parallel, so a shared name races EnsureDeleted.
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_DueWeeksTest;Trusted_Connection=True;TrustServerCertificate=True";

    private static EngagementDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>().UseSqlServer(ConnectionString).Options;
        return new EngagementDbContext(options);
    }

    public DistinctEndedWeeksPersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static readonly LeagueWeek W1 = new(new DateOnly(2030, 1, 7));        // Monday
    private static readonly LeagueWeek W2 = new(new DateOnly(2030, 1, 14));       // Monday
    private static readonly LeagueWeek Current = new(new DateOnly(2030, 1, 21));  // Monday — the current week

    [Fact]
    public async Task Returns_distinct_ended_weeks_ascending_excluding_current()
    {
        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            // Two learners in W1 proves DISTINCT collapses the week to one entry.
            await repo.AddAsync(LeagueStanding.Create(new LearnerId(Guid.NewGuid()), W1, LeagueTier.Bronze), CancellationToken.None);
            await repo.AddAsync(LeagueStanding.Create(new LearnerId(Guid.NewGuid()), W1, LeagueTier.Bronze), CancellationToken.None);
            await repo.AddAsync(LeagueStanding.Create(new LearnerId(Guid.NewGuid()), W2, LeagueTier.Bronze), CancellationToken.None);
            await repo.AddAsync(LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Current, LeagueTier.Bronze), CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using var verify = NewContext();
        var due = await new LeagueStandingRepository(verify).GetDistinctEndedWeeksAsync(Current, CancellationToken.None);

        Assert.Equal(new[] { W1.Start, W2.Start }, due.Select(w => w.Start).ToArray()); // ascending, current excluded
    }
}
