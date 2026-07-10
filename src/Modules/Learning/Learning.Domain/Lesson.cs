using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Lesson : AggregateRoot
{
    public LessonId Id { get; private set; } = default!;
    public UnitId UnitId { get; private set; } = default!; // reference by id, not a navigation
    public Title Title { get; private set; } = default!;
    public int Position { get; private set; }
    public bool IsPublished { get; private set; }

    private Lesson() { } // EF

    public static Lesson Create(LessonId id, UnitId unitId, Title title, int position, bool isPublished) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        UnitId = unitId ?? throw new ArgumentNullException(nameof(unitId)),
        Title = title ?? throw new ArgumentNullException(nameof(title)),
        Position = position,
        IsPublished = isPublished
    };

    // Tell-don't-ask: the handler tells the lesson it is being completed; the lesson enforces its rule.
    public void EnsureCompletable()
    {
        if (!IsPublished)
            throw new InvalidOperationException($"Lesson '{Id}' is not published and cannot be completed.");
    }
}
