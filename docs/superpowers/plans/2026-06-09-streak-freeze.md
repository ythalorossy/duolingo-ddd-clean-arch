# Streak Freeze Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a learner protect a streak across a missed day by auto-applying a capped, lazily-settled streak freeze held on the existing `LearnerStreak` aggregate.

**Architecture:** Add `FreezeBalance` to `LearnerStreak`. One rule — `consumed = min(gap, FreezeBalance)`, streak survives iff `consumed == gap` — drives both the write path (`RegisterQualifyingActivity`, persisted) and a pure read projection (`Report`, no mutation). A shared `GapBetween` helper guarantees write and read agree. No background job; idempotency is preserved because consumption is bound to advancing `LastQualifyingDate`. Acquisition is an abstract `GrantStreakFreeze` command behind `POST /me/streak-freezes`.

**Tech Stack:** C#/.NET 10, ASP.NET Core Minimal APIs, EF Core 10 on SQL Server LocalDB, xUnit, `Microsoft.Extensions.Time.Testing.FakeTimeProvider`.

**Spec:** [`docs/superpowers/specs/2026-06-09-streak-freeze-design.md`](../specs/2026-06-09-streak-freeze-design.md)

**Branch:** `feat/streak-freeze` (already created; the design spec is committed on it).

---

## File map

| File | Change | Responsibility |
|---|---|---|
| `src/Modules/Engagement/Engagement.Domain/LearnerStreak.cs` | Modify | Add `FreezeBalance`, `MaxFreezes`, `GrantFreeze()`, `GapBetween` helper, gap-bridging, freeze-aware `Report` |
| `src/Modules/Engagement/Engagement.Domain/StreakReport.cs` | Modify | Add `FreezesAvailable` |
| `src/Modules/Engagement/Engagement.Domain/StreakFrozen.cs` | Create | Domain event raised when freezes bridge a gap |
| `src/Modules/Engagement/Engagement.Infrastructure/LearnerStreakConfiguration.cs` | Modify | Map `FreezeBalance` |
| `src/Modules/Engagement/Engagement.Infrastructure/Migrations/*_AddStreakFreeze.cs` | Create (generated) | Add `FreezeBalance` column |
| `src/Modules/Engagement/Engagement.Application/GrantStreakFreeze.cs` | Create | Command + handler (abstract acquisition) |
| `src/Modules/Engagement/Engagement.Application/GetLearnerStreak.cs` | Modify | Add `FreezesAvailable` to `StreakDto` + handler |
| `src/Host/Program.cs` | Modify | `POST /me/streak-freezes` endpoint |
| `tests/Engagement.Domain.Tests/LearnerStreakTests.cs` | Modify | Grant, gap-bridging, projection, event tests |
| `tests/Engagement.Integration.Tests/Infrastructure/StreakPersistenceTests.cs` | Modify | `FreezeBalance` round-trip |
| `tests/Engagement.Integration.Tests/Application/StreakApplicationTests.cs` | Modify | `GrantStreakFreeze` + DTO field |
| `tests/Engagement.Integration.Tests/EndToEnd/StreakApiTests.cs` | Modify | Grant + freeze-preserves + clamp E2E |

---

## Task 1: Freeze inventory — `FreezeBalance` + `GrantFreeze()` with cap

**Files:**
- Modify: `src/Modules/Engagement/Engagement.Domain/LearnerStreak.cs`
- Test: `tests/Engagement.Domain.Tests/LearnerStreakTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `LearnerStreakTests`:

```csharp
[Fact]
public void Granting_a_freeze_increments_the_balance()
{
    var s = NewUtcLearner();
    s.GrantFreeze();
    Assert.Equal(1, s.FreezeBalance);
}

[Fact]
public void Freeze_balance_is_clamped_at_the_cap()
{
    var s = NewUtcLearner();
    for (var i = 0; i < 5; i++) s.GrantFreeze();
    Assert.Equal(LearnerStreak.MaxFreezes, s.FreezeBalance);
    Assert.Equal(2, LearnerStreak.MaxFreezes);
}

