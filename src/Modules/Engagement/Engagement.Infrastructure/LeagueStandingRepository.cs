using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class LeagueStandingRepository(EngagementDbContext context) : ILeagueStandingRepository
{
    public Task<LeagueStanding?> GetAsync(LearnerId id, LeagueWeek week, CancellationToken ct) =>
        context.LeagueStandings.FirstOrDefaultAsync(s => s.Id == id && s.Week == week, ct);

    public Task<LeagueStanding?> GetMostRecentAsync(LearnerId id, CancellationToken ct) =>
        context.LeagueStandings
            .Where(s => s.Id == id)
            .OrderByDescending(s => s.Week)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(LeagueStanding standing, CancellationToken ct) =>
        await context.LeagueStandings.AddAsync(standing, ct);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(
        LeagueTier tier, LeagueWeek week, CancellationToken ct)
    {
        return await context.LeagueStandings
            .Where(s => s.Tier == tier && s.Week == week)
            .OrderByDescending(s => s.WeeklyXp)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);
    }
}
