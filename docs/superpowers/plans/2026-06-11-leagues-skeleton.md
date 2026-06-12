# Leagues — Slice 1 (League Skeleton) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** As a learner earns XP, accumulate their weekly score in a per-learner `LeagueStanding` (Bronze, UTC week), fed by the existing `XpAwarded` domain event through a new in-process dispatcher, and expose a `GET /me/league` leaderboard ranked by weekly XP.

**Architecture:** Reactive per-learner aggregate (mirrors `LearnerStreak`) with a lazy week-roll. A new domain-event dispatcher building block publishes `XpAwarded` to `IDomainEventHandler<XpAwarded>` after the XP write is persisted, inside `EngagementDbContext.SaveChangesAsync` (re-entrancy-guarded). The leaderboard is a read-model query over standings. No promotion/demotion (Slice 2), no scheduler, no outbox.

**Tech Stack:** C# / .NET 10, EF Core 10 (SQL Server LocalDB), the hand-rolled mediator in `BuildingBlocks.Mediator`, xUnit + `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`), NetArchTest.

**Spec:** [`docs/superpowers/specs/2026-06-11-leagues-skeleton-design.md`](../specs/2026-06-11-leagues-skeleton-design.md)

---

## Task 1: `LeagueWeek` value object

The UTC-Monday-anchored week. Pure domain, no dependencies beyond `ValueObject`.

**Files:**
- Create: `tests/Engagement.Domain.Tests/LeagueWeekTests.cs`
- Create: `src/Modules/Engagement/Engagement.Domain/LeagueWeek.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Engagement.Domain.Tests/LeagueWeekTests.cs`:

```csharp
using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LeagueWeekTests
{
    // Jan 7 2030 is a Monday; Jan 9 = Wed, Jan 13 = Sun, Jan 14 = next Monday.
    [Fact]
    public void Containing_returns_the_monday_of_the_week()
    {
        var w = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 15, 0, 0, TimeSpan.Zero)); // Wed
        Assert.Equal(new DateOnly(2030, 1, 7), w.Start);
    }

    [Fact]
    public void Sunday_and_the_following_monday_are_different_weeks()
    {
        var sun = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 13, 23, 59, 59, TimeSpan.Zero));
        var mon = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 14, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2030, 1, 7), sun.Start);
        Assert.Equal(new DateOnly(2030, 1, 14), mon.Start);
        Assert.NotEqual(sun, mon);
    }

    [Fact]
    public void Keys_off_the_utc_instant_not_the_offset()
    {
        // 2030-01-14 13:00 +14:00 == 2030-01-13 23:00 UTC (a Sunday) → week of Jan 7.
        var w = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 14, 13, 0, 0, TimeSpan.FromHours(14)));
        Assert.Equal(new DateOnly(2030, 1, 7), w.Start);
    }

    [Fact]
    public void Constructor_rejects_a_non_monday()
    {
        Assert.Throws<ArgumentException>(() => new LeagueWeek(new DateOnly(2030, 1, 9))); // Wednesday
    }

    [Fact]
    public void Same_week_instances_are_equal()
    {
        var a = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 1, 0, 0, TimeSpan.Zero));
        var b = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 11, 22, 0, 0, TimeSpan.Zero));
        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Domain.Tests --filter "FullyQualifiedName~LeagueWeekTests"`
Expected: FAIL — compile error, `LeagueWeek` does not exist.

- [ ] **Step 3: Implement `LeagueWeek`**

Create `src/Modules/Engagement/Engagement.Domain/LeagueWeek.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

// A league competition week: a fixed UTC calendar week anchored to its Monday.
// Shared by every learner in a cohort, so it uses ONE canonical clock (UTC) — unlike
// LearnerStreak, which is per-learner and uses the learner's own time zone.
public sealed class LeagueWeek : ValueObject
{
    public DateOnly Start { get; } // the Monday (UTC)

    public LeagueWeek(DateOnly start)
    {
        if (start.DayOfWeek != DayOfWeek.Monday)
            throw new ArgumentException("A league week must start on a Monday.", nameof(start));
        Start = start;
    }

    public static LeagueWeek Containing(DateTimeOffset instant)
    {
        var date = DateOnly.FromDateTime(instant.UtcDateTime);
        // DayOfWeek: Sunday=0..Saturday=6. Days since Monday: Mon→0 … Sun→6.
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return new LeagueWeek(date.AddDays(-daysSinceMonday));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
    }

    public override string ToString() => Start.ToString("yyyy-MM-dd");
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Domain.Tests --filter "FullyQualifiedName~LeagueWeekTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Engagement/Engagement.Domain/LeagueWeek.cs tests/Engagement.Domain.Tests/LeagueWeekTests.cs
git commit -m "feat(leagues): add LeagueWeek value object (UTC Monday-anchored week)"
```

---

## Task 2: `LeagueTier` enum + `LeagueStanding` aggregate

The per-learner aggregate with the lazy week-roll.

**Files:**
- Create: `src/Modules/Engagement/Engagement.Domain/LeagueTier.cs`
- Create: `tests/Engagement.Domain.Tests/LeagueStandingTests.cs`
- Create: `src/Modules/Engagement/Engagement.Domain/LeagueStanding.cs`

- [ ] **Step 1: Create the `LeagueTier` enum**

