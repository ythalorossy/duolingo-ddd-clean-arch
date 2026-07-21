using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class ExerciseId : ValueObject
{
    public Guid Value { get; }

    public ExerciseId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ExerciseId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
