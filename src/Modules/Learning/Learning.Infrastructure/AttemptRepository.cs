using Learning.Domain;

namespace Learning.Infrastructure;

// Learning's first write path: add the attempt and commit in one unit of work.
public sealed class AttemptRepository(LearningDbContext context) : IAttemptRepository
{
    public async Task AddAsync(Attempt attempt, CancellationToken ct)
    {
        context.Attempts.Add(attempt);
        await context.SaveChangesAsync(ct);
    }
}