Create `src/Modules/Engagement/Engagement.Domain/LeagueTier.cs`:

```csharp
namespace Engagement.Domain;

// The 10-tier ladder. Slice 1 only ever uses Bronze; promotion/demotion (and any
// ordering/neighbour logic) arrives in Slice 2. Declared in ascending order.
public enum LeagueTier
{
    Bronze,
    Silver,
    Gold,
    Sapphire,
    Ruby,
    Emerald,
    Amethyst,
    Pearl,
    Obsidian,
    Diamond
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Engagement.Domain.Tests/LeagueStandingTests.cs`:

```csharp
using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LeagueStandingTests
{
    private static readonly DateTimeOffset Wed = new(2030, 1, 9, 12, 0, 0, TimeSpan.Zero);      // week of Jan 7
    private static readonly DateTimeOffset Thu = new(2030, 1, 10, 12, 0, 0, TimeSpan.Zero);     // same week
    private static readonly DateTimeOffset NextWed = new(2030, 1, 16, 12, 0, 0, TimeSpan.Zero); // week of Jan 14

    private static LeagueStanding NewBronze(DateTimeOffset asOf) =>
        LeagueStanding.Create(new LearnerId(Guid.NewGuid()), LeagueWeek.Containing(asOf));

    [Fact]
    public void Create_starts_in_bronze_with_zero_xp()
    {
        var s = NewBronze(Wed);
        Assert.Equal(LeagueTier.Bronze, s.Tier);
        Assert.Equal(0, s.WeeklyXp.Value);
        Assert.Equal(new DateOnly(2030, 1, 7), s.Week.Start);
    }

    [Fact]
    public void RecordXp_accumulates_within_the_same_week()
    {
        var s = NewBronze(Wed);
        s.RecordXp(15, Wed);
        s.RecordXp(10, Thu);
        Assert.Equal(25, s.WeeklyXp.Value);
        Assert.Equal(new DateOnly(2030, 1, 7), s.Week.Start);
    }

    [Fact]
    public void RecordXp_in_a_later_week_resets_the_total()
    {
        var s = NewBronze(Wed);
        s.RecordXp(40, Wed);
        s.RecordXp(15, NextWed);
        Assert.Equal(15, s.WeeklyXp.Value);
        Assert.Equal(new DateOnly(2030, 1, 14), s.Week.Start);
    }

    [Fact]
    public void RecordXp_in_an_earlier_week_is_ignored()
    {
        var s = NewBronze(NextWed);
        s.RecordXp(20, NextWed);
        s.RecordXp(99, Wed); // earlier week — defensive guard
        Assert.Equal(20, s.WeeklyXp.Value);
        Assert.Equal(new DateOnly(2030, 1, 14), s.Week.Start);
    }

    [Fact]
    public void RecordXp_rejects_a_negative_amount()
    {
        var s = NewBronze(Wed);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.RecordXp(-5, Wed));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Domain.Tests --filter "FullyQualifiedName~LeagueStandingTests"`
Expected: FAIL — compile error, `LeagueStanding` does not exist.

- [ ] **Step 4: Implement `LeagueStanding`**

Create `src/Modules/Engagement/Engagement.Domain/LeagueStanding.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

// One small aggregate per learner. Accumulates XP for the current UTC week and rolls
// lazily on the next activity in a later week — the same "settle on activity, never on a
// timer" idea as LearnerStreak. Raises no domain event in Slice 1.
public sealed class LeagueStanding : AggregateRoot
{
    public LearnerId Id { get; private set; } = default!;
    public LeagueTier Tier { get; private set; }
    public LeagueWeek Week { get; private set; } = default!;
    public Xp WeeklyXp { get; private set; } = Xp.Zero;

    private LeagueStanding() { } // EF

    public static LeagueStanding Create(LearnerId id, LeagueWeek week) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        Tier = LeagueTier.Bronze,
        Week = week ?? throw new ArgumentNullException(nameof(week)),
        WeeklyXp = Xp.Zero
    };

    public void RecordXp(int amount, DateTimeOffset asOfUtc)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "XP amount cannot be negative.");

        var week = LeagueWeek.Containing(asOfUtc);

        if (week.Start > Week.Start)
        {
            // A later week began — roll over: this week starts fresh.
            Week = week;
            WeeklyXp = new Xp(amount);
            return;
        }

        if (week.Start < Week.Start)
            return; // clock moved back — defensive, cannot revive a past week

        WeeklyXp = new Xp(WeeklyXp.Value + amount); // same week — accumulate
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Domain.Tests --filter "FullyQualifiedName~LeagueStandingTests"`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Engagement/Engagement.Domain/LeagueTier.cs src/Modules/Engagement/Engagement.Domain/LeagueStanding.cs tests/Engagement.Domain.Tests/LeagueStandingTests.cs
git commit -m "feat(leagues): add LeagueStanding aggregate with lazy weekly roll"
```

---

## Task 3: Domain-event dispatcher building block

A new in-process path for `IDomainEvent`s, parallel to the existing `INotification` path. Lives in `BuildingBlocks.Mediator`.

**Files:**
- Modify: `src/BuildingBlocks/Mediator/BuildingBlocks.Mediator.csproj` (add `BuildingBlocks.Domain` reference)
- Create: `src/BuildingBlocks/Mediator/IDomainEventHandler.cs`
- Create: `src/BuildingBlocks/Mediator/IDomainEventDispatcher.cs`
- Create: `src/BuildingBlocks/Mediator/DomainEventDispatcher.cs`
- Modify: `src/BuildingBlocks/Mediator/MediatorServiceCollectionExtensions.cs`
- Create: `tests/Engagement.Integration.Tests/Mediator/DomainEventDispatcherTests.cs`

- [ ] **Step 1: Add the `BuildingBlocks.Domain` project reference**

The dispatcher must reference `IDomainEvent` (which lives in `BuildingBlocks.Domain`). Edit `src/BuildingBlocks/Mediator/BuildingBlocks.Mediator.csproj` — add a `<ProjectReference>` inside the existing `<ItemGroup>` that holds the package reference (or a new `<ItemGroup>`):

```xml
  <ItemGroup>
    <ProjectReference Include="..\Domain\BuildingBlocks.Domain.csproj" />
  </ItemGroup>
