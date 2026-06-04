using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record GetLearnerStreak(Guid LearnerId) : IRequest<StreakDto>;

public sealed record StreakDto(
    Guid LearnerId, int CurrentStreak, int LongestStreak, string Status, DateOnly? LastQualifyingDate);

public sealed class GetLearnerStreakHandler(ILearnerStreakRepository repository, TimeProvider clock)
    : IRequestHandler<GetLearnerStreak, StreakDto>
{
    public async Task<StreakDto> HandleAsync(GetLearnerStreak request, CancellationToken ct)
    {
        var streak = await repository.GetAsync(new LearnerId(request.LearnerId), ct);
        if (streak is null)
            return new StreakDto(request.LearnerId, 0, 0, nameof(StreakStatus.None), null);

        var today = streak.TimeZone.LocalDateOf(clock.GetUtcNow());
        var report = streak.Report(today);
        return new StreakDto(request.LearnerId, report.CurrentStreak, report.LongestStreak,
            report.Status.ToString(), streak.LastQualifyingDate);
    }
}
