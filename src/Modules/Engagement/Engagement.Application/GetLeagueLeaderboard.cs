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

        var mine = await repository.GetAsync(id, ct);
        var tier = mine?.Tier ?? LeagueTier.Bronze; // unknown learner defaults to Bronze

        var cohort = await repository.GetCohortAsync(tier, currentWeek, ct);

        var rows = cohort
            .Select((s, index) => new LeaderboardRow(index + 1, s.Id.Value, s.WeeklyXp.Value))
            .ToList();

        var myRank = rows.FirstOrDefault(r => r.LearnerId == request.LearnerId)?.Rank;

        return new LeaderboardDto(tier.ToString(), currentWeek.Start, rows, myRank);
    }
}
