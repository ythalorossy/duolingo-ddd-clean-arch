using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed class RegisterStreakForLessonCompletedHandler(ILearnerStreakRepository repository)
    : INotificationHandler<LessonCompleted>
{
    public async Task HandleAsync(LessonCompleted notification, CancellationToken ct)
    {
        var id = new LearnerId(notification.LearnerId);

        var streak = await repository.GetAsync(id, ct);
        if (streak is null)
        {
            streak = LearnerStreak.Create(id);
            await repository.AddAsync(streak, ct);
        }

        streak.RegisterQualifyingActivity(notification.OccurredOn);
        await repository.SaveChangesAsync(ct);
    }
}
