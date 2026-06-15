# Leagues — Slice 2 (Settlement) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close a league week by ranking each tier's cohort and promoting the top `floor(20%)` / demoting the bottom `floor(20%)` (Bronze floor, Diamond summit), via an explicit, idempotent `SettleLeagueWeek` command — built on a per-(learner, week) standing so week-N history survives.

**Architecture:** Reshape `LeagueStanding` to identity `(LearnerId, Week)` (history preserved; `RecordXp` becomes add-only, week selection moves to the handler with tier carry-forward). Add ladder movement to `LeagueTier`, subscriber-less `Promoted`/`Demoted` domain events, and a per-week settled marker for idempotency. Settlement is a cross-aggregate application service behind a `POST /leagues/weeks/{weekStart}/settle` seam; the automatic trigger is deferred.

**Tech Stack:** C# / .NET 10, EF Core 10 (SQL Server LocalDB, composite key with value-converted members), the hand-rolled mediator + domain-event dispatcher, xUnit + `FakeTimeProvider`, NetArchTest.

**Spec:** [`docs/superpowers/specs/2026-06-12-leagues-settlement-design.md`](../specs/2026-06-12-leagues-settlement-design.md)

> **Note on the reshape (Task 2):** changing `LeagueStanding`'s identity is a coordinated change — the aggregate, EF config, repo, handler, leaderboard, and Slice 1's *league* tests must all move together to compile. Task 2 stages it domain-first (Domain.Tests compile against `Engagement.Domain` alone and go green) before updating Infrastructure/Application/Host and their tests. The single commit lands only when the whole suite is green.

---

## Task 1: `LeagueTier` ladder movement

Pure, additive — `Next()`/`Previous()` with edge clamping. No existing code breaks.

**Files:**
- Modify: `src/Modules/Engagement/Engagement.Domain/LeagueTier.cs`
- Create: `tests/Engagement.Domain.Tests/LeagueTierTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Engagement.Domain.Tests/LeagueTierTests.cs`:

```csharp
using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LeagueTierTests
{
    [Fact]
    public void Next_steps_up_one_tier()
    {
        Assert.Equal(LeagueTier.Gold, LeagueTier.Silver.Next());
    }

    [Fact]
    public void Previous_steps_down_one_tier()
    {
        Assert.Equal(LeagueTier.Silver, LeagueTier.Gold.Previous());
    }

    [Fact]
    public void Diamond_does_not_promote_above_itself()
    {
        Assert.Equal(LeagueTier.Diamond, LeagueTier.Diamond.Next());
    }

    [Fact]
    public void Bronze_does_not_demote_below_itself()
    {
        Assert.Equal(LeagueTier.Bronze, LeagueTier.Bronze.Previous());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Domain.Tests --filter "FullyQualifiedName~LeagueTierTests"`
Expected: FAIL — `Next`/`Previous` don't exist.

- [ ] **Step 3: Add the ladder-movement helpers**

Replace `src/Modules/Engagement/Engagement.Domain/LeagueTier.cs` with:

```csharp
namespace Engagement.Domain;

// The 10-tier ladder, ascending. Bronze is the floor, Diamond the summit.
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

public static class LeagueTierExtensions
{
    // Up one tier; Diamond (summit) has nowhere higher, so it stays.
    public static LeagueTier Next(this LeagueTier tier) =>
        tier == LeagueTier.Diamond ? tier : tier + 1;

    // Down one tier; Bronze (floor) has nowhere lower, so it stays.
    public static LeagueTier Previous(this LeagueTier tier) =>
        tier == LeagueTier.Bronze ? tier : tier - 1;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Domain.Tests --filter "FullyQualifiedName~LeagueTierTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Engagement/Engagement.Domain/LeagueTier.cs tests/Engagement.Domain.Tests/LeagueTierTests.cs
git commit -m "feat(leagues): add LeagueTier ladder movement (Next/Previous, edge-clamped)"
```

---

## Task 2: Reshape `LeagueStanding` to per-(learner, week)

The coordinated reshape. Staged domain-first, then infra/app/host. One commit at the end, fully green.

**Files:**
- Modify: `src/Modules/Engagement/Engagement.Domain/LeagueStanding.cs`
- Modify: `src/Modules/Engagement/Engagement.Domain/ILeagueStandingRepository.cs`
- Modify: `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingConfiguration.cs`
- Modify: `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingRepository.cs`
- Modify: `src/Modules/Engagement/Engagement.Application/RecordLeagueXpOnXpAwarded.cs`
- Modify: `src/Modules/Engagement/Engagement.Application/GetLeagueLeaderboard.cs`
- Migration (generated): `...Infrastructure/Migrations/*_ReshapeLeagueStandingKey.cs`
- Rewrite: `tests/Engagement.Domain.Tests/LeagueStandingTests.cs`
- Rewrite: `tests/Engagement.Integration.Tests/Infrastructure/LeaguePersistenceTests.cs`
- Rewrite: `tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs`
- Modify: `tests/Engagement.Integration.Tests/EndToEnd/LeaguePipelineTests.cs`

- [ ] **Step 1: Rewrite the domain tests for the new shape (red)**

Replace `tests/Engagement.Domain.Tests/LeagueStandingTests.cs` with:

```csharp
using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LeagueStandingTests
{
    private static readonly LeagueWeek Wk = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)); // wk of Jan 7

    private static LeagueStanding NewBronze() =>
        LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Bronze);

    [Fact]
    public void Create_sets_identity_tier_and_zero_xp()
    {
        var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Gold);
        Assert.Equal(LeagueTier.Gold, s.Tier);
        Assert.Equal(new DateOnly(2030, 1, 7), s.Week.Start);
        Assert.Equal(0, s.WeeklyXp.Value);
    }

    [Fact]
    public void RecordXp_accumulates_on_this_weeks_row()
    {
        var s = NewBronze();
        s.RecordXp(15);
        s.RecordXp(10);
        Assert.Equal(25, s.WeeklyXp.Value);
    }

    [Fact]
    public void RecordXp_rejects_a_negative_amount()
    {
        var s = NewBronze();
        Assert.Throws<ArgumentOutOfRangeException>(() => s.RecordXp(-1));
    }
}
```

