using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Mediator;

public static class MediatorServiceCollectionExtensions
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(IRequestHandler<,>),
        typeof(INotificationHandler<>)
    ];

    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<IMediator, Mediator>();

        foreach (var assembly in assemblies)
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        foreach (var handlerInterface in type.GetInterfaces()
                     .Where(i => i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition())))
        {
            services.AddScoped(handlerInterface, type);
        }

        return services;
    }
}
