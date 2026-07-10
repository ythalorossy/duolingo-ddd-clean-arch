using Engagement.Infrastructure;
using Learning.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learning.Integration.Tests.EndToEnd;

public sealed class LearningApiFactory : WebApplicationFactory<Program>
{
    private const string EngagementDb =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_E2E_Engagement;Trusted_Connection=True;TrustServerCertificate=True";
    private const string LearningDb =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_E2E;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Engagement", EngagementDb);
        builder.UseSetting("ConnectionStrings:Learning", LearningDb);
        builder.UseSetting("Leagues:Settlement:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            using var scope = services.BuildServiceProvider().CreateScope();

            var engagement = scope.ServiceProvider.GetRequiredService<EngagementDbContext>();
            engagement.Database.EnsureDeleted();
            engagement.Database.Migrate();

            var learning = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            learning.Database.EnsureDeleted();
            learning.Database.Migrate(); // applies the learning schema + seed
        });
    }
}