- [ ] **Step 2: Run the domain tests to verify they fail**

Run: `dotnet test tests/Engagement.Domain.Tests --filter "FullyQualifiedName~LeagueStandingTests"`
Expected: FAIL — `Create` has the old 2-arg shape and `RecordXp` still takes `asOfUtc`.

- [ ] **Step 3: Reshape the aggregate**

Replace `src/Modules/Engagement/Engagement.Domain/LeagueStanding.cs` with:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

// One aggregate per (learner, week). A row is a single fixed week, so RecordXp just adds —
// the lazy week-roll moved to the handler (which find-or-creates the current week's row).
// Identity is (Id, Week); both are value objects with value-equality (via ValueObject), which
// is what makes the EF composite key safe.
public sealed class LeagueStanding : AggregateRoot
{
    public LearnerId Id { get; private set; } = default!;
    public LeagueWeek Week { get; private set; } = default!;
    public LeagueTier Tier { get; private set; }
    public Xp WeeklyXp { get; private set; } = Xp.Zero;

    private LeagueStanding() { } // EF

    public static LeagueStanding Create(LearnerId id, LeagueWeek week, LeagueTier tier) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        Week = week ?? throw new ArgumentNullException(nameof(week)),
        Tier = tier,
        WeeklyXp = Xp.Zero
    };

    public void RecordXp(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "XP amount cannot be negative.");
        WeeklyXp = new Xp(WeeklyXp.Value + amount);
    }
}
```

- [ ] **Step 4: Run the domain tests to verify they pass**

Run: `dotnet test tests/Engagement.Domain.Tests --filter "FullyQualifiedName~LeagueStandingTests"`
Expected: PASS (3 tests). (`Engagement.Domain` compiles alone; the rest of the solution does not yet — that's expected mid-reshape. Don't run the full build until Step 12.)

- [ ] **Step 5: Update the repository port**

Replace `src/Modules/Engagement/Engagement.Domain/ILeagueStandingRepository.cs` with:

```csharp
namespace Engagement.Domain;

public interface ILeagueStandingRepository
{
    Task<LeagueStanding?> GetAsync(LearnerId id, LeagueWeek week, CancellationToken ct);
    Task<LeagueStanding?> GetMostRecentAsync(LearnerId id, CancellationToken ct);
    Task AddAsync(LeagueStanding standing, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);

    // The cohort = everyone in a tier for a given week, ranked by weekly XP descending.
    Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(LeagueTier tier, LeagueWeek week, CancellationToken ct);
}
```

- [ ] **Step 6: Update the EF configuration to a composite key**

Replace the body of `Configure` in `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingConfiguration.cs` with:

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

        // Composite key (LearnerId, WeekStart). Each member is a value-converted VO with
        // value-equality, so EF tracks the key correctly without a custom ValueComparer.
        builder.HasKey(s => new { s.Id, s.Week });

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new LearnerId(value))
            .HasColumnName("LearnerId")
            .ValueGeneratedNever();

        builder.Property(s => s.Week)
            .HasConversion(w => w.Start, value => new LeagueWeek(value))
            .HasColumnName("WeekStart")
            .ValueGeneratedNever();

        builder.Property(s => s.Tier)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(s => s.WeeklyXp)
            .HasConversion(xp => xp.Value, value => new Xp(value))
            .HasColumnName("WeeklyXp");

        builder.Ignore(s => s.DomainEvents);
    }
}
```

- [ ] **Step 7: Update the EF repository**

Replace `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingRepository.cs` with:

```csharp
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class LeagueStandingRepository(EngagementDbContext context) : ILeagueStandingRepository
{
    public Task<LeagueStanding?> GetAsync(LearnerId id, LeagueWeek week, CancellationToken ct) =>
        context.LeagueStandings.FirstOrDefaultAsync(s => s.Id == id && s.Week == week, ct);

    public Task<LeagueStanding?> GetMostRecentAsync(LearnerId id, CancellationToken ct) =>
        context.LeagueStandings
            .Where(s => s.Id == id)
            .OrderByDescending(s => s.Week)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(LeagueStanding standing, CancellationToken ct) =>
        await context.LeagueStandings.AddAsync(standing, ct);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(
        LeagueTier tier, LeagueWeek week, CancellationToken ct)
    {
        return await context.LeagueStandings
            .Where(s => s.Tier == tier && s.Week == week)
            .OrderByDescending(s => s.WeeklyXp)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);
    }
}
```

- [ ] **Step 8: Update the XP handler (find-or-create + carry-forward tier)**

Replace `src/Modules/Engagement/Engagement.Application/RecordLeagueXpOnXpAwarded.cs` with:

```csharp
using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

// Reacts to XpAwarded and credits the learner's CURRENT-week row. The week comes from the
// injected clock; on a brand-new week's first activity the row is created carrying the learner's
// most-recent tier forward (Bronze if they've never played).
public sealed class RecordLeagueXpOnXpAwarded(ILeagueStandingRepository repository, TimeProvider clock)
    : IDomainEventHandler<XpAwarded>
{
    public async Task HandleAsync(XpAwarded domainEvent, CancellationToken ct)
    {
        var id = new LearnerId(domainEvent.LearnerId);
        var week = LeagueWeek.Containing(clock.GetUtcNow());

        var standing = await repository.GetAsync(id, week, ct);
        if (standing is null)
        {
            var prior = await repository.GetMostRecentAsync(id, ct);
            var tier = prior?.Tier ?? LeagueTier.Bronze;
            standing = LeagueStanding.Create(id, week, tier);
            await repository.AddAsync(standing, ct);
        }

        standing.RecordXp(domainEvent.Amount);
        await repository.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 9: Update the leaderboard tier resolution**

In `src/Modules/Engagement/Engagement.Application/GetLeagueLeaderboard.cs`, replace the `HandleAsync` body of `GetLeagueLeaderboardHandler` with:

```csharp
    public async Task<LeaderboardDto> HandleAsync(GetLeagueLeaderboard request, CancellationToken ct)
    {
        var id = new LearnerId(request.LearnerId);
        var currentWeek = LeagueWeek.Containing(clock.GetUtcNow());

        // Tier for the board: this week's row if present, else carry-forward from the latest row, else Bronze.
        var thisWeek = await repository.GetAsync(id, currentWeek, ct);
        var tier = thisWeek?.Tier
                   ?? (await repository.GetMostRecentAsync(id, ct))?.Tier
                   ?? LeagueTier.Bronze;

        var cohort = await repository.GetCohortAsync(tier, currentWeek, ct);

        var rows = cohort
            .Select((s, index) => new LeaderboardRow(index + 1, s.Id.Value, s.WeeklyXp.Value))
            .ToList();

        var myRank = rows.FirstOrDefault(r => r.LearnerId == request.LearnerId)?.Rank;

        return new LeaderboardDto(tier.ToString(), currentWeek.Start, rows, myRank);
    }
