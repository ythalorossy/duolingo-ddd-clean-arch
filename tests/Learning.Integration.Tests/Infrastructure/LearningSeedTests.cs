using Learning.Domain;
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class LearningSeedTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Seed_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public LearningSeedTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task Migration_seeds_one_course_two_units_and_four_lessons_with_one_unpublished()
    {
        await using var ctx = NewContext();
        Assert.Equal(1, await ctx.Courses.CountAsync());
        Assert.Equal(2, await ctx.Units.CountAsync());
        Assert.Equal(4, await ctx.Lessons.CountAsync());
        Assert.Equal(1, await ctx.Lessons.CountAsync(l => !l.IsPublished));
    }

    [Fact]
    public async Task Seed_gives_published_lessons_exercises_and_leaves_the_draft_empty()
    {
        await using var ctx = NewContext();
        var greetings = await ctx.Lessons.FirstAsync(l => l.Id == new LessonId(LearningSeedIds.GreetingsLesson));
        var draft = await ctx.Lessons.FirstAsync(l => l.Id == new LessonId(LearningSeedIds.DessertLessonDraft));

        Assert.NotEmpty(greetings.Exercises);
        Assert.Empty(draft.Exercises);
    }
}
