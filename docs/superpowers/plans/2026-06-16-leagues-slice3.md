# Leagues Slice 3 — Automatic Settlement Trigger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a league week close on its own — a .NET `BackgroundService` that periodically settles any week that has ended but isn't settled yet, reusing the unchanged Slice-2 `SettleLeagueWeek` operation.

**Architecture:** A new `BackgroundService` (Host, the repo's first) is a second *driving adapter* beside the existing settle endpoint. On a `PeriodicTimer` (fed the injected `TimeProvider`, so tests drive it) it opens a DI scope per tick and sends a new `SettleDueLeagueWeeks` *policy* command (Application). That handler asks a new repository query for the distinct **ended** weeks that have standings and sends `SettleLeagueWeek` for each, oldest-first — one send per week so each is its own committed unit of work (the settlement chain needs week N committed before N+1 is ranked). Idempotency is inherited free from the Slice-2 per-week marker, so re-ticking is a no-op. No schema change, no migration.

**Tech Stack:** C# / .NET 10, ASP.NET Core, `Microsoft.Extensions.Hosting.BackgroundService`, `System.Threading.PeriodicTimer`, EF Core 10 (SQL Server LocalDB), the hand-rolled mediator, xUnit, `Microsoft.Extensions.Time.Testing.FakeTimeProvider`.

**Spec:** [`docs/superpowers/specs/2026-06-16-leagues-slice3-design.md`](../specs/2026-06-16-leagues-slice3-design.md)

**Conventions:** Branch is `feat/leagues-auto-settlement` (already checked out). Every commit uses Conventional Commits and ends with the trailer `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`. Run commands from the repo root in PowerShell.

---

## File Structure

**Created:**
- `src/Modules/Engagement/Engagement.Application/SettleDueLeagueWeeks.cs` — the policy command + handler.
- `src/Host/LeagueSettlementOptions.cs` — `{ Enabled, PollInterval }`.
- `src/Host/LeagueSettlementScheduler.cs` — the `BackgroundService`.
- `tests/Engagement.Integration.Tests/Infrastructure/DistinctEndedWeeksPersistenceTests.cs` — EF query test.
- `tests/Engagement.Integration.Tests/Application/SettleDueLeagueWeeksTests.cs` — policy test.
- `tests/Engagement.Integration.Tests/Scheduler/LeagueSettlementSchedulerTests.cs` — mechanism test.

**Modified:**
- `src/Modules/Engagement/Engagement.Domain/ILeagueStandingRepository.cs` — add `GetDistinctEndedWeeksAsync`.
- `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingRepository.cs` — implement it.
- `tests/Engagement.Integration.Tests/Application/SettleLeagueWeekTests.cs` — extend the in-memory fake.
- `tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs` — extend the in-memory fake.
- `src/Host/Program.cs` — bind options + conditionally register the hosted service.
- `src/Host/appsettings.json` — add the `Leagues:Settlement` section.
- `tests/Engagement.Integration.Tests/EndToEnd/LeagueApiFactory.cs` — disable the scheduler.
- `tests/Engagement.Integration.Tests/EndToEnd/LeagueSettlementApiTests.cs` — disable the scheduler (its factory).
- `tests/Engagement.Integration.Tests/EndToEnd/StreakApiFactory.cs` — disable the scheduler.
- `tests/Engagement.Integration.Tests/EndToEnd/EngagementApiFactory.cs` — disable the scheduler.
- `CLAUDE.md` — mark Slice 3 in the Status section.

---

## Task 1: Repository query — `GetDistinctEndedWeeksAsync`

The policy needs "which weeks have standings and have fully ended?" Adding a method to `ILeagueStandingRepository` forces every implementer (the real repo + two in-memory test fakes) to define it, so they're all updated in this task to keep the build green.

**Files:**
- Create: `tests/Engagement.Integration.Tests/Infrastructure/DistinctEndedWeeksPersistenceTests.cs`
- Modify: `src/Modules/Engagement/Engagement.Domain/ILeagueStandingRepository.cs`
- Modify: `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingRepository.cs`
- Modify: `tests/Engagement.Integration.Tests/Application/SettleLeagueWeekTests.cs:23` (inside `InMemoryStandings`)
- Modify: `tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs:38` (inside `InMemoryStandings`)

- [ ] **Step 1: Write the failing persistence test**

Create `tests/Engagement.Integration.Tests/Infrastructure/DistinctEndedWeeksPersistenceTests.cs`:

```csharp
using Engagement.Domain;
using Engagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Engagement.Integration.Tests.Infrastructure;

public class DistinctEndedWeeksPersistenceTests
{
    // Unique DB name for this class — xUnit runs classes in parallel, so a shared name races EnsureDeleted.
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_DueWeeksTest;Trusted_Connection=True;TrustServerCertificate=True";

    private static EngagementDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>().UseSqlServer(ConnectionString).Options;
        return new EngagementDbContext(options);
    }

    public DistinctEndedWeeksPersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static readonly LeagueWeek W1 = new(new DateOnly(2030, 1, 7));        // Monday
    private static readonly LeagueWeek W2 = new(new DateOnly(2030, 1, 14));       // Monday
    private static readonly LeagueWeek Current = new(new DateOnly(2030, 1, 21));  // Monday — the current week

    [Fact]
    public async Task Returns_distinct_ended_weeks_ascending_excluding_current()
    {
        await using (var ctx = NewContext())
        {
            var repo = new LeagueStandingRepository(ctx);
            // Two learners in W1 proves DISTINCT collapses the week to one entry.
            await repo.AddAsync(LeagueStanding.Create(new LearnerId(Guid.NewGuid()), W1, LeagueTier.Bronze), CancellationToken.None);
            await repo.AddAsync(LeagueStanding.Create(new LearnerId(Guid.NewGuid()), W1, LeagueTier.Bronze), CancellationToken.None);
            await repo.AddAsync(LeagueStanding.Create(new LearnerId(Guid.NewGuid()), W2, LeagueTier.Bronze), CancellationToken.None);
            await repo.AddAsync(LeagueStanding.Create(new LearnerId(Guid.NewGuid()), Current, LeagueTier.Bronze), CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using var verify = NewContext();
        var due = await new LeagueStandingRepository(verify).GetDistinctEndedWeeksAsync(Current, CancellationToken.None);

        Assert.Equal(new[] { W1.Start, W2.Start }, due.Select(w => w.Start).ToArray()); // ascending, current excluded
    }
}
```

- [ ] **Step 2: Run the test to verify it fails (red)**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~DistinctEndedWeeksPersistenceTests"`
Expected: **build failure** — `error CS1061: 'ILeagueStandingRepository' does not contain a definition for 'GetDistinctEndedWeeksAsync'`.

- [ ] **Step 3: Add the method to the interface**

In `src/Modules/Engagement/Engagement.Domain/ILeagueStandingRepository.cs`, add after the `GetCohortAsync` line (before the closing brace):

```csharp
    // Distinct weeks that have at least one standing and have fully ended (strictly before the
    // current week), oldest first. The Slice-3 settlement driver uses this to find weeks owed a
    // settlement. Returns LeagueWeek values, which are always Mondays by construction.
    Task<IReadOnlyList<LeagueWeek>> GetDistinctEndedWeeksAsync(LeagueWeek currentWeek, CancellationToken ct);
```

- [ ] **Step 4: Implement it in the real repository**

In `src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingRepository.cs`, add this method inside the class (e.g. after `GetCohortAsync`):

```csharp
    public async Task<IReadOnlyList<LeagueWeek>> GetDistinctEndedWeeksAsync(
        LeagueWeek currentWeek, CancellationToken ct)
    {
        // EF can translate Distinct() over the whole value-converted Week column, but NOT a "<"
        // comparison on it (only == / OrderBy by the whole VO). So materialise the small distinct-week
        // set, then filter "ended" and sort by Start in memory.
        var weeks = await context.LeagueStandings.Select(s => s.Week).Distinct().ToListAsync(ct);
        return weeks.Where(w => w.Start < currentWeek.Start).OrderBy(w => w.Start).ToList();
    }
```

- [ ] **Step 5: Add the method to both in-memory fakes (so the test project compiles)**

In `tests/Engagement.Integration.Tests/Application/SettleLeagueWeekTests.cs`, inside the `InMemoryStandings` class (it uses a public field named `Store`), add:

```csharp
        public Task<IReadOnlyList<LeagueWeek>> GetDistinctEndedWeeksAsync(LeagueWeek currentWeek, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LeagueWeek>>(
                Store.Values.Select(s => s.Week).Distinct()
                    .Where(w => w.Start < currentWeek.Start).OrderBy(w => w.Start).ToList());
```

In `tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs`, inside its `InMemoryStandings` class (it uses a private field named `_store`), add:

```csharp
        public Task<IReadOnlyList<LeagueWeek>> GetDistinctEndedWeeksAsync(LeagueWeek currentWeek, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LeagueWeek>>(
                _store.Values.Select(s => s.Week).Distinct()
                    .Where(w => w.Start < currentWeek.Start).OrderBy(w => w.Start).ToList());
```

- [ ] **Step 6: Run the test to verify it passes (green)**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~DistinctEndedWeeksPersistenceTests"`
Expected: **Passed!** (1 test).

- [ ] **Step 7: Run the existing league tests to confirm nothing else broke**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~League"`
Expected: **Passed!** (all league persistence/application/e2e tests still green).

- [ ] **Step 8: Commit**

```bash
git add src/Modules/Engagement/Engagement.Domain/ILeagueStandingRepository.cs src/Modules/Engagement/Engagement.Infrastructure/LeagueStandingRepository.cs tests/Engagement.Integration.Tests/Infrastructure/DistinctEndedWeeksPersistenceTests.cs tests/Engagement.Integration.Tests/Application/SettleLeagueWeekTests.cs tests/Engagement.Integration.Tests/Application/LeagueApplicationTests.cs
git commit -m "feat(leagues): add GetDistinctEndedWeeksAsync repository query"
```

---

## Task 2: The policy command — `SettleDueLeagueWeeks`

A use case that finds the due weeks and settles them oldest-first by sending the existing `SettleLeagueWeek` command once per week. It is auto-registered by the mediator's assembly scan (`AddMediator(typeof(GetXpAccount).Assembly, …)` already scans `Engagement.Application`), so no DI change is needed.

**Files:**
- Create: `src/Modules/Engagement/Engagement.Application/SettleDueLeagueWeeks.cs`
- Create: `tests/Engagement.Integration.Tests/Application/SettleDueLeagueWeeksTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Engagement.Integration.Tests/Application/SettleDueLeagueWeeksTests.cs`:

```csharp
using BuildingBlocks.Mediator;
using Engagement.Application;
using Engagement.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Application;

public class SettleDueLeagueWeeksTests
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
        public Task<IReadOnlyList<LeagueWeek>> GetDistinctEndedWeeksAsync(LeagueWeek currentWeek, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LeagueWeek>>(
                Store.Values.Select(s => s.Week).Distinct()
                    .Where(w => w.Start < currentWeek.Start).OrderBy(w => w.Start).ToList());
    }

    private sealed class InMemorySettlements : ILeagueWeekSettlementRepository
    {
        public readonly HashSet<DateOnly> Settled = new();
        public Task<bool> ExistsAsync(LeagueWeek week, CancellationToken ct) => Task.FromResult(Settled.Contains(week.Start));
        public Task AddAsync(LeagueWeekSettlement marker, CancellationToken ct) { Settled.Add(marker.Week.Start); return Task.CompletedTask; }
    }

    // Routes SettleLeagueWeek to a REAL handler over the same repos, and records the order weeks
    // were settled in — so we can assert both the policy (which/what order) and the real outcome.
    private sealed class DispatchingMediator(
        ILeagueStandingRepository st, ILeagueWeekSettlementRepository se, TimeProvider clock) : IMediator
    {
        public readonly List<DateOnly> SettledOrder = new();
        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is SettleLeagueWeek cmd)
            {
                SettledOrder.Add(cmd.WeekStart);
                await new SettleLeagueWeekHandler(st, se, clock).HandleAsync(cmd, ct);
                return (TResponse)(object)Unit.Value;
            }
            throw new NotSupportedException(request.GetType().Name);
        }
        public Task PublishAsync(INotification notification, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static readonly LeagueWeek W1 = new(new DateOnly(2030, 1, 7));
    private static readonly LeagueWeek W2 = new(new DateOnly(2030, 1, 14));
    private static readonly DateOnly W3Start = new(2030, 1, 21); // the current week
    private static FakeTimeProvider ClockInW3() => new(new DateTimeOffset(2030, 1, 21, 12, 0, 0, TimeSpan.Zero));

    private static void Seed(InMemoryStandings st, LeagueWeek week, int n, LeagueTier tier)
    {
        for (var i = 0; i < n; i++)
        {
            var s = LeagueStanding.Create(new LearnerId(Guid.NewGuid()), week, tier);
            s.RecordXp((n - i) * 10); // descending XP: first seeded is the top
            st.Store[(s.Id.Value, week.Start)] = s;
        }
    }

    [Fact]
    public async Task Settles_ended_weeks_oldest_first_excluding_current()
    {
        var st = new InMemoryStandings();
        var se = new InMemorySettlements();
        Seed(st, W2, 5, LeagueTier.Bronze);                       // seeded out of order on purpose
        Seed(st, W1, 5, LeagueTier.Bronze);
        Seed(st, new LeagueWeek(W3Start), 5, LeagueTier.Bronze);  // current week — must NOT settle
        var mediator = new DispatchingMediator(st, se, ClockInW3());
        var handler = new SettleDueLeagueWeeksHandler(st, mediator, ClockInW3());

        await handler.HandleAsync(new SettleDueLeagueWeeks(), CancellationToken.None);

        Assert.Equal(new[] { W1.Start, W2.Start }, mediator.SettledOrder.ToArray());
    }

    [Fact]
    public async Task Nothing_due_when_only_the_current_week_has_standings()
    {
        var st = new InMemoryStandings();
        var se = new InMemorySettlements();
        Seed(st, new LeagueWeek(W3Start), 5, LeagueTier.Bronze);
        var mediator = new DispatchingMediator(st, se, ClockInW3());
        var handler = new SettleDueLeagueWeeksHandler(st, mediator, ClockInW3());

        await handler.HandleAsync(new SettleDueLeagueWeeks(), CancellationToken.None);

        Assert.Empty(mediator.SettledOrder);
    }

    [Fact]
    public async Task Catch_up_settles_in_order_so_w1_promotion_lands_in_w2()
    {
        var st = new InMemoryStandings();
        var se = new InMemorySettlements();
        Seed(st, W1, 5, LeagueTier.Bronze); // floor(0.2*5)=1 → the top learner promotes Bronze→Silver
        var topW1 = st.Store.Values.Where(s => s.Week == W1)
            .OrderByDescending(s => s.WeeklyXp.Value).First().Id.Value;
        // The same top learner also earned early in W2 at the carried-forward Bronze tier.
        var early = LeagueStanding.Create(new LearnerId(topW1), W2, LeagueTier.Bronze);
        early.RecordXp(5);
        st.Store[(topW1, W2.Start)] = early;
        var mediator = new DispatchingMediator(st, se, ClockInW3()); // W1 and W2 both ended
        var handler = new SettleDueLeagueWeeksHandler(st, mediator, ClockInW3());

        await handler.HandleAsync(new SettleDueLeagueWeeks(), CancellationToken.None);

        Assert.Equal(new[] { W1.Start, W2.Start }, mediator.SettledOrder.ToArray());  // oldest first
        Assert.Equal(LeagueTier.Silver, st.Store[(topW1, W2.Start)].Tier);            // W1 settled first → W2 row reconciled up
    }
}
```

- [ ] **Step 2: Run the test to verify it fails (red)**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~SettleDueLeagueWeeksTests"`
Expected: **build failure** — `error CS0246: The type or namespace name 'SettleDueLeagueWeeks' could not be found` (and `SettleDueLeagueWeeksHandler`).

- [ ] **Step 3: Create the command and handler**

Create `src/Modules/Engagement/Engagement.Application/SettleDueLeagueWeeks.cs`:

```csharp
using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

// Policy: settle every league week that has ended but isn't settled yet, oldest-first. Reuses
// SettleLeagueWeek per week — each send is its own unit of work, which the settlement chain needs
// (week N must be committed before week N+1 is ranked). Idempotent: SettleLeagueWeek no-ops an
// already-settled week via its per-week marker, so this is safe to run on every scheduler tick.
public sealed record SettleDueLeagueWeeks : IRequest<Unit>;

public sealed class SettleDueLeagueWeeksHandler(
    ILeagueStandingRepository standings,
    IMediator mediator,
    TimeProvider clock) : IRequestHandler<SettleDueLeagueWeeks, Unit>
{
    public async Task<Unit> HandleAsync(SettleDueLeagueWeeks request, CancellationToken ct)
    {
        var currentWeek = LeagueWeek.Containing(clock.GetUtcNow());
        var dueWeeks = await standings.GetDistinctEndedWeeksAsync(currentWeek, ct); // ascending
        foreach (var week in dueWeeks)
            await mediator.SendAsync(new SettleLeagueWeek(week.Start), ct);
        return Unit.Value;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes (green)**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~SettleDueLeagueWeeksTests"`
Expected: **Passed!** (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Engagement/Engagement.Application/SettleDueLeagueWeeks.cs tests/Engagement.Integration.Tests/Application/SettleDueLeagueWeeksTests.cs
git commit -m "feat(leagues): add SettleDueLeagueWeeks policy command"
```

---

## Task 3: The scheduler — `LeagueSettlementScheduler` (BackgroundService)

Pure mechanism: a `PeriodicTimer` driven by the injected `TimeProvider`, a DI scope per tick, a fault-tolerant loop, clean shutdown. Types are `public` so the test project (which already references the Host via `WebApplicationFactory<Program>`) can construct them directly.

**Files:**
- Create: `src/Host/LeagueSettlementOptions.cs`
- Create: `src/Host/LeagueSettlementScheduler.cs`
- Create: `tests/Engagement.Integration.Tests/Scheduler/LeagueSettlementSchedulerTests.cs`

- [ ] **Step 1: Write the failing mechanism test**

Create `tests/Engagement.Integration.Tests/Scheduler/LeagueSettlementSchedulerTests.cs`:

```csharp
using BuildingBlocks.Mediator;
using Engagement.Application;
using Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Scheduler;

public class LeagueSettlementSchedulerTests
{
    // Records every request sent; can throw on a chosen call index to simulate a failing tick.
    private sealed class SpyMediator(int throwOnCallIndex = -1) : IMediator
    {
        private readonly List<object> _sent = new();
        private readonly object _gate = new();
        private int _calls;
        public IReadOnlyList<object> Sent { get { lock (_gate) return _sent.ToList(); } }

        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            lock (_gate)
            {
                if (_calls++ == throwOnCallIndex)
                    throw new InvalidOperationException("simulated tick failure");
                _sent.Add(request);
            }
            return Task.FromResult((TResponse)(object)Unit.Value);
        }
        public Task PublishAsync(INotification notification, CancellationToken ct = default) => Task.CompletedTask;

        // Polls (the loop resumes on a background continuation after the fake clock advances).
        public async Task WaitForSendCountAsync(int count, TimeSpan timeout)
        {
            var start = Environment.TickCount64;
            while (Sent.Count < count)
            {
                if (Environment.TickCount64 - start > timeout.TotalMilliseconds)
                    throw new TimeoutException($"Expected {count} sends; saw {Sent.Count}.");
                await Task.Delay(10);
            }
        }
    }

    private static IServiceScopeFactory ScopeFactoryFor(IMediator mediator)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => mediator);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static LeagueSettlementScheduler NewScheduler(IMediator mediator, FakeTimeProvider clock, TimeSpan interval) =>
        new(ScopeFactoryFor(mediator), clock, NullLogger<LeagueSettlementScheduler>.Instance,
            Options.Create(new LeagueSettlementOptions { PollInterval = interval }));

    private static FakeTimeProvider NewClock() => new(new DateTimeOffset(2030, 1, 21, 0, 0, 0, TimeSpan.Zero));
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Sends_on_startup_and_on_each_interval()
    {
        var clock = NewClock();
        var spy = new SpyMediator();
        var sut = NewScheduler(spy, clock, Interval);

        await sut.StartAsync(CancellationToken.None);
        await spy.WaitForSendCountAsync(1, Timeout);   // initial pass on startup
        clock.Advance(Interval);
        await spy.WaitForSendCountAsync(2, Timeout);   // first interval
        clock.Advance(Interval);
        await spy.WaitForSendCountAsync(3, Timeout);   // second interval
        await sut.StopAsync(CancellationToken.None);

        Assert.All(spy.Sent, r => Assert.IsType<SettleDueLeagueWeeks>(r));
    }

    [Fact]
    public async Task A_failing_tick_does_not_stop_the_loop()
    {
        var clock = NewClock();
        var spy = new SpyMediator(throwOnCallIndex: 0); // the startup tick throws
        var sut = NewScheduler(spy, clock, Interval);

        await sut.StartAsync(CancellationToken.None); // startup tick throws, is logged + swallowed
        clock.Advance(Interval);
        await spy.WaitForSendCountAsync(1, Timeout);  // a later tick still succeeds → loop survived
        await sut.StopAsync(CancellationToken.None);

        Assert.Single(spy.Sent);
    }

    [Fact]
    public async Task Stops_cleanly_and_ticks_no_more_after_stop()
    {
        var clock = NewClock();
        var spy = new SpyMediator();
        var sut = NewScheduler(spy, clock, Interval);

        await sut.StartAsync(CancellationToken.None);
        await spy.WaitForSendCountAsync(1, Timeout);
        await sut.StopAsync(CancellationToken.None);   // completes without throwing
        var countAtStop = spy.Sent.Count;

        clock.Advance(Interval);
        await Task.Delay(100);
        Assert.Equal(countAtStop, spy.Sent.Count);     // timer disposed → no further ticks
    }
}
```

- [ ] **Step 2: Run the test to verify it fails (red)**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueSettlementSchedulerTests"`
Expected: **build failure** — `error CS0246: The type or namespace name 'LeagueSettlementScheduler' could not be found` (and `LeagueSettlementOptions`).

