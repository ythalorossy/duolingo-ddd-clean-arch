using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class CatalogReadServiceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Catalog_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public CatalogReadServiceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task Returns_the_seeded_catalog_nested_and_ordered_by_position()
    {
        await using var ctx = NewContext();

        var catalog = await new CatalogReadService(ctx).GetCatalogAsync(CancellationToken.None);

        var course = Assert.Single(catalog.Courses);
        Assert.Equal("Spanish", course.Title);
        Assert.Equal("es", course.Language);
        Assert.Equal(2, course.Units.Count);
        Assert.Equal("Basics", course.Units[0].Title); // Position 1 sorts first
        Assert.Equal("Food", course.Units[1].Title);   // Position 2
        Assert.Equal(2, course.Units[0].Lessons.Count);
        Assert.Equal("Greetings", course.Units[0].Lessons[0].Title);
        Assert.Contains(course.Units.SelectMany(u => u.Lessons), l => !l.IsPublished);
    }
}
