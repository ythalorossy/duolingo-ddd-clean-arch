using System.Net;
using System.Net.Http.Json;
using Learning.Infrastructure;
using Xunit;

namespace Learning.Integration.Tests.EndToEnd;

public class LearningApiTests(LearningApiFactory factory) : IClassFixture<LearningApiFactory>
{
    private sealed record XpResponse(Guid LearnerId, int TotalXp);
    private sealed record CatalogView(List<CourseView> Courses);
    private sealed record CourseView(Guid Id, string Title, string? Language, List<UnitView> Units);
    private sealed record UnitView(Guid Id, string Title, int Position, List<LessonView> Lessons);
    private sealed record LessonView(Guid Id, string Title, int Position, bool IsPublished);

    private HttpClient ClientForLearner(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    [Fact]
    public async Task Get_courses_returns_the_seeded_catalog()
    {
        var catalog = await factory.CreateClient().GetFromJsonAsync<CatalogView>("/courses");

        Assert.NotNull(catalog);
        var course = Assert.Single(catalog!.Courses);
        Assert.Equal("Spanish", course.Title);
        Assert.Equal(2, course.Units.Count);
        Assert.Equal(4, course.Units.Sum(u => u.Lessons.Count));
    }

    [Fact]
    public async Task Completing_a_published_lesson_returns_200_and_awards_xp()
    {
        var learnerId = Guid.NewGuid();
        var client = ClientForLearner(learnerId);

        var post = await client.PostAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/complete", null);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var xp = await client.GetFromJsonAsync<XpResponse>("/me/xp");
        Assert.Equal(10, xp!.TotalXp);
    }

    [Fact]
    public async Task Completing_an_unknown_lesson_returns_404()
    {
        var post = await ClientForLearner(Guid.NewGuid())
            .PostAsync($"/lessons/{Guid.NewGuid()}/complete", null);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task Completing_an_unpublished_lesson_returns_409()
    {
        var post = await ClientForLearner(Guid.NewGuid())
            .PostAsync($"/lessons/{LearningSeedIds.DessertLessonDraft}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, post.StatusCode);
    }

    [Fact]
    public async Task Completing_the_same_lesson_twice_awards_xp_twice()
    {
        var learnerId = Guid.NewGuid();
        var client = ClientForLearner(learnerId);

        await client.PostAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/complete", null);
        await client.PostAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/complete", null);

        var xp = await client.GetFromJsonAsync<XpResponse>("/me/xp");
        Assert.Equal(20, xp!.TotalXp);
    }
}
