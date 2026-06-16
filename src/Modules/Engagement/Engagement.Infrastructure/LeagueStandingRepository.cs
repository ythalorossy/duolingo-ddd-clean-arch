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

    public async Task<IReadOnlyList<LeagueWeek>> GetDistinctEndedWeeksAsync(
        LeagueWeek currentWeek, CancellationToken ct)
    {
        // EF can translate Distinct() over the whole value-converted Week column, but NOT a "<"
        // comparison on it (only == / OrderBy by the whole VO). So materialise the small distinct-week
        // set, then filter "ended" and sort by Start in memory.
        var weeks = await context.LeagueStandings.Select(s => s.Week).Distinct().ToListAsync(ct);
        return weeks.Where(w => w.Start < currentWeek.Start).OrderBy(w => w.Start).ToList();
    }
}
