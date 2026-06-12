using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class LeagueStandingRepository(EngagementDbContext context) : ILeagueStandingRepository
{
    public Task<LeagueStanding?> GetAsync(LearnerId id, CancellationToken ct) =>
        context.LeagueStandings.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(LeagueStanding standing, CancellationToken ct) =>
        await context.LeagueStandings.AddAsync(standing, ct);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(
        LeagueTier tier, LeagueWeek week, CancellationToken ct)
    {
        // ThenBy(s => s.Id) gives ties a stable, deterministic order.
        return await context.LeagueStandings
            .Where(s => s.Tier == tier && s.Week == week)
            .OrderByDescending(s => s.WeeklyXp)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);
    }
}
