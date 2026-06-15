namespace Engagement.Domain;

public interface ILeagueStandingRepository
{
    Task<LeagueStanding?> GetAsync(LearnerId id, LeagueWeek week, CancellationToken ct);
    Task<LeagueStanding?> GetMostRecentAsync(LearnerId id, CancellationToken ct);
    Task AddAsync(LeagueStanding standing, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);

    // The cohort = everyone in a tier for a given week, ranked by weekly XP descending.
    Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(LeagueTier tier, LeagueWeek week, CancellationToken ct);
}
