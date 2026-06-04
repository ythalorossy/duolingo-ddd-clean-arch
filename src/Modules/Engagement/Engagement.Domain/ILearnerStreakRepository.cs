namespace Engagement.Domain;

public interface ILearnerStreakRepository
{
    Task<LearnerStreak?> GetAsync(LearnerId id, CancellationToken ct);
    Task AddAsync(LearnerStreak streak, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
