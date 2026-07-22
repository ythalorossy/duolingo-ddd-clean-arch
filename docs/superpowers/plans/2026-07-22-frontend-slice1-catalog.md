# Frontend Slice 1 — Catalog Browse Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up an Angular multi-project workspace under `web/` that renders the course catalog (`GET /courses` → Course → Unit → Lesson) end-to-end, with a library-per-bounded-context structure, an anti-corruption mapping seam, generated DTO types, and lint-enforced import boundaries.

**Architecture:** A multi-project Angular workspace (`shell` app + `contracts` + `learning` libraries) mirrors the backend's Clean-Architecture/DDD layout. Each library exposes only its `public-api.ts` (structural boundary ≈ project references); ESLint boundary rules enforce cross-context and intra-layer dependency rules (≈ NetArchTest). The `learning` library is layered `ui → application → data → domain`; generated DTOs live in `contracts` and are hand-mapped to a `learning` domain model in the `data` layer.

**Tech Stack:** Angular 21 (standalone components, signals, `inject()`, new control flow, zoneless by default), TypeScript (strict), ESLint + `eslint-plugin-boundaries`, `openapi-typescript` (types-only codegen), **Vitest** via the `@angular/build:unit-test` builder (globals enabled). Backend: .NET 10, ASP.NET Core Minimal APIs, `Microsoft.AspNetCore.OpenApi` (built-in), xUnit + `WebApplicationFactory`.

## Global Constraints

- **Backend target framework:** `net10.0` (do not change any `*.csproj` TargetFramework).
- **OpenAPI producer:** built-in `Microsoft.AspNetCore.OpenApi` (NOT Swashbuckle — net10 compatibility). Document served at `/openapi/v1.json`. This refines the spec's "Swashbuckle" to the framework-native equivalent; same goal.
- **Type generator:** `openapi-typescript` (types only, zero runtime). Refines the spec's "DTO types only" — no client services are generated. `contracts` stays a pure-types library.
- **Frontend location:** everything frontend lives under `web/`. Never add frontend files outside `web/`.
- **Angular idioms (assumed, non-negotiable):** standalone components only (no NgModules), signals for state, `inject()` over constructor DI in stores/services, new control flow (`@if`/`@for`), `input()`/`output()` for dumb components.
- **⚠️ Angular-21 environment (CONFIRMED at scaffold time — supersedes any earlier "Karma/Jasmine" wording in this plan):**
  - **Toolchain pinned to Angular 21** (CLI 21.2.x). Always run the CLI as `npx ng …` from inside `web/` (local v21). `@angular/cli@latest` (v22) is blocked on this machine's Node.
  - **Test runner is Vitest** via the `@angular/build:unit-test` builder, with **globals enabled** (`tsconfig.spec.json` has `"types": ["vitest/globals"]`). Therefore in `*.spec.ts`: `describe`, `it`, `expect`, `beforeEach`, `afterEach`, and **`vi`** are **global — do NOT import them**. Use **`vi.fn()`** for spies (NOT `jasmine.createSpy`). Run tests with `npx ng test <project> --no-watch` (Vitest flag; `--watch=false` also accepted).
  - **Zoneless by default.** `app.config.ts` was generated with the zoneless change-detection provider — **keep generated providers and MERGE** new ones in; never replace the providers array wholesale. In component tests drive change detection explicitly with `fixture.detectChanges()` / `await fixture.whenStable()` (no zone.js).
  - **Generated file names (Angular-21 style, no `.component.`/`Component` suffix):** shell root component is `projects/shell/src/app/app.ts` (class **`App`**), with `app.config.ts`, `app.routes.ts` (exports `routes`), `app.html` (separate template). Default library entries are `projects/contracts/src/lib/contracts.ts` (class `Contracts`) + `contracts.spec.ts`, and `projects/learning/src/lib/learning.ts` (class `Learning`) + `learning.spec.ts`; each `public-api.ts` re-exports `./lib/<name>`. **No service files were generated.** Hand-authored files in later tasks may keep the plan's chosen names/paths (e.g. `catalog-page.component.ts`, class `CatalogPageComponent`) — that's fine; only the *generated* files follow the new convention.
- **Dependency rule (enforced by ESLint):** inside a library, imports point inward `ui → application → data → domain`; `domain` imports nothing from sibling layers; `ui` never imports `data`. Across libraries, contexts meet only through `@duolingo/contracts`; nothing imports `shell`.
- **Generated code:** committed to git, never hand-edited, regenerated via `npm run generate:contracts`.
- **"Now"/time & auth:** slice 1 is anonymous read-only — do NOT add `X-Learner-Id`, interceptors, or auth. Those are Slice 2.
- **Commit style:** Conventional Commits (`feat:`, `test:`, `chore:`, `docs:`). Frontend commits scope `feat(web):`.
- **Backend tests must stay green:** `dotnet build` and `dotnet test` pass after every backend task.

---

## File Structure

**Backend (modified):**
- `src/Host/Host.csproj` — add `Microsoft.AspNetCore.OpenApi` package reference.
- `src/Host/Program.cs` — register OpenAPI + a config-driven CORS policy; add `.Produces<CatalogDto>()` metadata to `/courses`; call `app.MapOpenApi()` and `app.UseCors(...)`.
- `src/Host/appsettings.json` — add a `Cors:AllowedOrigins` array.
- `tests/Learning.Integration.Tests/EndToEnd/OpenApiAndCorsTests.cs` — new e2e tests for the doc + CORS headers (reuses existing `LearningApiFactory`).

**Frontend (created under `web/`):**
- `web/angular.json`, `web/package.json`, `web/tsconfig.json` (+ path aliases), `web/eslint.config.js`, `web/ng-openapi-typescript` npm script.
- `web/openapi.json` — committed snapshot of the Host's OpenAPI doc (generation input).
- `web/projects/contracts/` — pure-types library: `src/generated/openapi-types.ts` (generated), `src/public-api.ts` (hand-written friendly aliases).
- `web/projects/learning/src/lib/` — `domain/course.ts`, `data/catalog.mapper.ts`, `data/catalog.repository.ts`, `application/catalog.store.ts`, `ui/catalog-page/`, `ui/course-tree/`; `learning.routes.ts`; `src/public-api.ts` (exports routes only).
- `web/projects/shell/` — app with routing to the lazy `learning` routes, root layout, providers (`provideHttpClient`, base API URL).

---

## Task 1: Backend — expose OpenAPI document + typed `/courses` response

**Files:**
- Modify: `src/Host/Host.csproj`
- Modify: `src/Host/Program.cs` (endpoint at lines 38-40; builder section ~11-18; after `var app = builder.Build();` ~35)
- Test: `tests/Learning.Integration.Tests/EndToEnd/OpenApiAndCorsTests.cs` (create)

