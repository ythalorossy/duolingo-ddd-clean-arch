using BuildingBlocks.Domain;

namespace BuildingBlocks.Mediator;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct);
}
