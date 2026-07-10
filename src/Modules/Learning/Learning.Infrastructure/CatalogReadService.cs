using Learning.Application;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure;

public sealed class CatalogReadService(LearningDbContext context) : ICatalogReadService
{
    public async Task<CatalogDto> GetCatalogAsync(CancellationToken ct)
    {
        // The catalog is small (seeded); materialize all three tables and assemble in memory.
        // In-memory ordering/joins on the value objects avoids any EF value-converter translation limits.
        var courses = await context.Courses.ToListAsync(ct);
        var units = await context.Units.ToListAsync(ct);
        var lessons = await context.Lessons.ToListAsync(ct);

        var courseDtos = courses
            .OrderBy(c => c.Title.Value)
            .Select(c => new CourseDto(
                c.Id.Value,
                c.Title.Value,
                c.Language,
                units.Where(u => u.CourseId == c.Id)
                    .OrderBy(u => u.Position)
                    .Select(u => new UnitDto(
                        u.Id.Value,
                        u.Title.Value,
                        u.Position,
                        lessons.Where(l => l.UnitId == u.Id)
                            .OrderBy(l => l.Position)
                            .Select(l => new LessonDto(l.Id.Value, l.Title.Value, l.Position, l.IsPublished))
                            .ToList()))
                    .ToList()))
            .ToList();

        return new CatalogDto(courseDtos);
    }
}
