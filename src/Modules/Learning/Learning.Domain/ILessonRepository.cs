namespace Learning.Domain;

// Read port owned by the Domain; implemented in Infrastructure. Slice 1's completion path only reads.
public interface ILessonRepository
{
    Task<Lesson?> GetByIdAsync(LessonId id, CancellationToken ct);
}