```

(The records `GetLeagueLeaderboard`, `LeaderboardDto`, `LeaderboardRow` are unchanged.)

- [ ] **Step 10: Generate the key-change migration**

Run (PowerShell):

```powershell
dotnet ef migrations add ReshapeLeagueStandingKey `
  -p src/Modules/Engagement/Engagement.Infrastructure `
  -s src/Modules/Engagement/Engagement.Infrastructure -o Migrations
```

Expected: a migration that drops the `LearnerId`-only PK and adds a composite PK on `(LearnerId, WeekStart)`. Open it and confirm it contains `DropPrimaryKey` + `AddPrimaryKey("PK_LeagueStandings", … new[] { "LearnerId", "WeekStart" })`.

- [ ] **Step 11: Rewrite the league integration tests for the new model**

Replace `tests/Engagement.Integration.Tests/Infrastructure/LeaguePersistenceTests.cs` with:

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
        return new EngagementDbContext(options);
    }

    public LeaguePersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static readonly LeagueWeek Wk1 = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero));  // Jan 7
    private static readonly LeagueWeek Wk2 = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 16, 12, 0, 0, TimeSpan.Zero)); // Jan 14

    [Fact]
    public async Task League_standing_round_trips()
    {
        var id = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            var s = LeagueStanding.Create(id, Wk1, LeagueTier.Gold);
            s.RecordXp(42);
            await repo.AddAsync(s, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var reloaded = await new LeagueStandingRepository(ctx).GetAsync(id, Wk1, CancellationToken.None);
            Assert.NotNull(reloaded);
            Assert.Equal(LeagueTier.Gold, reloaded!.Tier);
            Assert.Equal(42, reloaded.WeeklyXp.Value);
        }
    }

    [Fact]
    public async Task Same_learner_has_a_row_per_week_and_most_recent_wins()
    {
        var id = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            await repo.AddAsync(LeagueStanding.Create(id, Wk1, LeagueTier.Bronze), CancellationToken.None);
            await repo.AddAsync(LeagueStanding.Create(id, Wk2, LeagueTier.Silver), CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            Assert.Equal(LeagueTier.Bronze, (await repo.GetAsync(id, Wk1, CancellationToken.None))!.Tier);
            Assert.Equal(LeagueTier.Silver, (await repo.GetMostRecentAsync(id, CancellationToken.None))!.Tier);
        }
    }

    [Fact]
    public async Task Cohort_query_filters_by_tier_and_week_and_orders_desc()
    {
        var low = new LearnerId(Guid.NewGuid());
        var high = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            var a = LeagueStanding.Create(low, Wk1, LeagueTier.Bronze); a.RecordXp(10);
            var b = LeagueStanding.Create(high, Wk1, LeagueTier.Bronze); b.RecordXp(90);
            await repo.AddAsync(a, CancellationToken.None);
            await repo.AddAsync(b, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var cohort = await new LeagueStandingRepository(ctx)
                .GetCohortAsync(LeagueTier.Bronze, Wk1, CancellationToken.None);
            Assert.Equal(high.Value, cohort[0].Id.Value);
            Assert.Equal(low.Value, cohort[1].Id.Value);
        }
    }
}
```

