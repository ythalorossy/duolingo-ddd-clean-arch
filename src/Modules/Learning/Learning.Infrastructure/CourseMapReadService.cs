using Learning.Application;
using Learning.Domain;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure;

public sealed class CourseMapReadService(LearningDbContext context) : ICourseMapReadService
{
    public async Task<CourseMapDto?> GetCourseMapAsync(Guid courseId, Guid learnerId, CancellationToken ct)
    {
        var courseKey = new CourseId(courseId);

        // Small seeded catalog: materialize and assemble in memory (mirrors CatalogReadService),
        // which also sidesteps value-converter translation limits.
        var courses = await context.Courses.ToListAsync(ct);
        var course = courses.FirstOrDefault(c => c.Id == courseKey);
        if (course is null)
            return null;

        var units = await context.Units.ToListAsync(ct);
        var lessons = await context.Lessons.ToListAsync(ct);

        var learnerKey = new LearnerId(learnerId);
        var passedLessonIds = (await context.Attempts
                .Where(a => a.LearnerId == learnerKey) // whole-VO comparison — EF-translatable
                .ToListAsync(ct))
            .Where(a => a.Passed)
            .Select(a => a.LessonId)
            .ToHashSet();

        var courseUnits = units.Where(u => u.CourseId == courseKey).OrderBy(u => u.Position).ToList();
        var courseUnitIds = courseUnits.Select(u => u.Id).ToHashSet();
        var publishedLessons = lessons
            .Where(l => l.IsPublished && courseUnitIds.Contains(l.UnitId))
            .ToList();

        var status = LessonProgression.Classify(courseUnits, publishedLessons, passedLessonIds);

        var unitDtos = courseUnits
            .Select(u => new UnitMapDto(
                u.Id.Value,
                u.Title.Value,
                u.Position,
                publishedLessons
                    .Where(l => l.UnitId == u.Id)
                    .OrderBy(l => l.Position)
                    .Select(l => new LessonNodeDto(l.Id.Value, l.Title.Value, l.Position, status[l.Id].ToString()))
                    .ToList()))
            .Where(u => u.Lessons.Count > 0) // omit units with no published lessons
            .ToList();

        return new CourseMapDto(course.Id.Value, course.Title.Value, unitDtos);
    }
}
