using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record SettleLeagueWeek(DateOnly WeekStart) : IRequest<Unit>;

public sealed class SettleLeagueWeekHandler(
    ILeagueStandingRepository standings,
    ILeagueWeekSettlementRepository settlements,
    TimeProvider clock) : IRequestHandler<SettleLeagueWeek, Unit>
{
    // Each tier's cohort is settled independently, so processing order does not affect the
    // result; Enum.GetValues returns ascending declaration order (Bronze…Diamond), which is fine.
    private static readonly LeagueTier[] AllTiers = Enum.GetValues<LeagueTier>();

    // The settlement rule: top/bottom this fraction of each cohort move a tier.
    private const double PromotionDemotionFraction = 0.2;

    public async Task<Unit> HandleAsync(SettleLeagueWeek request, CancellationToken ct)
    {
        var week = new LeagueWeek(request.WeekStart); // throws ArgumentException if not a Monday
        if (await settlements.ExistsAsync(week, ct))
            return Unit.Value; // idempotent

        var nextWeek = week.Next();

        foreach (var tier in AllTiers)
        {
            var cohort = await standings.GetCohortAsync(tier, week, ct); // ranked desc, id tiebreak
            var k = (int)Math.Floor(PromotionDemotionFraction * cohort.Count);
            if (k == 0)
                continue;

            for (var rank = 0; rank < cohort.Count; rank++)
            {
                LeagueTier newTier;
                if (rank < k) newTier = tier.Next();
                else if (rank >= cohort.Count - k) newTier = tier.Previous();
                else continue; // middle stays

                if (newTier == tier)
                    continue; // edge (Bronze bottom / Diamond top) — no move

                var learnerId = cohort[rank].Id;
                var placement = await standings.GetAsync(learnerId, nextWeek, ct);
                if (placement is null)
                {
                    placement = LeagueStanding.Create(learnerId, nextWeek, tier); // start at old tier…
                    await standings.AddAsync(placement, ct);
                }
                placement.PlaceInto(newTier); // …then move (raises Promoted/Demoted)
            }
        }

        await settlements.AddAsync(new LeagueWeekSettlement(week, clock.GetUtcNow()), ct);
        await standings.SaveChangesAsync(ct); // one unit of work persists placements + marker
        return Unit.Value;
    }
}
