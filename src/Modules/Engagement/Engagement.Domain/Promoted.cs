using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed record Promoted(
    Guid LearnerId, LeagueTier From, LeagueTier To, DateOnly Week, DateTimeOffset OccurredOn) : IDomainEvent;
