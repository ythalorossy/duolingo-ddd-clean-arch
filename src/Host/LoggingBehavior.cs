using BuildingBlocks.Mediator;
using Microsoft.Extensions.Logging;

namespace Host;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next();
        logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}
