# Sub-project 6 — Frontend · Slice 1: Catalog browse (walking skeleton)

**Date:** 2026-07-22
**Status:** Approved (design)
**Introduces:** the project's **first frontend** — an Angular SPA, built as a *second learning
vehicle* where the backend's Clean-Architecture / DDD discipline is re-expressed in Angular terms.

**Part of:** **Frontend (Angular SPA)** — a multi-slice study that grows one bounded-context library at a
time, mirroring how the backend grew module by module:
- **Slice 1 (this document):** read-only **catalog browse** — the walking skeleton. `shell` + `contracts`
  + `learning` libraries; Swashbuckle + CORS on the Host; DTO generation; a signal-based store; enforced
  boundaries live.
- **Slice 2 (later):** **do-a-lesson** (read + write) — forms, the write/command path, an
  `X-Learner-Id` interceptor, richer error states, first Playwright e2e.
- **Slice 3 (later):** **learner dashboard** (`engagement` library) — XP + streak + league composed; the
  likely graduation point to **NgRx SignalStore**.
- **Later:** real `identity` library when backend Identity lands; league/promotion UI.

## Product context

The learner-facing app is (eventually) a 2D tile/overworld map of lessons. Slice 1 does **not** build
that — it builds the truest possible walking skeleton: a single read that proves the whole pipe
(Angular → HTTP → the existing API → screen), touching every architectural layer once, with no auth, no
forms, and no write. Every later slice layers architecture in only when a feature justifies it — the same
restraint the backend followed (the domain-event dispatcher, the settlement scheduler, and the
`X-Learner-Id`/interceptor seam were each added the slice that earned them).

## Goal

Stand up the Angular workspace and render the course catalog (`GET /courses` → Course → Unit → Lesson)
end-to-end, with the architectural scaffolding that makes the frontend a genuine DDD/Clean-Architecture
study: a **multi-project workspace**, a **library per bounded context**, an **anti-corruption mapping
seam**, and **enforced import boundaries** (the frontend analogue of NetArchTest). `GET /courses` is
anonymous today, so slice 1 deliberately does not touch the fake-identity story — that lands with slice 2.

## Decisions settled in brainstorming

1. **Role of the frontend:** a *second learning vehicle* for frontend architecture (not a thin demo) —
   held to the same rigor as the backend.
2. **First slice:** read-only **catalog browse** (`GET /courses`) — thinnest thing that still touches
   every layer.
3. **Location:** same repo, a new `web/` folder (backend + frontend slices stay in lockstep; docs and
   branch-per-slice rhythm preserved).
4. **Angular baseline:** modern Angular assumed (standalone components, signals, `inject()`, new control
   flow) — design effort spent on architecture, not framework mechanics.
5. **Structure:** bounded contexts as **enforced feature areas**, built up incrementally.
6. **Workspace model:** a **multi-project Angular workspace** (`ng new web --no-create-application`) with a
   **library per bounded context** — chosen over a single app with folders because a library's
   `public-api.ts` is a *structural* boundary.
7. **Enforcement:** Angular CLI + **ESLint boundary rules** (no Nx) — the rule-based analogue of
   NetArchTest, sitting cleanly inside `web/`.
8. **API contract (hybrid):** add **Swashbuckle** to the Host; **generate DTO types only** into the
   `contracts` library; **hand-write** the repositories and DTO → domain mapping (the anti-corruption
   seam is authored deliberately).
