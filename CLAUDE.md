# CLAUDE.md

Guidance for AI assistants (and humans) working in this repository.

## What this project is

A **Duolingo-style language-learning app** (lessons, streaks, XP) built as a **learning
vehicle** for **system design, Domain-Driven Design (DDD), and Clean Architecture**. It is
not a commercial product — the goal is to practice designing and building complex systems
well. **Explain the *why* behind decisions; teach, don't just produce code.**

## Tech stack

- **Backend:** C# / .NET 10 (`net10.0`), ASP.NET Core Minimal APIs
- **Frontend (future):** Angular SPA (explicitly **not** Razor)
- **Persistence:** EF Core 10 on SQL Server **LocalDB** (`(localdb)\MSSQLLocalDB`)
- **Tests:** xUnit; NetArchTest for architecture rules
- **Mediator:** hand-rolled (no MediatR dependency) — see `docs/design/building-blocks/mediator.md`

## Architecture: evolutionary modular monolith

One deployable (`src/Host`), but every module boundary is designed so it *could* be
extracted into a service later. Modules: **Identity** (generic), **Learning** (supporting),
**Engagement** (⭐ core — XP/streaks/leagues), **Notifications/Social/Billing** (generic).

### Non-negotiable rules (enforced by project references + NetArchTest)

1. **No direct cross-module type references.** Modules communicate only through the
   `BuildingBlocks/Contracts` assembly (integration events/DTOs) and the in-process mediator.