Replace `tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs` with:

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
        // keyed by (learnerId, weekStart)
        private readonly Dictionary<(Guid, DateOnly), LeagueStanding> _store = new();

        public Task<LeagueStanding?> GetAsync(LearnerId id, LeagueWeek week, CancellationToken ct) =>
            Task.FromResult(_store.GetValueOrDefault((id.Value, week.Start)));

        public Task<LeagueStanding?> GetMostRecentAsync(LearnerId id, CancellationToken ct) =>
            Task.FromResult(_store.Values
                .Where(s => s.Id.Value == id.Value)
                .OrderByDescending(s => s.Week.Start)
                .FirstOrDefault());

        public Task AddAsync(LeagueStanding s, CancellationToken ct)
        {
            _store[(s.Id.Value, s.Week.Start)] = s;
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
    public async Task XpAwarded_creates_a_bronze_row_for_the_current_week()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9);
        var learner = Guid.NewGuid();

        await new RecordLeagueXpOnXpAwarded(repo, clock)
            .HandleAsync(new XpAwarded(learner, 15, 15, DateTimeOffset.UtcNow), CancellationToken.None);

        var s = await repo.GetAsync(new LearnerId(learner), LeagueWeek.Containing(clock.GetUtcNow()), CancellationToken.None);
        Assert.NotNull(s);
        Assert.Equal(LeagueTier.Bronze, s!.Tier);
        Assert.Equal(15, s.WeeklyXp.Value);
    }

    [Fact]
    public async Task A_new_week_creates_a_new_row_carrying_the_prior_tier_forward()
    {
        var repo = new InMemoryStandings();
        var learner = new LearnerId(Guid.NewGuid());
        // Seed a week-1 Gold row directly.
        await repo.AddAsync(LeagueStanding.Create(learner, ClockAtWeek(2030, 1, 9), LeagueTier.Gold), CancellationToken.None);

        var clock = ClockAt(2030, 1, 16); // next week
        await new RecordLeagueXpOnXpAwarded(repo, clock)
            .HandleAsync(new XpAwarded(learner.Value, 5, 5, DateTimeOffset.UtcNow), CancellationToken.None);

        var s = await repo.GetAsync(learner, LeagueWeek.Containing(clock.GetUtcNow()), CancellationToken.None);
        Assert.Equal(LeagueTier.Gold, s!.Tier); // carried forward
        Assert.Equal(5, s.WeeklyXp.Value);

        static LeagueWeek ClockAtWeek(int y, int m, int d) =>
            LeagueWeek.Containing(new DateTimeOffset(y, m, d, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Leaderboard_ranks_by_weekly_xp_desc_with_my_rank()
    {
        var repo = new InMemoryStandings();
        var clock = ClockAt(2030, 1, 9);
        var record = new RecordLeagueXpOnXpAwarded(repo, clock);
        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();
        await record.HandleAsync(new XpAwarded(ana, 30, 30, DateTimeOffset.UtcNow), CancellationToken.None);
        await record.HandleAsync(new XpAwarded(bruno, 50, 50, DateTimeOffset.UtcNow), CancellationToken.None);

        var dto = await new GetLeagueLeaderboardHandler(repo, clock)
            .HandleAsync(new GetLeagueLeaderboard(ana), CancellationToken.None);

        Assert.Equal("Bronze", dto.Tier);
        Assert.Equal(new DateOnly(2030, 1, 7), dto.WeekStart);
        Assert.Equal(bruno, dto.Rows[0].LearnerId);
        Assert.Equal(2, dto.MyRank);
    }

    [Fact]
    public async Task Unknown_learner_gets_the_bronze_board_with_null_rank()
    {
        var dto = await new GetLeagueLeaderboardHandler(new InMemoryStandings(), ClockAt(2030, 1, 9))
            .HandleAsync(new GetLeagueLeaderboard(Guid.NewGuid()), CancellationToken.None);
        Assert.Equal("Bronze", dto.Tier);
        Assert.Empty(dto.Rows);
        Assert.Null(dto.MyRank);
    }
}
```

In `tests/Engagement.Integration.Tests/EndToEnd/LeaguePipelineTests.cs`, two fixes:
1. Both helpers fetch the standing with the old single-arg `GetAsync`. Replace each `repo.GetAsync(new LearnerId(learner), CancellationToken.None)` call with `repo.GetMostRecentAsync(new LearnerId(learner), CancellationToken.None)`. (Two occurrences: in `Completing_a_lesson_creates_a_bronze_league_standing` and in the `WeeklyXpOf()` local of `Re_delivered_lesson_does_not_double_count_weekly_xp`.)
2. **Test isolation:** these tests previously relied on the factory's *default* clock, but the league e2e classes now share one factory (one mutable `FakeTimeProvider`) via the `"League E2E"` collection, and `LeagueSettlementApiTests` moves that clock to March. Add `factory.Clock.SetUtcNow(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero));` as the **first line of each `[Fact]`** in `LeaguePipelineTests` so they no longer depend on another test's clock state (and `Completing_a_lesson_creates_a_bronze_league_standing`'s `Week.Start == 2030-01-07` assertion stays deterministic).

- [ ] **Step 12: Build and run the full suite green**

Run: `dotnet test`
Expected: PASS — the whole suite, including all XP/streak/mediator/architecture tests untouched and the rewritten league tests. If `ReshapeLeagueStandingKey` fails to apply, confirm the migration's drop/add-primary-key against `(LearnerId, WeekStart)`.

- [ ] **Step 13: Commit**

```bash
git add src/Modules/Engagement/ tests/Engagement.Domain.Tests/LeagueStandingTests.cs tests/Engagement.Integration.Tests/Infrastructure/LeaguePersistenceTests.cs tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs tests/Engagement.Integration.Tests/EndToEnd/LeaguePipelineTests.cs
git commit -m "refactor(leagues): reshape LeagueStanding to per-(learner, week) rows"
```

---

## Task 3: `Promoted`/`Demoted` events + `PlaceInto`

Additive on the reshaped aggregate — settlement uses these to move a learner.

**Files:**
- Create: `src/Modules/Engagement/Engagement.Domain/Promoted.cs`
- Create: `src/Modules/Engagement/Engagement.Domain/Demoted.cs`
- Modify: `src/Modules/Engagement/Engagement.Domain/LeagueStanding.cs`
- Modify: `tests/Engagement.Domain.Tests/LeagueStandingTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to the `LeagueStandingTests` class (before its closing brace) in `tests/Engagement.Domain.Tests/LeagueStandingTests.cs`:

```csharp
    [Fact]
    public void PlaceInto_a_higher_tier_raises_Promoted()
    {
        var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Gold);
        s.PlaceInto(LeagueTier.Sapphire);
        Assert.Equal(LeagueTier.Sapphire, s.Tier);
        var evt = Assert.Single(s.DomainEvents.OfType<Promoted>());
        Assert.Equal(LeagueTier.Gold, evt.From);
        Assert.Equal(LeagueTier.Sapphire, evt.To);
    }

    [Fact]
    public void PlaceInto_a_lower_tier_raises_Demoted()
    {
        var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Gold);
        s.PlaceInto(LeagueTier.Silver);
        Assert.Equal(LeagueTier.Silver, s.Tier);
        Assert.Single(s.DomainEvents.OfType<Demoted>());
    }

    [Fact]
    public void PlaceInto_the_same_tier_is_a_no_op()
    {
        var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Wk, LeagueTier.Gold);
        s.PlaceInto(LeagueTier.Gold);
        Assert.Empty(s.DomainEvents);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Domain.Tests --filter "FullyQualifiedName~LeagueStandingTests"`
Expected: FAIL — `PlaceInto`, `Promoted`, `Demoted` don't exist.

- [ ] **Step 3: Create the events**

Create `src/Modules/Engagement/Engagement.Domain/Promoted.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed record Promoted(
    Guid LearnerId, LeagueTier From, LeagueTier To, DateOnly Week, DateTimeOffset OccurredOn) : IDomainEvent;
```

Create `src/Modules/Engagement/Engagement.Domain/Demoted.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed record Demoted(
    Guid LearnerId, LeagueTier From, LeagueTier To, DateOnly Week, DateTimeOffset OccurredOn) : IDomainEvent;
```

- [ ] **Step 4: Add `PlaceInto` to the aggregate**

In `src/Modules/Engagement/Engagement.Domain/LeagueStanding.cs`, add this method after `RecordXp`:

```csharp
    // Settlement moves a learner to their next-week tier. Raises Promoted/Demoted (no subscriber
    // yet — same pattern as StreakAdvanced/StreakFrozen). OccurredOn matches the XpAwarded
    // convention of stamping the processing instant.
    public void PlaceInto(LeagueTier tier)
    {
        if (tier == Tier)
            return;

        var from = Tier;
        Tier = tier;
        RaiseDomainEvent(tier > from
            ? new Promoted(Id.Value, from, tier, Week.Start, DateTimeOffset.UtcNow)
            : new Demoted(Id.Value, from, tier, Week.Start, DateTimeOffset.UtcNow));
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Domain.Tests --filter "FullyQualifiedName~LeagueStandingTests"`
Expected: PASS (6 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Engagement/Engagement.Domain/Promoted.cs src/Modules/Engagement/Engagement.Domain/Demoted.cs src/Modules/Engagement/Engagement.Domain/LeagueStanding.cs tests/Engagement.Domain.Tests/LeagueStandingTests.cs
git commit -m "feat(leagues): add PlaceInto + Promoted/Demoted domain events"
```

---

## Task 4: `LeagueWeekSettlement` marker (idempotency)

A tiny per-week marker + repo + table.

**Files:**
- Create: `src/Modules/Engagement/Engagement.Domain/LeagueWeekSettlement.cs`
- Create: `src/Modules/Engagement/Engagement.Domain/ILeagueWeekSettlementRepository.cs`
- Modify: `src/Modules/Engagement/Engagement.Domain/LeagueWeek.cs` (add `Next()`)
- Create: `src/Modules/Engagement/Engagement.Infrastructure/LeagueWeekSettlementConfiguration.cs`
- Create: `src/Modules/Engagement/Engagement.Infrastructure/LeagueWeekSettlementRepository.cs`
- Modify: `src/Modules/Engagement/Engagement.Infrastructure/EngagementDbContext.cs`
- Modify: `src/Modules/Engagement/Engagement.Infrastructure/EngagementInfrastructureExtensions.cs`
- Migration (generated): `*_AddLeagueWeekSettlement.cs`
- Create: `tests/Engagement.Integration.Tests/Infrastructure/LeagueWeekSettlementPersistenceTests.cs`

- [ ] **Step 1: Add `LeagueWeek.Next()` (needed to place movers into next week)**

In `src/Modules/Engagement/Engagement.Domain/LeagueWeek.cs`, add this method after `Containing`:

```csharp
    public LeagueWeek Next() => new(Start.AddDays(7));
```

- [ ] **Step 2: Create the marker aggregate**

Create `src/Modules/Engagement/Engagement.Domain/LeagueWeekSettlement.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

// One row per settled week — the idempotency marker. Its existence means "week already settled."
public sealed class LeagueWeekSettlement : AggregateRoot
{
    public LeagueWeek Week { get; private set; } = default!;
    public DateTimeOffset SettledAt { get; private set; }

    private LeagueWeekSettlement() { } // EF

    public LeagueWeekSettlement(LeagueWeek week, DateTimeOffset settledAt)
    {
        Week = week ?? throw new ArgumentNullException(nameof(week));
        SettledAt = settledAt;
    }
}
```

- [ ] **Step 3: Create the marker repository port**

Create `src/Modules/Engagement/Engagement.Domain/ILeagueWeekSettlementRepository.cs`:

```csharp
namespace Engagement.Domain;

public interface ILeagueWeekSettlementRepository
{
    Task<bool> ExistsAsync(LeagueWeek week, CancellationToken ct);
    Task AddAsync(LeagueWeekSettlement marker, CancellationToken ct);
}
```

- [ ] **Step 4: Create the EF configuration**

Create `src/Modules/Engagement/Engagement.Infrastructure/LeagueWeekSettlementConfiguration.cs`:

```csharp
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Engagement.Infrastructure;

internal sealed class LeagueWeekSettlementConfiguration : IEntityTypeConfiguration<LeagueWeekSettlement>
{
    public void Configure(EntityTypeBuilder<LeagueWeekSettlement> builder)
    {
        builder.ToTable("LeagueWeekSettlements");

        builder.HasKey(s => s.Week);
        builder.Property(s => s.Week)
            .HasConversion(w => w.Start, value => new LeagueWeek(value))
            .HasColumnName("WeekStart")
            .ValueGeneratedNever();

        builder.Property(s => s.SettledAt);

        builder.Ignore(s => s.DomainEvents);
    }
}
```

- [ ] **Step 5: Create the repository**

Create `src/Modules/Engagement/Engagement.Infrastructure/LeagueWeekSettlementRepository.cs`:

```csharp
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class LeagueWeekSettlementRepository(EngagementDbContext context) : ILeagueWeekSettlementRepository
{
    public Task<bool> ExistsAsync(LeagueWeek week, CancellationToken ct) =>
        context.LeagueWeekSettlements.AnyAsync(s => s.Week == week, ct);

    public async Task AddAsync(LeagueWeekSettlement marker, CancellationToken ct) =>
        await context.LeagueWeekSettlements.AddAsync(marker, ct);
}
```

- [ ] **Step 6: Register the DbSet, configuration, and repository**

In `src/Modules/Engagement/Engagement.Infrastructure/EngagementDbContext.cs`, add the DbSet after `LeagueStandings`:

```csharp
    public DbSet<LeagueWeekSettlement> LeagueWeekSettlements => Set<LeagueWeekSettlement>();
```

and the configuration line after `LeagueStandingConfiguration`:

```csharp
        modelBuilder.ApplyConfiguration(new LeagueWeekSettlementConfiguration());
```

In `src/Modules/Engagement/Engagement.Infrastructure/EngagementInfrastructureExtensions.cs`, add after the league-standing registration:

```csharp
        services.AddScoped<ILeagueWeekSettlementRepository, LeagueWeekSettlementRepository>();
```

- [ ] **Step 7: Generate the migration**

Run (PowerShell):

```powershell
dotnet ef migrations add AddLeagueWeekSettlement `
  -p src/Modules/Engagement/Engagement.Infrastructure `
  -s src/Modules/Engagement/Engagement.Infrastructure -o Migrations
```

Expected: creates `engagement.LeagueWeekSettlements` with `WeekStart` (date, PK) and `SettledAt` (datetimeoffset).

- [ ] **Step 8: Write the failing persistence test**

Create `tests/Engagement.Integration.Tests/Infrastructure/LeagueWeekSettlementPersistenceTests.cs`:

```csharp
using Engagement.Domain;
using Engagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Engagement.Integration.Tests.Infrastructure;

public class LeagueWeekSettlementPersistenceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_LeagueSettleTest;Trusted_Connection=True;TrustServerCertificate=True";

    private static EngagementDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>().UseSqlServer(ConnectionString).Options;
        return new EngagementDbContext(options);
    }

    public LeagueWeekSettlementPersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static readonly LeagueWeek Wk = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Marker_round_trips_and_exists_reports_true()
    {
        await using (var ctx = NewContext())
        {
            var repo = new LeagueWeekSettlementRepository(ctx);
            Assert.False(await repo.ExistsAsync(Wk, CancellationToken.None));
            await repo.AddAsync(new LeagueWeekSettlement(Wk, new DateTimeOffset(2030, 1, 14, 0, 0, 0, TimeSpan.Zero)), CancellationToken.None);
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            Assert.True(await new LeagueWeekSettlementRepository(ctx).ExistsAsync(Wk, CancellationToken.None));
        }
    }

    [Fact]
    public void LeagueWeek_Next_advances_seven_days()
    {
        Assert.Equal(new DateOnly(2030, 1, 14), Wk.Next().Start);
    }
}
```

- [ ] **Step 9: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueWeekSettlementPersistenceTests"`
Expected: PASS (2 tests).

