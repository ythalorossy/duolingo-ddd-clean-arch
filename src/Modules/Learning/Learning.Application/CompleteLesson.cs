using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Learning.Domain;
using Unit = BuildingBlocks.Mediator.Unit; // disambiguate from Learning.Domain.Unit (the aggregate)

namespace Learning.Application;

public sealed record CompleteLesson(Guid LearnerId, Guid LessonId) : IRequest<Unit>;

public sealed class CompleteLessonHandler(
    ILessonRepository lessons,
    IMediator mediator,
    TimeProvider clock) : IRequestHandler<CompleteLesson, Unit>
{
    public async Task<Unit> HandleAsync(CompleteLesson request, CancellationToken ct)
    {
        var lesson = await lessons.GetByIdAsync(new LessonId(request.LessonId), ct)
            ?? throw new KeyNotFoundException($"Lesson '{request.LessonId}' was not found.");

        lesson.EnsureCompletable();

        // Fresh EventId per completion → repeatable; the AppliedAward ledger still dedups true redelivery.
        // Contract unchanged / XP-free; OccurredOn from the injected clock (never DateTimeOffset.UtcNow).
        await mediator.PublishAsync(
            new LessonCompleted(
                EventId: Guid.NewGuid(),
                LearnerId: request.LearnerId,
                LessonId: request.LessonId,
                OccurredOn: clock.GetUtcNow()),
            ct);

        return Unit.Value;
    }
}
