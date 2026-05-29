using BuildingBlocks.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.Mediator;

public class MediatorTests
{
    public record Ping(string Text) : IRequest<string>;

    public class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken ct) =>
            Task.FromResult($"pong:{request.Text}");
    }

    public class ShoutBehavior : IPipelineBehavior<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, RequestHandlerDelegate<string> next, CancellationToken ct)
        {
            var result = await next();
            return result.ToUpperInvariant();
        }
    }

    public record Notified(string Text) : INotification;

    public class RecordingHandler : INotificationHandler<Notified>
    {
        public static readonly List<string> Received = new();
        public Task HandleAsync(Notified notification, CancellationToken ct)
        {
            Received.Add(notification.Text);
            return Task.CompletedTask;
        }
    }

    private static IMediator Build(Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddMediator(typeof(MediatorTests).Assembly);
        extra?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Send_routes_to_the_matching_handler()
    {
        var mediator = Build();
        var result = await mediator.SendAsync(new Ping("hi"));
        Assert.Equal("pong:hi", result);
    }

    [Fact]
    public async Task Pipeline_behavior_wraps_the_handler()
    {
        var mediator = Build(s => s.AddScoped<IPipelineBehavior<Ping, string>, ShoutBehavior>());
        var result = await mediator.SendAsync(new Ping("hi"));
        Assert.Equal("PONG:HI", result);
    }

    [Fact]
    public async Task Publish_invokes_all_notification_handlers()
    {
        RecordingHandler.Received.Clear();
        var mediator = Build();
        await mediator.PublishAsync(new Notified("x"));
        Assert.Contains("x", RecordingHandler.Received);
    }
}
