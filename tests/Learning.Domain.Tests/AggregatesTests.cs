using Learning.Domain;
using Xunit;

namespace Learning.Domain.Tests;

public class AggregatesTests
{
    [Fact]
    public void Course_create_sets_fields()
    {
        var id = new CourseId(Guid.NewGuid());
        var course = Course.Create(id, new Title("Spanish"), "es");

        Assert.Equal(id, course.Id);
        Assert.Equal("Spanish", course.Title.Value);
        Assert.Equal("es", course.Language);
    }

    [Fact]
    public void Unit_create_sets_fields_including_the_course_id_reference()
    {
        var courseId = new CourseId(Guid.NewGuid());
        var unit = Unit.Create(new UnitId(Guid.NewGuid()), courseId, new Title("Basics"), 1);

        Assert.Equal(courseId, unit.CourseId);
        Assert.Equal(1, unit.Position);
    }

    [Fact]
    public void Lesson_create_sets_fields_including_the_unit_id_reference()
    {
        var unitId = new UnitId(Guid.NewGuid());
        var lesson = Lesson.Create(new LessonId(Guid.NewGuid()), unitId, new Title("Greetings"), 2, isPublished: true);

        Assert.Equal(unitId, lesson.UnitId);
        Assert.Equal(2, lesson.Position);
        Assert.True(lesson.IsPublished);
    }

    [Fact]
    public void EnsureCompletable_passes_for_a_published_lesson()
    {
        var lesson = Lesson.Create(new LessonId(Guid.NewGuid()), new UnitId(Guid.NewGuid()), new Title("Greetings"), 1, isPublished: true);
        lesson.EnsureCompletable(); // does not throw
    }

    [Fact]
    public void EnsureCompletable_throws_for_an_unpublished_lesson()
    {
        var lesson = Lesson.Create(new LessonId(Guid.NewGuid()), new UnitId(Guid.NewGuid()), new Title("Greetings"), 1, isPublished: false);
        Assert.Throws<InvalidOperationException>(() => lesson.EnsureCompletable());
    }

    [Fact]
    public void Exercise_IsCorrect_is_true_only_for_the_correct_index()
    {
        var exercise = Exercise.Create(
            new ExerciseId(Guid.NewGuid()), 1,
            new Prompt("How do you say hello?"),
            new Choices(new[] { "Hola", "Adios", "Gracias" }),
            correctChoiceIndex: 0);

        Assert.True(exercise.IsCorrect(0));
        Assert.False(exercise.IsCorrect(1));
        Assert.False(exercise.IsCorrect(99)); // out-of-range selection is simply wrong, not an error
    }

    [Fact]
    public void Exercise_Create_rejects_a_correct_index_outside_the_choices()
    {
        Assert.Throws<ArgumentException>(() => Exercise.Create(
            new ExerciseId(Guid.NewGuid()), 1,
            new Prompt("Q"), new Choices(new[] { "a", "b" }), correctChoiceIndex: 2));
    }

    [Fact]
    public void Exercise_does_not_expose_the_correct_answer()
    {
        // Guards the server-authoritative rule: no public member reveals the key.
        Assert.DoesNotContain(typeof(Exercise).GetProperties(),
            p => p.Name.Contains("Correct", StringComparison.OrdinalIgnoreCase));
    }

    private static Lesson PublishedLessonWith(params (int correct, string[] options)[] exercises)
    {
        var built = exercises.Select((e, i) => Exercise.Create(
            new ExerciseId(Guid.NewGuid()), i + 1,
            new Prompt($"Q{i + 1}"), new Choices(e.options), e.correct)).ToList();
        return Lesson.Create(new LessonId(Guid.NewGuid()), new UnitId(Guid.NewGuid()),
            new Title("Greetings"), 1, isPublished: true, exercises: built);
    }

    private static SubmittedAnswer Answer(Lesson lesson, int exerciseIndex, int choice) =>
        new(lesson.Exercises.ElementAt(exerciseIndex).Id, choice);

    [Fact]
    public void Grade_all_correct_passes()
    {
        var lesson = PublishedLessonWith((0, new[] { "a", "b" }), (1, new[] { "a", "b" }));
        var result = lesson.Grade(new[] { Answer(lesson, 0, 0), Answer(lesson, 1, 1) });

        Assert.Equal(Outcome.Passed, result.Outcome);
        Assert.Equal(2, result.Score.Correct);
        Assert.Equal(2, result.Score.Total);
        Assert.All(result.Answers, a => Assert.True(a.WasCorrect));
    }

