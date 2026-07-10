using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Learning.Application;
using Learning.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Learning.Integration.Tests.Application;

public class CompleteLessonHandlerTests
{
    private sealed class StubLessonRepository(Lesson? lesson) : ILessonRepository
    {
        public Task<Lesson?> GetByIdAsync(LessonId id, CancellationToken ct) => Task.FromResult(lesson);
    }

    private sealed class CapturingMediator : IMediator
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

    private static Lesson MakeLesson(Guid id, bool published) =>
        Lesson.Create(new LessonId(id), new UnitId(Guid.NewGuid()), new Title("Greetings"), 1, published);

    [Fact]
    public async Task Completing_a_published_lesson_publishes_LessonCompleted_from_the_clock()
    {
        var lessonId = Guid.NewGuid();
        var learnerId = Guid.NewGuid();
        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var mediator = new CapturingMediator();
        var handler = new CompleteLessonHandler(new StubLessonRepository(MakeLesson(lessonId, published: true)), mediator, clock);

        await handler.HandleAsync(new CompleteLesson(learnerId, lessonId), CancellationToken.None);

        var evt = Assert.IsType<LessonCompleted>(Assert.Single(mediator.Published));
        Assert.Equal(learnerId, evt.LearnerId);
        Assert.Equal(lessonId, evt.LessonId);
        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.Equal(clock.GetUtcNow(), evt.OccurredOn);
    }

    [Fact]
    public async Task Completing_an_unpublished_lesson_throws_and_publishes_nothing()
    {
        var lessonId = Guid.NewGuid();
        var mediator = new CapturingMediator();
        var handler = new CompleteLessonHandler(new StubLessonRepository(MakeLesson(lessonId, published: false)), mediator, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new CompleteLesson(Guid.NewGuid(), lessonId), CancellationToken.None));
        Assert.Empty(mediator.Published);
    }

    [Fact]
    public async Task Completing_an_unknown_lesson_throws_KeyNotFound_and_publishes_nothing()
    {
        var mediator = new CapturingMediator();
        var handler = new CompleteLessonHandler(new StubLessonRepository(null), mediator, TimeProvider.System);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.HandleAsync(new CompleteLesson(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
        Assert.Empty(mediator.Published);
    }
}
