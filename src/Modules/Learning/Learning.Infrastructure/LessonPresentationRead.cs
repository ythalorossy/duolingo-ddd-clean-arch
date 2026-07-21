using Learning.Application;
using Learning.Domain;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure;

public sealed class LessonPresentationRead(LearningDbContext context) : ILessonPresentationRead
{
    public async Task<LessonPresentationDto?> GetLessonAsync(Guid lessonId, CancellationToken ct)
    {
        var id = new LessonId(lessonId);
        var lesson = await context.Lessons.FirstOrDefaultAsync(l => l.Id == id, ct); // owned exercises auto-load
        if (lesson is null)
            return null;

        var exercises = lesson.Exercises
            .OrderBy(e => e.Position)
            .Select(e => new ExercisePresentationDto(e.Id.Value, e.Position, e.Prompt.Value, e.Choices.Values))
            .ToList();

        return new LessonPresentationDto(lesson.Id.Value, lesson.Title.Value, lesson.IsPublished, exercises);
    }
}
