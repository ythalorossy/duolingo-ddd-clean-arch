using Learning.Domain;
using Xunit;

namespace Learning.Domain.Tests;

public class ValueObjectsTests
{
    [Fact]
    public void Typed_ids_reject_empty_guid()
    {
        Assert.Throws<ArgumentException>(() => new CourseId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new UnitId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new LessonId(Guid.Empty));
    }

    [Fact]
    public void Typed_ids_have_value_equality()
    {
        var g = Guid.NewGuid();
        Assert.Equal(new LessonId(g), new LessonId(g));
        Assert.NotEqual(new LessonId(g), new LessonId(Guid.NewGuid()));
    }

    [Fact]
    public void Title_rejects_empty_or_whitespace()
    {
        Assert.Throws<ArgumentException>(() => new Title(""));
        Assert.Throws<ArgumentException>(() => new Title("   "));
    }

    [Fact]
    public void Title_trims_and_bounds_length()
    {
        Assert.Equal("Greetings", new Title("  Greetings  ").Value);
        Assert.Throws<ArgumentException>(() => new Title(new string('x', Title.MaxLength + 1)));
    }

    [Fact]
    public void Exercise_attempt_learner_ids_reject_empty_guid()
    {
        Assert.Throws<ArgumentException>(() => new ExerciseId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new AttemptId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new LearnerId(Guid.Empty));
    }

    [Fact]
    public void Prompt_trims_and_rejects_empty()
    {
        Assert.Equal("Pick the greeting", new Prompt("  Pick the greeting  ").Value);
        Assert.Throws<ArgumentException>(() => new Prompt("   "));
        Assert.Throws<ArgumentException>(() => new Prompt(new string('x', Prompt.MaxLength + 1)));
    }

    [Fact]
    public void Choices_requires_at_least_two_non_empty_options_and_indexes()
    {
        var choices = new Choices(new[] { "Hola", "Adios" });
        Assert.Equal(2, choices.Count);
        Assert.Equal("Hola", choices[0]);
        Assert.Throws<ArgumentException>(() => new Choices(new[] { "only one" }));
        Assert.Throws<ArgumentException>(() => new Choices(new[] { "ok", "  " }));
    }

    [Fact]
    public void Choices_has_value_equality()
    {
        Assert.Equal(new Choices(new[] { "a", "b" }), new Choices(new[] { "a", "b" }));
        Assert.NotEqual(new Choices(new[] { "a", "b" }), new Choices(new[] { "b", "a" }));
    }

    [Fact]
    public void Score_computes_percentage_and_rejects_invalid()
    {
        Assert.Equal(0.5, new Score(1, 2).Percentage, 5);
        Assert.Throws<ArgumentException>(() => new Score(3, 2));   // correct > total
        Assert.Throws<ArgumentException>(() => new Score(-1, 2));  // negative
        Assert.Throws<ArgumentException>(() => new Score(0, 0));   // total < 1
    }

    [Theory]
    [InlineData(4, 5, 0.8, true)]   // exactly at threshold passes
    [InlineData(3, 5, 0.8, false)]  // below fails
    [InlineData(5, 5, 0.8, true)]   // perfect passes
    public void Score_MeetsThreshold_is_inclusive_at_the_boundary(int correct, int total, double threshold, bool expected)
    {
        Assert.Equal(expected, new Score(correct, total).MeetsThreshold(threshold));
    }
}