9. **State management:** **native signals + a store service now**; graduate to **NgRx SignalStore** when a
   later slice earns it (likely Slice 3's dashboard).

## Architecture — workspace layout

```
duolingo/
├── src/            # backend (one small Host prereq — see "Backend prerequisites")
├── tests/
├── docs/
└── web/                                # ← the Angular workspace (new)
    ├── angular.json                    # config for all projects (≈ Duolingo.slnx)
    ├── tsconfig.json                   # path aliases → each library's public-api.ts
    ├── eslint.config.js                # boundary rules (≈ NetArchTest)
    ├── package.json
    └── projects/
        ├── shell/                      # application: routing, layout, composition root
        ├── contracts/                  # library: generated DTOs (≈ BuildingBlocks/Contracts)
        └── learning/                   # library: the first bounded context
```

**Slice 1 creates only `shell` + `contracts` + `learning`.** `engagement` and `identity` are designed
for but not built until their slices arrive.

**Backend ↔ frontend mapping (the learning payoff):**

| Backend | Frontend |
|---|---|
| `src/Host` (composition root + endpoints) | `projects/shell` (routing, layout, providers) |
| `BuildingBlocks/Contracts` assembly | `projects/contracts` library |
| `Modules/Learning/*` (a bounded context) | `projects/learning` library |
| Project references + NetArchTest | `public-api.ts` + ESLint boundary rules |

## Internal layering of a bounded-context library

Inside `projects/learning/src/`, the backend's `Domain → Application → Infrastructure → Presentation`
inward-dependency rule is re-expressed:

```
learning/src/
├── public-api.ts          # the ONLY external surface (Slice 1: exports the lazy routes)
└── lib/
    ├── domain/            # frontend domain model — pure, framework-light
    │   └── course.ts          # Course / Unit / Lesson view models + client-side display state
    ├── data/              # anti-corruption layer: talks HTTP, maps DTO → domain
    │   ├── catalog.repository.ts   # HttpClient + generated DTOs from @duolingo/contracts
    │   └── catalog.mapper.ts       # DTO → domain mapping (the hand-authored seam)
    ├── application/       # state + use cases (signal-based)
    │   └── catalog.store.ts        # CatalogStore: signals, computed, load()
    └── ui/                # components + routes (presentation)
        ├── catalog-page/           # smart component: injects the store
        └── course-tree/            # dumb components: input()/output() only
```

**The dependency rule (`ui → application → data → domain`), enforced by ESLint:**
- `domain` imports **nothing** from the other layers (mirrors "Domain references nothing infrastructural").
- `data` may import `domain` and `@duolingo/contracts` — nothing from `ui`/`application`.
- `ui` may import `application` + `domain`, but **not** `data` directly (components never call `HttpClient`;
  they go through the store).
- Only `public-api.ts` is reachable from `shell` or other libraries.

**Two deliberate parallels:**
1. **The mapper is the anti-corruption layer.** Generated DTOs never leak past `data`; the rest of the
   library speaks its own `domain` types — like a backend module hand-maps a contract into its aggregate.
2. **The store is CQRS-lite, client-side.** `CatalogStore.load()` is a query use-case; command-style
   methods join it when writes arrive (Slice 2) — the same command/query split as `IRequestHandler`s.

**Anti-over-mirroring caution:** the Slice-1 `domain` is *thin* (real invariants live server-side). It
holds real view-model types plus any genuinely client-side rule (e.g. a lesson's `isLocked` /
`isPublished` display state). It must **not** invent server-owned invariants to fatten the layer — that
would be ceremony, not learning.

## Data flow (Slice 1)

```
CatalogPage (ui, smart)
   │  loads on init → store.load()
   ▼
CatalogStore (application)          signals: courses, loading, error  (+ computed, e.g. lesson count)
   │  calls repository, sets signals
   ▼
CatalogRepository (data)
   │  HttpClient.get<CourseDto[]>('/courses')   ← DTO type from @duolingo/contracts
   ▼
CatalogMapper (data)   CourseDto[] → Course[]   ← anti-corruption seam
   ▲
   └── domain Course/Unit/Lesson flows back up; ui renders with @for / @if
```

- Store state: `courses = signal<Course[]>([])`, `loading = signal(false)`,
  `error = signal<string | null>(null)`, plus one or two `computed` values.
- `course-tree` dumb components take `Course` via `input()` and render — no injection, no HTTP.

## Backend prerequisites (`src/Host` — the only backend change Slice 1 needs)

1. **CORS.** The SPA (Angular dev server, `http://localhost:4200`) is a different origin than the API. Add
   a named, **config-driven** CORS policy allowing the dev origin (`GET` suffices for Slice 1; widen per
   slice). Not hard-coded — environment-specific.
2. **OpenAPI via Swashbuckle.** Add `Swashbuckle.AspNetCore`; expose `/swagger/v1/swagger.json`. This is
   the source the `contracts` library generates from, and closes the previously-noted "no Swagger" gap.

**Contract-generation loop (hybrid — option C):**

```
src/Host ──Swashbuckle──▶ /swagger/v1/swagger.json
                                  │  ng-openapi-gen (npm run generate:contracts)
                                  ▼
        web/projects/contracts/src/generated/*.ts   (DTO types ONLY)
                                  │  re-exported via contracts/public-api.ts
                                  ▼
        learning/data imports @duolingo/contracts, maps DTO → domain
```

- Generated files land in `contracts/src/generated/`, are **committed** (reproducible build without a
  running backend), regenerated via an explicit npm script, and **never hand-edited**.
- Generate **DTO types only** — not client services. Repositories and mapping stay hand-authored (the
  deliberate seam).

## Error handling

- The `data` layer catches HTTP failures and translates them into a **domain-meaningful result** — the
  store never sees an `HttpErrorResponse` (same anti-corruption spirit as the mapper). Slice 1:
  network/5xx → `error` signal set to a friendly message; empty catalog → an explicit empty-state, not an
  error.
- The `ui` renders three states off the store signals: `loading` → skeleton/spinner, `error` → retry
  affordance, `courses` → the tree. This tri-state pattern becomes the template for every future screen.
- A single app-level `HttpInterceptor` (in `shell`) is **designed for but deferred** to Slice 2, when
  auth headers (`X-Learner-Id`) and cross-cutting error handling arrive — mirrors adding an
  `IPipelineBehavior` only when a cross-cutting concern is justified. Slice 1 keeps error handling local
  to the repository (no premature infrastructure).

## Boundary enforcement (the frontend "NetArchTest")

Two layers, mirroring the backend's structural + rule-based pair.

**Layer 1 — Structural (`public-api.ts` + TS path aliases ≈ project references + Contracts assembly).**
Each library exposes only what its `public-api.ts` exports; the rest is unreachable across the boundary.
`web/tsconfig.json` maps aliases (`@duolingo/contracts`, `@duolingo/learning` → each library's
`public-api.ts`). For Slice 1, `learning/public-api.ts` exports only the lazy **routes**; the store,
repository, mapper, and domain types stay private. This is the literal analogue of "modules communicate
only through Contracts."

**Layer 2 — Rule-based (ESLint ≈ NetArchTest).** `eslint.config.js` encodes two rule families that fail
CI on violation:
1. **Cross-context rules:** `learning` may import `@duolingo/contracts` but **not** any sibling context
   (`@duolingo/engagement`, …); contexts meet only through `contracts`. `shell` may import any library's
   public-api; libraries may **not** import `shell`.
2. **Intra-context layer rules** (enforcing `ui → application → data → domain`): `domain/**` imports from
   no other layer; `ui/**` may not import `data/**` (must go through `application`); nothing imports
   another library's internals (only its path alias / public-api).

Tooling: `eslint-plugin-boundaries` with per-folder element tags (preferred), or scoped
`no-restricted-imports` patterns (fallback). Either way a disallowed import fails `ng lint` / CI — you
cannot accidentally cross a boundary.

**Caveat:** `public-api.ts` already makes most cross-library reaches impossible, so Layer 2's
cross-context rules are partly belt-and-suspenders. Both are kept because (a) the *intra*-library layer
rules have no structural equivalent and genuinely need ESLint, and (b) explicit rules document intent.

## Testing strategy

| Test kind | Scope | Slice-1 targets |
|---|---|---|
| **Unit (pure)** | `domain` types, `CatalogMapper` | DTO → domain mapping; any client-side invariant (≈ `*.Domain.Tests`) |
| **Store / component** | `CatalogStore` + smart component with a **fake repository** | `load()` sets loading → courses; error path sets the error signal |
| **Boundary / architecture** | `ng lint` with the ESLint boundary rules | a deliberately-bad import fails lint (≈ NetArchTest) |
| **E2E (deferred)** | Playwright against a running Host | introduced in Slice 2 when a *write* is worth proving end-to-end |

- Test runner: Angular's scaffolded default (**Karma/Jasmine**). Vitest/Jest noted as a conscious future
  tooling study; not swapped in Slice 1.
- Highest-value early tests: the **mapper** and the **boundary** tests — they protect the two things this
  slice exists to teach (the anti-corruption seam and the enforced boundaries).

## Scope

### In scope (Slice 1)
- `web/` multi-project Angular workspace: `shell` app + `contracts` + `learning` libraries; `angular.json`,
  root `tsconfig.json` path aliases, `eslint.config.js`, `package.json`; `.gitignore` gains
  `node_modules`.
- Host prerequisites: config-driven **CORS** policy + **Swashbuckle** OpenAPI document.
- `contracts`: generated DTO types (`GET /courses` shapes) + `public-api.ts`; `npm run generate:contracts`.
- `learning`: `domain` (Course/Unit/Lesson view models), `data` (`CatalogRepository` + `CatalogMapper`),
  `application` (`CatalogStore`), `ui` (`catalog-page` smart + `course-tree` dumb), lazy routes exported
  from `public-api.ts`.
- ESLint boundary rules (cross-context + intra-layer); the two structural + rule-based enforcement layers.
- Tests: mapper unit, store/component with fake repository, a boundary/lint test.

### Out of scope (later slices)
- Any **write** path, forms, or the `POST /attempts` flow (Slice 2).
- `X-Learner-Id` / auth, the `HttpInterceptor` (Slice 2).
- `engagement` / `identity` libraries; XP / streak / league UI (Slice 3+).
- NgRx SignalStore (graduation deferred to when a slice earns it).
- Playwright e2e (Slice 2).
- The 2D overworld map rendering; visual/theming polish beyond the tri-state pattern.
- Host auto-applying migrations / deployment concerns.

## Success criteria

- `ng serve` renders the real catalog from a running Host: Course → Unit → Lesson, with visible
  loading / error / empty states.
- The catalog DTOs are **generated** from the Host's OpenAPI doc and consumed via `@duolingo/contracts`;
  no hand-maintained wire types.
- A deliberately-illegal import (e.g. `ui` importing `data`, or `learning` importing a sibling context)
  **fails `ng lint`**.
- Mapper and store/component tests pass; `dotnet build` / `dotnet test` remain green (backend prereqs
  don't regress existing behaviour).
- `learning/public-api.ts` exposes only the routes; the store/repository/mapper/domain are unreachable
  from `shell`.
```