- [ ] **Step 10: Commit**

```bash
git add src/Modules/Engagement/ tests/Engagement.Integration.Tests/Infrastructure/LeagueWeekSettlementPersistenceTests.cs
git commit -m "feat(leagues): add LeagueWeekSettlement idempotency marker + LeagueWeek.Next"
```

---

## Task 5: `SettleLeagueWeek` command + handler

The settlement algorithm: rank each tier, move the floor-20% slices, write next-week placements, mark the week settled.

**Files:**
- Create: `src/Modules/Engagement/Engagement.Application/SettleLeagueWeek.cs`
- Create: `tests/Engagement.Integration.Tests/Application/SettleLeagueWeekTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Engagement.Integration.Tests/Application/SettleLeagueWeekTests.cs`:

```csharp
using Engagement.Application;
using Engagement.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Application;

public class SettleLeagueWeekTests
{
    private sealed class InMemoryStandings : ILeagueStandingRepository
    {
        public readonly Dictionary<(Guid, DateOnly), LeagueStanding> Store = new();
        public Task<LeagueStanding?> GetAsync(LearnerId id, LeagueWeek week, CancellationToken ct) =>
            Task.FromResult(Store.GetValueOrDefault((id.Value, week.Start)));
        public Task<LeagueStanding?> GetMostRecentAsync(LearnerId id, CancellationToken ct) =>
            Task.FromResult(Store.Values.Where(s => s.Id.Value == id.Value).OrderByDescending(s => s.Week.Start).FirstOrDefault());
        public Task AddAsync(LeagueStanding s, CancellationToken ct) { Store[(s.Id.Value, s.Week.Start)] = s; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<LeagueStanding>> GetCohortAsync(LeagueTier tier, LeagueWeek week, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LeagueStanding>>(
                Store.Values.Where(s => s.Tier == tier && s.Week == week)
                    .OrderByDescending(s => s.WeeklyXp.Value).ThenBy(s => s.Id.Value).ToList());
    }

    private sealed class InMemorySettlements : ILeagueWeekSettlementRepository
    {
        public readonly HashSet<DateOnly> Settled = new();
        public Task<bool> ExistsAsync(LeagueWeek week, CancellationToken ct) => Task.FromResult(Settled.Contains(week.Start));
        public Task AddAsync(LeagueWeekSettlement marker, CancellationToken ct) { Settled.Add(marker.Week.Start); return Task.CompletedTask; }
    }

    private static readonly LeagueWeek Wk = LeagueWeek.Containing(new DateTimeOffset(2030, 1, 9, 12, 0, 0, TimeSpan.Zero)); // Jan 7
    private static readonly DateOnly NextStart = new(2030, 1, 14);
    private static FakeTimeProvider Clock => new(new DateTimeOffset(2030, 1, 14, 0, 0, 0, TimeSpan.Zero));

    // Build a Bronze cohort of `n` with descending XP (so rank i has XP = n-i).
    private static (InMemoryStandings, List<Guid>) BronzeCohort(int n)
    {
        var repo = new InMemoryStandings();
        var ids = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            var id = new LearnerId(Guid.NewGuid());
            ids.Add(id.Value);
            var s = LeagueStanding.Create(id, Wk, LeagueTier.Bronze);
            s.RecordXp((n - i) * 10);
            repo.Store[(id.Value, Wk.Start)] = s;
        }
        return (repo, ids); // ids[0] is the top, ids[n-1] the bottom
    }

    private static SettleLeagueWeekHandler Handler(InMemoryStandings st, InMemorySettlements se) =>
        new(st, se, Clock);

    [Fact]
    public async Task Top_and_bottom_floor20pct_move_others_stay()
    {
        var (st, ids) = BronzeCohort(12); // floor(0.2*12)=2
        var se = new InMemorySettlements();

        await Handler(st, se).HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);

        // Bronze never demotes, so only the top 2 move (to Silver) for next week.
        Assert.Equal(LeagueTier.Silver, st.Store[(ids[0], NextStart)].Tier);
        Assert.Equal(LeagueTier.Silver, st.Store[(ids[1], NextStart)].Tier);
        // A middle learner has no next-week placement row.
        Assert.False(st.Store.ContainsKey((ids[5], NextStart)));
    }

    [Fact]
    public async Task Gold_cohort_promotes_top_and_demotes_bottom()
    {
        var (st, ids) = BronzeCohort(12);
        foreach (var key in st.Store.Keys.ToList()) // relabel the seeded cohort as Gold
            st.Store[key].PlaceInto(LeagueTier.Gold);
        var se = new InMemorySettlements();

        await Handler(st, se).HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);

        Assert.Equal(LeagueTier.Sapphire, st.Store[(ids[0], NextStart)].Tier);  // top up
        Assert.Equal(LeagueTier.Silver, st.Store[(ids[11], NextStart)].Tier);   // bottom down
    }

    [Fact]
    public async Task Tiny_cohort_of_four_does_not_move()
    {
        var (st, ids) = BronzeCohort(4); // floor(0.2*4)=0
        var se = new InMemorySettlements();
        await Handler(st, se).HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);
        Assert.All(ids, id => Assert.False(st.Store.ContainsKey((id, NextStart))));
    }

    [Fact]
    public async Task Re_settling_the_same_week_is_a_no_op()
    {
        var (st, ids) = BronzeCohort(12);
        var se = new InMemorySettlements();
        var handler = Handler(st, se);

        await handler.HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);
        var tierAfterFirst = st.Store[(ids[0], NextStart)].Tier;
        await handler.HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);

        Assert.Equal(tierAfterFirst, st.Store[(ids[0], NextStart)].Tier); // unchanged
    }

    [Fact]
    public async Task Existing_next_week_row_keeps_its_xp_when_reconciled()
    {
        var (st, ids) = BronzeCohort(12);
        foreach (var key in st.Store.Keys.ToList()) st.Store[key].PlaceInto(LeagueTier.Gold);
        // Top learner already earned in next week at carried-forward Gold.
        var top = ids[0];
        var early = LeagueStanding.Create(new LearnerId(top), Wk.Next(), LeagueTier.Gold);
        early.RecordXp(99);
        st.Store[(top, NextStart)] = early;
        var se = new InMemorySettlements();

        await Handler(st, se).HandleAsync(new SettleLeagueWeek(Wk.Start), CancellationToken.None);

        Assert.Equal(LeagueTier.Sapphire, st.Store[(top, NextStart)].Tier); // moved
        Assert.Equal(99, st.Store[(top, NextStart)].WeeklyXp.Value);        // XP preserved
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~SettleLeagueWeekTests"`
Expected: FAIL — `SettleLeagueWeek` / `SettleLeagueWeekHandler` don't exist.

