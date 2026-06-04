using Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Engagement.Infrastructure;

public static class EngagementInfrastructureExtensions
{
    public static IServiceCollection AddEngagementInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<EngagementDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IXpAccountRepository, XpAccountRepository>();
        services.AddScoped<LessonCompletionXpPolicy>();
        return services;
    }
}
