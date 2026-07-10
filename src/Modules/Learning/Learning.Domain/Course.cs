using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Course : AggregateRoot
{
    public CourseId Id { get; private set; } = default!;
    public Title Title { get; private set; } = default!;
    public string? Language { get; private set; }

    private Course() { } // EF

    public static Course Create(CourseId id, Title title, string? language = null) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        Title = title ?? throw new ArgumentNullException(nameof(title)),
        Language = language
    };
}
