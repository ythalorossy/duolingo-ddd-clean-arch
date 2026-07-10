using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class CourseId : ValueObject
{
    public Guid Value { get; }

    public CourseId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("CourseId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
