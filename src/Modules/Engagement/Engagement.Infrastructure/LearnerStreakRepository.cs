using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class LearnerStreakRepository(EngagementDbContext context) : ILearnerStreakRepository
{
    public Task<LearnerStreak?> GetAsync(LearnerId id, CancellationToken ct) =>
        context.LearnerStreaks.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(LearnerStreak streak, CancellationToken ct) =>
        await context.LearnerStreaks.AddAsync(streak, ct);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
