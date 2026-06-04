using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed class AwardXpForLessonCompletedHandler(
    IXpAccountRepository repository,
    LessonCompletionXpPolicy policy) : INotificationHandler<LessonCompleted>
{
    public async Task HandleAsync(LessonCompleted notification, CancellationToken ct)
    {
        var learnerId = new LearnerId(notification.LearnerId);

        var learner = await repository.GetAsync(learnerId, ct);
        if (learner is null)
        {
            learner = XpAccount.Create(learnerId);
            await repository.AddAsync(learner, ct);
        }

        // SourceId = the event id → re-delivery of the same event is idempotent.
        var award = new XpAward(policy.XpForCompletedLesson(), nameof(LessonCompleted), notification.EventId);
        learner.AwardXp(award);

        await repository.SaveChangesAsync(ct);
    }
}