```

The full file becomes:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.8" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Domain\BuildingBlocks.Domain.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Engagement.Integration.Tests/Mediator/DomainEventDispatcherTests.cs`:

```csharp
using BuildingBlocks.Domain;
using BuildingBlocks.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.Mediator;

public class DomainEventDispatcherTests
{
    private sealed record TestEvent(DateTimeOffset OccurredOn) : IDomainEvent;

    private sealed class SpyHandler : IDomainEventHandler<TestEvent>
    {
        public int Calls { get; private set; }
        public Task HandleAsync(TestEvent domainEvent, CancellationToken ct)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Dispatches_to_a_registered_handler()
    {
        var spy = new SpyHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestEvent>>(spy);
        var dispatcher = new DomainEventDispatcher(services.BuildServiceProvider());

        await dispatcher.DispatchAsync(new TestEvent(DateTimeOffset.UnixEpoch), CancellationToken.None);

        Assert.Equal(1, spy.Calls);
    }

    [Fact]
    public async Task No_registered_handler_is_a_no_op()
    {
        var dispatcher = new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider());

        // Must not throw when nothing handles the event.
        await dispatcher.DispatchAsync(new TestEvent(DateTimeOffset.UnixEpoch), CancellationToken.None);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~DomainEventDispatcherTests"`
Expected: FAIL — compile error, `IDomainEventHandler` / `DomainEventDispatcher` do not exist.

- [ ] **Step 4: Create `IDomainEventHandler`**

Create `src/BuildingBlocks/Mediator/IDomainEventHandler.cs`:

```csharp
using BuildingBlocks.Domain;

namespace BuildingBlocks.Mediator;

// Intra-module handler for a domain event (distinct from INotificationHandler, which is for
// cross-module integration events).
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
```

- [ ] **Step 5: Create `IDomainEventDispatcher`**

Create `src/BuildingBlocks/Mediator/IDomainEventDispatcher.cs`:

```csharp
using BuildingBlocks.Domain;

namespace BuildingBlocks.Mediator;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct);
}
```

- [ ] **Step 6: Create `DomainEventDispatcher`**

Create `src/BuildingBlocks/Mediator/DomainEventDispatcher.cs` (mirrors `Mediator.PublishAsync`):

```csharp
using BuildingBlocks.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Mediator;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = (IEnumerable<object>)serviceProvider.GetServices(handlerType);

        foreach (dynamic handler in handlers)
            await handler.HandleAsync((dynamic)domainEvent, ct);
    }
}
```

- [ ] **Step 7: Register handlers + dispatcher in `AddMediator`**

Edit `src/BuildingBlocks/Mediator/MediatorServiceCollectionExtensions.cs`. Add `IDomainEventHandler<>` to the scanned interfaces and register the dispatcher. The full file becomes:

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Mediator;

public static class MediatorServiceCollectionExtensions
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(IRequestHandler<,>),
        typeof(INotificationHandler<>),
        typeof(IDomainEventHandler<>)
    ];

    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

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
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~DomainEventDispatcherTests"`
Expected: PASS (2 tests).

- [ ] **Step 9: Commit**

```bash
git add src/BuildingBlocks/Mediator/ tests/Engagement.Integration.Tests/Mediator/DomainEventDispatcherTests.cs
git commit -m "feat(mediator): add domain-event dispatcher building block"
```

---

## Task 4: Persist `LeagueStanding` (port, EF config, migration, repository)

**Files:**
- Create: `src/Modules/Engagement/Engagement.Domain/ILeagueStandingRepository.cs`
- Create: `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingConfiguration.cs`
- Modify: `src/Modules/Engagement/Engagement.Infrastructure/EngagementDbContext.cs` (add `DbSet` + apply config)
- Create: `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingRepository.cs`
- Create (generated): migration under `src/Modules/Engagement/Engagement.Infrastructure/Migrations/`
- Create: `tests/Engagement.Integration.Tests/Infrastructure/LeaguePersistenceTests.cs`

- [ ] **Step 1: Create the repository port**

Create `src/Modules/Engagement/Engagement.Domain/ILeagueStandingRepository.cs`:

```csharp
namespace Engagement.Domain;

public interface ILeagueStandingRepository
{
    Task<LeagueStanding?> GetAsync(LearnerId id, CancellationToken ct);
    Task AddAsync(LeagueStanding standing, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);

    // The cohort = everyone in a tier for a given week, ranked by weekly XP descending.
    Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(LeagueTier tier, LeagueWeek week, CancellationToken ct);
}
```

