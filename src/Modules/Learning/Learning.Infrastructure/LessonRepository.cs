using Learning.Domain;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure;

public sealed class LessonRepository(LearningDbContext context) : ILessonRepository
{
    // Whole-VO equality translates (== on the converted Id column); never reach into id.Value in the query.
    public Task<Lesson?> GetByIdAsync(LessonId id, CancellationToken ct) =>
        context.Lessons.FirstOrDefaultAsync(l => l.Id == id, ct);
}
