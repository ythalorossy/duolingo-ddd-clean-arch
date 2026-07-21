using BuildingBlocks.Domain;

namespace Learning.Domain;

// A child ENTITY of the Lesson aggregate — reached only through its Lesson, never by a global id.
// The correct answer lives here, behind IsCorrect, and is never exposed as a public member.
public sealed class Exercise
{
    public ExerciseId Id { get; private set; } = default!;
    public int Position { get; private set; }
    public Prompt Prompt { get; private set; } = default!;
    public Choices Choices { get; private set; } = default!;

    private int _correctChoiceIndex;

    private Exercise() { } // EF

    public static Exercise Create(ExerciseId id, int position, Prompt prompt, Choices choices, int correctChoiceIndex)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(choices);
        if (!choices.IsValidIndex(correctChoiceIndex))
            throw new ArgumentException("Correct choice index must point at one of the choices.", nameof(correctChoiceIndex));

        return new Exercise
        {
            Id = id,
            Position = position,
            Prompt = prompt,
            Choices = choices,
            _correctChoiceIndex = correctChoiceIndex
        };
    }

    // Tell-don't-ask: the grader asks "is this choice right?" — the key never leaves the exercise.
    public bool IsCorrect(int selectedChoiceIndex) => selectedChoiceIndex == _correctChoiceIndex;
}