- [ ] **Step 2: Create the EF configuration**

Create `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingConfiguration.cs`:

```csharp
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Engagement.Infrastructure;

internal sealed class LeagueStandingConfiguration : IEntityTypeConfiguration<LeagueStanding>
{
    public void Configure(EntityTypeBuilder<LeagueStanding> builder)
    {
        builder.ToTable("LeagueStandings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new LearnerId(value))
            .HasColumnName("LearnerId")
            .ValueGeneratedNever();

        builder.Property(s => s.Tier)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(s => s.Week)
            .HasConversion(w => w.Start, value => new LeagueWeek(value))
            .HasColumnName("WeekStart");

        builder.Property(s => s.WeeklyXp)
            .HasConversion(xp => xp.Value, value => new Xp(value))
            .HasColumnName("WeeklyXp");

        builder.Ignore(s => s.DomainEvents);
    }
}
```

- [ ] **Step 3: Register the `DbSet` and apply the configuration**

Edit `src/Modules/Engagement/Engagement.Infrastructure/EngagementDbContext.cs`. Add the `DbSet` property after `LearnerStreaks`:

```csharp
    public DbSet<LeagueStanding> LeagueStandings => Set<LeagueStanding>();
```

And add the configuration line in `OnModelCreating` after the `LearnerStreakConfiguration` line:

```csharp
        modelBuilder.ApplyConfiguration(new LeagueStandingConfiguration());
```

(Leave the constructor and `SaveChangesAsync` as they are for now — the dispatcher is wired in Task 6.)

- [ ] **Step 4: Create the repository**

Create `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingRepository.cs`:

```csharp
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class LeagueStandingRepository(EngagementDbContext context) : ILeagueStandingRepository
{
    public Task<LeagueStanding?> GetAsync(LearnerId id, CancellationToken ct) =>
        context.LeagueStandings.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(LeagueStanding standing, CancellationToken ct) =>
        await context.LeagueStandings.AddAsync(standing, ct);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(
        LeagueTier tier, LeagueWeek week, CancellationToken ct)
    {
        // ThenBy(s => s.Id) gives ties a stable, deterministic order.
        return await context.LeagueStandings
            .Where(s => s.Tier == tier && s.Week == week)
            .OrderByDescending(s => s.WeeklyXp)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);
    }
}
```

> If EF Core cannot translate `OrderByDescending(s => s.WeeklyXp)` over the value-converted `Xp` (the Task-4 cohort test will surface this as a runtime `InvalidOperationException`), fall back to ordering after materialization: replace the query body with
> `var rows = await context.LeagueStandings.Where(s => s.Tier == tier && s.Week == week).ToListAsync(ct); return rows.OrderByDescending(s => s.WeeklyXp.Value).ThenBy(s => s.Id.Value).ToList();`

- [ ] **Step 5: Generate the migration**

Run (PowerShell):

```powershell
dotnet ef migrations add AddLeagueStanding `
  -p src/Modules/Engagement/Engagement.Infrastructure `
  -s src/Modules/Engagement/Engagement.Infrastructure -o Migrations
```

Expected: a new `*_AddLeagueStanding.cs` migration is created that builds an `engagement.LeagueStandings` table with columns `LearnerId` (uniqueidentifier, PK), `Tier` (nvarchar(16)), `WeekStart` (date), `WeeklyXp` (int). Open the generated file and confirm those columns before continuing.

- [ ] **Step 6: Write the failing persistence tests**

Create `tests/Engagement.Integration.Tests/Infrastructure/LeaguePersistenceTests.cs`:

```csharp
using Engagement.Domain;
using Engagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Engagement.Integration.Tests.Infrastructure;

public class LeaguePersistenceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_LeagueTest;Trusted_Connection=True;TrustServerCertificate=True";

    private static EngagementDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>().UseSqlServer(ConnectionString).Options;
        return new EngagementDbContext(options); // dispatcher defaults to null at this layer
    }

    public LeaguePersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static readonly DateTimeOffset Wed = new(2030, 1, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task League_standing_round_trips()
    {
        var id = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            var s = LeagueStanding.Create(id, LeagueWeek.Containing(Wed));
            s.RecordXp(42, Wed);
            await repo.AddAsync(s, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var reloaded = await new LeagueStandingRepository(ctx).GetAsync(id, CancellationToken.None);
            Assert.NotNull(reloaded);
            Assert.Equal(LeagueTier.Bronze, reloaded!.Tier);
            Assert.Equal(42, reloaded.WeeklyXp.Value);
            Assert.Equal(new DateOnly(2030, 1, 7), reloaded.Week.Start);
        }
    }

    [Fact]
    public async Task Cohort_query_filters_by_tier_and_week_and_orders_desc()
    {
        var week = LeagueWeek.Containing(Wed);
        var low = new LearnerId(Guid.NewGuid());
        var high = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            var a = LeagueStanding.Create(low, week); a.RecordXp(10, Wed);
            var b = LeagueStanding.Create(high, week); b.RecordXp(90, Wed);
            await repo.AddAsync(a, CancellationToken.None);
            await repo.AddAsync(b, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var cohort = await new LeagueStandingRepository(ctx)
                .GetCohortAsync(LeagueTier.Bronze, week, CancellationToken.None);
            Assert.Equal(2, cohort.Count);
            Assert.Equal(high.Value, cohort[0].Id.Value); // 90 ranks first
            Assert.Equal(low.Value, cohort[1].Id.Value);
        }
    }
}
```