[Fact]
public void New_learner_starts_with_zero_freezes()
{
    Assert.Equal(0, NewUtcLearner().FreezeBalance);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: FAIL — compile error, `LearnerStreak` has no `FreezeBalance`/`GrantFreeze`/`MaxFreezes`.

- [ ] **Step 3: Add the field, constant, and operation**

In `LearnerStreak.cs`, add the property next to the other state (after `LastQualifyingDate`):

```csharp
    public int FreezeBalance { get; private set; }

    public const int MaxFreezes = 2;
```

And add the operation (place it after `ChangeTimeZone`):

```csharp
    public void GrantFreeze() =>
        FreezeBalance = Math.Min(FreezeBalance + 1, MaxFreezes);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: PASS (all existing tests still pass — no behavior changed yet).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Engagement/Engagement.Domain/LearnerStreak.cs tests/Engagement.Domain.Tests/LearnerStreakTests.cs
git commit -m "feat(engagement): add capped FreezeBalance + GrantFreeze to LearnerStreak"
```

---

## Task 2: Gap-bridging in `RegisterQualifyingActivity`

The core write-path rule. Introduces the shared `GapBetween` helper (reused by `Report` in Task 3).

**Files:**
- Modify: `src/Modules/Engagement/Engagement.Domain/LearnerStreak.cs`
- Test: `tests/Engagement.Domain.Tests/LearnerStreakTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `LearnerStreakTests`:

```csharp
[Fact]
public void One_freeze_bridges_a_single_missed_day_and_streak_continues()
{
    var s = NewUtcLearner();
    s.GrantFreeze();
    s.RegisterQualifyingActivity(Noon(2030, 1, 1)); // current 1
    s.RegisterQualifyingActivity(Noon(2030, 1, 3)); // Jan 2 missed → freeze bridges it
    Assert.Equal(2, s.CurrentStreak);   // continued, NOT reset
    Assert.Equal(0, s.FreezeBalance);   // the freeze was consumed
}

[Fact]
public void A_frozen_day_does_not_increment_the_count()
{
    var s = NewUtcLearner();
    s.GrantFreeze();
    s.RegisterQualifyingActivity(Noon(2030, 1, 1)); // current 1
    s.RegisterQualifyingActivity(Noon(2030, 1, 3)); // only Jan 3 adds, Jan 2 was merely bridged
    Assert.Equal(2, s.CurrentStreak);   // 1 (Jan1) + 1 (Jan3); the bridged Jan2 added nothing
}

[Fact]
public void Two_freezes_bridge_a_two_day_gap()
{
    var s = NewUtcLearner();
    s.GrantFreeze();
    s.GrantFreeze();
    s.RegisterQualifyingActivity(Noon(2030, 1, 1));
    s.RegisterQualifyingActivity(Noon(2030, 1, 4)); // Jan 2 + Jan 3 missed → both bridged
    Assert.Equal(2, s.CurrentStreak);
    Assert.Equal(0, s.FreezeBalance);
}

[Fact]
public void Gap_larger_than_balance_resets_and_burns_all_freezes()
{
    var s = NewUtcLearner();
    s.GrantFreeze();                                 // only 1 freeze
    s.RegisterQualifyingActivity(Noon(2030, 1, 1));
    s.RegisterQualifyingActivity(Noon(2030, 1, 4)); // 2-day gap, only 1 freeze → reset
    Assert.Equal(1, s.CurrentStreak);
    Assert.Equal(0, s.FreezeBalance);                // the freeze was burned trying
}

[Fact]
public void Same_day_does_not_consume_a_freeze()
{
    var s = NewUtcLearner();
    s.GrantFreeze();
    s.RegisterQualifyingActivity(Noon(2030, 1, 1));
    s.RegisterQualifyingActivity(new DateTimeOffset(2030, 1, 1, 20, 0, 0, TimeSpan.Zero));
    Assert.Equal(1, s.FreezeBalance);                // untouched
}

[Fact]
public void Longest_survives_a_freeze_bridge()
{
    var s = NewUtcLearner();
    s.GrantFreeze();
    s.RegisterQualifyingActivity(Noon(2030, 1, 1));
    s.RegisterQualifyingActivity(Noon(2030, 1, 2)); // current 2, longest 2
    s.RegisterQualifyingActivity(Noon(2030, 1, 4)); // Jan 3 bridged → current 3
    Assert.Equal(3, s.CurrentStreak);
    Assert.Equal(3, s.LongestStreak);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: FAIL — gaps currently always reset, so the "bridges"/"continues" assertions fail (e.g. `One_freeze_bridges...` gets `CurrentStreak == 1`).

- [ ] **Step 3: Rewrite `RegisterQualifyingActivity` with the gap rule + add `GapBetween`**

Replace the existing `RegisterQualifyingActivity` method body in `LearnerStreak.cs` with:

```csharp
    public void RegisterQualifyingActivity(DateTimeOffset occurredOnUtc)
    {
        var day = TimeZone.LocalDateOf(occurredOnUtc);

        if (LastQualifyingDate is { } last)
        {
            if (day <= last)
                return; // same day (idempotent) or late/out-of-order

            // One freeze is burned per missed day, up to what's held.
            // The streak survives only if freezes cover the WHOLE gap.
            var gap = GapBetween(last, day);
            var consumed = Math.Min(gap, FreezeBalance);
            FreezeBalance -= consumed;

            CurrentStreak = consumed == gap ? CurrentStreak + 1 : 1;
        }
        else
        {
            CurrentStreak = 1;
        }

        LastQualifyingDate = day;
        if (CurrentStreak > LongestStreak)
            LongestStreak = CurrentStreak;

        RaiseDomainEvent(new StreakAdvanced(Id.Value, CurrentStreak, day, occurredOnUtc));
    }

    // Whole days missed strictly between two local dates (0 when consecutive).
    // Only meaningful for to > from, which is the only caller context.
    private static int GapBetween(DateOnly from, DateOnly to) =>
        to.DayNumber - from.DayNumber - 1;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: PASS — including the pre-existing `Gap_resets_to_one_but_longest_is_retained` (balance 0 ⇒ `consumed == 0 != gap` ⇒ reset, unchanged).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Engagement/Engagement.Domain/LearnerStreak.cs tests/Engagement.Domain.Tests/LearnerStreakTests.cs
git commit -m "feat(engagement): bridge streak gaps with freezes (consume = min(gap, balance))"
```

---

## Task 3: Freeze-aware read projection — `Report` + `StreakReport.FreezesAvailable`

**Files:**
- Modify: `src/Modules/Engagement/Engagement.Domain/StreakReport.cs`
- Modify: `src/Modules/Engagement/Engagement.Domain/LearnerStreak.cs`
- Test: `tests/Engagement.Domain.Tests/LearnerStreakTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `LearnerStreakTests`:

```csharp
[Fact]
public void Report_projects_freeze_coverage_without_consuming_it()
{
    var s = NewUtcLearner();
    s.GrantFreeze();                                // balance 1
    s.RegisterQualifyingActivity(Noon(2030, 1, 1)); // current 1, last Jan 1
    var r = s.Report(new DateOnly(2030, 1, 3));     // Jan 2 missed, freeze would cover it
    Assert.Equal(StreakStatus.AtRisk, r.Status);    // protected, not broken
    Assert.Equal(1, r.CurrentStreak);
    Assert.Equal(0, r.FreezesAvailable);            // projected 1 - 1
    Assert.Equal(1, s.FreezeBalance);               // STORED balance unchanged — read is pure
}

[Fact]
public void Report_is_broken_when_gap_exceeds_freezes()
{
    var s = NewUtcLearner();
    s.GrantFreeze();                                // 1 freeze
    s.RegisterQualifyingActivity(Noon(2030, 1, 1));
    var r = s.Report(new DateOnly(2030, 1, 4));     // 2-day gap, only 1 freeze
    Assert.Equal(StreakStatus.Broken, r.Status);
    Assert.Equal(0, r.CurrentStreak);
    Assert.Equal(0, r.FreezesAvailable);
}

[Fact]
public void Report_exposes_balance_while_active()
{
    var s = NewUtcLearner();
    s.GrantFreeze();
    s.RegisterQualifyingActivity(Noon(2030, 1, 1));
    var r = s.Report(new DateOnly(2030, 1, 1));     // active today
    Assert.Equal(StreakStatus.Active, r.Status);
    Assert.Equal(1, r.FreezesAvailable);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: FAIL — compile error, `StreakReport` has no `FreezesAvailable`.

- [ ] **Step 3: Add `FreezesAvailable` to `StreakReport`**

Replace the contents of `StreakReport.cs`:

```csharp
namespace Engagement.Domain;

// Derived view of the streak relative to a given local "today".
public sealed record StreakReport(StreakStatus Status, int CurrentStreak, int LongestStreak, int FreezesAvailable);
```

- [ ] **Step 4: Rewrite `Report` to be freeze-aware (same gap math as the write path)**

Replace the existing `Report` method in `LearnerStreak.cs` with:

```csharp
    public StreakReport Report(DateOnly today)
    {
        if (LastQualifyingDate is not { } last)
            return new StreakReport(StreakStatus.None, 0, LongestStreak, FreezeBalance);

        if (today <= last)
            return new StreakReport(StreakStatus.Active, CurrentStreak, LongestStreak, FreezeBalance);

        // Project the same rule the write path applies — without mutating.
        var gap = GapBetween(last, today);
        var consumed = Math.Min(gap, FreezeBalance);

        return consumed == gap
            ? new StreakReport(StreakStatus.AtRisk, CurrentStreak, LongestStreak, FreezeBalance - consumed)
            : new StreakReport(StreakStatus.Broken, 0, LongestStreak, FreezeBalance - consumed);
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: PASS — including pre-existing `Report_is_at_risk_the_next_day` (gap 0 ⇒ AtRisk) and `Report_is_broken_after_a_missed_day_with_effective_zero` (gap 1, balance 0 ⇒ Broken).

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Engagement/Engagement.Domain/StreakReport.cs src/Modules/Engagement/Engagement.Domain/LearnerStreak.cs tests/Engagement.Domain.Tests/LearnerStreakTests.cs
git commit -m "feat(engagement): freeze-aware StreakReport projection (pure read)"
```

---

## Task 4: `StreakFrozen` domain event

Pattern-only (no subscriber yet), raised when freezes actually bridged a gap. Matches the YAGNI stance of `StreakAdvanced`/`XpAwarded`.

**Files:**
- Create: `src/Modules/Engagement/Engagement.Domain/StreakFrozen.cs`
- Modify: `src/Modules/Engagement/Engagement.Domain/LearnerStreak.cs`
- Test: `tests/Engagement.Domain.Tests/LearnerStreakTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `LearnerStreakTests` (top of file already has `using Engagement.Domain;`):

```csharp
[Fact]
public void Bridging_a_gap_raises_StreakFrozen_with_days_frozen()
{
    var s = NewUtcLearner();
    s.GrantFreeze();
    s.RegisterQualifyingActivity(Noon(2030, 1, 1));
    s.RegisterQualifyingActivity(Noon(2030, 1, 3)); // Jan 2 bridged

    var frozen = Assert.Single(s.DomainEvents.OfType<StreakFrozen>());
    Assert.Equal(1, frozen.DaysFrozen);
    Assert.Equal(new DateOnly(2030, 1, 3), frozen.Date);
}

[Fact]
public void A_normal_consecutive_advance_raises_no_StreakFrozen()
{
    var s = NewUtcLearner();
    s.RegisterQualifyingActivity(Noon(2030, 1, 1));
    s.RegisterQualifyingActivity(Noon(2030, 1, 2)); // consecutive, no freeze used
    Assert.Empty(s.DomainEvents.OfType<StreakFrozen>());
}

[Fact]
public void A_reset_that_burns_freezes_raises_no_StreakFrozen()
{
    var s = NewUtcLearner();
    s.GrantFreeze();                                // 1 freeze, gap will be 2
    s.RegisterQualifyingActivity(Noon(2030, 1, 1));
    s.RegisterQualifyingActivity(Noon(2030, 1, 4)); // burns the freeze but still resets
    Assert.Empty(s.DomainEvents.OfType<StreakFrozen>());
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: FAIL — compile error, `StreakFrozen` does not exist.

- [ ] **Step 3: Create the event**

Create `src/Modules/Engagement/Engagement.Domain/StreakFrozen.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

// Raised when one or more freezes were spent to keep a streak alive across missed days.
// No subscriber yet (pattern only); the natural future "streak saved!" notification hook.
public sealed record StreakFrozen(
    Guid LearnerId,
    int DaysFrozen,
    DateOnly Date) : IDomainEvent;
```

- [ ] **Step 4: Raise it from `RegisterQualifyingActivity`**

In `LearnerStreak.cs`, the gap branch currently computes `consumed` and sets `CurrentStreak`. Capture whether the streak survived and raise the event. Replace the gap branch (inside `if (LastQualifyingDate is { } last)`) with:

```csharp
            if (day <= last)
                return; // same day (idempotent) or late/out-of-order

            // One freeze is burned per missed day, up to what's held.
            // The streak survives only if freezes cover the WHOLE gap.
            var gap = GapBetween(last, day);
            var consumed = Math.Min(gap, FreezeBalance);
            FreezeBalance -= consumed;

            var survived = consumed == gap;
            CurrentStreak = survived ? CurrentStreak + 1 : 1;

            if (survived && consumed > 0)
                RaiseDomainEvent(new StreakFrozen(Id.Value, consumed, day));
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Engagement/Engagement.Domain/StreakFrozen.cs src/Modules/Engagement/Engagement.Domain/LearnerStreak.cs tests/Engagement.Domain.Tests/LearnerStreakTests.cs
git commit -m "feat(engagement): raise StreakFrozen domain event when a gap is bridged"
```

---

## Task 5: Persist `FreezeBalance` — EF mapping + migration

**Files:**
- Modify: `src/Modules/Engagement/Engagement.Infrastructure/LearnerStreakConfiguration.cs`
- Create (generated): `src/Modules/Engagement/Engagement.Infrastructure/Migrations/*_AddStreakFreeze.cs`
- Test: `tests/Engagement.Integration.Tests/Infrastructure/StreakPersistenceTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `StreakPersistenceTests`:

```csharp
[Fact]
public async Task Freeze_balance_round_trips()
{
    var id = new LearnerId(Guid.NewGuid());

    await using (var ctx = NewContext())
    {
        var repo = new LearnerStreakRepository(ctx);
        var s = LearnerStreak.Create(id);
        s.GrantFreeze();
        s.GrantFreeze();
        await repo.AddAsync(s, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);
    }

    await using (var ctx = NewContext())
    {
        var reloaded = await new LearnerStreakRepository(ctx).GetAsync(id, CancellationToken.None);
        Assert.Equal(2, reloaded!.FreezeBalance);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~StreakPersistenceTests.Freeze_balance_round_trips"`
Expected: FAIL — the `FreezeBalance` column does not exist in the migrated schema (SqlException on the round-trip), since no migration adds it yet.

- [ ] **Step 3: Map `FreezeBalance` explicitly in the EF configuration**

In `LearnerStreakConfiguration.cs`, after the `LastQualifyingDate` property line, add:

```csharp
        builder.Property(s => s.FreezeBalance);
```

- [ ] **Step 4: Generate the migration**

Run (PowerShell, from repo root):

```powershell
dotnet ef migrations add AddStreakFreeze `
  -p src/Modules/Engagement/Engagement.Infrastructure `
  -s src/Modules/Engagement/Engagement.Infrastructure -o Migrations
```

Then open the generated `*_AddStreakFreeze.cs` and confirm `Up` adds the column as NOT NULL with a zero default for existing rows, e.g.:

```csharp
migrationBuilder.AddColumn<int>(
    name: "FreezeBalance",
    schema: "engagement",
    table: "LearnerStreaks",
    type: "int",
    nullable: false,
    defaultValue: 0);
```

If the generated `defaultValue` is missing, add `defaultValue: 0` so existing rows backfill. Do not hand-edit anything else.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~StreakPersistenceTests"`
Expected: PASS — the test factory's `EnsureDeleted` + `Migrate` rebuilds the schema with the new column. The existing `Streak_round_trips_with_timezone_and_dateonly` still passes.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Engagement/Engagement.Infrastructure/LearnerStreakConfiguration.cs src/Modules/Engagement/Engagement.Infrastructure/Migrations tests/Engagement.Integration.Tests/Infrastructure/StreakPersistenceTests.cs
git commit -m "feat(engagement): persist FreezeBalance + AddStreakFreeze migration"
```

---

## Task 6: `GrantStreakFreeze` command + handler

**Files:**
- Create: `src/Modules/Engagement/Engagement.Application/GrantStreakFreeze.cs`
- Test: `tests/Engagement.Integration.Tests/Application/StreakApplicationTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `StreakApplicationTests` (reuses the existing private `InMemoryStreaks` repo in that file):

```csharp
[Fact]
public async Task Grant_creates_streak_and_adds_a_freeze()
{
    var repo = new InMemoryStreaks();
    var handler = new GrantStreakFreezeHandler(repo);
    var learnerId = Guid.NewGuid();

    await handler.HandleAsync(new GrantStreakFreeze(learnerId), CancellationToken.None);

    var s = await repo.GetAsync(new LearnerId(learnerId), CancellationToken.None);
    Assert.Equal(1, s!.FreezeBalance);
}

[Fact]
public async Task Grant_respects_the_cap()
{
    var repo = new InMemoryStreaks();
    var handler = new GrantStreakFreezeHandler(repo);
    var learnerId = Guid.NewGuid();

    for (var i = 0; i < 5; i++)
        await handler.HandleAsync(new GrantStreakFreeze(learnerId), CancellationToken.None);

    var s = await repo.GetAsync(new LearnerId(learnerId), CancellationToken.None);
    Assert.Equal(LearnerStreak.MaxFreezes, s!.FreezeBalance);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~StreakApplicationTests"`
Expected: FAIL — compile error, `GrantStreakFreeze`/`GrantStreakFreezeHandler` do not exist.

- [ ] **Step 3: Create the command + handler**

Create `src/Modules/Engagement/Engagement.Application/GrantStreakFreeze.cs` (mirrors `SetLearnerTimeZone`):

```csharp
using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record GrantStreakFreeze(Guid LearnerId) : IRequest<Unit>;

public sealed class GrantStreakFreezeHandler(ILearnerStreakRepository repository)
    : IRequestHandler<GrantStreakFreeze, Unit>
{
    public async Task<Unit> HandleAsync(GrantStreakFreeze request, CancellationToken ct)
    {
        var id = new LearnerId(request.LearnerId);

        var streak = await repository.GetAsync(id, ct);
        if (streak is null)
        {
            streak = LearnerStreak.Create(id);
            await repository.AddAsync(streak, ct);
        }

        streak.GrantFreeze();
        await repository.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~StreakApplicationTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Engagement/Engagement.Application/GrantStreakFreeze.cs tests/Engagement.Integration.Tests/Application/StreakApplicationTests.cs
git commit -m "feat(engagement): GrantStreakFreeze command + handler"
```

---

## Task 7: Expose `FreezesAvailable` on the streak query

**Files:**
- Modify: `src/Modules/Engagement/Engagement.Application/GetLearnerStreak.cs`
- Test: `tests/Engagement.Integration.Tests/Application/StreakApplicationTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `StreakApplicationTests`:

```csharp
[Fact]
public async Task Query_exposes_projected_freezes_available()
{
    var repo = new InMemoryStreaks();
    var learnerId = Guid.NewGuid();
    await new GrantStreakFreezeHandler(repo).HandleAsync(new GrantStreakFreeze(learnerId), CancellationToken.None);
    await new RegisterStreakForLessonCompletedHandler(repo)
        .HandleAsync(new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), Noon(2030, 1, 1)), CancellationToken.None);

    var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero));
    var dto = await new GetLearnerStreakHandler(repo, clock)
        .HandleAsync(new GetLearnerStreak(learnerId), CancellationToken.None);

    Assert.Equal(1, dto.FreezesAvailable);
}

[Fact]
public async Task Unknown_learner_reports_zero_freezes()
{
    var dto = await new GetLearnerStreakHandler(new InMemoryStreaks(), new FakeTimeProvider())
        .HandleAsync(new GetLearnerStreak(Guid.NewGuid()), CancellationToken.None);
    Assert.Equal(0, dto.FreezesAvailable);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~StreakApplicationTests"`
Expected: FAIL — compile error, `StreakDto` has no `FreezesAvailable`.

- [ ] **Step 3: Add the field to `StreakDto` and populate it**

In `GetLearnerStreak.cs`, change the `StreakDto` record to:

```csharp
public sealed record StreakDto(
    Guid LearnerId, int CurrentStreak, int LongestStreak, string Status, DateOnly? LastQualifyingDate, int FreezesAvailable);
```

In `GetLearnerStreakHandler.HandleAsync`, update both return sites:

```csharp
        if (streak is null)
            return new StreakDto(request.LearnerId, 0, 0, nameof(StreakStatus.None), null, 0);

        var today = streak.TimeZone.LocalDateOf(clock.GetUtcNow());
        var report = streak.Report(today);
        return new StreakDto(request.LearnerId, report.CurrentStreak, report.LongestStreak,
            report.Status.ToString(), streak.LastQualifyingDate, report.FreezesAvailable);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~StreakApplicationTests"`
Expected: PASS (the existing `Query_reports_active_on_the_local_today` and `Query_returns_none_for_unknown_learner` still pass — the new positional field is appended).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Engagement/Engagement.Application/GetLearnerStreak.cs tests/Engagement.Integration.Tests/Application/StreakApplicationTests.cs
git commit -m "feat(engagement): expose projected FreezesAvailable on GetLearnerStreak"
```

---

## Task 8: `POST /me/streak-freezes` endpoint + end-to-end

**Files:**
- Modify: `src/Host/Program.cs`
- Test: `tests/Engagement.Integration.Tests/EndToEnd/StreakApiTests.cs`

- [ ] **Step 1: Extend the test-local response record and write the failing E2E tests**

In `StreakApiTests.cs`, update the private response record to include the new field:

```csharp
    private sealed record StreakResponse(Guid LearnerId, int CurrentStreak, int LongestStreak, string Status, DateOnly? LastQualifyingDate, int FreezesAvailable);
```

Then add these tests (distinct learners + month ranges so they don't interfere with existing tests under the shared factory clock):

```csharp
[Fact]
public async Task A_freeze_preserves_the_streak_across_a_missed_day()
{
    var learner = Guid.NewGuid();
    var client = ClientFor(learner);

    var grant = await client.PostAsync("/me/streak-freezes", null);
    Assert.Equal(HttpStatusCode.OK, grant.StatusCode);

    factory.Clock.SetUtcNow(Noon(2030, 2, 1).AddHours(1));
    await CompleteLessonOn(learner, Noon(2030, 2, 1));
    factory.Clock.SetUtcNow(Noon(2030, 2, 2).AddHours(1));
    await CompleteLessonOn(learner, Noon(2030, 2, 2));

    // Skip Feb 3. Feb 4 completion → the freeze bridges Feb 3, streak survives to 3.
    factory.Clock.SetUtcNow(Noon(2030, 2, 4).AddHours(1));
    await CompleteLessonOn(learner, Noon(2030, 2, 4));

    var dto = await client.GetFromJsonAsync<StreakResponse>("/me/streak");
    Assert.Equal(3, dto!.CurrentStreak);
    Assert.Equal("Active", dto.Status);
    Assert.Equal(0, dto.FreezesAvailable); // the freeze was consumed bridging Feb 3
}

[Fact]
public async Task Granting_beyond_the_cap_is_clamped()
{
    var learner = Guid.NewGuid();
    var client = ClientFor(learner);

    for (var i = 0; i < 5; i++)
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/me/streak-freezes", null)).StatusCode);

    // No qualifying activity yet → status None, but the capped balance is visible.
    factory.Clock.SetUtcNow(Noon(2030, 3, 1));
    var dto = await client.GetFromJsonAsync<StreakResponse>("/me/streak");
    Assert.Equal("None", dto!.Status);
    Assert.Equal(2, dto.FreezesAvailable);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~StreakApiTests"`
Expected: FAIL — `POST /me/streak-freezes` returns 404 (endpoint not mapped), so the grant assertion fails.

- [ ] **Step 3: Map the endpoint**

In `src/Host/Program.cs`, add after the `GET /me/streak` mapping (before `app.Run();`):

```csharp
app.MapPost("/me/streak-freezes",
    async (ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        await mediator.SendAsync(new GrantStreakFreeze(user.LearnerId), ct);
        return Results.Ok();
    });
```

`GrantStreakFreeze` is in `Engagement.Application`, already imported via `using Engagement.Application;` at the top of `Program.cs`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~StreakApiTests"`
Expected: PASS — all StreakApiTests (existing + new).

- [ ] **Step 5: Commit**

```bash
git add src/Host/Program.cs tests/Engagement.Integration.Tests/EndToEnd/StreakApiTests.cs
git commit -m "feat(host): POST /me/streak-freezes grants a freeze; streak survives a bridged day end-to-end"
```

---

## Task 9: Full verification + status update

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Run the entire test suite**

Run: `dotnet test`
Expected: PASS — all projects, including `Engagement.Domain.Tests`, `Engagement.Integration.Tests` (application, persistence, e2e, contracts, mediator, **architecture**). The architecture tests must still pass: no new cross-module references were introduced (the event lives in `Engagement.Domain`, the command in `Engagement.Application`).

- [ ] **Step 2: Update the project status**

In `CLAUDE.md`, under `## Status`, mark sub-project 3 done and adjust "Next". Replace the `⏭️ Next` line with:

```markdown
- ✅ **Sub-project 3 — Streak freeze** (PR #3): auto-applied, lazily-settled, capped freeze on
  `LearnerStreak`. One rule — `consumed = min(gap, FreezeBalance)`, survive ⇔ `consumed == gap` —
  shared by the write path and the pure read projection. Abstract `GrantStreakFreeze` acquisition
  seam (`POST /me/streak-freezes`); no nightly job; idempotency preserved via advancing
  `LastQualifyingDate`. Raises a (subscriber-less) `StreakFrozen` event.
- ⏭️ **Next:** leagues → real Learning engine → real Identity (and a real freeze economy —
  earning/buying — when Billing exists).
```

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: mark streak-freeze sub-project complete; next is leagues"
```

- [ ] **Step 4: Finish the branch**

Use the **superpowers:finishing-a-development-branch** skill to push `feat/streak-freeze` and open PR #3 against `main`.

---

## Self-review notes

- **Spec coverage:** auto-apply + gap-bridging (Task 2), lazy pure projection (Task 3), cap (Task 1), `StreakFrozen` (Task 4), persistence + migration (Task 5), `GrantStreakFreeze` (Task 6), `freezesAvailable` on read (Task 7), `POST /me/streak-freezes` + idempotency-preserving behavior exercised E2E (Task 8), backward-compat verified by the untouched existing tests passing in every task. All 8 acceptance criteria map to tasks.
- **Type consistency:** `FreezeBalance`, `MaxFreezes`, `GrantFreeze()`, `GapBetween`, `StreakReport.FreezesAvailable`, `StreakFrozen(LearnerId, DaysFrozen, Date)`, `GrantStreakFreeze` / `GrantStreakFreezeHandler`, `StreakDto(..., FreezesAvailable)` are used identically across tasks.
- **No placeholders:** every code step shows full code; every run step shows the command and expected result.
