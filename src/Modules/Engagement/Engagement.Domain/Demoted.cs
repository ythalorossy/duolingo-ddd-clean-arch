using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed record Demoted(
    Guid LearnerId, LeagueTier From, LeagueTier To, DateOnly Week, DateTimeOffset OccurredOn) : IDomainEvent;
