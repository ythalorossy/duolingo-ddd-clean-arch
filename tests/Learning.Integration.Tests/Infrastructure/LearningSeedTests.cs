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
}
