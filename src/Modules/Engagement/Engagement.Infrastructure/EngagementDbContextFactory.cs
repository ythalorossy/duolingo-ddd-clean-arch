using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Engagement.Infrastructure;

// Used ONLY by the EF Core CLI at design time (migrations). The runtime app wires the
// DbContext through AddEngagementInfrastructure with the real connection string.
public sealed class EngagementDbContextFactory : IDesignTimeDbContextFactory<EngagementDbContext>
{
    public EngagementDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_Design;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new EngagementDbContext(options);
    }
}
