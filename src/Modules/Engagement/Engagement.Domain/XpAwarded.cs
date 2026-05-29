using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed record XpAwarded(
    Guid LearnerId,
    int Amount,
    int NewTotal,
    DateTimeOffset OccurredOn) : IDomainEvent;
