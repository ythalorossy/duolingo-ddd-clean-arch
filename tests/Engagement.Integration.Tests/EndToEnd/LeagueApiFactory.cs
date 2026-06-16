using Engagement.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.EndToEnd;

public sealed class LeagueApiFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_League_E2E;Trusted_Connection=True;TrustServerCertificate=True";

    // Wed Jan 9 2030 → league week of Mon Jan 7.
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Engagement", TestConnectionString);
        builder.UseSetting("Leagues:Settlement:Enabled", "false"); // keep the scheduler out of E2E (shared FakeTimeProvider)

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EngagementDbContext>();
            db.Database.EnsureDeleted();
            db.Database.Migrate();
        });
    }
}

// Both league e2e classes share ONE factory (one DB) and run sequentially as a collection,
// so their EnsureDeleted/Migrate can't race each other under xUnit's parallel class execution.
[CollectionDefinition("League E2E")]
public sealed class LeagueE2ECollection : ICollectionFixture<LeagueApiFactory>;