**Interfaces:**
- Consumes: existing `GetCatalog`/`CatalogDto` from `Learning.Application`; existing `LearningApiFactory` (`WebApplicationFactory<Program>`).
- Produces: an OpenAPI document at `GET /openapi/v1.json` whose components include the `CatalogDto` schema. No new C# public types.

- [ ] **Step 1: Write the failing test**

Create `tests/Learning.Integration.Tests/EndToEnd/OpenApiAndCorsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Learning.Integration.Tests.EndToEnd;

public class OpenApiAndCorsTests(LearningApiFactory factory) : IClassFixture<LearningApiFactory>
{
    [Fact]
    public async Task OpenApi_document_is_served_and_describes_the_catalog()
    {
        var json = await factory.CreateClient().GetStringAsync("/openapi/v1.json");

        using var doc = JsonDocument.Parse(json);
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");

        Assert.True(schemas.TryGetProperty("CatalogDto", out _),
            "OpenAPI components must include the CatalogDto schema so the frontend can generate its types.");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~OpenApiAndCorsTests"`
Expected: FAIL — `GET /openapi/v1.json` returns 404 (no OpenAPI registered yet).

- [ ] **Step 3: Add the OpenAPI package**

Add to `src/Host/Host.csproj` inside a `<ItemGroup>` (a new one is fine):

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
  </ItemGroup>
```

Then run `dotnet restore` (Host). If version `10.0.0` is unavailable, run `dotnet add src/Host package Microsoft.AspNetCore.OpenApi` to let the SDK pick the matching net10 version.

- [ ] **Step 4: Register and map OpenAPI, and add response metadata**

In `src/Host/Program.cs`, add to the builder section (after line 12, `AddScoped<ICurrentUser...>`):

```csharp
builder.Services.AddOpenApi();
```

After `var app = builder.Build();` (line 35), add:

```csharp
app.MapOpenApi(); // serves /openapi/v1.json
```

Change the `/courses` endpoint (lines 38-40) to declare its response type so the schema is emitted:

```csharp
app.MapGet("/courses",
    async (IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.SendAsync(new GetCatalog(), ct)))
    .Produces<CatalogDto>();
```

Add the needed using at the top if not already present via `ImplicitUsings` (Program.cs already imports `Engagement.Application`; `CatalogDto` lives in `Learning.Application`, already imported at line 5). No extra using required.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~OpenApiAndCorsTests"`
Expected: PASS.

- [ ] **Step 6: Confirm the whole backend still builds and tests green**

Run: `dotnet build` then `dotnet test`
Expected: build succeeds; all tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Host/Host.csproj src/Host/Program.cs tests/Learning.Integration.Tests/EndToEnd/OpenApiAndCorsTests.cs
git commit -m "feat(host): serve OpenAPI document and declare /courses response type"
```

---

## Task 2: Backend — config-driven CORS for the SPA dev origin

**Files:**
- Modify: `src/Host/appsettings.json`
- Modify: `src/Host/Program.cs`
- Test: `tests/Learning.Integration.Tests/EndToEnd/OpenApiAndCorsTests.cs` (add a test)

**Interfaces:**
- Consumes: `Cors:AllowedOrigins` config array; the DB-free `OpenApiApiFactory` created in Task 1 (the test class's fixture).
- Produces: a named CORS policy `"spa"` applied app-wide (`app.UseCors("spa")`); an `Access-Control-Allow-Origin` header echoed on any cross-origin GET from the configured origin.

> **Adapted from the original plan (Task 1 deviation).** Task 1's `OpenApiAndCorsTests` uses a **DB-free** `OpenApiApiFactory` (it added a dedicated factory to avoid a parallel-DB race). So this CORS test must NOT probe `/courses` (that would need the DB and 500 under the DB-free factory). Instead it probes the DB-free **`/openapi/v1.json`** endpoint with an `Origin` header — CORS is applied app-wide via `app.UseCors("spa")`, so the header is emitted on *any* endpoint's response, and this keeps the whole test class DB-free. This tests the real (non-preflight) cross-origin GET path.

- [ ] **Step 1: Write the failing test + tidy usings**

In `tests/Learning.Integration.Tests/EndToEnd/OpenApiAndCorsTests.cs`, **remove the unused `using System.Net.Http.Json;`** (neither test needs it) and ensure the remaining usings are `System.Net;`, `System.Text.Json;`, `Xunit;`. Then add this test method to the `OpenApiAndCorsTests` class:

```csharp
    [Fact]
    public async Task Cross_origin_get_from_the_spa_origin_echoes_allow_origin_header()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:4200");

        // Probe the DB-free OpenAPI endpoint; the "spa" policy is applied app-wide,
        // so the CORS header appears on any endpoint's response without needing the DB.
        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"),
            "A cross-origin GET from the SPA dev origin must echo an Access-Control-Allow-Origin header.");
        Assert.Contains("http://localhost:4200",
            response.Headers.GetValues("Access-Control-Allow-Origin"));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~OpenApiAndCorsTests"`
Expected: the new test FAILS — no `Access-Control-Allow-Origin` header present.

- [ ] **Step 3: Add allowed origins to config**

Add to `src/Host/appsettings.json` (top-level, after `"AllowedHosts": "*",`):

```json
  "Cors": {
    "AllowedOrigins": [ "http://localhost:4200" ]
  },
```

- [ ] **Step 4: Register and apply the CORS policy**

In `src/Host/Program.cs`, add to the builder section (near the other `AddScoped`/`AddOpenApi` lines):

```csharp
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(options =>
    options.AddPolicy("spa", policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));
```

After `var app = builder.Build();` and before the endpoints (place it just before `app.MapOpenApi();`), add:

```csharp
app.UseCors("spa");
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~OpenApiAndCorsTests"`
Expected: both tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Host/appsettings.json src/Host/Program.cs tests/Learning.Integration.Tests/EndToEnd/OpenApiAndCorsTests.cs
git commit -m "feat(host): add config-driven CORS policy for the SPA dev origin"
```

---

## Task 3: Capture the OpenAPI snapshot for the frontend

**Files:**
- Create: `web/openapi.json` (committed generation input)

**Interfaces:**
- Consumes: the running Host's `/openapi/v1.json`.
- Produces: `web/openapi.json` — a committed OpenAPI 3 document containing the `CatalogDto`/`CourseDto`/`UnitDto`/`LessonDto` schemas, consumed by Task 6's generator.

> This task has no unit test; its deliverable is a committed file verified by inspection. It is the seam that lets frontend codegen run without a live backend later.

- [ ] **Step 1: Run the Host**

Run (in one terminal): `dotnet run --project src/Host`
Wait for `Now listening on: http://localhost:5225`.

- [ ] **Step 2: Fetch the OpenAPI document into the repo**

Run (in another terminal), creating the `web/` folder if needed:

