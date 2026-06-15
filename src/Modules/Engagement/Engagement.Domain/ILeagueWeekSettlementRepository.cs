namespace Engagement.Domain;

public interface ILeagueWeekSettlementRepository
{
    Task<bool> ExistsAsync(LeagueWeek week, CancellationToken ct);
    Task AddAsync(LeagueWeekSettlement marker, CancellationToken ct);
}
