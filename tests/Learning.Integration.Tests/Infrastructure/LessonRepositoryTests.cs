using Learning.Domain;
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class LessonRepositoryTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Lesson_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public LessonRepositoryTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task GetByIdAsync_returns_the_seeded_published_lesson()
    {
        await using var ctx = NewContext();
        var lesson = await new LessonRepository(ctx)
            .GetByIdAsync(new LessonId(LearningSeedIds.GreetingsLesson), CancellationToken.None);

        Assert.NotNull(lesson);
        Assert.True(lesson!.IsPublished);
        Assert.Equal("Greetings", lesson.Title.Value);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_an_unknown_lesson()
    {
        await using var ctx = NewContext();
        var lesson = await new LessonRepository(ctx)
            .GetByIdAsync(new LessonId(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(lesson);
    }

    [Fact]
    public async Task GetByIdAsync_loads_the_seeded_exercises()
    {
        await using var ctx = NewContext();
        var lesson = await new LessonRepository(ctx)
            .GetByIdAsync(new LessonId(LearningSeedIds.GreetingsLesson), CancellationToken.None);

        Assert.NotNull(lesson);
        Assert.NotEmpty(lesson!.Exercises);
        Assert.All(lesson.Exercises, e => Assert.True(e.Choices.Count >= 2));
    }
}
