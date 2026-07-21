using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class AttemptId : ValueObject
{
    public Guid Value { get; }

    public AttemptId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("AttemptId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
