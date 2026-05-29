using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Xunit;

namespace Engagement.Integration.Tests.Contracts;

public class LessonCompletedTests
{
    [Fact]
    public void LessonCompleted_is_a_notification_with_the_expected_shape()
    {
        var evt = new LessonCompleted(
            EventId: Guid.NewGuid(),
            LearnerId: Guid.NewGuid(),
            LessonId: Guid.NewGuid(),
            OccurredOn: DateTimeOffset.UtcNow);

        Assert.IsAssignableFrom<INotification>(evt);
        Assert.NotEqual(Guid.Empty, evt.EventId);
    }
}
