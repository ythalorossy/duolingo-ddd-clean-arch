using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record SetLearnerTimeZone(Guid LearnerId, string IanaId) : IRequest<Unit>;

public sealed class SetLearnerTimeZoneHandler(ILearnerStreakRepository repository)
    : IRequestHandler<SetLearnerTimeZone, Unit>
{
    public async Task<Unit> HandleAsync(SetLearnerTimeZone request, CancellationToken ct)
    {
        var id = new LearnerId(request.LearnerId);

        var streak = await repository.GetAsync(id, ct);
        if (streak is null)
        {
            streak = LearnerStreak.Create(id);
            await repository.AddAsync(streak, ct);
        }

        streak.ChangeTimeZone(new LearnerTimeZone(request.IanaId)); // throws ArgumentException on bad id
        await repository.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
