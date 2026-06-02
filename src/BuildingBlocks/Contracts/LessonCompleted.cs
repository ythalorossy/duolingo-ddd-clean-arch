using BuildingBlocks.Mediator;

namespace BuildingBlocks.Contracts;

// Published by Learning when a learner finishes a lesson. EventId doubles as the
// idempotency key (SourceId) for any downstream award.
public sealed record LessonCompleted(
    Guid EventId,
    Guid LearnerId,
    Guid LessonId,
    DateTimeOffset OccurredOn) : INotification;
