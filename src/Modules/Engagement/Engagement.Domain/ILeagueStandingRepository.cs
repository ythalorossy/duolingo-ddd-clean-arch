namespace Engagement.Domain;

public interface ILeagueStandingRepository
{
    Task<LeagueStanding?> GetAsync(LearnerId id, LeagueWeek week, CancellationToken ct);
    Task<LeagueStanding?> GetMostRecentAsync(LearnerId id, CancellationToken ct);
    Task AddAsync(LeagueStanding standing, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);

    // The cohort = everyone in a tier for a given week, ranked by weekly XP descending.
    Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(LeagueTier tier, LeagueWeek week, CancellationToken ct);

    // Distinct weeks that have at least one standing and have fully ended (strictly before the
    // current week), oldest first. The Slice-3 settlement driver uses this to find weeks owed a
    // settlement. Returns LeagueWeek values, which are always Mondays by construction.
    Task<IReadOnlyList<LeagueWeek>> GetDistinctEndedWeeksAsync(LeagueWeek currentWeek, CancellationToken ct);
}
