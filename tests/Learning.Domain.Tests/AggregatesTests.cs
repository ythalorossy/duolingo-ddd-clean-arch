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
}
