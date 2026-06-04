using BuildingBlocks.Domain;
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class EngagementDbContext(DbContextOptions<EngagementDbContext> options) : DbContext(options)
{
    public const string Schema = "engagement";

    public DbSet<XpAccount> XpAccounts => Set<XpAccount>();
    public DbSet<LearnerStreak> LearnerStreaks => Set<LearnerStreak>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new XpAccountConfiguration());
        modelBuilder.ApplyConfiguration(new LearnerStreakConfiguration());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var result = await base.SaveChangesAsync(ct);

        // Slice 1: no subscribers to XpAwarded yet (YAGNI). Clear raised events so a
        // long-lived context doesn't accumulate them. A real dispatcher arrives when a
        // subscriber (Notifications/Achievements) exists.
        foreach (var aggregate in ChangeTracker.Entries<AggregateRoot>().Select(e => e.Entity))
            aggregate.ClearDomainEvents();

        return result;
    }
}
