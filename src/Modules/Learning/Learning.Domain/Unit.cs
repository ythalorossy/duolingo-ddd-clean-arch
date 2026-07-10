using BuildingBlocks.Domain;

namespace Learning.Domain;

// NOTE: shares its simple name with BuildingBlocks.Mediator.Unit. Domain never imports Mediator,
// so there is no clash here; Application files that touch both add a using-alias.
public sealed class Unit : AggregateRoot
{
    public UnitId Id { get; private set; } = default!;
    public CourseId CourseId { get; private set; } = default!; // reference by id, not a navigation
    public Title Title { get; private set; } = default!;
    public int Position { get; private set; }

    private Unit() { } // EF

    public static Unit Create(UnitId id, CourseId courseId, Title title, int position) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        CourseId = courseId ?? throw new ArgumentNullException(nameof(courseId)),
        Title = title ?? throw new ArgumentNullException(nameof(title)),
        Position = position
    };
}
