using System.Net.Http.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.EndToEnd;

[Collection("League E2E")]
public class LeagueApiTests(LeagueApiFactory factory)
{
    private sealed record LeaderboardResponse(string Tier, DateOnly WeekStart, List<Row> Rows, int? MyRank);
    private sealed record Row(int Rank, Guid LearnerId, int WeeklyXp);

    private HttpClient ClientFor(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    private async Task CompleteLesson(Guid learnerId)
    {
        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.PublishAsync(new LessonCompleted(
            Guid.NewGuid(), learnerId, Guid.NewGuid(),
            new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task Leaderboard_ranks_learners_by_weekly_xp()
    {
        factory.Clock.SetUtcNow(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)); // week of Jan 7
        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();

        // Bruno completes two lessons, Ana one → Bruno has the higher weekly XP.
        await CompleteLesson(ana);
        await CompleteLesson(bruno);
        await CompleteLesson(bruno);

        var resp = await ClientFor(bruno).GetFromJsonAsync<LeaderboardResponse>("/me/league");

        Assert.NotNull(resp);
        Assert.Equal("Bronze", resp!.Tier);
        Assert.Equal(new DateOnly(2030, 1, 7), resp.WeekStart);
        Assert.Equal(bruno, resp.Rows[0].LearnerId); // ranked first
        Assert.Equal(1, resp.MyRank);
        Assert.True(resp.Rows[0].WeeklyXp > resp.Rows[1].WeeklyXp);
    }
}
