using BuildingBlocks.Mediator;
using Engagement.Application;
using Engagement.Infrastructure;
using Host;
using Learning.Application;
using Learning.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- Composition root ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();
builder.Services.AddOpenApi();

builder.Services.AddMediator(
    typeof(GetXpAccount).Assembly,        // Engagement.Application handlers
    typeof(SubmitAttempt).Assembly);      // Learning.Application handlers

builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

builder.Services.AddEngagementInfrastructure(
    builder.Configuration.GetConnectionString("Engagement")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Engagement"));

builder.Services.AddLearningInfrastructure(
    builder.Configuration.GetConnectionString("Learning")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Learning"));

// League weeks close automatically: a BackgroundService periodically settles any ended-but-unsettled
// week. Feature-flagged so the E2E test hosts can disable it (they jump a shared FakeTimeProvider).
builder.Services.Configure<LeagueSettlementOptions>(
    builder.Configuration.GetSection("Leagues:Settlement"));
if (builder.Configuration.GetValue("Leagues:Settlement:Enabled", true))
    builder.Services.AddHostedService<LeagueSettlementScheduler>();

var app = builder.Build();

app.MapOpenApi(); // serves /openapi/v1.json

// --- Endpoints ---
app.MapGet("/courses",
    async (IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.SendAsync(new GetCatalog(), ct)))
    .Produces<CatalogDto>();

app.MapGet("/lessons/{lessonId:guid}",
    async (Guid lessonId, IMediator mediator, CancellationToken ct) =>
    {
        var dto = await mediator.SendAsync(new GetLesson(lessonId), ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    });

app.MapPost("/lessons/{lessonId:guid}/attempts",
    async (Guid lessonId, AttemptBody body, ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        try
        {
            var result = await mediator.SendAsync(
                new SubmitAttempt(user.LearnerId, lessonId,
                    body.Answers.Select(a => new SubmittedAnswerInput(a.ExerciseId, a.SelectedChoiceIndex)).ToList()),
                ct);
            return Results.Ok(result); // grading + any XP award ran in-process before we return
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message }); // lesson exists but is not completable
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message }); // answer set does not match the exercises
        }
    });

app.MapGet("/me/courses/{courseId:guid}/map",
    async (Guid courseId, ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        var dto = await mediator.SendAsync(new GetCourseMap(courseId, user.LearnerId), ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    });

app.MapGet("/me/xp",
    async (ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        var dto = await mediator.SendAsync(new GetXpAccount(user.LearnerId), ct);
        return Results.Ok(dto);
    });

app.MapPut("/me/timezone",
    async (SetTimeZoneRequest body, ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        try
        {
            await mediator.SendAsync(new SetLearnerTimeZone(user.LearnerId, body.IanaId), ct);
            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            // An invalid IANA id is a client error (bad input), not a 500.
            return Results.BadRequest(new { error = ex.Message });
        }
    });

app.MapGet("/me/streak",
    async (ICurrentUser user, IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.SendAsync(new GetLearnerStreak(user.LearnerId), ct)));

app.MapGet("/me/league",
    async (ICurrentUser user, IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.SendAsync(new GetLeagueLeaderboard(user.LearnerId), ct)));

app.MapPost("/leagues/weeks/{weekStart}/settle",
    async (DateOnly weekStart, IMediator mediator, CancellationToken ct) =>
    {
        try
        {
            await mediator.SendAsync(new SettleLeagueWeek(weekStart), ct);
            return Results.Ok();
        }
        catch (ArgumentException ex)
        {
            // A non-Monday weekStart is bad input, not a 500.
            return Results.BadRequest(new { error = ex.Message });
        }
    });

app.MapPost("/me/streak-freezes",
    async (ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        await mediator.SendAsync(new GrantStreakFreeze(user.LearnerId), ct);
        return Results.Ok();
    });

app.Run();

public sealed record AttemptBody(List<AnswerBody> Answers);
public sealed record AnswerBody(Guid ExerciseId, int SelectedChoiceIndex);

// Exposes the implicit Program class to WebApplicationFactory in tests.
public partial class Program { }
