using Learning.Application;
using Learning.Domain;
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class CourseMapReadServiceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_CourseMap_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public CourseMapReadServiceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static Attempt SeededAttempt(Guid learner, Guid lessonId, Score score, Outcome outcome) =>
        Attempt.Create(
            new AttemptId(Guid.NewGuid()),
            new LearnerId(learner),
            new LessonId(lessonId),
            new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero),
            new GradingResult(score, outcome, new List<GradedAnswer>()));

    [Fact]
    public async Task No_attempts_first_lesson_unlocked_rest_locked_and_draft_absent()
    {
        await using var ctx = NewContext();

        var map = await new CourseMapReadService(ctx)
            .GetCourseMapAsync(LearningSeedIds.SpanishCourse, Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(map);
        Assert.Equal(2, map!.Units.Count);                 // Basics + Food (both have published lessons)
        var basics = map.Units[0];
        Assert.Equal("Basics", basics.Title);
        Assert.Equal("Unlocked", basics.Lessons[0].Status); // Greetings
        Assert.Equal("Locked", basics.Lessons[1].Status);   // The verb ser
        var food = map.Units[1];
        Assert.Single(food.Lessons);                         // Dessert draft absent
        Assert.Equal("Locked", food.Lessons[0].Status);      // At the cafe (Unit 2 not open)
    }

    [Fact]
    public async Task A_passing_attempt_marks_the_lesson_completed_and_unlocks_the_next()
    {
        var learner = Guid.NewGuid();
        await using (var seed = NewContext())
        {
            seed.Attempts.Add(SeededAttempt(learner, LearningSeedIds.GreetingsLesson, new Score(2, 2), Outcome.Passed));
            await seed.SaveChangesAsync();
        }

        await using var ctx = NewContext();
        var map = await new CourseMapReadService(ctx)
            .GetCourseMapAsync(LearningSeedIds.SpanishCourse, learner, CancellationToken.None);

        var basics = map!.Units[0];
        Assert.Equal("Completed", basics.Lessons[0].Status); // Greetings passed
        Assert.Equal("Unlocked", basics.Lessons[1].Status);  // ser is now the frontier
    }

    [Fact]
    public async Task A_failing_attempt_does_not_complete_the_lesson()
    {
        var learner = Guid.NewGuid();
        await using (var seed = NewContext())
        {
            seed.Attempts.Add(SeededAttempt(learner, LearningSeedIds.GreetingsLesson, new Score(0, 2), Outcome.Failed));
            await seed.SaveChangesAsync();
        }

        await using var ctx = NewContext();
        var map = await new CourseMapReadService(ctx)
            .GetCourseMapAsync(LearningSeedIds.SpanishCourse, learner, CancellationToken.None);

        Assert.Equal("Unlocked", map!.Units[0].Lessons[0].Status); // still the frontier, not Completed
    }

    [Fact]
    public async Task Unknown_course_returns_null()
    {
        await using var ctx = NewContext();

        var map = await new CourseMapReadService(ctx)
            .GetCourseMapAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(map);
    }
}