    [Fact]
    public void Grade_below_threshold_fails_and_records_per_exercise_correctness()
    {
        var lesson = PublishedLessonWith((0, new[] { "a", "b" }), (1, new[] { "a", "b" }));
        var result = lesson.Grade(new[] { Answer(lesson, 0, 0), Answer(lesson, 1, 0) }); // 1/2 = 50%

        Assert.Equal(Outcome.Failed, result.Outcome);
        Assert.Equal(1, result.Score.Correct);
        Assert.Contains(result.Answers, a => !a.WasCorrect);
    }

    [Fact]
    public void Grade_exactly_at_threshold_passes()
    {
        var lesson = PublishedLessonWith(
            (0, new[] { "a", "b" }), (0, new[] { "a", "b" }), (0, new[] { "a", "b" }),
            (0, new[] { "a", "b" }), (1, new[] { "a", "b" }));
        // answer the first four correctly, last one wrong -> 4/5 = 80%
        var answers = new[]
        {
            Answer(lesson, 0, 0), Answer(lesson, 1, 0), Answer(lesson, 2, 0),
            Answer(lesson, 3, 0), Answer(lesson, 4, 0)
        };
        Assert.Equal(Outcome.Passed, lesson.Grade(answers).Outcome);
    }

    [Fact]
    public void Grade_rejects_an_answer_set_that_does_not_cover_the_exercises()
    {
        var lesson = PublishedLessonWith((0, new[] { "a", "b" }), (1, new[] { "a", "b" }));

        // missing one answer
        Assert.Throws<ArgumentException>(() => lesson.Grade(new[] { Answer(lesson, 0, 0) }));
        // unknown exercise id
        Assert.Throws<ArgumentException>(() => lesson.Grade(new[]
        {
            Answer(lesson, 0, 0), new SubmittedAnswer(new ExerciseId(Guid.NewGuid()), 0)
        }));
        // choice index outside the exercise's options
        Assert.Throws<ArgumentException>(() => lesson.Grade(new[] { Answer(lesson, 0, 5), Answer(lesson, 1, 0) }));
    }

    [Fact]
    public void Grade_throws_when_the_lesson_has_no_exercises()
    {
        var lesson = PublishedLessonWith();
        Assert.Throws<InvalidOperationException>(() => lesson.Grade(Array.Empty<SubmittedAnswer>()));
    }

    [Fact]
    public void Grade_rejects_an_answer_set_with_a_duplicate_exercise_id()
    {
        var lesson = PublishedLessonWith((0, new[] { "a", "b" }), (1, new[] { "a", "b" }));

        // count matches (2) but both answers reference the same exercise, so ids are not distinct
        Assert.Throws<ArgumentException>(() => lesson.Grade(new[] { Answer(lesson, 0, 0), Answer(lesson, 0, 0) }));
    }

    [Fact]
    public void Attempt_Create_records_the_grading_result()
    {
        var lesson = PublishedLessonWith((0, new[] { "a", "b" }), (1, new[] { "a", "b" }));
        var result = lesson.Grade(new[] { Answer(lesson, 0, 0), Answer(lesson, 1, 0) }); // 1/2 -> Failed

        var learnerId = new LearnerId(Guid.NewGuid());
        var at = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var attempt = Attempt.Create(new AttemptId(Guid.NewGuid()), learnerId, lesson.Id, at, result);

        Assert.Equal(learnerId, attempt.LearnerId);
        Assert.Equal(lesson.Id, attempt.LessonId);
        Assert.Equal(at, attempt.SubmittedAt);
        Assert.Equal(Outcome.Failed, attempt.Outcome);
        Assert.False(attempt.Passed);
        Assert.Equal(2, attempt.Answers.Count);
        Assert.Equal(result.Answers.Select(a => a.WasCorrect), attempt.Answers.Select(a => a.WasCorrect));
        Assert.Equal(result.Answers.Select(a => a.ExerciseId), attempt.Answers.Select(a => a.ExerciseId));
        Assert.Equal(result.Answers.Select(a => a.SelectedChoiceIndex), attempt.Answers.Select(a => a.SelectedChoiceIndex));
    }
}
