using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class LessonId : ValueObject
{
    public Guid Value { get; }

    public LessonId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("LessonId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
