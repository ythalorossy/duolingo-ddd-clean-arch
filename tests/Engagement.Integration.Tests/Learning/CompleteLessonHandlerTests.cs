using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Learning.Stub;
using Xunit;

namespace Engagement.Integration.Tests.Learning;

public class CompleteLessonHandlerTests
{
    private sealed class CapturingPublisher : IMediator
    {
        public readonly List<INotification> Published = new();
        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task PublishAsync(INotification notification, CancellationToken ct = default)
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Completing_a_lesson_publishes_LessonCompleted()
    {
        var publisher = new CapturingPublisher();
        var handler = new CompleteLessonHandler(publisher);
        var learnerId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();

        await handler.HandleAsync(new CompleteLesson(learnerId, lessonId), CancellationToken.None);

        var evt = Assert.IsType<LessonCompleted>(Assert.Single(publisher.Published));
        Assert.Equal(learnerId, evt.LearnerId);
        Assert.Equal(lessonId, evt.LessonId);
        Assert.NotEqual(Guid.Empty, evt.EventId);
    }
}
