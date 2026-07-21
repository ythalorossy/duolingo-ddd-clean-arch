using BuildingBlocks.Domain;

namespace Learning.Domain;

// Owned child of Attempt — the durable record of one graded answer.
public sealed class Answer
{
    public ExerciseId ExerciseId { get; private set; } = default!;
    public int SelectedChoiceIndex { get; private set; }
    public bool WasCorrect { get; private set; }

    private Answer() { } // EF

    internal Answer(ExerciseId exerciseId, int selectedChoiceIndex, bool wasCorrect)
    {
        ExerciseId = exerciseId;
        SelectedChoiceIndex = selectedChoiceIndex;
        WasCorrect = wasCorrect;
    }
}

public sealed class Attempt : AggregateRoot
{
    private readonly List<Answer> _answers = new();

    public AttemptId Id { get; private set; } = default!;
    public LearnerId LearnerId { get; private set; } = default!;
    public LessonId LessonId { get; private set; } = default!; // reference by id
    public DateTimeOffset SubmittedAt { get; private set; }
    public Score Score { get; private set; } = default!;
    public Outcome Outcome { get; private set; }

    public IReadOnlyCollection<Answer> Answers => _answers.AsReadOnly();
    public bool Passed => Outcome == Outcome.Passed;

    private Attempt() { } // EF

    public static Attempt Create(
        AttemptId id, LearnerId learnerId, LessonId lessonId, DateTimeOffset submittedAt, GradingResult result)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(learnerId);
        ArgumentNullException.ThrowIfNull(lessonId);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(result.Answers);

        var attempt = new Attempt
        {
            Id = id,
            LearnerId = learnerId,
            LessonId = lessonId,
            SubmittedAt = submittedAt,
            Score = result.Score,
            Outcome = result.Outcome
        };
        foreach (var g in result.Answers)
            attempt._answers.Add(new Answer(g.ExerciseId, g.SelectedChoiceIndex, g.WasCorrect));
        return attempt;
    }
}