```bash
mkdir -p web
curl -s http://localhost:5225/openapi/v1.json -o web/openapi.json
```

- [ ] **Step 3: Verify the snapshot contains the catalog schemas**

Run: `grep -o '"CatalogDto"\|"CourseDto"\|"UnitDto"\|"LessonDto"' web/openapi.json | sort -u`
Expected: all four schema names printed. Stop the Host (`Ctrl+C`).

- [ ] **Step 4: Commit**

```bash
git add web/openapi.json
git commit -m "chore(web): capture OpenAPI snapshot for contract generation"
```

---

## Task 4: Scaffold the Angular multi-project workspace

**Files:**
- Create: `web/` workspace (`angular.json`, `package.json`, `tsconfig.json`, etc.) with **no root application**.
- Modify: `.gitignore` (repo root) — ignore `web/node_modules` and Angular caches.

**Interfaces:**
- Consumes: nothing.
- Produces: an Angular workspace rooted at `web/` where `ng generate application|library` can add projects; `web/tsconfig.json` present for later path aliases.

> Scaffolding task — no test; deliverable verified by `ng` reporting the workspace and `npm ls` succeeding.

- [ ] **Step 1: Create the workspace with no root app**

Run from the repo root:

```bash
cd web 2>/dev/null || mkdir web
# create the workspace IN-PLACE inside web/: run ng new into a temp then move, OR run from repo root:
cd ..
npx -y @angular/cli@latest new web --no-create-application --directory web --package-manager npm --skip-git
```

Notes: `--skip-git` (the repo already has git at the parent). If `ng new` refuses because `web/` exists and is non-empty (it now holds `openapi.json`), run `ng new duolingo-web --no-create-application --directory web-tmp --skip-git`, then move its files into `web/` preserving `web/openapi.json`. Confirm `web/angular.json` exists at the end.

- [ ] **Step 2: Ignore node_modules and Angular caches**

Add to the repo-root `.gitignore` (append):

```
# Angular / Node (web workspace)
web/node_modules/
web/.angular/
web/dist/
```

- [ ] **Step 3: Verify the workspace**

Run:

```bash
cd web && npm install && npx ng version
```

Expected: Angular CLI prints version info and lists the workspace (no applications yet). `web/node_modules` exists but is gitignored.

- [ ] **Step 4: Commit**

```bash
cd ..
git add web/angular.json web/package.json web/package-lock.json web/tsconfig.json .gitignore
# add any other tracked scaffold files ng created (e.g. web/tsconfig.*.json, web/.editorconfig, web/README.md); do NOT add web/node_modules
git add web/.editorconfig web/README.md 2>/dev/null || true
git commit -m "chore(web): scaffold Angular multi-project workspace (no root app)"
```

---

## Task 5: Generate the `shell` app and `contracts` + `learning` libraries

**Files:**
- Create: `web/projects/shell/` (application), `web/projects/contracts/` (library), `web/projects/learning/` (library) via `ng generate`.
- Modify: `web/tsconfig.json` — add `paths` aliases for the two libraries.

**Interfaces:**
- Consumes: the workspace from Task 4.
- Produces: TypeScript path aliases `@duolingo/contracts` → `projects/contracts/src/public-api.ts` and `@duolingo/learning` → `projects/learning/src/public-api.ts`; a runnable `shell` app (`ng serve shell`).

> Scaffolding task — verified by `ng build`/`ng serve` of the shell; no unit test yet.

- [ ] **Step 1: Generate the three projects**

Run from `web/`:

```bash
npx ng generate application shell --routing --style=css --skip-tests=false
npx ng generate library contracts
npx ng generate library learning
```

- [ ] **Step 2: Add path aliases**

In `web/tsconfig.json`, ensure `compilerOptions.paths` includes (merge with any Angular already added):

```jsonc
"paths": {
  "@duolingo/contracts": ["projects/contracts/src/public-api.ts"],
  "@duolingo/learning": ["projects/learning/src/public-api.ts"]
}
```

