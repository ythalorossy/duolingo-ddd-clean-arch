using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record GrantStreakFreeze(Guid LearnerId) : IRequest<Unit>;

public sealed class GrantStreakFreezeHandler(ILearnerStreakRepository repository)
    : IRequestHandler<GrantStreakFreeze, Unit>
{
    public async Task<Unit> HandleAsync(GrantStreakFreeze request, CancellationToken ct)
    {
        var id = new LearnerId(request.LearnerId);

        var streak = await repository.GetAsync(id, ct);
        if (streak is null)
        {
            streak = LearnerStreak.Create(id);
            await repository.AddAsync(streak, ct);
        }

        streak.GrantFreeze();
        await repository.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
