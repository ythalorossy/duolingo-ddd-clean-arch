using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class Xp : ValueObject
{
    public int Value { get; }

    public static Xp Zero => new(0);

    public Xp(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "XP can never be negative.");
        Value = value;
    }

    public Xp Add(XpAward award)
    {
        ArgumentNullException.ThrowIfNull(award);
        return new Xp(checked(Value + award.Amount)); // checked → overflow throws rather than wraps
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
