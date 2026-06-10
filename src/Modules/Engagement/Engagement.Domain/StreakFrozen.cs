using BuildingBlocks.Domain;

namespace Engagement.Domain;

// Raised when one or more freezes were spent to keep a streak alive across missed days.
// No subscriber yet (pattern only); the natural future "streak saved!" notification hook.
public sealed record StreakFrozen(
    Guid LearnerId,
    int FreezesConsumed,
    DateOnly Date,
    DateTimeOffset OccurredOn) : IDomainEvent;
