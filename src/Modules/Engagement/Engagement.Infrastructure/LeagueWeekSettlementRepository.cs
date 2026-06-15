using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class LeagueWeekSettlementRepository(EngagementDbContext context) : ILeagueWeekSettlementRepository
{
    public Task<bool> ExistsAsync(LeagueWeek week, CancellationToken ct) =>
        context.LeagueWeekSettlements.AnyAsync(s => s.Week == week, ct);

    public async Task AddAsync(LeagueWeekSettlement marker, CancellationToken ct) =>
        await context.LeagueWeekSettlements.AddAsync(marker, ct);
}
