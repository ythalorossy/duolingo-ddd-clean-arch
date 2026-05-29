using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record GetLearnerEngagement(Guid LearnerId) : IRequest<LearnerEngagementDto>;

public sealed record LearnerEngagementDto(Guid LearnerId, int TotalXp);

public sealed class GetLearnerEngagementHandler(ILearnerEngagementRepository repository)
    : IRequestHandler<GetLearnerEngagement, LearnerEngagementDto>
{
    public async Task<LearnerEngagementDto> HandleAsync(GetLearnerEngagement request, CancellationToken ct)
    {
        var learner = await repository.GetAsync(new LearnerId(request.LearnerId), ct);
        return new LearnerEngagementDto(request.LearnerId, learner?.TotalXp.Value ?? 0);
    }
}