- [ ] **Step 7: Run the persistence tests to verify they pass**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeaguePersistenceTests"`
Expected: PASS (2 tests). If `Cohort_query` throws an EF translation error, apply the fallback noted in Step 4 and re-run.

- [ ] **Step 8: Commit**

```bash
git add src/Modules/Engagement/Engagement.Domain/ILeagueStandingRepository.cs src/Modules/Engagement/Engagement.Infrastructure/ tests/Engagement.Integration.Tests/Infrastructure/LeaguePersistenceTests.cs
git commit -m "feat(leagues): persist LeagueStanding + cohort query (table, config, repo, migration)"
```

---

## Task 5: `RecordLeagueXpOnXpAwarded` handler

The domain-event handler that turns an `XpAwarded` into a weekly-score update.

**Files:**
- Create: `tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs`
- Create: `src/Modules/Engagement/Engagement.Application/RecordLeagueXpOnXpAwarded.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs`:

```csharp
using Engagement.Application;
using Engagement.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Application;

public class LeagueApplicationTests
{
    private sealed class InMemoryStandings : ILeagueStandingRepository
    {
        private readonly Dictionary<Guid, LeagueStanding> _store = new();

        public Task<LeagueStanding?> GetAsync(LearnerId id, CancellationToken ct) =>
            Task.FromResult(_store.GetValueOrDefault(id.Value));

        public Task AddAsync(LeagueStanding s, CancellationToken ct)
        {
            _store[s.Id.Value] = s;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(LeagueTier tier, LeagueWeek week, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LeagueStanding>>(
                _store.Values
                    .Where(s => s.Tier == tier && s.Week == week)
                    .OrderByDescending(s => s.WeeklyXp.Value)
                    .ThenBy(s => s.Id.Value)
                    .ToList());
    }

    private static FakeTimeProvider ClockAt(int y, int m, int d) =>
        new(new DateTimeOffset(y, m, d, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task XpAwarded_creates_a_bronze_standing_and_records_xp()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9); // Wed, week of Jan 7
        var handler = new RecordLeagueXpOnXpAwarded(repo, clock);
        var learner = Guid.NewGuid();

        await handler.HandleAsync(new XpAwarded(learner, 15, 15, DateTimeOffset.UtcNow), CancellationToken.None);

        var s = await repo.GetAsync(new LearnerId(learner), CancellationToken.None);
        Assert.NotNull(s);
        Assert.Equal(LeagueTier.Bronze, s!.Tier);
        Assert.Equal(15, s.WeeklyXp.Value);
        Assert.Equal(new DateOnly(2030, 1, 7), s.Week.Start);
    }

    [Fact]
    public async Task Two_awards_in_the_same_week_accumulate()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9);
        var handler = new RecordLeagueXpOnXpAwarded(repo, clock);
        var learner = Guid.NewGuid();

        await handler.HandleAsync(new XpAwarded(learner, 15, 15, DateTimeOffset.UtcNow), CancellationToken.None);
        clock.SetUtcNow(new DateTimeOffset(2030, 1, 10, 12, 0, 0, TimeSpan.Zero)); // Thu, same week
        await handler.HandleAsync(new XpAwarded(learner, 10, 25, DateTimeOffset.UtcNow), CancellationToken.None);

        var s = await repo.GetAsync(new LearnerId(learner), CancellationToken.None);
        Assert.Equal(25, s!.WeeklyXp.Value);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueApplicationTests"`
Expected: FAIL — compile error, `RecordLeagueXpOnXpAwarded` does not exist.

- [ ] **Step 3: Implement the handler**

Create `src/Modules/Engagement/Engagement.Application/RecordLeagueXpOnXpAwarded.cs`:

```csharp
using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

// Reacts to XpAwarded (a domain event) and credits the learner's weekly league score.
// The week comes from the injected clock at handling time (XpAwarded.OccurredOn is the
// award-processing instant, not a trustworthy earn-time). Mirrors the streak handler:
// the handler supplies the time; the aggregate stays clock-free.
public sealed class RecordLeagueXpOnXpAwarded(ILeagueStandingRepository repository, TimeProvider clock)
    : IDomainEventHandler<XpAwarded>
{
    public async Task HandleAsync(XpAwarded domainEvent, CancellationToken ct)
    {
        var id = new LearnerId(domainEvent.LearnerId);
        var now = clock.GetUtcNow();

        var standing = await repository.GetAsync(id, ct);
        if (standing is null)
        {
            standing = LeagueStanding.Create(id, LeagueWeek.Containing(now));
            await repository.AddAsync(standing, ct);
        }

        standing.RecordXp(domainEvent.Amount, now);
        await repository.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueApplicationTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Engagement/Engagement.Application/RecordLeagueXpOnXpAwarded.cs tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs
git commit -m "feat(leagues): record weekly XP on XpAwarded"
```

---

## Task 6: Wire the dispatcher into the unit of work + register the repository

Now make `XpAwarded` actually reach the handler: dispatch domain events after they are persisted, register `ILeagueStandingRepository`, and prove the full pipeline (complete a lesson → a league standing appears).

