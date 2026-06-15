using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Engagement.Domain;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.EndToEnd;

[Collection("League E2E")]
public class LeaguePipelineTests(LeagueApiFactory factory)
{
    [Fact]
    public async Task Completing_a_lesson_creates_a_bronze_league_standing()
    {
        factory.Clock.SetUtcNow(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero));

        var learner = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.PublishAsync(new LessonCompleted(
                Guid.NewGuid(), learner, Guid.NewGuid(),
                new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ILeagueStandingRepository>();
            var s = await repo.GetMostRecentAsync(new LearnerId(learner), CancellationToken.None);
            Assert.NotNull(s);
            Assert.Equal(LeagueTier.Bronze, s!.Tier);
            Assert.Equal(new DateOnly(2030, 1, 7), s.Week.Start);
            Assert.True(s.WeeklyXp.Value > 0); // the XP policy awards a positive amount per lesson
        }
    }

    [Fact]
    public async Task Re_delivered_lesson_does_not_double_count_weekly_xp()
    {
        factory.Clock.SetUtcNow(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero));

        var learner = Guid.NewGuid();
        // Same event identity on both deliveries → AwardXp's AppliedAward ledger blocks the
        // second award, so no second XpAwarded is raised and the weekly score must not grow.
        var lesson = new LessonCompleted(
            Guid.NewGuid(), learner, Guid.NewGuid(),
            new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero));

        async Task Deliver()
        {
            using var scope = factory.Services.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.PublishAsync(lesson);
        }

        async Task<int> WeeklyXpOf()
        {
            using var scope = factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ILeagueStandingRepository>();
            var s = await repo.GetMostRecentAsync(new LearnerId(learner), CancellationToken.None);
            return s!.WeeklyXp.Value;
        }

        await Deliver();
        var afterFirst = await WeeklyXpOf();

        await Deliver(); // re-delivery of the identical event

        Assert.Equal(afterFirst, await WeeklyXpOf());
    }
}
