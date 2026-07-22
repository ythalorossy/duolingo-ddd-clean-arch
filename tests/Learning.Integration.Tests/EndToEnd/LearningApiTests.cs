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
    private sealed record UnitView(Guid Id, string Title, int Position, List<LessonNode> Lessons);
    private sealed record LessonNode(Guid Id, string Title, int Position, bool IsPublished);

    private sealed record LessonPresentation(Guid Id, string Title, bool IsPublished, List<ExercisePresentation> Exercises);
    private sealed record ExercisePresentation(Guid Id, int Position, string Prompt, List<string> Choices);
    private sealed record AttemptResult(Guid AttemptId, int ScoreCorrect, int ScoreTotal, string Outcome, List<PerExercise> PerExercise);
    private sealed record PerExercise(Guid ExerciseId, bool WasCorrect);
    private sealed record AnswerInput(Guid ExerciseId, int SelectedChoiceIndex);
    private sealed record AttemptRequest(List<AnswerInput> Answers);

    private sealed record CourseMapView(Guid CourseId, string Title, List<UnitMapView> Units);
    private sealed record UnitMapView(Guid Id, string Title, int Position, List<LessonNodeView> Lessons);
    private sealed record LessonNodeView(Guid Id, string Title, int Position, string Status);

    private HttpClient ClientForLearner(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    // The Greetings lesson's correct answers, from the seed (the API never returns them).
    private static AttemptRequest GreetingsAnswers(int ex1Pick, int ex2Pick, LessonPresentation lesson) =>
        new(new List<AnswerInput>
        {
            new(lesson.Exercises[0].Id, ex1Pick),
            new(lesson.Exercises[1].Id, ex2Pick)
        });

    private async Task<LessonPresentation> GetGreetings(HttpClient client) =>
        (await client.GetFromJsonAsync<LessonPresentation>($"/lessons/{LearningSeedIds.GreetingsLesson}"))!;

    [Fact]
    public async Task Get_courses_returns_the_seeded_catalog()
    {
        var catalog = await factory.CreateClient().GetFromJsonAsync<CatalogView>("/courses");
        Assert.NotNull(catalog);
        var course = Assert.Single(catalog!.Courses);
        Assert.Equal(4, course.Units.Sum(u => u.Lessons.Count));
    }

    [Fact]
    public async Task Get_lesson_returns_exercises_without_the_answer_key()
    {
        var lesson = await GetGreetings(factory.CreateClient());
        Assert.Equal(2, lesson.Exercises.Count);
        Assert.All(lesson.Exercises, e => Assert.True(e.Choices.Count >= 2));
    }

    [Fact]
    public async Task Passing_attempt_returns_200_Passed_and_awards_xp()
    {
        var client = ClientForLearner(Guid.NewGuid());
        var lesson = await GetGreetings(client);

        var post = await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts",
            GreetingsAnswers(LearningSeedIds.GreetingsEx1Correct, LearningSeedIds.GreetingsEx2Correct, lesson));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var result = await post.Content.ReadFromJsonAsync<AttemptResult>();
        Assert.Equal("Passed", result!.Outcome);

        var xp = await client.GetFromJsonAsync<XpResponse>("/me/xp");
        Assert.Equal(10, xp!.TotalXp);
    }

    [Fact]
    public async Task Failing_attempt_returns_200_Failed_and_awards_no_xp()
    {
        var client = ClientForLearner(Guid.NewGuid());
        var lesson = await GetGreetings(client);

        var wrong = LearningSeedIds.GreetingsEx1Correct == 0 ? 1 : 0;
        var post = await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts",
            GreetingsAnswers(wrong, wrong, lesson));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var result = await post.Content.ReadFromJsonAsync<AttemptResult>();
        Assert.Equal("Failed", result!.Outcome);

        var xp = await client.GetFromJsonAsync<XpResponse>("/me/xp");
        Assert.Equal(0, xp!.TotalXp);
    }

    [Fact]
    public async Task Unknown_lesson_returns_404()
    {
        var post = await ClientForLearner(Guid.NewGuid())
            .PostAsJsonAsync($"/lessons/{Guid.NewGuid()}/attempts", new AttemptRequest(new List<AnswerInput>()));
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task Unpublished_lesson_returns_409()
    {
        var post = await ClientForLearner(Guid.NewGuid())
            .PostAsJsonAsync($"/lessons/{LearningSeedIds.DessertLessonDraft}/attempts",
                new AttemptRequest(new List<AnswerInput>()));
        Assert.Equal(HttpStatusCode.Conflict, post.StatusCode);
    }

    [Fact]
    public async Task Malformed_answer_set_returns_400()
    {
        var client = ClientForLearner(Guid.NewGuid());
        var lesson = await GetGreetings(client);

        // only one answer for a two-exercise lesson
        var bad = new AttemptRequest(new List<AnswerInput> { new(lesson.Exercises[0].Id, 0) });
        var post = await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts", bad);
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    [Fact]
    public async Task Passing_the_same_lesson_twice_awards_xp_twice()
    {
        var client = ClientForLearner(Guid.NewGuid());
        var lesson = await GetGreetings(client);
        var pass = GreetingsAnswers(LearningSeedIds.GreetingsEx1Correct, LearningSeedIds.GreetingsEx2Correct, lesson);

        await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts", pass);
        await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts", pass);

        var xp = await client.GetFromJsonAsync<XpResponse>("/me/xp");
        Assert.Equal(20, xp!.TotalXp);
    }

    [Fact]
    public async Task Course_map_for_a_new_learner_unlocks_only_the_first_lesson()
    {
        var client = ClientForLearner(Guid.NewGuid());

        var map = await client.GetFromJsonAsync<CourseMapView>($"/me/courses/{LearningSeedIds.SpanishCourse}/map");

        Assert.NotNull(map);
        Assert.Equal("Unlocked", map!.Units[0].Lessons[0].Status); // Greetings
        Assert.Equal("Locked", map.Units[0].Lessons[1].Status);    // The verb ser
        Assert.DoesNotContain(
            map.Units.SelectMany(u => u.Lessons),
            l => l.Id == LearningSeedIds.DessertLessonDraft);       // draft absent
    }

    [Fact]
    public async Task Passing_a_lesson_advances_the_map()
    {
        var client = ClientForLearner(Guid.NewGuid());
        var lesson = await GetGreetings(client);
        await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts",
            GreetingsAnswers(LearningSeedIds.GreetingsEx1Correct, LearningSeedIds.GreetingsEx2Correct, lesson));

        var map = await client.GetFromJsonAsync<CourseMapView>($"/me/courses/{LearningSeedIds.SpanishCourse}/map");

        var basics = map!.Units[0];
        Assert.Equal("Completed", basics.Lessons[0].Status);
        Assert.Equal("Unlocked", basics.Lessons[1].Status);
    }

    [Fact]
    public async Task Unknown_course_map_returns_404()
    {
        var resp = await ClientForLearner(Guid.NewGuid())
            .GetAsync($"/me/courses/{Guid.NewGuid()}/map");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
