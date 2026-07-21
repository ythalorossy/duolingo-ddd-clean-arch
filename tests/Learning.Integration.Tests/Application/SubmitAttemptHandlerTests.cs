using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Learning.Application;
using Learning.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Learning.Integration.Tests.Application;

public class SubmitAttemptHandlerTests
{
    private sealed class StubLessonRepository(Lesson? lesson) : ILessonRepository
    {
        public Task<Lesson?> GetByIdAsync(LessonId id, CancellationToken ct) => Task.FromResult(lesson);
    }

    private sealed class RecordingAttemptRepository : IAttemptRepository
    {
        public Attempt? Saved;
        public Task AddAsync(Attempt attempt, CancellationToken ct) { Saved = attempt; return Task.CompletedTask; }
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

    private static Lesson PublishedLesson(bool published = true)
    {
        var e1 = Exercise.Create(new ExerciseId(Guid.NewGuid()), 1, new Prompt("Q1"), new Choices(new[] { "a", "b" }), 0);
        var e2 = Exercise.Create(new ExerciseId(Guid.NewGuid()), 2, new Prompt("Q2"), new Choices(new[] { "a", "b" }), 1);
        return Lesson.Create(new LessonId(Guid.NewGuid()), new UnitId(Guid.NewGuid()),
            new Title("Greetings"), 1, published, exercises: new[] { e1, e2 });
    }

    private static SubmitAttempt SubmissionFor(Lesson lesson, Guid learnerId, params int[] picks) =>
        new(learnerId, lesson.Id.Value,
            lesson.Exercises.Select((e, i) => new SubmittedAnswerInput(e.Id.Value, picks[i])).ToList());

    private static (SubmitAttemptHandler handler, RecordingAttemptRepository attempts, CapturingMediator mediator) Build(Lesson? lesson)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var attempts = new RecordingAttemptRepository();
        var mediator = new CapturingMediator();
        return (new SubmitAttemptHandler(new StubLessonRepository(lesson), attempts, mediator, clock), attempts, mediator);
    }

    [Fact]
    public async Task Passing_attempt_persists_and_publishes_LessonCompleted_from_the_clock()
    {
        var learnerId = Guid.NewGuid();
        var lesson = PublishedLesson();
        var (handler, attempts, mediator) = Build(lesson);

        var dto = await handler.HandleAsync(SubmissionFor(lesson, learnerId, 0, 1), CancellationToken.None);

        Assert.Equal("Passed", dto.Outcome);
        Assert.NotNull(attempts.Saved);
        var evt = Assert.IsType<LessonCompleted>(Assert.Single(mediator.Published));
        Assert.Equal(learnerId, evt.LearnerId);
        Assert.Equal(lesson.Id.Value, evt.LessonId);
        Assert.Equal(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero), evt.OccurredOn);
    }

    [Fact]
    public async Task Failing_attempt_persists_but_publishes_nothing()
    {
        var lesson = PublishedLesson();
        var (handler, attempts, mediator) = Build(lesson);

        var dto = await handler.HandleAsync(SubmissionFor(lesson, Guid.NewGuid(), 0, 0), CancellationToken.None); // 1/2

        Assert.Equal("Failed", dto.Outcome);
        Assert.NotNull(attempts.Saved);
        Assert.Empty(mediator.Published);
    }

    [Fact]
    public async Task Unknown_lesson_throws_KeyNotFound_and_writes_nothing()
    {
        var (handler, attempts, mediator) = Build(lesson: null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(
            new SubmitAttempt(Guid.NewGuid(), Guid.NewGuid(), new List<SubmittedAnswerInput>()), CancellationToken.None));
        Assert.Null(attempts.Saved);
        Assert.Empty(mediator.Published);
    }

    [Fact]
    public async Task Unpublished_lesson_throws_InvalidOperation_and_writes_nothing()
    {
        var lesson = PublishedLesson(published: false);
        var (handler, attempts, mediator) = Build(lesson);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            SubmissionFor(lesson, Guid.NewGuid(), 0, 1), CancellationToken.None));
        Assert.Null(attempts.Saved);
        Assert.Empty(mediator.Published);
    }

    [Fact]
    public async Task Malformed_answer_set_throws_ArgumentException_and_writes_nothing()
    {
        var lesson = PublishedLesson();
        var (handler, attempts, mediator) = Build(lesson);

        // only one answer for a two-exercise lesson
        var bad = new SubmitAttempt(Guid.NewGuid(), lesson.Id.Value,
            new[] { new SubmittedAnswerInput(lesson.Exercises.First().Id.Value, 0) });

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(bad, CancellationToken.None));
        Assert.Null(attempts.Saved);
        Assert.Empty(mediator.Published);
    }
}
