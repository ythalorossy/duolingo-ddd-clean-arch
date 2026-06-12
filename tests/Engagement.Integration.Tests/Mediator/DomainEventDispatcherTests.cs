using BuildingBlocks.Domain;
using BuildingBlocks.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.Mediator;

public class DomainEventDispatcherTests
{
    // Public (not private): the dispatcher invokes HandleAsync via `dynamic`, whose binder
    // honours accessibility across assemblies. Production handlers/events are public too.
    public sealed record TestEvent(DateTimeOffset OccurredOn) : IDomainEvent;

    public sealed class SpyHandler : IDomainEventHandler<TestEvent>
    {
        public int Calls { get; private set; }
        public Task HandleAsync(TestEvent domainEvent, CancellationToken ct)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Dispatches_to_a_registered_handler()
    {
        var spy = new SpyHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestEvent>>(spy);
        var dispatcher = new DomainEventDispatcher(services.BuildServiceProvider());

        await dispatcher.DispatchAsync(new TestEvent(DateTimeOffset.UnixEpoch), CancellationToken.None);

        Assert.Equal(1, spy.Calls);
    }

    [Fact]
    public async Task No_registered_handler_is_a_no_op()
    {
        var dispatcher = new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider());

        // Must not throw when nothing handles the event.
        await dispatcher.DispatchAsync(new TestEvent(DateTimeOffset.UnixEpoch), CancellationToken.None);
    }
}
