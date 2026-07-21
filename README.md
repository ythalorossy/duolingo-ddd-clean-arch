# Duolingo Clone — a System Design / DDD / Clean Architecture learning project

A Duolingo-style language-learning app (lessons, streaks, XP) built **to learn how to design
and build complex systems well** — using **strategic & tactical Domain-Driven Design**,
**Clean Architecture**, and an **evolutionary modular-monolith** approach. The product is the
vehicle; the real deliverable is the engineering practice.

> This repository documents not just *what* was built, but *why* — every significant decision
> is captured (with rejected alternatives) in [`docs/superpowers/specs`](docs/superpowers/specs).

## Objectives

- Practice **strategic DDD**: subdomains (core / supporting / generic), bounded contexts, a
  context map, and integration patterns.
- Practice **tactical DDD**: aggregates, value objects, domain events, invariants.
- Practice **Clean Architecture**: the Dependency Rule and Dependency Inversion, enforced by
  the compiler *and* by tests.
- Practice **system-design judgement**: where to invest modeling effort, how to keep module
  boundaries honest, and how to design for future scale without over-engineering today.
- Practice **disciplined delivery**: spec → plan → test-driven implementation, small commits.

## Tech stack

| Layer | Choice |
|---|---|
| Backend | C# / .NET 10, ASP.NET Core Minimal APIs |
| Frontend (planned) | Angular SPA |
| Persistence | EF Core 10 · SQL Server LocalDB |
| Tests | xUnit · NetArchTest |
| Messaging | Hand-rolled in-process mediator (CQRS-lite) |

## Architecture at a glance

An **evolutionary modular monolith**: one deployable today, but every module boundary is
designed so it *could* become a service later. The **Engagement** context is the **core
domain** (the habit-forming loop — XP, streaks, leagues); everything else is supporting or
generic.

```mermaid
flowchart TB
    Identity["Identity & Access<br/><i>generic</i>"]
    Learning["Learning<br/><i>supporting</i><br/>catalog · engine · grading · progress"]
    Engagement["⭐ Engagement<br/><b>CORE</b><br/>XP · streaks · leagues"]
    Notifications["Notifications<br/><i>generic</i>"]
    Social["Social<br/><i>generic</i>"]
    Billing["Billing<br/><i>generic</i>"]

    Identity -- "UserId (Conformist)" --> Learning
    Identity -- "UserId (Conformist)" --> Engagement
    Learning == "LessonCompleted event" ==> Engagement
    Engagement -- "reminders" --> Notifications
    Engagement <-- "leaderboards" --> Social
    Billing -. "entitlements (ACL)" .-> Engagement
```

Each module is internally layered (Clean Architecture); the **Domain depends on nothing**
infrastructural. Full reasoning in
[`docs/superpowers/specs/2026-05-28-architecture-foundations-design.md`](docs/superpowers/specs/2026-05-28-architecture-foundations-design.md).

## Project structure

```
src/
  Host/                              composition root + Minimal API
  BuildingBlocks/{Domain,Mediator,Contracts}
  Modules/Engagement/{Engagement.Domain, .Application, .Infrastructure}
  Modules/Learning/Learning.Stub
tests/
  Engagement.Domain.Tests            pure domain unit tests
  Engagement.Integration.Tests       mediator · application · persistence · e2e · architecture
docs/
  superpowers/specs/                 design specs + archived diagrams
  superpowers/plans/                 implementation plans
  design/building-blocks/            explainers (e.g. the hand-rolled mediator)
```

## Getting started

### Prerequisites

- **.NET 10 SDK** (`dotnet --version` → 10.x)
- **SQL Server LocalDB** (`MSSQLLocalDB`) — ships with Visual Studio / SQL Server Express.
  It starts on demand; nothing to run manually.
- **EF Core CLI** (for migrations): `dotnet tool install --global dotnet-ef`

### Build & test

```powershell
dotnet build
dotnet test          # 114 tests: 60 domain unit + 54 integration (incl. end-to-end on LocalDB)
```

The integration tests create and migrate their own databases automatically, then clean up.

### The API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/lessons/{lessonId}/complete` | Complete a (stubbed) lesson → awards XP, advances the streak, accrues league XP |
| `GET`  | `/me/xp` | The learner's total XP |
| `GET`  | `/me/streak` | Current & longest streak, status, freezes available |
| `PUT`  | `/me/timezone` | Set the learner's IANA time zone (drives streak day boundaries) |
| `POST` | `/me/streak-freezes` | Grant a streak freeze (abstract acquisition seam) |
| `GET`  | `/me/league` | The learner's weekly league leaderboard |
| `POST` | `/leagues/weeks/{weekStart}/settle` | Settle a league week — promote/demote cohorts |

The current user is faked via an `X-Learner-Id` header (real auth arrives with the Identity
module). See `CLAUDE.md` for a note on running the Host against a dev database.

## Documentation

Every sub-project is captured as a **spec** (the *why*, with rejected alternatives) and a
**plan** (its TDD task breakdown).

- **Design foundations** (strategic DDD, architecture, build order): [`docs/superpowers/specs/2026-05-28-architecture-foundations-design.md`](docs/superpowers/specs/2026-05-28-architecture-foundations-design.md)
- **All specs:** [`docs/superpowers/specs`](docs/superpowers/specs) · **all plans:** [`docs/superpowers/plans`](docs/superpowers/plans) — XP skeleton, streaks, streak freeze, leagues (slices 1–3), and Learning (slices 1 & 2)
- **How the mediator works:** [`docs/design/building-blocks/mediator.md`](docs/design/building-blocks/mediator.md)

## Status & roadmap

- ✅ **Sub-project 1 — XP walking skeleton:** earn + read XP end-to-end.
- ✅ **Sub-project 2 — Streaks:** timezone-correct daily streaks (current + longest) as a derived
  aggregate reacting to `LessonCompleted`.
- ✅ **Sub-project 3 — Streak freeze:** auto-applied, lazily-settled, capped freeze — one shared
  rule across the write path and the read projection, no nightly job.
- ✅ **Sub-project 4 — Leagues:** weekly XP accumulation + leaderboard (Slice 1), cohort
  promotion/demotion at week close (Slice 2), and a feature-flagged background scheduler that settles
  ended weeks automatically (Slice 3) — fed by an in-process domain-event dispatcher.
- ✅ **Sub-project 5 — Learning:** a real content catalog (Course → Unit → Lesson) with validated
  completion (Slice 1), then **earned completion** (Slice 2) — multiple-choice exercises inside the
  `Lesson` aggregate, `Lesson.Grade` scoring against a pass threshold, a persisted `Attempt`, and
  `POST /lessons/{id}/attempts` publishing `LessonCompleted` only on a pass (server-authoritative grading).
- ⏭️ **Next:** Learning Slice 3 (per-learner progress / mastery / unlocking + completion economy) → real
  Identity; a subscriber for the `Promoted`/`Demoted` events.

172 tests green. Each step follows its own **brainstorm → spec → plan → TDD** cycle on a dedicated
branch.
