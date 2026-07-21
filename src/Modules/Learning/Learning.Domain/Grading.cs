namespace Learning.Domain;

public enum Outcome { Failed, Passed }

public sealed record SubmittedAnswer(ExerciseId ExerciseId, int SelectedChoiceIndex);
public sealed record GradedAnswer(ExerciseId ExerciseId, int SelectedChoiceIndex, bool WasCorrect);
public sealed record GradingResult(Score Score, Outcome Outcome, IReadOnlyList<GradedAnswer> Answers);
