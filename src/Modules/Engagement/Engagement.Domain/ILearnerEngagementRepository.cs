namespace Engagement.Domain;

public interface ILearnerEngagementRepository
{
    Task<LearnerEngagement?> GetAsync(LearnerId id, CancellationToken ct);
    Task AddAsync(LearnerEngagement learner, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