**Files:**
- Modify: `src/Modules/Engagement/Engagement.Infrastructure/EngagementDbContext.cs` (ctor + `SaveChangesAsync`)
- Modify: `src/Modules/Engagement/Engagement.Infrastructure/EngagementInfrastructureExtensions.cs` (register repo)
- Create: `tests/Engagement.Integration.Tests/EndToEnd/LeagueApiFactory.cs`
- Create: `tests/Engagement.Integration.Tests/EndToEnd/LeaguePipelineTests.cs`

- [ ] **Step 1: Write the failing integration test (with its factory)**

Create `tests/Engagement.Integration.Tests/EndToEnd/LeagueApiFactory.cs`:

```csharp
using Engagement.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

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
```

Create `tests/Engagement.Integration.Tests/EndToEnd/LeaguePipelineTests.cs`:

```csharp
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Engagement.Domain;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.EndToEnd;

public class LeaguePipelineTests(LeagueApiFactory factory) : IClassFixture<LeagueApiFactory>
{
    [Fact]
    public async Task Completing_a_lesson_creates_a_bronze_league_standing()
    {
        var learner = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.PublishAsync(new LessonCompleted(
                Guid.NewGuid(), learner, Guid.NewGuid(),
                new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ILeagueStandingRepository>();
            var s = await repo.GetAsync(new LearnerId(learner), CancellationToken.None);
            Assert.NotNull(s);
            Assert.Equal(LeagueTier.Bronze, s!.Tier);
            Assert.Equal(new DateOnly(2030, 1, 7), s.Week.Start);
            Assert.True(s.WeeklyXp.Value > 0); // the XP policy awards a positive amount per lesson
        }
    }

    [Fact]
    public async Task Re_delivered_lesson_does_not_double_count_weekly_xp()
    {
        var learner = Guid.NewGuid();
        // Same event identity on both deliveries → AwardXp's AppliedAward ledger blocks the
        // second award, so no second XpAwarded is raised and the weekly score must not grow.
        var lesson = new LessonCompleted(
            Guid.NewGuid(), learner, Guid.NewGuid(),
            new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero));

        async Task Deliver()
        {
            using var scope = factory.Services.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.PublishAsync(lesson);
        }

        async Task<int> WeeklyXpOf()
        {
            using var scope = factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ILeagueStandingRepository>();
            var s = await repo.GetAsync(new LearnerId(learner), CancellationToken.None);
            return s!.WeeklyXp.Value;
        }

        await Deliver();
        var afterFirst = await WeeklyXpOf();

        await Deliver(); // re-delivery of the identical event

        Assert.Equal(afterFirst, await WeeklyXpOf());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeaguePipelineTests"`
Expected: FAIL — `ILeagueStandingRepository` is not registered (resolves to null/throws), and/or no standing is created because the dispatcher isn't wired yet.

- [ ] **Step 3: Register the repository**

Edit `src/Modules/Engagement/Engagement.Infrastructure/EngagementInfrastructureExtensions.cs`. Add this line next to the other `AddScoped` repository registrations:

```csharp
        services.AddScoped<ILeagueStandingRepository, LeagueStandingRepository>();
```

- [ ] **Step 4: Wire the dispatcher into `EngagementDbContext`**

Replace `src/Modules/Engagement/Engagement.Infrastructure/EngagementDbContext.cs` with:

```csharp
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
```

> The dispatcher parameter is **optional** so the design-time factory (`new EngagementDbContext(options)`) and the persistence tests keep compiling and run with no dispatch. At runtime, `AddDbContext` resolves the registered `IDomainEventDispatcher` from the application's service provider and supplies it as the extra constructor argument. The Task-6 test fails if that injection doesn't happen, so this is verified, not assumed. **Fallback if it doesn't fire** (the pipeline test creates no standing because `dispatcher` stays null): inject `IServiceProvider` into the DbContext instead and resolve `serviceProvider.GetService<IDomainEventDispatcher>()` lazily inside `SaveChangesAsync`, keeping that parameter optional the same way.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeaguePipelineTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Run the full suite to confirm nothing regressed**

