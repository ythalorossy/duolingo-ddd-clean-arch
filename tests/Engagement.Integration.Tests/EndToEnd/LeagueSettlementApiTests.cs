using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Engagement.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.EndToEnd;

// The settlement tests advance the shared FakeTimeProvider clock significantly (January → March),
// which would prevent other league e2e tests from going back to January. A dedicated factory
// (separate DB, separate clock) isolates this clock progression.
public sealed class LeagueSettlementApiFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_LeagueSettlement_E2E;Trusted_Connection=True;TrustServerCertificate=True";

    // Start well before March 2030 so settlement tests can advance forward freely.
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Engagement", TestConnectionString);
        builder.UseSetting("Leagues:Settlement:Enabled", "false"); // keep the scheduler out of E2E (shared FakeTimeProvider)

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EngagementDbContext>();
            db.Database.EnsureDeleted();
            db.Database.Migrate();
        });
    }
}

[CollectionDefinition("League Settlement E2E")]
public sealed class LeagueSettlementE2ECollection : ICollectionFixture<LeagueSettlementApiFactory>;

[Collection("League Settlement E2E")]
public class LeagueSettlementApiTests(LeagueSettlementApiFactory factory)
{
    private sealed record LeaderboardResponse(string Tier, DateOnly WeekStart, List<Row> Rows, int? MyRank);
    private sealed record Row(int Rank, Guid LearnerId, int WeeklyXp);

    private HttpClient ClientFor(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    private async Task CompleteLesson(Guid learnerId, DateTimeOffset whenUtc)
    {
        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.PublishAsync(new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), whenUtc));
    }

    [Fact]
    public async Task Settling_promotes_the_top_learner_visible_next_week()
    {
        // Use a distinct, far week to stay isolated from the January data in other league e2e tests.
        var wk = new DateTimeOffset(2030, 3, 6, 12, 0, 0, TimeSpan.Zero); // Wed; week of Mon Mar 4
        factory.Clock.SetUtcNow(wk);

        // Build a Bronze cohort of 5 (floor(0.2*5)=1 promotes). Distinct XP via lesson counts.
        var learners = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            learners.Add(id);
            for (var l = 0; l <= i; l++) // learner i completes i+1 lessons → more XP for higher i
                await CompleteLesson(id, wk);
        }
        var topEarner = learners[4]; // most lessons → rank 1

        var settle = await ClientFor(Guid.NewGuid())
            .PostAsync("/leagues/weeks/2030-03-04/settle", content: null);
        Assert.Equal(HttpStatusCode.OK, settle.StatusCode);

        // Next week, the top earner is in Silver.
        factory.Clock.SetUtcNow(new DateTimeOffset(2030, 3, 13, 12, 0, 0, TimeSpan.Zero)); // week of Mon Mar 11
        await CompleteLesson(topEarner, new DateTimeOffset(2030, 3, 13, 12, 0, 0, TimeSpan.Zero));
        var board = await ClientFor(topEarner).GetFromJsonAsync<LeaderboardResponse>("/me/league");
        Assert.Equal("Silver", board!.Tier);
    }

    [Fact]
    public async Task Non_monday_weekstart_is_a_400()
    {
        var resp = await ClientFor(Guid.NewGuid())
            .PostAsync("/leagues/weeks/2030-03-06/settle", content: null); // Wednesday
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
