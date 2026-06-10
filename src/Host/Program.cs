using BuildingBlocks.Mediator;
using Engagement.Application;
using Engagement.Infrastructure;
using Host;
using Learning.Stub;

var builder = WebApplication.CreateBuilder(args);

// --- Composition root ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();

builder.Services.AddMediator(
    typeof(GetXpAccount).Assembly,   // Engagement.Application handlers
    LearningStubExtensions.Assembly);         // Learning.Stub handlers

builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

builder.Services.AddEngagementInfrastructure(
    builder.Configuration.GetConnectionString("Engagement")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Engagement"));

var app = builder.Build();

// --- Endpoints ---
app.MapPost("/lessons/{lessonId:guid}/complete",
    async (Guid lessonId, ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        await mediator.SendAsync(new CompleteLesson(user.LearnerId, lessonId), ct);
        return Results.Accepted();
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

app.MapPost("/me/streak-freezes",
    async (ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        await mediator.SendAsync(new GrantStreakFreeze(user.LearnerId), ct);
        return Results.Ok();
    });

app.Run();

// Exposes the implicit Program class to WebApplicationFactory in tests.
public partial class Program { }