Run: `dotnet test`
Expected: PASS — all prior tests still green (existing XP/streak e2e tests now also create a league standing as a harmless side effect; no assertions break).

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Engagement/Engagement.Infrastructure/EngagementDbContext.cs src/Modules/Engagement/Engagement.Infrastructure/EngagementInfrastructureExtensions.cs tests/Engagement.Integration.Tests/EndToEnd/LeagueApiFactory.cs tests/Engagement.Integration.Tests/EndToEnd/LeaguePipelineTests.cs
git commit -m "feat(leagues): dispatch XpAwarded to league handler via the unit of work"
```

---

## Task 7: `GetLeagueLeaderboard` query + handler

The read model: my tier, the current week, the ranked cohort, and my rank.

**Files:**
- Add tests to: `tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs`
- Create: `src/Modules/Engagement/Engagement.Application/GetLeagueLeaderboard.cs`

- [ ] **Step 1: Write the failing tests**

Append these three tests inside the `LeagueApplicationTests` class in `tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs` (before the closing brace; they reuse the `InMemoryStandings` and `ClockAt` helpers already defined):

```csharp
    [Fact]
    public async Task Leaderboard_ranks_by_weekly_xp_desc_with_my_rank()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9); // week of Jan 7
        var record = new RecordLeagueXpOnXpAwarded(repo, clock);

        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();
        await record.HandleAsync(new XpAwarded(ana, 30, 30, DateTimeOffset.UtcNow), CancellationToken.None);
        await record.HandleAsync(new XpAwarded(bruno, 50, 50, DateTimeOffset.UtcNow), CancellationToken.None);

        var dto = await new GetLeagueLeaderboardHandler(repo, clock)
            .HandleAsync(new GetLeagueLeaderboard(ana), CancellationToken.None);

        Assert.Equal("Bronze", dto.Tier);
        Assert.Equal(new DateOnly(2030, 1, 7), dto.WeekStart);
        Assert.Equal(2, dto.Rows.Count);
        Assert.Equal(bruno, dto.Rows[0].LearnerId); // 50 ranks first
        Assert.Equal(ana, dto.Rows[1].LearnerId);
        Assert.Equal(2, dto.MyRank);                // ana is rank 2
    }

    [Fact]
    public async Task Unknown_learner_gets_the_bronze_board_with_null_rank()
    {
        var dto = await new GetLeagueLeaderboardHandler(new InMemoryStandings(), ClockAt(2030, 1, 9))
            .HandleAsync(new GetLeagueLeaderboard(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("Bronze", dto.Tier);
        Assert.Empty(dto.Rows);
        Assert.Null(dto.MyRank);
        Assert.Equal(new DateOnly(2030, 1, 7), dto.WeekStart);
    }

    [Fact]
    public async Task A_stale_week_standing_is_absent_from_the_current_board()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9); // week of Jan 7
        var learner = Guid.NewGuid();
        await new RecordLeagueXpOnXpAwarded(repo, clock)
            .HandleAsync(new XpAwarded(learner, 20, 20, DateTimeOffset.UtcNow), CancellationToken.None);

        clock.SetUtcNow(new DateTimeOffset(2030, 1, 16, 12, 0, 0, TimeSpan.Zero)); // next week (Jan 14)
        var dto = await new GetLeagueLeaderboardHandler(repo, clock)
            .HandleAsync(new GetLeagueLeaderboard(learner), CancellationToken.None);

        Assert.Empty(dto.Rows);
        Assert.Null(dto.MyRank);
        Assert.Equal(new DateOnly(2030, 1, 14), dto.WeekStart);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueApplicationTests"`
Expected: FAIL — compile error, `GetLeagueLeaderboard` / `GetLeagueLeaderboardHandler` do not exist.

- [ ] **Step 3: Implement the query slice**

Create `src/Modules/Engagement/Engagement.Application/GetLeagueLeaderboard.cs` (request + DTOs + handler in one file, per the project's file-organization convention):

```csharp
using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record GetLeagueLeaderboard(Guid LearnerId) : IRequest<LeaderboardDto>;

public sealed record LeaderboardDto(
    string Tier, DateOnly WeekStart, IReadOnlyList<LeaderboardRow> Rows, int? MyRank);

public sealed record LeaderboardRow(int Rank, Guid LearnerId, int WeeklyXp);

public sealed class GetLeagueLeaderboardHandler(ILeagueStandingRepository repository, TimeProvider clock)
    : IRequestHandler<GetLeagueLeaderboard, LeaderboardDto>
{
    public async Task<LeaderboardDto> HandleAsync(GetLeagueLeaderboard request, CancellationToken ct)
    {
        var id = new LearnerId(request.LearnerId);
        var currentWeek = LeagueWeek.Containing(clock.GetUtcNow());

        var mine = await repository.GetAsync(id, ct);
        var tier = mine?.Tier ?? LeagueTier.Bronze; // unknown learner defaults to Bronze

        var cohort = await repository.GetCohortAsync(tier, currentWeek, ct);

        var rows = cohort
            .Select((s, index) => new LeaderboardRow(index + 1, s.Id.Value, s.WeeklyXp.Value))
            .ToList();

        var myRank = rows.FirstOrDefault(r => r.LearnerId == request.LearnerId)?.Rank;

        return new LeaderboardDto(tier.ToString(), currentWeek.Start, rows, myRank);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueApplicationTests"`
Expected: PASS (5 tests total in the class).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Engagement/Engagement.Application/GetLeagueLeaderboard.cs tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs
git commit -m "feat(leagues): add GetLeagueLeaderboard read model"
```

---

## Task 8: `GET /me/league` endpoint

Expose the leaderboard over HTTP, learner from `ICurrentUser`.

**Files:**
- Modify: `src/Host/Program.cs` (add the endpoint)
- Create: `tests/Engagement.Integration.Tests/EndToEnd/LeagueApiTests.cs`

- [ ] **Step 1: Write the failing e2e test**

Create `tests/Engagement.Integration.Tests/EndToEnd/LeagueApiTests.cs`:

```csharp
using System.Net.Http.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.EndToEnd;

public class LeagueApiTests(LeagueApiFactory factory) : IClassFixture<LeagueApiFactory>
{
    private sealed record LeaderboardResponse(string Tier, DateOnly WeekStart, List<Row> Rows, int? MyRank);
    private sealed record Row(int Rank, Guid LearnerId, int WeeklyXp);

    private HttpClient ClientFor(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    private async Task CompleteLesson(Guid learnerId)
    {
        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.PublishAsync(new LessonCompleted(
            Guid.NewGuid(), learnerId, Guid.NewGuid(),
            new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task Leaderboard_ranks_learners_by_weekly_xp()
    {
        factory.Clock.SetUtcNow(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)); // week of Jan 7
        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();

        // Bruno completes two lessons, Ana one → Bruno has the higher weekly XP.
        await CompleteLesson(ana);
        await CompleteLesson(bruno);
        await CompleteLesson(bruno);

        var resp = await ClientFor(bruno).GetFromJsonAsync<LeaderboardResponse>("/me/league");

        Assert.NotNull(resp);
        Assert.Equal("Bronze", resp!.Tier);
        Assert.Equal(new DateOnly(2030, 1, 7), resp.WeekStart);
        Assert.Equal(bruno, resp.Rows[0].LearnerId); // ranked first
        Assert.Equal(1, resp.MyRank);
        Assert.True(resp.Rows[0].WeeklyXp > resp.Rows[1].WeeklyXp);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueApiTests"`
Expected: FAIL — `GET /me/league` returns 404 (route not mapped), so deserialization/assertion fails.

- [ ] **Step 3: Add the endpoint**

Edit `src/Host/Program.cs`. Add this endpoint next to the other `/me/...` maps (e.g. right after the `app.MapGet("/me/streak", ...)` block):

```csharp
app.MapGet("/me/league",
    async (ICurrentUser user, IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.SendAsync(new GetLeagueLeaderboard(user.LearnerId), ct)));
```

(`GetLeagueLeaderboard` is in `Engagement.Application`, which `Program.cs` already imports via `using Engagement.Application;`.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueApiTests"`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/Host/Program.cs tests/Engagement.Integration.Tests/EndToEnd/LeagueApiTests.cs
git commit -m "feat(leagues): expose GET /me/league leaderboard endpoint"
```

---

## Task 9: Full verification against the spec

**Files:** none (verification only).

- [ ] **Step 1: Build clean**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s). 0 Error(s)`.

- [ ] **Step 2: Run the entire test suite**

Run: `dotnet test`
Expected: PASS — every test green, including the existing XP/streak/architecture tests (acceptance criterion 7) and the architecture tests confirming `Engagement.Domain` depends on nothing infrastructural (acceptance criterion 8).

- [ ] **Step 3: Confirm spec acceptance criteria**

Eyeball the suite against the spec's acceptance criteria and confirm each maps to a passing test:
1. Earning XP creates/updates a Bronze standing for the current UTC week → `LeagueApplicationTests.XpAwarded_creates_a_bronze_standing_and_records_xp`, `LeaguePipelineTests`.
2. New week resets weekly XP → `LeagueStandingTests.RecordXp_in_a_later_week_resets_the_total`.
3. `GET /me/league` returns tier + week + ranked board + my rank → `LeagueApiTests.Leaderboard_ranks_learners_by_weekly_xp`.
4. Re-delivered `LessonCompleted` does not double-count → covered by the `AppliedAward` idempotency (no second `XpAwarded`); existing `EngagementApiTests`/idempotency tests stay green.
5. Read never mutates; stale week absent → `LeagueApplicationTests.A_stale_week_standing_is_absent_from_the_current_board`.
6. Unknown learner → Bronze board, null rank → `LeagueApplicationTests.Unknown_learner_gets_the_bronze_board_with_null_rank`.
7. Dispatcher delivers `XpAwarded`; existing tests unchanged → `LeaguePipelineTests` + full green suite.
8. Domain references nothing infrastructural → `ArchitectureTests.Domain_does_not_depend_on_EfCore_or_AspNetCore`.

- [ ] **Step 4: Update the status note in `CLAUDE.md`**

In the `## Status` section of `CLAUDE.md`, replace the `⏭️ **Next:**` line with a completed-sub-project entry plus a new next line, consistent with the existing bullets:

```markdown
- ✅ **Sub-project 4 — Leagues, Slice 1 (skeleton)** (PR #4): weekly XP accumulation as a
  per-learner `LeagueStanding` (Bronze, UTC week, lazy roll), fed by `XpAwarded` through a new
  in-process **domain-event dispatcher**; leaderboard read at `GET /me/league`.
- ⏭️ **Next:** leagues Slice 2 (settlement — cohort-wide promotion/demotion at week close) →
  real Learning engine → real Identity.
```

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: mark leagues Slice 1 (skeleton) complete; next is settlement"
```

---

## Notes for the implementer

- **TDD discipline:** every code task is red → green → commit. Don't write implementation before its failing test.
- **The week is always UTC** and always anchored to Monday. Both league handlers get "now" from the injected `TimeProvider`; never use `DateTimeOffset.UtcNow` directly in handlers (tests rely on the `FakeTimeProvider`).
- **Re-entrancy:** the league handler calls `SaveChangesAsync`, which re-enters `EngagementDbContext.SaveChangesAsync` while dispatching — the `_dispatching` guard makes the inner call persist-only. Don't remove it.
- **EF value-converter translation:** if a cohort query throws at runtime, use the materialize-then-sort fallback in Task 4, Step 4.
- **No Slice-2 work here:** no promotion/demotion, no tiers above Bronze, no scheduler. If you find yourself adding those, stop — they belong to the next slice.
```