- [ ] **Step 3: Create the options type**

Create `src/Host/LeagueSettlementOptions.cs`:

```csharp
namespace Host;

public sealed class LeagueSettlementOptions
{
    // Feature flag for the hosted scheduler. Default on; the E2E test hosts turn it off.
    public bool Enabled { get; set; } = true;

    // How often the scheduler checks for ended-but-unsettled weeks.
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);
}
```

- [ ] **Step 4: Create the BackgroundService**

Create `src/Host/LeagueSettlementScheduler.cs`:

```csharp
using BuildingBlocks.Mediator;
using Engagement.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Host;

// The repo's first BackgroundService. Pure mechanism: on a timer, ask the application to settle any
// league week that has ended but isn't settled yet. The "what is due" policy lives in
// SettleDueLeagueWeeks (Application); this type owns only timing, scope, and lifecycle.
public sealed class LeagueSettlementScheduler(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<LeagueSettlementScheduler> logger,
    IOptions<LeagueSettlementOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer takes the injected TimeProvider, so FakeTimeProvider drives it in tests.
        using var timer = new PeriodicTimer(options.Value.PollInterval, clock);
        try
        {
            // do/while = run an initial pass on startup (fast catch-up), then once per interval.
            do
            {
                try
                {
                    // A BackgroundService is a singleton; the mediator/DbContext are scoped — so
                    // open a fresh scope per tick.
                    using var scope = scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    await mediator.SendAsync(new SettleDueLeagueWeeks(), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One bad tick (e.g. a transient DB error, or the schema not yet applied) must
                    // not kill the service for the Host's lifetime — log and retry next interval.
                    logger.LogError(ex, "League settlement tick failed; retrying next interval.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down — exit cleanly.
        }
    }
}
```

