namespace Engagement.Domain;

// Core rule: how much a completed lesson is worth. Engagement owns this — Learning does not.
// Flat for the skeleton; will grow (combos, boosts, weekend XP) without changing callers.
public sealed class LessonCompletionXpPolicy
{
    public const int FlatLessonXp = 10;
    public int XpForCompletedLesson() => FlatLessonXp;
}
