using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class XpAccountRepository(EngagementDbContext context) : IXpAccountRepository
{
    public Task<XpAccount?> GetAsync(LearnerId id, CancellationToken ct) =>
        context.XpAccounts.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task AddAsync(XpAccount learner, CancellationToken ct) =>
        await context.XpAccounts.AddAsync(learner, ct);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