(Angular's library generation typically adds `dist`-based aliases; replace them with the `src/public-api.ts` source paths above so the app compiles against library source in this single-workspace setup.)

- [ ] **Step 3: Verify the shell builds and serves**

Run: `npx ng build shell`
Expected: build succeeds. Optionally `npx ng serve shell` → `http://localhost:4200` shows the default app; stop it.

- [ ] **Step 4: Commit**

```bash
cd ..
git add web/angular.json web/tsconfig.json web/projects
git commit -m "chore(web): generate shell app + contracts and learning libraries"
```

---

## Task 6: Generate DTO types into `contracts` + author friendly aliases

**Files:**
- Modify: `web/package.json` — add `openapi-typescript` devDependency + `generate:contracts` script.
- Create: `web/projects/contracts/src/generated/openapi-types.ts` (generated).
- Modify: `web/projects/contracts/src/public-api.ts` — export friendly DTO aliases.
- Test: `web/projects/contracts/src/public-api.spec.ts` (compile-time type test).

**Interfaces:**
- Consumes: `web/openapi.json` (Task 3).
- Produces: exported types `CatalogDto`, `CourseDto`, `UnitDto`, `LessonDto` from `@duolingo/contracts`, matching the backend records:
  - `CatalogDto` = `{ courses: CourseDto[] }`
  - `CourseDto` = `{ id: string; title: string; language: string | null; units: UnitDto[] }`
  - `UnitDto` = `{ id: string; title: string; position: number; lessons: LessonDto[] }`
  - `LessonDto` = `{ id: string; title: string; position: number; isPublished: boolean }`

- [ ] **Step 1: Add the generator + npm script**

Run from `web/`: `npm install -D openapi-typescript`

Add to `web/package.json` `"scripts"`:

```json
"generate:contracts": "openapi-typescript openapi.json -o projects/contracts/src/generated/openapi-types.ts"
```

- [ ] **Step 2: Generate the types**

Run from `web/`: `npm run generate:contracts`
Expected: `projects/contracts/src/generated/openapi-types.ts` created, containing a `components` interface with a `schemas` member including `CatalogDto`, `CourseDto`, `UnitDto`, `LessonDto`.

- [ ] **Step 3: Write the failing type test**

Replace `web/projects/contracts/src/public-api.ts` content is done in Step 4; first write the test at `web/projects/contracts/src/public-api.spec.ts`:

```typescript
import { CatalogDto, CourseDto, LessonDto, UnitDto } from './public-api';

describe('contracts public API', () => {
  it('exposes catalog DTO shapes matching the backend', () => {
    const lesson: LessonDto = { id: 'l1', title: 'Greetings', position: 1, isPublished: true };
    const unit: UnitDto = { id: 'u1', title: 'Basics', position: 1, lessons: [lesson] };
    const course: CourseDto = { id: 'c1', title: 'Spanish', language: 'es', units: [unit] };
    const catalog: CatalogDto = { courses: [course] };

    expect(catalog.courses[0].units[0].lessons[0].isPublished).toBe(true);
    // language is nullable, mirroring string? on the backend
    const noLanguage: CourseDto = { id: 'c2', title: 'X', language: null, units: [] };
    expect(noLanguage.language).toBeNull();
  });
});
```

- [ ] **Step 4: Run the test to verify it fails**

Run from `web/`: `npx ng test contracts --watch=false`
Expected: FAIL — `public-api.ts` does not export `CatalogDto`/`CourseDto`/`UnitDto`/`LessonDto` yet.

- [ ] **Step 5: Author friendly aliases in `public-api.ts`**

Replace `web/projects/contracts/src/public-api.ts` with:

```typescript
// Public API of @duolingo/contracts — the shared wire vocabulary (≈ BuildingBlocks/Contracts).
// Generated wire types are re-exported here as friendly, named aliases. Hand-authored on purpose;
// the generated file (./generated/openapi-types.ts) is never imported outside this file.
import type { components } from './generated/openapi-types';

export type CatalogDto = components['schemas']['CatalogDto'];
export type CourseDto = components['schemas']['CourseDto'];
export type UnitDto = components['schemas']['UnitDto'];
export type LessonDto = components['schemas']['LessonDto'];
```

The generated library created `projects/contracts/src/lib/contracts.ts` (class `Contracts`) and `contracts.spec.ts`, with `public-api.ts` re-exporting `./lib/contracts`. **Delete both `lib/contracts.ts` and `lib/contracts.spec.ts`** (the `public-api.ts` replacement above no longer references them) — `contracts` is a pure-types library with no component. Run `npx ng test contracts --no-watch` to confirm only the type test remains and it passes.

- [ ] **Step 6: Run the test to verify it passes**

Run from `web/`: `npx ng test contracts --watch=false`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
cd ..
git add web/package.json web/package-lock.json web/projects/contracts
git commit -m "feat(web): generate catalog DTO types into contracts library"
```

---

## Task 7: `learning` domain model + DTO→domain mapper (anti-corruption seam)

**Files:**
- Create: `web/projects/learning/src/lib/domain/course.ts`
- Create: `web/projects/learning/src/lib/data/catalog.mapper.ts`
- Test: `web/projects/learning/src/lib/data/catalog.mapper.spec.ts`

**Interfaces:**
- Consumes: `CatalogDto`, `CourseDto`, `UnitDto`, `LessonDto` from `@duolingo/contracts`.
- Produces:
  - domain types `Course`, `Unit`, `Lesson` (readonly view models) with a client-side `isLocked` display flag on `Lesson`.
  - `mapCatalog(dto: CatalogDto): Course[]` — pure function, the only place DTOs are read.

- [ ] **Step 1: Write the failing test**

Create `web/projects/learning/src/lib/data/catalog.mapper.spec.ts`:

```typescript
import { CatalogDto } from '@duolingo/contracts';
import { mapCatalog } from './catalog.mapper';

describe('mapCatalog', () => {
  const dto: CatalogDto = {
    courses: [
      {
        id: 'c1', title: 'Spanish', language: 'es',
        units: [
          {
            id: 'u1', title: 'Basics', position: 1,
            lessons: [
              { id: 'l1', title: 'Greetings', position: 1, isPublished: true },
              { id: 'l2', title: 'Draft', position: 2, isPublished: false },
            ],
          },
        ],
      },
    ],
  };

  it('maps DTO tree into domain Course/Unit/Lesson', () => {
    const [course] = mapCatalog(dto);
    expect(course.title).toBe('Spanish');
    expect(course.units[0].lessons[0].title).toBe('Greetings');
  });

  it('derives a client-side isLocked flag from isPublished', () => {
    const [course] = mapCatalog(dto);
    expect(course.units[0].lessons[0].isLocked).toBe(false); // published
    expect(course.units[0].lessons[1].isLocked).toBe(true);  // unpublished
  });

  it('returns an empty array for an empty catalog', () => {
    expect(mapCatalog({ courses: [] })).toEqual([]);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run from `web/`: `npx ng test learning --watch=false`
Expected: FAIL — `./catalog.mapper` and `mapCatalog` do not exist.

- [ ] **Step 3: Write the domain model**

Create `web/projects/learning/src/lib/domain/course.ts`:

```typescript
// Frontend domain model for the Learning context. Deliberately thin — the authoritative
// invariants live server-side. Holds only view-model shape + genuinely client-side display state.
export interface Lesson {
  readonly id: string;
  readonly title: string;
  readonly position: number;
  /** Client-side display state: an unpublished lesson renders as locked. */
  readonly isLocked: boolean;
}

export interface Unit {
  readonly id: string;
  readonly title: string;
  readonly position: number;
  readonly lessons: readonly Lesson[];
}

export interface Course {
  readonly id: string;
  readonly title: string;
  readonly language: string | null;
  readonly units: readonly Unit[];
}
```

- [ ] **Step 4: Write the mapper**

Create `web/projects/learning/src/lib/data/catalog.mapper.ts`:

```typescript
// Anti-corruption seam: the ONLY place @duolingo/contracts DTOs are read. Wire types never
// leak past this file — the rest of the library speaks the domain model in ./domain/course.
import { CatalogDto, CourseDto, LessonDto, UnitDto } from '@duolingo/contracts';
import { Course, Lesson, Unit } from '../domain/course';

export function mapCatalog(dto: CatalogDto): Course[] {
  return dto.courses.map(mapCourse);
}

function mapCourse(dto: CourseDto): Course {
  return { id: dto.id, title: dto.title, language: dto.language, units: dto.units.map(mapUnit) };
}

function mapUnit(dto: UnitDto): Unit {
  return { id: dto.id, title: dto.title, position: dto.position, lessons: dto.lessons.map(mapLesson) };
}

function mapLesson(dto: LessonDto): Lesson {
  return { id: dto.id, title: dto.title, position: dto.position, isLocked: !dto.isPublished };
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run from `web/`: `npx ng test learning --watch=false`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
cd ..
git add web/projects/learning/src/lib/domain web/projects/learning/src/lib/data/catalog.mapper.ts web/projects/learning/src/lib/data/catalog.mapper.spec.ts
git commit -m "feat(web): learning domain model + DTO->domain catalog mapper"
```

---

## Task 8: `CatalogRepository` — HTTP + error translation

**Files:**
- Create: `web/projects/learning/src/lib/data/catalog.repository.ts`
- Create: `web/projects/learning/src/lib/data/api-base-url.token.ts`
- Test: `web/projects/learning/src/lib/data/catalog.repository.spec.ts`

**Interfaces:**
- Consumes: `HttpClient`; `CatalogDto` from `@duolingo/contracts`; `mapCatalog` (Task 7); an `API_BASE_URL` injection token.
- Produces:
  - `API_BASE_URL` — `InjectionToken<string>`.
  - `CatalogRepository` (`@Injectable`) with `getCatalog(): Observable<Course[]>`; on any HTTP error it emits a rejected observable carrying a domain-friendly `Error` (message `'Unable to load the catalog.'`) — never a raw `HttpErrorResponse`.

- [ ] **Step 1: Write the failing test**

Create `web/projects/learning/src/lib/data/catalog.repository.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CatalogRepository } from './catalog.repository';
import { API_BASE_URL } from './api-base-url.token';

describe('CatalogRepository', () => {
  let repo: CatalogRepository;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://test.local' },
        CatalogRepository,
      ],
    });
    repo = TestBed.inject(CatalogRepository);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('GETs /courses and maps to domain courses', () => {
    let result: unknown;
    repo.getCatalog().subscribe((c) => (result = c));

    const req = http.expectOne('http://test.local/courses');
    expect(req.request.method).toBe('GET');
    req.flush({
      courses: [{ id: 'c1', title: 'Spanish', language: 'es',
        units: [{ id: 'u1', title: 'Basics', position: 1,
          lessons: [{ id: 'l1', title: 'Greetings', position: 1, isPublished: true }] }] }],
    });

    expect((result as any)[0].units[0].lessons[0].isLocked).toBe(false);
  });

  it('translates an HTTP error into a domain-friendly error', () => {
    let error: Error | undefined;
    repo.getCatalog().subscribe({ error: (e) => (error = e) });

    http.expectOne('http://test.local/courses').flush('boom',
      { status: 500, statusText: 'Server Error' });

    expect(error).toBeInstanceOf(Error);
    expect(error!.message).toBe('Unable to load the catalog.');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run from `web/`: `npx ng test learning --watch=false`
Expected: FAIL — repository/token modules do not exist.

- [ ] **Step 3: Create the base-URL token**

Create `web/projects/learning/src/lib/data/api-base-url.token.ts`:

```typescript
import { InjectionToken } from '@angular/core';

/** Base URL of the backend API. Provided by the shell (composition root). */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
```

- [ ] **Step 4: Write the repository**

Create `web/projects/learning/src/lib/data/catalog.repository.ts`:

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { CatalogDto } from '@duolingo/contracts';
import { Course } from '../domain/course';
import { mapCatalog } from './catalog.mapper';
import { API_BASE_URL } from './api-base-url.token';

@Injectable({ providedIn: 'root' })
export class CatalogRepository {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getCatalog(): Observable<Course[]> {
    return this.http.get<CatalogDto>(`${this.baseUrl}/courses`).pipe(
      map(mapCatalog),
      // Anti-corruption: the store never sees an HttpErrorResponse — only a domain-friendly Error.
      catchError(() => throwError(() => new Error('Unable to load the catalog.'))),
    );
  }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run from `web/`: `npx ng test learning --watch=false`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
cd ..
git add web/projects/learning/src/lib/data/api-base-url.token.ts web/projects/learning/src/lib/data/catalog.repository.ts web/projects/learning/src/lib/data/catalog.repository.spec.ts
git commit -m "feat(web): CatalogRepository with domain-friendly error translation"
```

---

## Task 9: `CatalogStore` — signal-based application state

**Files:**
- Create: `web/projects/learning/src/lib/application/catalog.store.ts`
- Test: `web/projects/learning/src/lib/application/catalog.store.spec.ts`

**Interfaces:**
- Consumes: `CatalogRepository` (Task 8); `Course` domain type.
- Produces: `CatalogStore` (`@Injectable`) exposing readonly signals `courses: Signal<Course[]>`, `loading: Signal<boolean>`, `error: Signal<string | null>`, a computed `lessonCount: Signal<number>`, and a `load(): void` method (sets loading, then courses or error).

- [ ] **Step 1: Write the failing test**

Create `web/projects/learning/src/lib/application/catalog.store.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CatalogStore } from './catalog.store';
import { CatalogRepository } from '../data/catalog.repository';
import { Course } from '../domain/course';

function fakeCourses(): Course[] {
  return [{ id: 'c1', title: 'Spanish', language: 'es',
    units: [{ id: 'u1', title: 'Basics', position: 1,
      lessons: [{ id: 'l1', title: 'Greetings', position: 1, isLocked: false }] }] }];
}

describe('CatalogStore', () => {
  function setup(repo: Partial<CatalogRepository>) {
    TestBed.configureTestingModule({
      providers: [CatalogStore, { provide: CatalogRepository, useValue: repo }],
    });
    return TestBed.inject(CatalogStore);
  }

  it('load() populates courses and clears loading on success', () => {
    const store = setup({ getCatalog: () => of(fakeCourses()) });
    store.load();
    expect(store.loading()).toBe(false);
    expect(store.courses().length).toBe(1);
    expect(store.error()).toBeNull();
    expect(store.lessonCount()).toBe(1);
  });

  it('load() sets a friendly error message on failure', () => {
    const store = setup({ getCatalog: () => throwError(() => new Error('Unable to load the catalog.')) });
    store.load();
    expect(store.loading()).toBe(false);
    expect(store.error()).toBe('Unable to load the catalog.');
    expect(store.courses()).toEqual([]);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run from `web/`: `npx ng test learning --watch=false`
Expected: FAIL — `CatalogStore` does not exist.

- [ ] **Step 3: Write the store**

Create `web/projects/learning/src/lib/application/catalog.store.ts`:

```typescript
import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { CatalogRepository } from '../data/catalog.repository';
import { Course } from '../domain/course';

@Injectable({ providedIn: 'root' })
export class CatalogStore {
  private readonly repo = inject(CatalogRepository);

  private readonly _courses = signal<Course[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly courses: Signal<Course[]> = this._courses.asReadonly();
  readonly loading: Signal<boolean> = this._loading.asReadonly();
  readonly error: Signal<string | null> = this._error.asReadonly();
  readonly lessonCount = computed(() =>
    this._courses().reduce((n, c) => n + c.units.reduce((m, u) => m + u.lessons.length, 0), 0));

  /** Query use-case: load the catalog into signal state. */
  load(): void {
    this._loading.set(true);
    this._error.set(null);
    this.repo.getCatalog().subscribe({
      next: (courses) => { this._courses.set(courses); this._loading.set(false); },
      error: (e: Error) => { this._error.set(e.message); this._loading.set(false); },
    });
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run from `web/`: `npx ng test learning --watch=false`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd ..
git add web/projects/learning/src/lib/application
git commit -m "feat(web): signal-based CatalogStore (query use-case)"
```

---

## Task 10: UI — `course-tree` (dumb) + `catalog-page` (smart) + routes

**Files:**
- Create: `web/projects/learning/src/lib/ui/course-tree/course-tree.component.ts`
- Create: `web/projects/learning/src/lib/ui/catalog-page/catalog-page.component.ts`
- Create: `web/projects/learning/src/lib/learning.routes.ts`
- Modify: `web/projects/learning/src/public-api.ts`
- Test: `web/projects/learning/src/lib/ui/course-tree/course-tree.component.spec.ts`
- Test: `web/projects/learning/src/lib/ui/catalog-page/catalog-page.component.spec.ts`

**Interfaces:**
- Consumes: `Course` domain type; `CatalogStore`.
- Produces:
  - `CourseTreeComponent` — standalone, selector `lib-course-tree`, `courses = input.required<readonly Course[]>()`, renders Course→Unit→Lesson; locked lessons get a `locked` CSS class.
  - `CatalogPageComponent` — standalone, injects `CatalogStore`, calls `load()` on init, renders tri-state (loading / error+retry / tree).
  - `LEARNING_ROUTES: Routes` exported from `public-api.ts` (the library's ONLY export).

- [ ] **Step 1: Write the failing dumb-component test**

Create `web/projects/learning/src/lib/ui/course-tree/course-tree.component.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { CourseTreeComponent } from './course-tree.component';
import { Course } from '../../domain/course';

const courses: Course[] = [{ id: 'c1', title: 'Spanish', language: 'es',
  units: [{ id: 'u1', title: 'Basics', position: 1, lessons: [
    { id: 'l1', title: 'Greetings', position: 1, isLocked: false },
    { id: 'l2', title: 'Draft', position: 2, isLocked: true },
  ] }] }];

describe('CourseTreeComponent', () => {
  it('renders course, unit, and lesson titles and marks locked lessons', async () => {
    await TestBed.configureTestingModule({ imports: [CourseTreeComponent] }).compileComponents();
    const fixture = TestBed.createComponent(CourseTreeComponent);
    fixture.componentRef.setInput('courses', courses);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Spanish');
    expect(text).toContain('Basics');
    expect(text).toContain('Greetings');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.locked').length).toBe(1);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run from `web/`: `npx ng test learning --watch=false`
Expected: FAIL — `CourseTreeComponent` does not exist.

- [ ] **Step 3: Write the dumb component**

Create `web/projects/learning/src/lib/ui/course-tree/course-tree.component.ts`:

```typescript
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Course } from '../../domain/course';

@Component({
  selector: 'lib-course-tree',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (course of courses(); track course.id) {
      <section class="course">
        <h2>{{ course.title }}</h2>
        @for (unit of course.units; track unit.id) {
          <div class="unit">
            <h3>{{ unit.title }}</h3>
            <ul>
              @for (lesson of unit.lessons; track lesson.id) {
                <li [class.locked]="lesson.isLocked">{{ lesson.title }}</li>
              }
            </ul>
          </div>
        }
      </section>
    }
  `,
})
export class CourseTreeComponent {
  readonly courses = input.required<readonly Course[]>();
}
```

- [ ] **Step 4: Run the dumb-component test to verify it passes**

Run from `web/`: `npx ng test learning --watch=false`
Expected: the `CourseTreeComponent` test PASSES.

- [ ] **Step 5: Write the failing smart-component test**

Create `web/projects/learning/src/lib/ui/catalog-page/catalog-page.component.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { CatalogPageComponent } from './catalog-page.component';
import { CatalogStore } from '../../application/catalog.store';
import { Course } from '../../domain/course';

class FakeStore {
  courses = signal<Course[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);
  lessonCount = signal(0);
  load = vi.fn();
}

describe('CatalogPageComponent', () => {
  function render(store: FakeStore) {
    TestBed.configureTestingModule({
      imports: [CatalogPageComponent],
      providers: [{ provide: CatalogStore, useValue: store }],
    });
    const fixture = TestBed.createComponent(CatalogPageComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('calls load() on init', () => {
    const store = new FakeStore();
    render(store);
    expect(store.load).toHaveBeenCalled();
  });

  it('shows a loading indicator while loading', () => {
    const store = new FakeStore();
    store.loading.set(true);
    const fixture = render(store);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading');
  });

  it('shows the error with a retry that re-invokes load()', () => {
    const store = new FakeStore();
    store.error.set('Unable to load the catalog.');
    const fixture = render(store);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Unable to load the catalog.');
    el.querySelector('button')!.dispatchEvent(new Event('click'));
    expect(store.load).toHaveBeenCalledTimes(2); // once on init, once on retry
  });
});
```

- [ ] **Step 6: Run the test to verify it fails**

Run from `web/`: `npx ng test learning --watch=false`
Expected: FAIL — `CatalogPageComponent` does not exist.

- [ ] **Step 7: Write the smart component**

Create `web/projects/learning/src/lib/ui/catalog-page/catalog-page.component.ts`:

```typescript
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { CatalogStore } from '../../application/catalog.store';
import { CourseTreeComponent } from '../course-tree/course-tree.component';

@Component({
  selector: 'lib-catalog-page',
  standalone: true,
  imports: [CourseTreeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (store.loading()) {
      <p class="loading">Loading catalog…</p>
    } @else if (store.error(); as message) {
      <div class="error">
        <p>{{ message }}</p>
        <button type="button" (click)="store.load()">Retry</button>
      </div>
    } @else if (store.courses().length === 0) {
      <p class="empty">No courses yet.</p>
    } @else {
      <lib-course-tree [courses]="store.courses()" />
    }
  `,
})
export class CatalogPageComponent implements OnInit {
  readonly store = inject(CatalogStore);
  ngOnInit(): void { this.store.load(); }
}
```

- [ ] **Step 8: Write the routes and update the public API**

Create `web/projects/learning/src/lib/learning.routes.ts`:

```typescript
import { Routes } from '@angular/router';
import { CatalogPageComponent } from './ui/catalog-page/catalog-page.component';

export const LEARNING_ROUTES: Routes = [
  { path: '', component: CatalogPageComponent },
];
```

Replace `web/projects/learning/src/public-api.ts` with (the library's ONLY external surface):

```typescript
// Public API of @duolingo/learning. Slice 1 exposes ONLY the routes — the store, repository,
// mapper, and domain types stay private to the library (structural boundary ≈ project references).
export { LEARNING_ROUTES } from './lib/learning.routes';
```

Delete the default library files `projects/learning/src/lib/learning.ts` (class `Learning`) and `learning.spec.ts` that `ng generate library` created — the `public-api.ts` above replaces the default `export * from './lib/learning'`, so nothing references them anymore.

- [ ] **Step 9: Run all learning tests to verify they pass**

Run from `web/`: `npx ng test learning --watch=false`
Expected: all `learning` tests PASS.

- [ ] **Step 10: Commit**

```bash
cd ..
git add web/projects/learning/src/lib/ui web/projects/learning/src/lib/learning.routes.ts web/projects/learning/src/public-api.ts
git commit -m "feat(web): catalog page + course tree UI with tri-state rendering"
```

---

## Task 11: Wire the shell — routing, HttpClient, API base URL

**Files:**
- Modify: `web/projects/shell/src/app/app.config.ts`
- Modify: `web/projects/shell/src/app/app.routes.ts`
- Modify: `web/projects/shell/src/app/app.ts` (root component, class `App`) and `app.html` (its template)
- Modify: `web/projects/shell/src/environments/environment*.ts` (create if absent)
- Test: `web/projects/shell/src/app/app.routes.spec.ts`

**Interfaces:**
- Consumes: `LEARNING_ROUTES` from `@duolingo/learning`; `API_BASE_URL` token from `@duolingo/learning` (re-export it — see Step 3 note).
- Produces: a running app where `/` (or `/learn`) lazy-loads the learning routes; `HttpClient` provided; `API_BASE_URL` set to `http://localhost:5225` in dev.

> **Note on the token boundary:** `API_BASE_URL` lives in `learning/data`, which is NOT exported from `learning`'s public-api. The shell must set it without importing library internals. Add `export { API_BASE_URL } from './lib/data/api-base-url.token';` to `learning`'s `public-api.ts` so the composition root can provide it — a deliberate, documented part of the public surface (a library may legitimately export a configuration token it needs its consumer to supply).

- [ ] **Step 1: Export the token from the learning public API**

Append to `web/projects/learning/src/public-api.ts`:

```typescript
// The composition root (shell) must supply the API base URL; expose the token for that purpose.
export { API_BASE_URL } from './lib/data/api-base-url.token';
```

- [ ] **Step 2: Add environment files**

Create `web/projects/shell/src/environments/environment.ts`:

```typescript
export const environment = { apiBaseUrl: 'http://localhost:5225' };
```

Create `web/projects/shell/src/environments/environment.development.ts`:

```typescript
export const environment = { apiBaseUrl: 'http://localhost:5225' };
```

(If Angular's `ng generate application` did not create an `environments/` folder, this is expected in newer CLIs — creating them here is correct. No `fileReplacements` change is needed since both point at the dev API for slice 1.)

- [ ] **Step 3: Configure providers (MERGE — do not replace)**

The Angular-21 generated `web/projects/shell/src/app/app.config.ts` already contains a providers array (with the zoneless change-detection provider, a global error-listener provider, and `provideRouter(routes)`). **Keep all of those.** Only ADD two providers — `provideHttpClient()` and the `API_BASE_URL` value — and the imports they need. The result should look like this (preserve whatever generated providers/imports were already present; the two new lines are `provideHttpClient()` and the `API_BASE_URL` entry):

```typescript
// ...existing generated imports stay...
import { provideHttpClient } from '@angular/common/http';
import { API_BASE_URL } from '@duolingo/learning';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    // ...existing generated providers stay (zoneless change detection, error listeners, provideRouter(routes))...
    provideHttpClient(),
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
  ],
};
```

Do NOT remove or swap the generated change-detection provider (the app is zoneless by default).

- [ ] **Step 4: Write the failing routes test**

Create `web/projects/shell/src/app/app.routes.spec.ts`:

```typescript
import { routes } from './app.routes';

describe('app routes', () => {
  it('lazy-loads the learning catalog at the default path', () => {
    const learn = routes.find((r) => r.path === '');
    expect(learn).toBeTruthy();
    expect(typeof learn!.loadChildren).toBe('function');
  });
});
```

- [ ] **Step 5: Run the test to verify it fails**

Run from `web/`: `npx ng test shell --watch=false`
Expected: FAIL — default route does not lazy-load learning yet.

- [ ] **Step 6: Wire the routes**

Set `web/projects/shell/src/app/app.routes.ts` to:

```typescript
import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadChildren: () => import('@duolingo/learning').then((m) => m.LEARNING_ROUTES),
  },
];
```

The generated root component is `web/projects/shell/src/app/app.ts` (class `App`) with its template in the separate file `app.html`. Make the root render only the router outlet:

1. In `app.ts`, ensure the component imports `RouterOutlet` from `@angular/router` (add it to the standalone `imports` array if not already there). Keep the class name `App` and the `templateUrl: './app.html'` reference.
2. Replace the entire contents of `app.html` (which currently holds the "Hello, shell" starter markup) with a single line:

```html
<router-outlet />
```

- [ ] **Step 7: Run the test to verify it passes**

Run from `web/`: `npx ng test shell --watch=false`
Expected: PASS.

- [ ] **Step 8: Build the whole workspace**

Run from `web/`: `npx ng build shell`
Expected: build succeeds (shell compiles against `@duolingo/learning` and `@duolingo/contracts` source).

- [ ] **Step 9: Commit**

```bash
cd ..
git add web/projects/shell web/projects/learning/src/public-api.ts
git commit -m "feat(web): wire shell routing, HttpClient, and API base URL"
```

---

## Task 12: Enforce boundaries with ESLint (the frontend "NetArchTest")

**Files:**
- Modify: `web/package.json` — add ESLint + `eslint-plugin-boundaries` (+ `angular-eslint` if not already added by CLI) and a `lint` script.
- Create/Modify: `web/eslint.config.js` — element-type tags + boundary rules.
- Test (executable): a deliberately-illegal import that must make `npm run lint` fail, then its removal.

**Interfaces:**
- Consumes: the folder layout established in Tasks 6-11.
- Produces: `npm run lint` (from `web/`) that FAILS on (a) `ui` importing `data`, (b) `learning` importing a sibling context, and passes on the legal code.

- [ ] **Step 1: Add ESLint + boundaries plugin**

Run from `web/`:

```bash
npx ng add @angular-eslint/schematics --skip-confirmation
npm install -D eslint-plugin-boundaries
```

Confirm `web/package.json` has a `"lint": "ng lint"` script (the schematic adds it). If not, add `"lint": "ng lint"`.

- [ ] **Step 2: Configure element types + boundary rules**

Edit `web/eslint.config.js` — add the `boundaries` plugin, tag element types by path, and add rules. Merge this into the config the schematic generated (keep its Angular/TS blocks; add the block below to the array):

```javascript
const boundaries = require('eslint-plugin-boundaries');

module.exports = [
  // ...existing @angular-eslint / typescript-eslint config blocks stay above...
  {
    files: ['projects/**/*.ts'],
    plugins: { boundaries },
    settings: {
      'boundaries/elements': [
        { type: 'contracts', pattern: 'projects/contracts/**' },
        { type: 'shell', pattern: 'projects/shell/**' },
        { type: 'learning-domain', pattern: 'projects/learning/src/lib/domain/**' },
        { type: 'learning-data', pattern: 'projects/learning/src/lib/data/**' },
        { type: 'learning-application', pattern: 'projects/learning/src/lib/application/**' },
        { type: 'learning-ui', pattern: 'projects/learning/src/lib/ui/**' },
      ],
    },
    rules: {
      'boundaries/element-types': ['error', {
        default: 'disallow',
        rules: [
          // intra-learning layering: ui -> application -> data -> domain
          { from: 'learning-ui', allow: ['learning-application', 'learning-domain'] },
          { from: 'learning-application', allow: ['learning-data', 'learning-domain'] },
          { from: 'learning-data', allow: ['learning-domain', 'contracts'] },
          { from: 'learning-domain', allow: [] },
          // shell may reach libraries (via their public-api); libraries may not reach shell
          { from: 'shell', allow: ['learning-ui', 'learning-application', 'learning-data', 'learning-domain', 'contracts'] },
          { from: 'contracts', allow: [] },
        ],
      }],
    },
  },
];
```

> Note: cross-library public-api enforcement is already structural (path aliases). These `element-types` rules add the intra-layer enforcement that has no structural equivalent, plus the `shell`/`contracts` direction rules.

- [ ] **Step 3: Verify the legal code lints clean**

Run from `web/`: `npm run lint`
Expected: PASS (no boundary violations in the code written so far).

- [ ] **Step 4: Prove the boundary fails on an illegal import (ui → data)**

Temporarily add to the TOP of `web/projects/learning/src/lib/ui/catalog-page/catalog-page.component.ts`:

```typescript
import { CatalogRepository } from '../../data/catalog.repository'; // ILLEGAL: ui must not import data
```

Run from `web/`: `npm run lint`
Expected: FAIL with a `boundaries/element-types` error for `learning-ui` → `learning-data`.

- [ ] **Step 5: Remove the illegal import**

Delete the line added in Step 4. Run from `web/`: `npm run lint`
Expected: PASS again.

- [ ] **Step 6: Commit**

```bash
cd ..
git add web/package.json web/package-lock.json web/eslint.config.js web/angular.json
git commit -m "feat(web): enforce module boundaries with eslint-plugin-boundaries"
```

---

## Task 13: End-to-end smoke — run the app against the Host

**Files:**
- Create: `web/README.md` (run instructions) — or update the one `ng new` created.

**Interfaces:**
- Consumes: everything above.
- Produces: documented, verified manual run: Host on `:5225` + `ng serve` on `:4200` renders the real seeded catalog.

> Verification/documentation task — no unit test; this is the slice's success-criteria check.

- [ ] **Step 1: Run the backend**

Run: `dotnet run --project src/Host`
Expected: listening on `http://localhost:5225`; `GET http://localhost:5225/courses` returns the seeded catalog JSON.

- [ ] **Step 2: Run the frontend**

Run from `web/`: `npx ng serve shell`
Open `http://localhost:4200`.
Expected: the page renders Course → Unit → Lesson from the real API; the unpublished seed lesson shows the `locked` style; briefly shows "Loading catalog…" first.

- [ ] **Step 3: Verify the error state**

Stop the Host (`Ctrl+C`). Reload `http://localhost:4200`.
Expected: "Unable to load the catalog." with a working **Retry** button (start the Host again, click Retry → catalog appears).

- [ ] **Step 4: Run the full frontend gate**

Run from `web/`:

```bash
npx ng test contracts --watch=false && npx ng test learning --watch=false && npx ng test shell --watch=false && npm run lint && npx ng build shell
```

Expected: all tests pass, lint passes, build succeeds.

- [ ] **Step 5: Document how to run**

Write `web/README.md`:

```markdown
# Duolingo Web (Angular)

Frontend for the Duolingo DDD/Clean-Architecture learning project. Multi-project
workspace: `shell` (app) + `contracts` + `learning` (libraries).

## Run

1. Backend: from the repo root, `dotnet run --project src/Host` (listens on http://localhost:5225).
2. Frontend: from `web/`, `npm install` then `npx ng serve shell` (http://localhost:4200).

## Regenerate API contract types

After a backend contract change: run the Host, then
`curl -s http://localhost:5225/openapi/v1.json -o openapi.json` and
`npm run generate:contracts`. Commit the updated `openapi.json` and generated types.

## Quality gate

`npx ng test contracts && npx ng test learning && npx ng test shell && npm run lint && npx ng build shell`
```

- [ ] **Step 6: Commit**

```bash
cd ..
git add web/README.md
git commit -m "docs(web): run instructions and quality gate for slice 1"
```

---

## Self-Review

**Spec coverage:**
- Multi-project workspace (`shell`+`contracts`+`learning`) → Tasks 4, 5. ✅
- Library-per-bounded-context + `public-api.ts` structural boundary → Tasks 5, 6, 10 (learning exports routes only), 11 (token export). ✅
- Internal layering `ui→application→data→domain` → Tasks 7-10. ✅
- Anti-corruption mapper; DTOs never leak past `data` → Task 7. ✅
- Hybrid contracts: OpenAPI on Host + generate DTO types only + hand-written repo/mapping → Tasks 1, 3, 6, 7, 8. ✅
- Backend prereqs: OpenAPI doc + config-driven CORS → Tasks 1, 2. ✅
- Signals store (native, no NgRx) with computed → Task 9. ✅
- Tri-state error handling; no interceptor/auth in slice 1 → Tasks 8 (error translation), 10 (tri-state UI). ✅
- Boundary enforcement (ESLint) as NetArchTest analogue → Task 12. ✅
- Tests: mapper unit, store/component, boundary/lint → Tasks 7, 9, 10, 12. ✅
- Success criteria (renders real catalog; illegal import fails lint; generated types; backend green) → Tasks 12, 13. ✅

**Deferred (correctly out of scope):** writes/`POST /attempts`, `X-Learner-Id`/interceptor, `engagement`/`identity` libraries, NgRx SignalStore, Playwright e2e, overworld map. Not in any task — matches spec.

**Placeholder scan:** no TBD/TODO/"add error handling"; every code step shows complete code. ✅

**Type consistency:** `mapCatalog(CatalogDto): Course[]`, `CatalogRepository.getCatalog(): Observable<Course[]>`, `CatalogStore` signals (`courses/loading/error/lessonCount`) and `load()`, `LEARNING_ROUTES`, `API_BASE_URL` — names/signatures match across Tasks 6-12. `Lesson.isLocked = !isPublished` used consistently in mapper (Task 7), tests (Tasks 7, 10), and template (Task 10). ✅

**Known execution-time caveats to watch (not blockers):**
- Exact Angular CLI scaffolding (file names `app.component.ts` vs `app.ts`, zone vs zoneless, whether `environments/` is generated) varies by CLI version — Tasks 11 steps say "match what `ng new` scaffolded." Adjust file names to the actual scaffold.
- `Microsoft.AspNetCore.OpenApi` version: use `dotnet add package` if the pinned `10.0.0` isn't the exact net10 match.
