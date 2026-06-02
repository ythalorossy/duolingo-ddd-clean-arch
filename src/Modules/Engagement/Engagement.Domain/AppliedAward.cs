namespace Engagement.Domain;

// Owned by LearnerEngagement; persisted so idempotency survives reloads.
public sealed class AppliedAward
{
    public Guid SourceId { get; private set; }
    public int Amount { get; private set; }
    public DateTimeOffset AppliedAt { get; private set; }

    private AppliedAward() { } // EF

    public AppliedAward(Guid sourceId, int amount, DateTimeOffset appliedAt)
    {
        SourceId = sourceId;
        Amount = amount;
        AppliedAt = appliedAt;
    }
}
