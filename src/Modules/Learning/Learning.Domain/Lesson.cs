using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Lesson : AggregateRoot
{
    public const double PassThreshold = 0.8;

    private readonly List<Exercise> _exercises = new();

    public LessonId Id { get; private set; } = default!;
    public UnitId UnitId { get; private set; } = default!; // reference by id, not a navigation
    public Title Title { get; private set; } = default!;
    public int Position { get; private set; }
    public bool IsPublished { get; private set; }

    public IReadOnlyCollection<Exercise> Exercises => _exercises.AsReadOnly();

    private Lesson() { } // EF

    public static Lesson Create(
        LessonId id, UnitId unitId, Title title, int position, bool isPublished,
        IEnumerable<Exercise>? exercises = null)
    {
        var lesson = new Lesson
        {
            Id = id ?? throw new ArgumentNullException(nameof(id)),
            UnitId = unitId ?? throw new ArgumentNullException(nameof(unitId)),
            Title = title ?? throw new ArgumentNullException(nameof(title)),
            Position = position,
            IsPublished = isPublished
        };
        if (exercises is not null)
            lesson._exercises.AddRange(exercises);
        return lesson;
    }

    // Tell-don't-ask: the handler tells the lesson it is being completed; the lesson enforces its rule.
    public void EnsureCompletable()
    {
        if (!IsPublished)
            throw new InvalidOperationException($"Lesson '{Id}' is not published and cannot be completed.");
    }

    // Grading lives on the aggregate that owns every input (exercises, keys, threshold).
    // Extract to a GradingService only when a rule spans beyond a single lesson's own data.
    public GradingResult Grade(IReadOnlyList<SubmittedAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        if (_exercises.Count == 0)
            throw new InvalidOperationException($"Lesson '{Id}' has no exercises to grade.");

        if (answers.Count != _exercises.Count ||
            answers.Select(a => a.ExerciseId).Distinct().Count() != answers.Count)
            throw new ArgumentException("Answers must cover each exercise exactly once.", nameof(answers));

        var graded = new List<GradedAnswer>(_exercises.Count);
        foreach (var answer in answers)
        {
            var exercise = _exercises.SingleOrDefault(e => e.Id == answer.ExerciseId)
                ?? throw new ArgumentException($"Answer references unknown exercise '{answer.ExerciseId}'.", nameof(answers));
            if (!exercise.Choices.IsValidIndex(answer.SelectedChoiceIndex))
                throw new ArgumentException($"Selected choice {answer.SelectedChoiceIndex} is out of range.", nameof(answers));

            graded.Add(new GradedAnswer(answer.ExerciseId, answer.SelectedChoiceIndex,
                exercise.IsCorrect(answer.SelectedChoiceIndex)));
        }

        var score = new Score(graded.Count(g => g.WasCorrect), _exercises.Count);
        var outcome = score.MeetsThreshold(PassThreshold) ? Outcome.Passed : Outcome.Failed;
        return new GradingResult(score, outcome, graded);
    }
}
