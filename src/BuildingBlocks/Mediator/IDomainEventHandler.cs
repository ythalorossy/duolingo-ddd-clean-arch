using BuildingBlocks.Domain;

namespace BuildingBlocks.Mediator;

// Intra-module handler for a domain event (distinct from INotificationHandler, which is for
// cross-module integration events).
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
