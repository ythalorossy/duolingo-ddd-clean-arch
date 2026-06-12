using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

// Reacts to XpAwarded (a domain event) and credits the learner's weekly league score.
// The week comes from the injected clock at handling time (XpAwarded.OccurredOn is the
// award-processing instant, not a trustworthy earn-time). Mirrors the streak handler:
// the handler supplies the time; the aggregate stays clock-free.
public sealed class RecordLeagueXpOnXpAwarded(ILeagueStandingRepository repository, TimeProvider clock)
    : IDomainEventHandler<XpAwarded>
{
    public async Task HandleAsync(XpAwarded domainEvent, CancellationToken ct)
    {
        var id = new LearnerId(domainEvent.LearnerId);
        var now = clock.GetUtcNow();

        var standing = await repository.GetAsync(id, ct);
        if (standing is null)
        {
            standing = LeagueStanding.Create(id, LeagueWeek.Containing(now));
            await repository.AddAsync(standing, ct);
        }

        standing.RecordXp(domainEvent.Amount, now);
        await repository.SaveChangesAsync(ct);
    }
}