- [ ] **Step 3: Implement the command + handler**

Create `src/Modules/Engagement/Engagement.Application/SettleLeagueWeek.cs`:

```csharp
using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record SettleLeagueWeek(DateOnly WeekStart) : IRequest<Unit>;

public sealed class SettleLeagueWeekHandler(
    ILeagueStandingRepository standings,
    ILeagueWeekSettlementRepository settlements,
    TimeProvider clock) : IRequestHandler<SettleLeagueWeek, Unit>
{
    private static readonly LeagueTier[] AllTiers = Enum.GetValues<LeagueTier>();

    public async Task<Unit> HandleAsync(SettleLeagueWeek request, CancellationToken ct)
    {
        var week = new LeagueWeek(request.WeekStart); // throws ArgumentException if not a Monday
        if (await settlements.ExistsAsync(week, ct))
            return Unit.Value; // idempotent

        var nextWeek = week.Next();

        foreach (var tier in AllTiers)
        {
            var cohort = await standings.GetCohortAsync(tier, week, ct); // ranked desc, id tiebreak
            var k = (int)Math.Floor(0.2 * cohort.Count);
            if (k == 0)
                continue;

            for (var rank = 0; rank < cohort.Count; rank++)
            {
                LeagueTier newTier;
                if (rank < k) newTier = tier.Next();
                else if (rank >= cohort.Count - k) newTier = tier.Previous();
                else continue; // middle stays

                if (newTier == tier)
                    continue; // edge (Bronze bottom / Diamond top) — no move

                var learnerId = cohort[rank].Id;
                var placement = await standings.GetAsync(learnerId, nextWeek, ct);
                if (placement is null)
                {
                    placement = LeagueStanding.Create(learnerId, nextWeek, tier); // start at old tier…
                    await standings.AddAsync(placement, ct);
                }
                placement.PlaceInto(newTier); // …then move (raises Promoted/Demoted)
            }
        }

        await settlements.AddAsync(new LeagueWeekSettlement(week, clock.GetUtcNow()), ct);
        await standings.SaveChangesAsync(ct); // one unit of work persists placements + marker
        return Unit.Value;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~SettleLeagueWeekTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Engagement/Engagement.Application/SettleLeagueWeek.cs tests/Engagement.Integration.Tests/Application/SettleLeagueWeekTests.cs
git commit -m "feat(leagues): add SettleLeagueWeek settlement command + handler"
```

