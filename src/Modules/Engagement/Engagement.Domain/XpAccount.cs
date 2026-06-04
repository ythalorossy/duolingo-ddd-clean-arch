using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class XpAccount : AggregateRoot
{
    private readonly List<AppliedAward> _appliedAwards = new();

    public LearnerId Id { get; private set; } = default!;
    public Xp TotalXp { get; private set; } = Xp.Zero;
    public IReadOnlyCollection<AppliedAward> AppliedAwards => _appliedAwards.AsReadOnly();

    private XpAccount() { } // EF

    public static XpAccount Create(LearnerId id) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        TotalXp = Xp.Zero
    };

    public void AwardXp(XpAward award)
    {
        ArgumentNullException.ThrowIfNull(award);

        if (_appliedAwards.Any(a => a.SourceId == award.SourceId))
            return; // idempotent: this source was already applied

        TotalXp = TotalXp.Add(award);
        _appliedAwards.Add(new AppliedAward(award.SourceId, award.Amount, DateTimeOffset.UtcNow));

        RaiseDomainEvent(new XpAwarded(Id.Value, award.Amount, TotalXp.Value, DateTimeOffset.UtcNow));
    }
}
