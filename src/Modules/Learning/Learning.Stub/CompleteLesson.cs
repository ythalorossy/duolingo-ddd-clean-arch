using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;

namespace Learning.Stub;

public sealed record CompleteLesson(Guid LearnerId, Guid LessonId) : IRequest<Unit>;

public sealed class CompleteLessonHandler(IMediator mediator) : IRequestHandler<CompleteLesson, Unit>
{
    public async Task<Unit> HandleAsync(CompleteLesson request, CancellationToken ct)
    {
        await mediator.PublishAsync(
            new LessonCompleted(
                EventId: Guid.NewGuid(),
                LearnerId: request.LearnerId,
                LessonId: request.LessonId,
                OccurredOn: DateTimeOffset.UtcNow),
            ct);

        return Unit.Value;
    }
}
