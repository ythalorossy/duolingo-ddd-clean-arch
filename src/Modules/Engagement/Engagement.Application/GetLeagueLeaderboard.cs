using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record GetLeagueLeaderboard(Guid LearnerId) : IRequest<LeaderboardDto>;

public sealed record LeaderboardDto(
    string Tier, DateOnly WeekStart, IReadOnlyList<LeaderboardRow> Rows, int? MyRank);

public sealed record LeaderboardRow(int Rank, Guid LearnerId, int WeeklyXp);

public sealed class GetLeagueLeaderboardHandler(ILeagueStandingRepository repository, TimeProvider clock)
    : IRequestHandler<GetLeagueLeaderboard, LeaderboardDto>
{
    public async Task<LeaderboardDto> HandleAsync(GetLeagueLeaderboard request, CancellationToken ct)
    {
        var id = new LearnerId(request.LearnerId);
        var currentWeek = LeagueWeek.Containing(clock.GetUtcNow());

        // Tier for the board: this week's row if present, else carry-forward from the latest row, else Bronze.
        var thisWeek = await repository.GetAsync(id, currentWeek, ct);
        var tier = thisWeek?.Tier
                   ?? (await repository.GetMostRecentAsync(id, ct))?.Tier
                   ?? LeagueTier.Bronze;

        var cohort = await repository.GetCohortAsync(tier, currentWeek, ct);

        var rows = cohort
            .Select((s, index) => new LeaderboardRow(index + 1, s.Id.Value, s.WeeklyXp.Value))
            .ToList();

        var myRank = rows.FirstOrDefault(r => r.LearnerId == request.LearnerId)?.Rank;

        return new LeaderboardDto(tier.ToString(), currentWeek.Start, rows, myRank);
    }
}