---

## Task 6: `POST /leagues/weeks/{weekStart}/settle` endpoint

The explicit trigger seam, end-to-end through the real pipeline.

**Files:**
- Modify: `src/Host/Program.cs`
- Create: `tests/Engagement.Integration.Tests/EndToEnd/LeagueSettlementApiTests.cs`

- [ ] **Step 1: Write the failing e2e test**

Create `tests/Engagement.Integration.Tests/EndToEnd/LeagueSettlementApiTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.EndToEnd;

[Collection("League E2E")]
public class LeagueSettlementApiTests(LeagueApiFactory factory)
{
    private sealed record LeaderboardResponse(string Tier, DateOnly WeekStart, List<Row> Rows, int? MyRank);
    private sealed record Row(int Rank, Guid LearnerId, int WeeklyXp);

    private HttpClient ClientFor(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    private async Task CompleteLesson(Guid learnerId, DateTimeOffset whenUtc)
    {
        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.PublishAsync(new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), whenUtc));
    }

    [Fact]
    public async Task Settling_promotes_the_top_learner_visible_next_week()
    {
        // Use a distinct, far week to stay isolated from other tests in this collection.
        var wk = new DateTimeOffset(2030, 3, 6, 12, 0, 0, TimeSpan.Zero); // Wed; week of Mon Mar 4
        factory.Clock.SetUtcNow(wk);

        // Build a Bronze cohort of 5 (floor(0.2*5)=1 promotes). Distinct XP via lesson counts.
        var learners = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            learners.Add(id);
            for (var l = 0; l <= i; l++) // learner i completes i+1 lessons → more XP for higher i
                await CompleteLesson(id, wk);
        }
        var topEarner = learners[4]; // most lessons → rank 1

        var settle = await ClientFor(Guid.NewGuid())
            .PostAsync("/leagues/weeks/2030-03-04/settle", content: null);
        Assert.Equal(HttpStatusCode.OK, settle.StatusCode);

        // Next week, the top earner is in Silver.
        factory.Clock.SetUtcNow(new DateTimeOffset(2030, 3, 13, 12, 0, 0, TimeSpan.Zero)); // week of Mon Mar 11
        await CompleteLesson(topEarner, new DateTimeOffset(2030, 3, 13, 12, 0, 0, TimeSpan.Zero));
        var board = await ClientFor(topEarner).GetFromJsonAsync<LeaderboardResponse>("/me/league");
        Assert.Equal("Silver", board!.Tier);
    }

    [Fact]
    public async Task Non_monday_weekstart_is_a_400()
    {
        var resp = await ClientFor(Guid.NewGuid())
            .PostAsync("/leagues/weeks/2030-03-06/settle", content: null); // Wednesday
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueSettlementApiTests"`
Expected: FAIL — `POST /leagues/weeks/{weekStart}/settle` returns 404.

