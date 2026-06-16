using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

// Policy: settle every league week that has ended but isn't settled yet, oldest-first. Reuses
// SettleLeagueWeek per week — each send is its own unit of work, which the settlement chain needs
// (week N must be committed before week N+1 is ranked). Idempotent: SettleLeagueWeek no-ops an
// already-settled week via its per-week marker, so this is safe to run on every scheduler tick.
public sealed record SettleDueLeagueWeeks : IRequest<Unit>;

public sealed class SettleDueLeagueWeeksHandler(
    ILeagueStandingRepository standings,
    IMediator mediator,
    TimeProvider clock) : IRequestHandler<SettleDueLeagueWeeks, Unit>
{
    public async Task<Unit> HandleAsync(SettleDueLeagueWeeks request, CancellationToken ct)
    {
        var currentWeek = LeagueWeek.Containing(clock.GetUtcNow());
        var dueWeeks = await standings.GetDistinctEndedWeeksAsync(currentWeek, ct); // ascending
        foreach (var week in dueWeeks)
            await mediator.SendAsync(new SettleLeagueWeek(week.Start), ct);
        return Unit.Value;
    }
}
