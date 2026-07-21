using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Learning.Domain;

namespace Learning.Application;

public sealed record SubmitAttempt(Guid LearnerId, Guid LessonId, IReadOnlyList<SubmittedAnswerInput> Answers)
    : IRequest<AttemptResultDto>;

public sealed record SubmittedAnswerInput(Guid ExerciseId, int SelectedChoiceIndex);
public sealed record AttemptResultDto(
    Guid AttemptId, int ScoreCorrect, int ScoreTotal, string Outcome, IReadOnlyList<PerExerciseResultDto> PerExercise);
public sealed record PerExerciseResultDto(Guid ExerciseId, bool WasCorrect);

public sealed class SubmitAttemptHandler(
    ILessonRepository lessons,
    IAttemptRepository attempts,
    IMediator mediator,
    TimeProvider clock) : IRequestHandler<SubmitAttempt, AttemptResultDto>
{
    public async Task<AttemptResultDto> HandleAsync(SubmitAttempt request, CancellationToken ct)
    {
        var lesson = await lessons.GetByIdAsync(new LessonId(request.LessonId), ct)
            ?? throw new KeyNotFoundException($"Lesson '{request.LessonId}' was not found.");

        lesson.EnsureCompletable(); // unpublished -> InvalidOperationException -> 409

        var submitted = request.Answers
            .Select(a => new SubmittedAnswer(new ExerciseId(a.ExerciseId), a.SelectedChoiceIndex))
            .ToList();

        var result = lesson.Grade(submitted); // malformed set -> ArgumentException -> 400

        var attempt = Attempt.Create(
            new AttemptId(Guid.NewGuid()),
            new LearnerId(request.LearnerId),
            lesson.Id,
            clock.GetUtcNow(),
            result);

        await attempts.AddAsync(attempt, ct); // persist first — the Attempt is the source of truth

        if (attempt.Passed)
        {
            // Integration event, XP-free; the AppliedAward ledger dedups true redelivery.
            await mediator.PublishAsync(
                new LessonCompleted(
                    EventId: Guid.NewGuid(),
                    LearnerId: request.LearnerId,
                    LessonId: request.LessonId,
                    OccurredOn: clock.GetUtcNow()),
                ct);
        }

        return new AttemptResultDto(
            attempt.Id.Value,
            result.Score.Correct,
            result.Score.Total,
            result.Outcome.ToString(),
            result.Answers.Select(a => new PerExerciseResultDto(a.ExerciseId.Value, a.WasCorrect)).ToList());
    }
}
