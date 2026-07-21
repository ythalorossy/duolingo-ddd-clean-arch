using Learning.Application;
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class LessonPresentationReadTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Present_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public LessonPresentationReadTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task GetLessonAsync_returns_prompts_and_choices_without_the_key()
    {
        await using var ctx = NewContext();
        var dto = await new LessonPresentationRead(ctx)
            .GetLessonAsync(LearningSeedIds.GreetingsLesson, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.Exercises.Count);
        Assert.All(dto.Exercises, e => Assert.True(e.Choices.Count >= 2));
        // The DTO type has no member that could carry the answer key.
        Assert.DoesNotContain(typeof(ExercisePresentationDto).GetProperties(),
            p => p.Name.Contains("Correct", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetLessonAsync_returns_null_for_an_unknown_lesson()
    {
        await using var ctx = NewContext();
        var dto = await new LessonPresentationRead(ctx).GetLessonAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(dto);
    }
}
