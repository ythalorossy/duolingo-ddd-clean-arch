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
}
