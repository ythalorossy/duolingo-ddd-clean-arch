using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.EndToEnd;

public class EngagementApiTests(EngagementApiFactory factory) : IClassFixture<EngagementApiFactory>
{
    private sealed record EngagementResponse(Guid LearnerId, int TotalXp);

    private HttpClient ClientForLearner(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    [Fact] // Criterion 3: idempotency on re-delivery of the SAME event
    public async Task Same_lesson_completed_event_delivered_twice_awards_once()
    {
        var learnerId = Guid.NewGuid();
        var evt = new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), DateTimeOffset.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.PublishAsync(evt);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.PublishAsync(evt); // same EventId -> same SourceId
        }

        var dto = await ClientForLearner(learnerId).GetFromJsonAsync<EngagementResponse>("/me/xp");
        Assert.Equal(10, dto!.TotalXp);
    }
}
