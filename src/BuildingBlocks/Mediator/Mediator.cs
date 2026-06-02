using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Mediator;

public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        var requestType = request.GetType();

        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        dynamic handler = serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for {requestType.Name}");

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = ((IEnumerable<object>)serviceProvider.GetServices(behaviorType)).Reverse().ToList();

        RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync((dynamic)request, ct);

        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            dynamic current = behavior;
            pipeline = () => current.HandleAsync((dynamic)request, next, ct);
        }

        return pipeline();
    }

    public async Task PublishAsync(INotification notification, CancellationToken ct = default)
    {
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notification.GetType());
        var handlers = (IEnumerable<object>)serviceProvider.GetServices(handlerType);

        foreach (dynamic handler in handlers)
            await handler.HandleAsync((dynamic)notification, ct);
    }
}
