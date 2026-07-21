using Learning.Application;
using Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Learning.Infrastructure;

public static class LearningInfrastructureExtensions
{
    public static IServiceCollection AddLearningInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LearningDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ICatalogReadService, CatalogReadService>();
        services.AddScoped<IAttemptRepository, AttemptRepository>();
        services.AddScoped<ILessonPresentationRead, LessonPresentationRead>();

        // TimeProvider is shared with Engagement; TryAdd avoids a duplicate registration when both modules load.
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
