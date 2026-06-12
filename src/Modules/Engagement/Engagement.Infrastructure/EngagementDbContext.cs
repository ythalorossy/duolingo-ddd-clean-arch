using BuildingBlocks.Domain;
using BuildingBlocks.Mediator;
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class EngagementDbContext(
    DbContextOptions<EngagementDbContext> options,
    IDomainEventDispatcher? dispatcher = null) : DbContext(options)
{
    public const string Schema = "engagement";

    // Guards against re-entrancy: a domain-event handler calls SaveChangesAsync to persist its
    // own aggregate, which would otherwise re-collect and re-dispatch events recursively.
    private bool _dispatching;

    public DbSet<XpAccount> XpAccounts => Set<XpAccount>();
    public DbSet<LearnerStreak> LearnerStreaks => Set<LearnerStreak>();
    public DbSet<LeagueStanding> LeagueStandings => Set<LeagueStanding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new XpAccountConfiguration());
        modelBuilder.ApplyConfiguration(new LearnerStreakConfiguration());
        modelBuilder.ApplyConfiguration(new LeagueStandingConfiguration());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var result = await base.SaveChangesAsync(ct);

        // Re-entrant save from inside a handler: persist only, do not re-dispatch.
        if (_dispatching)
            return result;

        var domainEvents = ChangeTracker.Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var aggregate in ChangeTracker.Entries<AggregateRoot>().Select(e => e.Entity))
            aggregate.ClearDomainEvents();

        // No dispatcher (design-time factory / migrations) or nothing raised → done.
        if (dispatcher is null || domainEvents.Count == 0)
            return result;

        _dispatching = true;
        try
        {
            foreach (var domainEvent in domainEvents)
                await dispatcher.DispatchAsync(domainEvent, ct);
        }
        finally
        {
            _dispatching = false;
        }

        return result;
    }
}