- [ ] **Step 5: Run the test to verify it passes (green)**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~LeagueSettlementSchedulerTests"`
Expected: **Passed!** (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Host/LeagueSettlementOptions.cs src/Host/LeagueSettlementScheduler.cs tests/Engagement.Integration.Tests/Scheduler/LeagueSettlementSchedulerTests.cs
git commit -m "feat(leagues): add LeagueSettlementScheduler background service"
```

---

## Task 4: Wire it into the Host and disable it in the test hosts

Register the scheduler (feature-flagged) and add the config section. Then disable it in all four `WebApplicationFactory<Program>` test hosts — they inject a shared `FakeTimeProvider` and jump it, so a live scheduler on that clock would settle weeks mid-test and race the assertions. This task is composition/config; it is verified by the **full suite staying green**.

**Files:**
- Modify: `src/Host/appsettings.json`
- Modify: `src/Host/Program.cs`
- Modify: `tests/Engagement.Integration.Tests/EndToEnd/LeagueApiFactory.cs`
- Modify: `tests/Engagement.Integration.Tests/EndToEnd/LeagueSettlementApiTests.cs`
- Modify: `tests/Engagement.Integration.Tests/EndToEnd/StreakApiFactory.cs`
- Modify: `tests/Engagement.Integration.Tests/EndToEnd/EngagementApiFactory.cs`

- [ ] **Step 1: Add the config section**

Replace the entire contents of `src/Host/appsettings.json` with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Engagement": "Server=(localdb)\\MSSQLLocalDB;Database=DuolingoEngagement;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Leagues": {
    "Settlement": {
      "Enabled": true,
      "PollInterval": "01:00:00"
    }
  }
}
```

- [ ] **Step 2: Register the scheduler in `Program.cs`**

In `src/Host/Program.cs`, insert the following immediately **before** the `var app = builder.Build();` line (`using Host;` is already imported at the top, so the types resolve):

```csharp
// League weeks close automatically: a BackgroundService periodically settles any ended-but-unsettled
// week. Feature-flagged so the E2E test hosts can disable it (they jump a shared FakeTimeProvider).
builder.Services.Configure<LeagueSettlementOptions>(
    builder.Configuration.GetSection("Leagues:Settlement"));
if (builder.Configuration.GetValue("Leagues:Settlement:Enabled", true))
    builder.Services.AddHostedService<LeagueSettlementScheduler>();
```

- [ ] **Step 3: Disable the scheduler in all four test hosts**

In **each** of these files, inside `ConfigureWebHost`, add this line immediately **after** the existing `builder.UseSetting("ConnectionStrings:Engagement", TestConnectionString);` line:

```csharp
        builder.UseSetting("Leagues:Settlement:Enabled", "false");
```

The four files (each has the identical `ConnectionStrings:Engagement` anchor):
- `tests/Engagement.Integration.Tests/EndToEnd/LeagueApiFactory.cs`
- `tests/Engagement.Integration.Tests/EndToEnd/LeagueSettlementApiTests.cs` (the `LeagueSettlementApiFactory` class near the top of the file)
- `tests/Engagement.Integration.Tests/EndToEnd/StreakApiFactory.cs`
- `tests/Engagement.Integration.Tests/EndToEnd/EngagementApiFactory.cs`

- [ ] **Step 4: Build the Host to confirm wiring compiles**

Run: `dotnet build src/Host`
Expected: **Build succeeded.** (0 errors).

- [ ] **Step 5: Run the full test suite (the real verification)**

Run: `dotnet test`
Expected: **Passed!** — every test green, including all four E2E suites (the scheduler is disabled in them, so behaviour is unchanged).

- [ ] **Step 6: Commit**

```bash
git add src/Host/appsettings.json src/Host/Program.cs tests/Engagement.Integration.Tests/EndToEnd/LeagueApiFactory.cs tests/Engagement.Integration.Tests/EndToEnd/LeagueSettlementApiTests.cs tests/Engagement.Integration.Tests/EndToEnd/StreakApiFactory.cs tests/Engagement.Integration.Tests/EndToEnd/EngagementApiFactory.cs
git commit -m "feat(leagues): run settlement automatically via a feature-flagged scheduler"
```

---

## Task 5: Update project status and final verification

**Files:**
- Modify: `CLAUDE.md` (the Status section)

- [ ] **Step 1: Mark Slice 3 in `CLAUDE.md`**

In `CLAUDE.md`, under `## Status`, add this bullet immediately after the existing Slice-2 (PR #5) bullet:

```markdown
- ✅ **Sub-project 4 — Leagues, Slice 3 (automatic trigger)**: a feature-flagged `BackgroundService`
  (`LeagueSettlementScheduler`, the repo's first) periodically settles every ended-but-unsettled week
  via a new `SettleDueLeagueWeeks` policy command, which sends the unchanged `SettleLeagueWeek` once
  per due week (oldest-first → the chain holds; idempotent via the Slice-2 marker). `PeriodicTimer` is
  fed the injected `TimeProvider` (so `FakeTimeProvider` drives it in tests); disabled in the E2E
  hosts. No schema change. New repo query `GetDistinctEndedWeeksAsync`.
```

Then update the `⏭️ Next` bullet to remove "the automatic settlement trigger" (now done), leaving the real Learning engine / real Identity / freeze economy as next.

- [ ] **Step 2: Run the full suite one final time**

Run: `dotnet test`
Expected: **Passed!** (all tests green).

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs(leagues): mark Slice 3 (automatic settlement trigger) complete"
```

---

## Self-Review

**Spec coverage** — each in-scope item maps to a task:
- `LeagueSettlementScheduler` (timer, scope-per-tick, fault-tolerant, clean shutdown, startup pass) → Task 3.
- `LeagueSettlementOptions` (`Enabled`, `PollInterval`) + `appsettings.json` → Tasks 3 & 4.
- `SettleDueLeagueWeeks` command + handler → Task 2.
- `GetDistinctEndedWeeksAsync` (Domain interface + Infra impl) → Task 1.
- Program wiring (conditional `AddHostedService`) → Task 4.
- Test-host isolation (4 factories) → Task 4.
- Policy test + mechanism test → Tasks 2 & 3; the EF query also gets a persistence test → Task 1.
- Acceptance criteria: auto-settle within an interval (Task 3 + 4 wiring), oldest-first catch-up (Tasks 1+2), idempotency (inherited; covered by `Nothing_due…` + Slice-2 tests), never-current-week (Tasks 1+2), fault-tolerant loop (Task 3), clean shutdown (Task 3), suite green / no behaviour change (Task 4), dependency rule (new Application/Domain types reference nothing infra — existing NetArchTest covers it; the `BackgroundService` is in Host), no migration (no schema change anywhere).

**Placeholder scan:** none — every code step shows complete code; every run step shows the exact command and expected result.

**Type consistency:** `GetDistinctEndedWeeksAsync(LeagueWeek, CancellationToken) → Task<IReadOnlyList<LeagueWeek>>` is identical across the interface, the real repo, and all three in-memory fakes. `SettleDueLeagueWeeks : IRequest<Unit>` / `SettleDueLeagueWeeksHandler` match between Task 2's creation and its test. `LeagueSettlementScheduler`'s constructor `(IServiceScopeFactory, TimeProvider, ILogger<LeagueSettlementScheduler>, IOptions<LeagueSettlementOptions>)` matches the test's `NewScheduler` helper. `SettleLeagueWeek(DateOnly WeekStart)` (existing) is used as `new SettleLeagueWeek(week.Start)`.

**Note for the executor:** the persistence and E2E tests use SQL Server LocalDB and self-manage their databases (`EnsureDeleted` + `Migrate`); the first connection spins LocalDB up. If `dotnet test` reports a connection error, confirm LocalDB is available (`sqllocaldb info`).
