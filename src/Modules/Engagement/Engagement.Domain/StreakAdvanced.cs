using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed record StreakAdvanced(
    Guid LearnerId,
    int CurrentStreak,
    DateOnly Date,
    DateTimeOffset OccurredOn) : IDomainEvent;
