using Learning.Domain;
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class AttemptRepositoryTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Attempt_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public AttemptRepositoryTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static Attempt SampleAttempt()
    {
        var ex = Exercise.Create(new ExerciseId(Guid.NewGuid()), 1,
            new Prompt("Q"), new Choices(new[] { "a", "b" }), 0);
        var lesson = Lesson.Create(new LessonId(Guid.NewGuid()), new UnitId(Guid.NewGuid()),
            new Title("L"), 1, isPublished: true, exercises: new[] { ex });
        var result = lesson.Grade(new[] { new SubmittedAnswer(ex.Id, 0) });
        return Attempt.Create(new AttemptId(Guid.NewGuid()), new LearnerId(Guid.NewGuid()),
            lesson.Id, new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public async Task AddAsync_persists_the_attempt_and_its_answers()
    {
        var attempt = SampleAttempt();

        await using (var ctx = NewContext())
            await new AttemptRepository(ctx).AddAsync(attempt, CancellationToken.None);

        await using var read = NewContext();
        var reloaded = await read.Attempts.FirstOrDefaultAsync(a => a.Id == attempt.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(Outcome.Passed, reloaded!.Outcome);
        Assert.Equal(1, reloaded.Score.Correct);
        Assert.Single(reloaded.Answers);
        Assert.True(reloaded.Answers.First().WasCorrect);
    }
}
