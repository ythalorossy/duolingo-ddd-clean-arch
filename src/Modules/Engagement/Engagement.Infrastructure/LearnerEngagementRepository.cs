using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class LearnerEngagementRepository(EngagementDbContext context) : ILearnerEngagementRepository
{
    public Task<LearnerEngagement?> GetAsync(LearnerId id, CancellationToken ct) =>
        context.Learners.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task AddAsync(LearnerEngagement learner, CancellationToken ct) =>
        await context.Learners.AddAsync(learner, ct);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
