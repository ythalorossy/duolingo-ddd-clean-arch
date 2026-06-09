namespace Engagement.Domain;

// Derived view of the streak relative to a given local "today".
public sealed record StreakReport(StreakStatus Status, int CurrentStreak, int LongestStreak, int FreezesAvailable);