- [ ] **Step 3: Add the endpoint**

In `src/Host/Program.cs`, add after the `app.MapGet("/me/league", …)` block:

```csharp
app.MapPost("/leagues/weeks/{weekStart}/settle",
    async (DateOnly weekStart, IMediator mediator, CancellationToken ct) =>
    {
        try
        {
            await mediator.SendAsync(new SettleLeagueWeek(weekStart), ct);
            return Results.Ok();
        }
        catch (ArgumentException ex)
        {
            // A non-Monday weekStart is bad input, not a 500.
            return Results.BadRequest(new { error = ex.Message });
        }
    });
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueSettlementApiTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Host/Program.cs tests/Engagement.Integration.Tests/EndToEnd/LeagueSettlementApiTests.cs
git commit -m "feat(leagues): expose POST /leagues/weeks/{weekStart}/settle"
```

---

## Task 7: Full verification

**Files:** none (verification + status note).

- [ ] **Step 1: Build clean**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s). 0 Error(s)`.

- [ ] **Step 2: Run the entire suite**

Run: `dotnet test`
Expected: PASS — every test, including all XP/streak/mediator/architecture tests untouched (per the spec's backward-compatibility section).

- [ ] **Step 3: Confirm spec acceptance criteria**

Map each to a passing test:
1. Top/bottom `floor(0.2×N)` move; middle stays → `SettleLeagueWeekTests.Top_and_bottom_floor20pct_move_others_stay`, `Gold_cohort_promotes_top_and_demotes_bottom`.
2. Bronze never demotes / Diamond never promotes → `LeagueTierTests` + the Bronze-only-promotes assertion in `Top_and_bottom_floor20pct_move_others_stay`.
3. Idempotent → `SettleLeagueWeekTests.Re_settling_the_same_week_is_a_no_op` + the marker persistence test.
4. Week-N result survives N+1 activity → `LeaguePersistenceTests.Same_learner_has_a_row_per_week_and_most_recent_wins`.
5. `GET /me/league` reflects the new tier → `LeagueSettlementApiTests.Settling_promotes_the_top_learner_visible_next_week`.
6. ≤4 cohorts don't move; no double-move → `SettleLeagueWeekTests.Tiny_cohort_of_four_does_not_move`.
7. Endpoint 200 / 400 for non-Monday → `LeagueSettlementApiTests` (both tests).
8. Domain references nothing infrastructural → `ArchitectureTests.Domain_does_not_depend_on_EfCore_or_AspNetCore`.

- [ ] **Step 4: Update `CLAUDE.md` status**

In the `## Status` section, replace the leagues `⏭️ **Next:**` line with:

```markdown
- ✅ **Sub-project 4 — Leagues, Slice 2 (settlement)** (PR #5): cohort-wide promotion/demotion behind
  an explicit `SettleLeagueWeek` seam — per-(learner, week) standings, `floor(0.2N)` up/down with
  Bronze/Diamond edges, per-week idempotency marker; subscriber-less `Promoted`/`Demoted` events;
  `POST /leagues/weeks/{weekStart}/settle`. Automatic trigger (scheduler / lazy) deferred.
- ⏭️ **Next:** the automatic settlement trigger → real Learning engine → real Identity.
```

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: mark leagues Slice 2 (settlement) complete"
```

---

## Notes for the implementer

- **TDD discipline:** red → green → commit on every code task. Task 2 is the exception in *size* (a coordinated reshape) — there, go domain-first (Domain.Tests green while the rest is mid-change), then bring Infrastructure/Application/Host green before the single commit.
- **Queries compare whole value objects** — `s.Week == week`, never `s.Week.Start` (EF can't translate reaching into a converted key/property).
- **The clock is always injected** in handlers (`TimeProvider`); never `DateTimeOffset.UtcNow` in a handler. The one allowed `UtcNow` is inside `PlaceInto`'s event stamp, matching the existing `XpAwarded` convention.
- **No automatic trigger here** — `SettleLeagueWeek` is invoked by the endpoint/tests only. If you find yourself adding an `IHostedService` or lazy-on-activity settlement, stop: that's the deferred next increment.
