using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

// Reacts to XpAwarded and credits the learner's CURRENT-week row. The week comes from the
// injected clock; on a brand-new week's first activity the row is created carrying the learner's
// most-recent tier forward (Bronze if they've never played).
public sealed class RecordLeagueXpOnXpAwarded(ILeagueStandingRepository repository, TimeProvider clock)
    : IDomainEventHandler<XpAwarded>
{
    public async Task HandleAsync(XpAwarded domainEvent, CancellationToken ct)
    {
        var id = new LearnerId(domainEvent.LearnerId);
        var week = LeagueWeek.Containing(clock.GetUtcNow());

        var standing = await repository.GetAsync(id, week, ct);
        if (standing is null)
        {
            var prior = await repository.GetMostRecentAsync(id, ct);
            var tier = prior?.Tier ?? LeagueTier.Bronze;
            standing = LeagueStanding.Create(id, week, tier);
            await repository.AddAsync(standing, ct);
        }

        standing.RecordXp(domainEvent.Amount);
        await repository.SaveChangesAsync(ct);
    }
}
