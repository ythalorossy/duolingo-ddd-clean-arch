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
    typeof(GetLearnerEngagement).Assembly,   // Engagement.Application handlers
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

app.MapGet("/me/engagement",
    async (ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        var dto = await mediator.SendAsync(new GetLearnerEngagement(user.LearnerId), ct);
        return Results.Ok(dto);
    });

app.Run();

// Exposes the implicit Program class to WebApplicationFactory in tests.
public partial class Program { }
