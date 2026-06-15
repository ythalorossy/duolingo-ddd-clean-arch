namespace Engagement.Domain;

// No SaveChangesAsync here by design: settlement persists this marker in the SAME unit of work as
// the next-week placement rows, committing once via ILeagueStandingRepository.SaveChangesAsync
// (both repositories share the scoped EngagementDbContext).
public interface ILeagueWeekSettlementRepository
{
    Task<bool> ExistsAsync(LeagueWeek week, CancellationToken ct);
    Task AddAsync(LeagueWeekSettlement marker, CancellationToken ct);
}
