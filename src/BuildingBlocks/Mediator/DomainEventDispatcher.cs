using BuildingBlocks.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Mediator;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = (IEnumerable<object>)serviceProvider.GetServices(handlerType);

        foreach (dynamic handler in handlers)
            await handler.HandleAsync((dynamic)domainEvent, ct);
    }
}
