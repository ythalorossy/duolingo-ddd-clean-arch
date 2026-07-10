using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class UnitId : ValueObject
{
    public Guid Value { get; }

    public UnitId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("UnitId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
