using Engagement.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Engagement.Integration.Tests.EndToEnd;

public sealed class EngagementApiFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_E2E;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Engagement", TestConnectionString);
        builder.UseSetting("Leagues:Settlement:Enabled", "false"); // keep the scheduler out of E2E (shared FakeTimeProvider)

        builder.ConfigureServices(services =>
        {
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EngagementDbContext>();
            db.Database.EnsureDeleted();
            db.Database.Migrate();
        });
    }
}
