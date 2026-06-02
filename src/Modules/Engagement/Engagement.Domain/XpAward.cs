using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class XpAward : ValueObject
{
    public int Amount { get; }
    public string Reason { get; }
    public Guid SourceId { get; }

    public XpAward(int amount, string reason, Guid sourceId)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "XP award must be positive.");
        if (sourceId == Guid.Empty)
            throw new ArgumentException("SourceId cannot be empty.", nameof(sourceId));

        Amount = amount;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        SourceId = sourceId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Reason;
        yield return SourceId;
    }
}
