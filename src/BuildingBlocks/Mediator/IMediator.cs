namespace BuildingBlocks.Mediator;

public interface IMediator
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
    Task PublishAsync(INotification notification, CancellationToken ct = default);
}
