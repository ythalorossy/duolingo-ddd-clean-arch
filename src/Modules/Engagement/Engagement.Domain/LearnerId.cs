using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class LearnerId : ValueObject
{
    public Guid Value { get; }

    public LearnerId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("LearnerId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
