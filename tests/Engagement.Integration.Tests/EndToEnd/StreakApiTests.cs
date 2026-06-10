using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.EndToEnd;

// NOTE: the orderer is referenced by string (type + assembly name). If LineNumberOrderer
// is renamed/moved, ordering silently breaks at runtime with no compile error — keep in sync.
[TestCaseOrderer("Engagement.Integration.Tests.EndToEnd.LineNumberOrderer",
                 "Engagement.Integration.Tests")]
public class StreakApiTests(StreakApiFactory factory) : IClassFixture<StreakApiFactory>
{
    private sealed record StreakResponse(Guid LearnerId, int CurrentStreak, int LongestStreak, string Status, DateOnly? LastQualifyingDate, int FreezesAvailable);

    private HttpClient ClientFor(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    private async Task CompleteLessonOn(Guid learnerId, DateTimeOffset whenUtc)
    {
        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.PublishAsync(new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), whenUtc));
    }

    private static DateTimeOffset Noon(int y, int m, int d) => new(y, m, d, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Invalid_time_zone_returns_400()
    {
        var resp = await ClientFor(Guid.NewGuid())
            .PutAsJsonAsync("/me/timezone", new { ianaId = "Nowhere/Void" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Builds_streak_across_consecutive_days_then_resets_after_a_gap()
    {
        var learner = Guid.NewGuid();
        var client = ClientFor(learner);

        // Default time zone is UTC, so local date == UTC date.
        factory.Clock.SetUtcNow(Noon(2030, 1, 1).AddHours(1));
        await CompleteLessonOn(learner, Noon(2030, 1, 1));
        var d1 = await client.GetFromJsonAsync<StreakResponse>("/me/streak");
        Assert.Equal(1, d1!.CurrentStreak);
        Assert.Equal("Active", d1.Status);

        factory.Clock.SetUtcNow(Noon(2030, 1, 2).AddHours(1));
        await CompleteLessonOn(learner, Noon(2030, 1, 2));
        var d2 = await client.GetFromJsonAsync<StreakResponse>("/me/streak");
        Assert.Equal(2, d2!.CurrentStreak);

        // Skip Jan 3. On Jan 3 the streak is at risk (last activity was yesterday).
        factory.Clock.SetUtcNow(Noon(2030, 1, 3));
        var atRisk = await client.GetFromJsonAsync<StreakResponse>("/me/streak");
        Assert.Equal("AtRisk", atRisk!.Status);
        Assert.Equal(2, atRisk.CurrentStreak);

        // Jan 4 with no Jan 3 activity → broken; completing restarts at 1, longest stays 2.
        factory.Clock.SetUtcNow(Noon(2030, 1, 4).AddHours(1));
        await CompleteLessonOn(learner, Noon(2030, 1, 4));
        var d4 = await client.GetFromJsonAsync<StreakResponse>("/me/streak");
        Assert.Equal(1, d4!.CurrentStreak);
        Assert.Equal(2, d4.LongestStreak);
        Assert.Equal("Active", d4.Status);
    }

    [Fact]
    public async Task Honors_a_set_time_zone_for_the_day_boundary()
    {
        var learner = Guid.NewGuid();
        var client = ClientFor(learner);

        var ok = await client.PutAsJsonAsync("/me/timezone", new { ianaId = "America/New_York" });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        // Tue 04:30 UTC = Mon 23:30 New York → local date Jan 7.
        await CompleteLessonOn(learner, new DateTimeOffset(2030, 1, 8, 4, 30, 0, TimeSpan.Zero));
        factory.Clock.SetUtcNow(new DateTimeOffset(2030, 1, 8, 4, 35, 0, TimeSpan.Zero)); // still Mon Jan 7 in NY

        var dto = await client.GetFromJsonAsync<StreakResponse>("/me/streak");
        Assert.Equal(1, dto!.CurrentStreak);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(new DateOnly(2030, 1, 7), dto.LastQualifyingDate);
    }

    [Fact]
    public async Task Streak_freeze_preserves_streak_across_a_missed_day()
    {
        var learner = Guid.NewGuid();
        var client = ClientFor(learner);

        var grant = await client.PostAsync("/me/streak-freezes", null);
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);

        factory.Clock.SetUtcNow(Noon(2030, 2, 1).AddHours(1));
        await CompleteLessonOn(learner, Noon(2030, 2, 1));
        factory.Clock.SetUtcNow(Noon(2030, 2, 2).AddHours(1));
        await CompleteLessonOn(learner, Noon(2030, 2, 2));

        // Skip Feb 3. Feb 4 completion → the freeze bridges Feb 3, streak survives to 3.
        factory.Clock.SetUtcNow(Noon(2030, 2, 4).AddHours(1));
        await CompleteLessonOn(learner, Noon(2030, 2, 4));

        var dto = await client.GetFromJsonAsync<StreakResponse>("/me/streak");
        Assert.Equal(3, dto!.CurrentStreak);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(0, dto.FreezesAvailable); // the freeze was consumed bridging Feb 3
    }

    [Fact]
    public async Task Granting_beyond_the_cap_is_clamped()
    {
        var learner = Guid.NewGuid();
        var client = ClientFor(learner);

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/me/streak-freezes", null)).StatusCode);

        // No qualifying activity yet → status None, but the capped balance is visible.
        factory.Clock.SetUtcNow(Noon(2030, 3, 1).AddHours(1));
        var dto = await client.GetFromJsonAsync<StreakResponse>("/me/streak");
        Assert.Equal("None", dto!.Status);
        Assert.Equal(2, dto.FreezesAvailable);
    }
}
