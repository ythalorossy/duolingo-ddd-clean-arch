using Engagement.Domain;
using Engagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Engagement.Integration.Tests.Infrastructure;

public class EngagementPersistenceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static EngagementDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new EngagementDbContext(options);
    }

    public EngagementPersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate(); // applies the engagement schema migration to a clean DB
    }

    [Fact]
    public async Task Aggregate_round_trips_with_total_and_applied_awards()
    {
        var learnerId = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LearnerEngagementRepository(ctx);
            var learner = LearnerEngagement.Create(learnerId);
            learner.AwardXp(new XpAward(10, "LessonCompleted", Guid.NewGuid()));
            await repo.AddAsync(learner, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var repo = new LearnerEngagementRepository(ctx);
            var reloaded = await repo.GetAsync(learnerId, CancellationToken.None);

            Assert.NotNull(reloaded);
            Assert.Equal(10, reloaded!.TotalXp.Value);
            Assert.Single(reloaded.AppliedAwards);
        }
    }

    [Fact]
    public async Task Domain_events_are_not_persisted_and_are_cleared_on_save()
    {
        var learnerId = new LearnerId(Guid.NewGuid());
        await using var ctx = NewContext();
        var repo = new LearnerEngagementRepository(ctx);

        var learner = LearnerEngagement.Create(learnerId);
        learner.AwardXp(new XpAward(10, "LessonCompleted", Guid.NewGuid()));
        await repo.AddAsync(learner, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        Assert.Empty(learner.DomainEvents);
    }
}
