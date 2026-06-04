namespace Engagement.Domain;

public interface IXpAccountRepository
{
    Task<XpAccount?> GetAsync(LearnerId id, CancellationToken ct);
    Task AddAsync(XpAccount learner, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