2. **The Dependency Rule.** Inside a module, dependencies point inward:
   `Presentation → Infrastructure → Application → Domain`. The **Domain references nothing**
   infrastructural (no EF Core, no ASP.NET). This is enforced structurally (the `*.Domain`
   project simply doesn't reference those packages) and by tests in
   `tests/Engagement.Integration.Tests/Architecture/`.
3. **No cross-schema JOINs.** Each module owns one SQL schema (e.g. `engagement`). Crossing a
   boundary means an event or a contract, never a JOIN.
4. **The core gets the richest model.** Engagement is where expressive aggregates live.
   Supporting/generic modules stay deliberately simple — resist gold-plating them.

### Tactical patterns in use

- **Aggregates** are the only mutators of their own state (no public setters); invariants live
  inside them. Example: `LearnerEngagement.AwardXp(...)`.
- **Value objects** (inherit `BuildingBlocks.Domain.ValueObject`) encode invariants and kill
  primitive obsession: `LearnerId`, `Xp`, `XpAward`.
- **Domain events** (`IDomainEvent`) are raised by aggregates; **integration events**
  (`INotification` in `Contracts`) cross module boundaries via the mediator.
- **CQRS-lite:** every use case is an `IRequest`/`IRequestHandler` (commands/queries) or an
  `INotificationHandler`. Cross-cutting concerns go in `IPipelineBehavior`s.
- **Idempotency is a domain rule** (the `AppliedAward` ledger), not an infra trick.
- **EF value-converter querying:** compare/order by the *whole* value object (`s.Week == week`,
  `OrderBy(s => s.Week)`); never reach into a converted member (`s.Week.Start`) — EF can't
  translate it. Value-equality (via `ValueObject`) lets VOs serve as (composite) keys.
- **"Now" comes from the injected `TimeProvider`** in handlers, never `DateTimeOffset.UtcNow` —
  except a domain event may stamp its own `OccurredOn` inline (the `XpAwarded` convention).

## Solution layout

```
src/
  Host/                              ASP.NET Core composition root + Minimal API endpoints
  BuildingBlocks/{Domain,Mediator,Contracts}
  Modules/Engagement/{Engagement.Domain, .Application, .Infrastructure}
  Modules/Learning/Learning.Stub     disposable event producer (real Learning comes later)
tests/
  Engagement.Domain.Tests            fast pure-domain unit tests
  Engagement.Integration.Tests       mediator, application, persistence, e2e, architecture
docs/
  superpowers/specs/                 design specs (+ archived diagrams)
  superpowers/plans/                 implementation plans
  design/building-blocks/            building-block explainers (e.g. the mediator)
```

## Common commands (PowerShell)

```powershell
dotnet build                                   # build the solution
dotnet test                                    # run ALL tests
dotnet test tests/Engagement.Domain.Tests      # fast domain tests only
dotnet test tests/<Project> --filter "FullyQualifiedName~<ClassName>"  # one test class (TDD loop)
dotnet run --project src/Host                  # run the API (see "Database" note)

# EF migrations (uses the design-time factory → DuolingoEngagement_Design DB)
dotnet ef migrations add <Name> `
  -p src/Modules/Engagement/Engagement.Infrastructure `
  -s src/Modules/Engagement/Engagement.Infrastructure -o Migrations
```

## Database

- **LocalDB is on-demand** — no service to start; the first connection spins it up.
- **Tests self-manage their databases** (`EnsureDeleted` + `Migrate`) using isolated names:
  `DuolingoEngagement_Test`, `DuolingoEngagement_E2E`, `DuolingoEngagement_Design`.
- **One unique DB name per persistence/e2e test class** — xUnit runs classes in parallel, so a
  shared name races `EnsureDeleted`.
- **`FakeTimeProvider` is forward-only** (`SetUtcNow` throws going backward). E2E classes sharing a
  factory share its clock, so a test needing a different time window needs its own factory.
- The running Host uses `DuolingoEngagement` (see `appsettings.json`).
- **Known gap / TODO:** the Host does **not** auto-apply migrations on startup yet, and only
  `Engagement.Infrastructure` references `Microsoft.EntityFrameworkCore.Design`. To run the
  app against a populated dev DB today you must apply the schema manually. A startup
  migration step (or adding Design to Host) is a reasonable future ergonomic improvement.

## How we work (process)

This project follows a deliberate cycle, one sub-project at a time:
**brainstorm → spec (`docs/superpowers/specs`) → plan (`docs/superpowers/plans`) → TDD
implementation**. Each sub-project gets its own branch (`feat/<name>`) and PR.

- **TDD is the default:** write a failing test → confirm red → minimal code → green → commit.
  Keep steps small; commit frequently.
- **Diagrams belong in the docs.** Every diagram produced while designing must be captured in
  `docs/` as Mermaid (and the original archived if it came from the visual companion). Don't
  leave learning artifacts in ephemeral scratch folders.
- **Keep the domain framework-free.** If you're tempted to add EF/ASP.NET to a `*.Domain`
  project, stop — the model belongs there, the mechanism belongs in Infrastructure.

## Git

- **Identity (repo-local):** Ythalo Saldanha <ythalorossy@gmail.com>.
- **Remote:** https://github.com/ythalorossy/duolingo-ddd-clean-arch
- Branch per sub-project; open a PR rather than committing implementation to `main`.
- Commit messages use Conventional Commits (`feat:`, `test:`, `docs:`, `chore:`).

## Status

- ✅ **Sub-project 1 — Engagement XP walking skeleton** (PR #1): earn + read XP end-to-end.
- ✅ **Sub-project 2 — Streaks** (PR #2): timezone-correct daily streaks (current + longest) as
  a derived `LearnerStreak` aggregate reacting to `LessonCompleted`; renamed
  `LearnerEngagement` → `XpAccount`. Also fixed a latent slice-1 bug (owned `AppliedAward`
  now uses a store-generated key so re-awards INSERT instead of UPDATE).
- ✅ **Sub-project 3 — Streak freeze** (PR #3): auto-applied, lazily-settled, capped freeze on
  `LearnerStreak`. One rule — `consumed = min(gap, FreezeBalance)`, survive ⇔ `consumed == gap` —
  shared by the write path and the pure read projection. Abstract `GrantStreakFreeze` acquisition
  seam (`POST /me/streak-freezes`); no nightly job; idempotency preserved via advancing
  `LastQualifyingDate`. Raises a (subscriber-less) `StreakFrozen` event.
- ✅ **Sub-project 4 — Leagues, Slice 1 (skeleton)** (PR #4): weekly XP accumulation as a
  per-learner `LeagueStanding` (Bronze, UTC Monday-anchored week, lazy roll), fed by `XpAwarded`
  through a new in-process **domain-event dispatcher** building block (`IDomainEventHandler<>` +
  re-entrancy-guarded dispatch in `EngagementDbContext`); leaderboard read at `GET /me/league`.
  Idempotency inherited free from the `AppliedAward` ledger (no second `XpAwarded`).
- ✅ **Sub-project 4 — Leagues, Slice 2 (settlement)** (PR #5): cohort-wide promotion/demotion
  behind an explicit `SettleLeagueWeek` seam — `LeagueStanding` reshaped to per-(learner, week)
  rows so week-N history survives; `floor(0.2·N)` promote/demote with Bronze/Diamond edges;
  per-week `LeagueWeekSettlement` idempotency marker; subscriber-less `Promoted`/`Demoted` events;
  `POST /leagues/weeks/{weekStart}/settle`. The automatic trigger (scheduler / lazy-on-activity)
  is deferred — the command is the seam.
- ✅ **Sub-project 4 — Leagues, Slice 3 (automatic trigger)** (branch `feat/leagues-auto-settlement`):
  a feature-flagged `BackgroundService` (`LeagueSettlementScheduler`, the repo's first) periodically
  settles every ended-but-unsettled week via a new `SettleDueLeagueWeeks` policy command, which sends
  the unchanged `SettleLeagueWeek` once per due week (oldest-first → the chain holds; idempotent via
  the Slice-2 marker). `PeriodicTimer` is fed the injected `TimeProvider` (so `FakeTimeProvider` drives
  it in tests); disabled in the E2E hosts via `Leagues:Settlement:Enabled=false`. New repo query
  `GetDistinctEndedWeeksAsync`; no schema change / no migration.
- ⏭️ **Next:** real Learning engine → real Identity (and a real freeze economy — earning/buying —
  when Billing exists); a subscriber for the `Promoted`/`Demoted` events.
