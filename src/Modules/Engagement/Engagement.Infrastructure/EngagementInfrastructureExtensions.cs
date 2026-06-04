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
        services.AddScoped<ILearnerStreakRepository, LearnerStreakRepository>();
        services.AddScoped<LessonCompletionXpPolicy>();

        // The system clock is an infrastructure adapter (like the DbContext): the application's
        // time-aware handlers depend on the TimeProvider abstraction; infrastructure supplies the
        // real one. Tests override it with a FakeTimeProvider.
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
