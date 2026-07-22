# Learning Slice 3a — Progress & Unlocking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose a per-learner course map (`GET /me/courses/{courseId}/map`) that classifies every published lesson node as `Completed | Unlocked | Locked`, derived read-only from existing `Attempt` history via a pure domain policy.

**Architecture:** No new aggregate, no write path, no migration. A pure, framework-free `LessonProgression` domain policy encodes the unlock rule (Rule A: linear-within-unit, sequential units). `Learning.Infrastructure` fetches the course structure + the learner's passed `LessonId`s and feeds the policy; `Learning.Application` shapes the DTO; the Host adds one read endpoint. Unknown course → 404 via the established nullable-DTO read convention (mirrors `GetLesson`).

**Tech Stack:** C# / .NET 10, ASP.NET Core Minimal APIs, EF Core 10 (SQL Server LocalDB), hand-rolled mediator, xUnit, NetArchTest.

## Global Constraints

- **Target framework:** `net10.0` (pinned per project).
- **Dependency Rule:** `Learning.Domain` references nothing infrastructural (no EF Core, no ASP.NET). `Learning.Application` must not reference EF Core. No `Learning → Engagement` type references. (Enforced by existing NetArchTest in `tests/Learning.Integration.Tests/Architecture/ArchitectureTests.cs`, which scans whole assemblies — the new policy/read service are auto-covered; no new architecture test needed.)
- **Value-object querying:** compare/order by the *whole* value object (`c.Id == cid`, `a.LearnerId == learner`); never reach into a converted member in a query. Prefer materialize-then-assemble-in-memory (as `CatalogReadService` does) to sidestep converter translation limits.
- **"Now" from `TimeProvider`** in handlers — not relevant here (no handler stamps time; test setup may use a literal `DateTimeOffset`).
- **Wire contract:** `LessonNodeDto.Status` is a **string** (`"Completed"|"Unlocked"|"Locked"`) via `NodeStatus.ToString()` — no dependence on enum ordering or a global `JsonStringEnumConverter`.
- **Read convention:** reads return a nullable DTO; the endpoint maps `null → 404` (mirror `GetLesson`, do **not** throw `KeyNotFoundException`).
- **Git:** implement on branch `feat/learning-progress-unlocking`; Conventional Commits; commit message trailer `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **Seed facts used by tests** (from `LearningSeedIds` / seed config): Course *Spanish* (`SpanishCourse`) → Unit *Basics* (pos 1) with *Greetings* (pos 1, published) + *The verb ser* (pos 2, published); Unit *Food* (pos 2) with *At the cafe* (pos 1, published) + *Ordering dessert* (pos 2, **unpublished draft**). Greetings correct answers: `GreetingsEx1Correct`, `GreetingsEx2Correct`.

---

## File Structure

- **Create** `src/Modules/Learning/Learning.Domain/NodeStatus.cs` — the `Locked/Unlocked/Completed` enum.
- **Create** `src/Modules/Learning/Learning.Domain/LessonProgression.cs` — the pure unlock policy (Rule A).
- **Create** `src/Modules/Learning/Learning.Application/GetCourseMap.cs` — query + DTOs + read port + handler.
- **Create** `src/Modules/Learning/Learning.Infrastructure/CourseMapReadService.cs` — fetch + classify + assemble.
- **Modify** `src/Modules/Learning/Learning.Infrastructure/LearningInfrastructureExtensions.cs` — register the read service.
- **Modify** `src/Host/Program.cs` — add `GET /me/courses/{courseId:guid}/map`.
- **Create** `tests/Learning.Domain.Tests/LessonProgressionTests.cs` — pure policy tests.
- **Create** `tests/Learning.Integration.Tests/Infrastructure/CourseMapReadServiceTests.cs` — read-service tests over a seeded DB.
- **Modify** `tests/Learning.Integration.Tests/EndToEnd/LearningApiTests.cs` — e2e map tests.

---

## Task 1: `NodeStatus` enum + `LessonProgression` domain policy

The center of gravity: the unlock rule as a pure function, fully unit-tested.

**Files:**
- Create: `src/Modules/Learning/Learning.Domain/NodeStatus.cs`
- Create: `src/Modules/Learning/Learning.Domain/LessonProgression.cs`
- Test: `tests/Learning.Domain.Tests/LessonProgressionTests.cs`

**Interfaces:**
- Consumes: `Unit`, `Lesson`, `UnitId`, `LessonId`, `Title`, `CourseId` (existing `Learning.Domain` types).
- Produces:
  - `enum NodeStatus { Locked, Unlocked, Completed }`
  - `static IReadOnlyDictionary<LessonId, NodeStatus> LessonProgression.Classify(IReadOnlyList<Unit> unitsInOrder, IReadOnlyList<Lesson> publishedLessons, IReadOnlySet<LessonId> passedLessonIds)`

- [ ] **Step 1: Create the `NodeStatus` enum**

Create `src/Modules/Learning/Learning.Domain/NodeStatus.cs`:

```csharp
namespace Learning.Domain;

// A lesson node's progression state on the learner's course map.
// Serialized to the wire as its string name (order here is irrelevant to the contract).
public enum NodeStatus
{
    Locked,
    Unlocked,
    Completed
}
```

- [ ] **Step 2: Write the failing policy tests**

Create `tests/Learning.Domain.Tests/LessonProgressionTests.cs`:

```csharp
using Learning.Domain;
using Xunit;

namespace Learning.Domain.Tests;

public class LessonProgressionTests
{
    private static readonly CourseId Course = new(Guid.NewGuid());
    private static readonly UnitId Unit1 = new(Guid.NewGuid());
    private static readonly UnitId Unit2 = new(Guid.NewGuid());

    private static Unit U(UnitId id, int pos) => Unit.Create(id, Course, new Title($"U{pos}"), pos);
    private static Lesson L(LessonId id, UnitId unit, int pos) =>
        Lesson.Create(id, unit, new Title($"L{pos}"), pos, isPublished: true);

    [Fact]
    public void Empty_course_yields_no_nodes()
    {
        var status = LessonProgression.Classify(new List<Unit>(), new List<Lesson>(), new HashSet<LessonId>());
        Assert.Empty(status);
    }

    [Fact]
    public void Nothing_passed_first_lesson_unlocked_rest_locked()
    {
        var l1 = new LessonId(Guid.NewGuid());
        var l2 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1) };
        var lessons = new[] { L(l1, Unit1, 1), L(l2, Unit1, 2) };

        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId>());

        Assert.Equal(NodeStatus.Unlocked, status[l1]);
        Assert.Equal(NodeStatus.Locked, status[l2]);
    }

    [Fact]
    public void Passing_the_first_lesson_completes_it_and_unlocks_the_second()
    {
        var l1 = new LessonId(Guid.NewGuid());
        var l2 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1) };
        var lessons = new[] { L(l1, Unit1, 1), L(l2, Unit1, 2) };

        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId> { l1 });

        Assert.Equal(NodeStatus.Completed, status[l1]);
        Assert.Equal(NodeStatus.Unlocked, status[l2]);
    }

    [Fact]
    public void Completing_a_unit_unlocks_the_next_units_first_lesson()
    {
        var a1 = new LessonId(Guid.NewGuid());
        var b1 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1), U(Unit2, 2) };
        var lessons = new[] { L(a1, Unit1, 1), L(b1, Unit2, 1) };

        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId> { a1 });

        Assert.Equal(NodeStatus.Completed, status[a1]);
        Assert.Equal(NodeStatus.Unlocked, status[b1]);
    }

    [Fact]
    public void Partial_unit_keeps_the_next_unit_locked()
    {
        var a1 = new LessonId(Guid.NewGuid());
        var a2 = new LessonId(Guid.NewGuid());
        var b1 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1), U(Unit2, 2) };
        var lessons = new[] { L(a1, Unit1, 1), L(a2, Unit1, 2), L(b1, Unit2, 1) };

        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId> { a1 });

        Assert.Equal(NodeStatus.Completed, status[a1]);
        Assert.Equal(NodeStatus.Unlocked, status[a2]);
        Assert.Equal(NodeStatus.Locked, status[b1]);
    }

    [Fact]
    public void Out_of_order_pass_is_completed_without_leaking_unlocks()
    {
        var a1 = new LessonId(Guid.NewGuid());
        var a2 = new LessonId(Guid.NewGuid());
        var a3 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1) };
        var lessons = new[] { L(a1, Unit1, 1), L(a2, Unit1, 2), L(a3, Unit1, 3) };

        // a2 passed while a1 is not: a2 Completed, a1 the only Unlocked (frontier), a3 Locked.
        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId> { a2 });

        Assert.Equal(NodeStatus.Unlocked, status[a1]);
        Assert.Equal(NodeStatus.Completed, status[a2]);
        Assert.Equal(NodeStatus.Locked, status[a3]);
    }

    [Fact]
    public void A_unit_with_no_published_lessons_opens_the_next_unit()
    {
        var b1 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1), U(Unit2, 2) }; // Unit1 contributes no published lessons
        var lessons = new[] { L(b1, Unit2, 1) };

        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId>());

        Assert.Equal(NodeStatus.Unlocked, status[b1]);
        Assert.Single(status);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~LessonProgressionTests"`
Expected: FAIL — compile error, `LessonProgression` does not exist.

- [ ] **Step 4: Implement the policy**

Create `src/Modules/Learning/Learning.Domain/LessonProgression.cs`:

```csharp
namespace Learning.Domain;

// Rule A — linear within a unit, sequential units. A pure function of (ordered structure, passed set).
// The caller passes PUBLISHED lessons only, so drafts are neither nodes nor gates.
public static class LessonProgression
{
    public static IReadOnlyDictionary<LessonId, NodeStatus> Classify(
        IReadOnlyList<Unit> unitsInOrder,
        IReadOnlyList<Lesson> publishedLessons,
        IReadOnlySet<LessonId> passedLessonIds)
    {
        var lessonsByUnit = publishedLessons
            .GroupBy(l => l.UnitId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Position).ToList());

        var status = new Dictionary<LessonId, NodeStatus>();
        var allPriorUnitsComplete = true; // nothing precedes the first unit

        foreach (var unit in unitsInOrder.OrderBy(u => u.Position))
        {
            var unitOpen = allPriorUnitsComplete;
            var priorLessonsComplete = true; // within this unit
            var thisUnitComplete = true;

            var lessons = lessonsByUnit.TryGetValue(unit.Id, out var found)
                ? found
                : new List<Lesson>();

            foreach (var lesson in lessons)
            {
                if (passedLessonIds.Contains(lesson.Id))
                {
                    status[lesson.Id] = NodeStatus.Completed;
                    continue; // a passed lesson never leaks an unlock and never blocks; it is simply done
                }

                thisUnitComplete = false;
                status[lesson.Id] = unitOpen && priorLessonsComplete
                    ? NodeStatus.Unlocked
                    : NodeStatus.Locked;
                priorLessonsComplete = false; // the frontier is the first unpassed lesson only
            }

            // A unit with no published lessons is vacuously complete, so the next unit still opens.
            allPriorUnitsComplete = allPriorUnitsComplete && thisUnitComplete;
        }

        return status;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~LessonProgressionTests"`
Expected: PASS — 7 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Learning/Learning.Domain/NodeStatus.cs \
        src/Modules/Learning/Learning.Domain/LessonProgression.cs \
        tests/Learning.Domain.Tests/LessonProgressionTests.cs
git commit -m "feat(learning): add LessonProgression unlock policy (Completed/Unlocked/Locked)"
```

---

## Task 2: Application contract + Infrastructure read service + DI

Wire the policy into a read path: query + DTOs + port (Application), fetch/classify/assemble (Infrastructure), register (DI). Verified by integration tests over the seeded DB.

**Files:**
- Create: `src/Modules/Learning/Learning.Application/GetCourseMap.cs`
- Create: `src/Modules/Learning/Learning.Infrastructure/CourseMapReadService.cs`
- Modify: `src/Modules/Learning/Learning.Infrastructure/LearningInfrastructureExtensions.cs`
- Test: `tests/Learning.Integration.Tests/Infrastructure/CourseMapReadServiceTests.cs`

**Interfaces:**
- Consumes: `LessonProgression.Classify(...)`, `NodeStatus` (Task 1); `LearningDbContext`, `LearningSeedIds` (Infrastructure); `CourseId`, `UnitId`, `LessonId`, `LearnerId`, `Attempt`, `AttemptId`, `GradingResult`, `Score`, `GradedAnswer`, `Outcome` (Domain); `IRequest`/`IRequestHandler` (`BuildingBlocks.Mediator`).
- Produces:
  - `record GetCourseMap(Guid CourseId, Guid LearnerId) : IRequest<CourseMapDto?>`
  - `record CourseMapDto(Guid CourseId, string Title, IReadOnlyList<UnitMapDto> Units)`
  - `record UnitMapDto(Guid Id, string Title, int Position, IReadOnlyList<LessonNodeDto> Lessons)`
  - `record LessonNodeDto(Guid Id, string Title, int Position, string Status)`
  - `interface ICourseMapReadService { Task<CourseMapDto?> GetCourseMapAsync(Guid courseId, Guid learnerId, CancellationToken ct); }`
  - `class CourseMapReadService : ICourseMapReadService`

- [ ] **Step 1: Create the Application contract + handler**

Create `src/Modules/Learning/Learning.Application/GetCourseMap.cs`:

```csharp
using BuildingBlocks.Mediator;

namespace Learning.Application;

public sealed record GetCourseMap(Guid CourseId, Guid LearnerId) : IRequest<CourseMapDto?>;

public sealed record CourseMapDto(Guid CourseId, string Title, IReadOnlyList<UnitMapDto> Units);
public sealed record UnitMapDto(Guid Id, string Title, int Position, IReadOnlyList<LessonNodeDto> Lessons);
public sealed record LessonNodeDto(Guid Id, string Title, int Position, string Status);

// Read-model port (returns a DTO) — distinct from the aggregate repositories. Implemented in Infrastructure.
public interface ICourseMapReadService
{
    Task<CourseMapDto?> GetCourseMapAsync(Guid courseId, Guid learnerId, CancellationToken ct);
}

public sealed class GetCourseMapHandler(ICourseMapReadService read)
    : IRequestHandler<GetCourseMap, CourseMapDto?>
{
    public Task<CourseMapDto?> HandleAsync(GetCourseMap request, CancellationToken ct) =>
        read.GetCourseMapAsync(request.CourseId, request.LearnerId, ct);
}
```

- [ ] **Step 2: Write the failing integration tests**

Create `tests/Learning.Integration.Tests/Infrastructure/CourseMapReadServiceTests.cs`:

```csharp
using Learning.Application;
using Learning.Domain;
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class CourseMapReadServiceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_CourseMap_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public CourseMapReadServiceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static Attempt SeededAttempt(Guid learner, Guid lessonId, Score score, Outcome outcome) =>
        Attempt.Create(
            new AttemptId(Guid.NewGuid()),
            new LearnerId(learner),
            new LessonId(lessonId),
            new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero),
            new GradingResult(score, outcome, new List<GradedAnswer>()));

    [Fact]
    public async Task No_attempts_first_lesson_unlocked_rest_locked_and_draft_absent()
    {
        await using var ctx = NewContext();

        var map = await new CourseMapReadService(ctx)
            .GetCourseMapAsync(LearningSeedIds.SpanishCourse, Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(map);
        Assert.Equal(2, map!.Units.Count);                 // Basics + Food (both have published lessons)
        var basics = map.Units[0];
        Assert.Equal("Basics", basics.Title);
        Assert.Equal("Unlocked", basics.Lessons[0].Status); // Greetings
        Assert.Equal("Locked", basics.Lessons[1].Status);   // The verb ser
        var food = map.Units[1];
        Assert.Single(food.Lessons);                         // Dessert draft absent
        Assert.Equal("Locked", food.Lessons[0].Status);      // At the cafe (Unit 2 not open)
    }

    [Fact]
    public async Task A_passing_attempt_marks_the_lesson_completed_and_unlocks_the_next()
    {
        var learner = Guid.NewGuid();
        await using (var seed = NewContext())
        {
            seed.Attempts.Add(SeededAttempt(learner, LearningSeedIds.GreetingsLesson, new Score(2, 2), Outcome.Passed));
            await seed.SaveChangesAsync();
        }

        await using var ctx = NewContext();
        var map = await new CourseMapReadService(ctx)
            .GetCourseMapAsync(LearningSeedIds.SpanishCourse, learner, CancellationToken.None);

        var basics = map!.Units[0];
        Assert.Equal("Completed", basics.Lessons[0].Status); // Greetings passed
        Assert.Equal("Unlocked", basics.Lessons[1].Status);  // ser is now the frontier
    }

    [Fact]
    public async Task A_failing_attempt_does_not_complete_the_lesson()
    {
        var learner = Guid.NewGuid();
        await using (var seed = NewContext())
        {
            seed.Attempts.Add(SeededAttempt(learner, LearningSeedIds.GreetingsLesson, new Score(0, 2), Outcome.Failed));
            await seed.SaveChangesAsync();
        }

        await using var ctx = NewContext();
        var map = await new CourseMapReadService(ctx)
            .GetCourseMapAsync(LearningSeedIds.SpanishCourse, learner, CancellationToken.None);

        Assert.Equal("Unlocked", map!.Units[0].Lessons[0].Status); // still the frontier, not Completed
    }

    [Fact]
    public async Task Unknown_course_returns_null()
    {
        await using var ctx = NewContext();

        var map = await new CourseMapReadService(ctx)
            .GetCourseMapAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(map);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~CourseMapReadServiceTests"`
Expected: FAIL — compile error, `CourseMapReadService` does not exist.

- [ ] **Step 4: Implement the read service**

Create `src/Modules/Learning/Learning.Infrastructure/CourseMapReadService.cs`:

```csharp
using Learning.Application;
using Learning.Domain;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure;

public sealed class CourseMapReadService(LearningDbContext context) : ICourseMapReadService
{
    public async Task<CourseMapDto?> GetCourseMapAsync(Guid courseId, Guid learnerId, CancellationToken ct)
    {
        var courseKey = new CourseId(courseId);

        // Small seeded catalog: materialize and assemble in memory (mirrors CatalogReadService),
        // which also sidesteps value-converter translation limits.
        var courses = await context.Courses.ToListAsync(ct);
        var course = courses.FirstOrDefault(c => c.Id == courseKey);
        if (course is null)
            return null;

        var units = await context.Units.ToListAsync(ct);
        var lessons = await context.Lessons.ToListAsync(ct);

        var learnerKey = new LearnerId(learnerId);
        var passedLessonIds = (await context.Attempts
                .Where(a => a.LearnerId == learnerKey) // whole-VO comparison — EF-translatable
                .ToListAsync(ct))
            .Where(a => a.Passed)
            .Select(a => a.LessonId)
            .ToHashSet();

        var courseUnits = units.Where(u => u.CourseId == courseKey).OrderBy(u => u.Position).ToList();
        var courseUnitIds = courseUnits.Select(u => u.Id).ToHashSet();
        var publishedLessons = lessons
            .Where(l => l.IsPublished && courseUnitIds.Contains(l.UnitId))
            .ToList();

        var status = LessonProgression.Classify(courseUnits, publishedLessons, passedLessonIds);

        var unitDtos = courseUnits
            .Select(u => new UnitMapDto(
                u.Id.Value,
                u.Title.Value,
                u.Position,
                publishedLessons
                    .Where(l => l.UnitId == u.Id)
                    .OrderBy(l => l.Position)
                    .Select(l => new LessonNodeDto(l.Id.Value, l.Title.Value, l.Position, status[l.Id].ToString()))
                    .ToList()))
            .Where(u => u.Lessons.Count > 0) // omit units with no published lessons
            .ToList();

        return new CourseMapDto(course.Id.Value, course.Title.Value, unitDtos);
    }
}
```

- [ ] **Step 5: Register the read service in DI**

Modify `src/Modules/Learning/Learning.Infrastructure/LearningInfrastructureExtensions.cs` — add the registration alongside the other Learning read services (after the `ILessonPresentationRead` line):

```csharp
        services.AddScoped<ILessonPresentationRead, LessonPresentationRead>();
        services.AddScoped<ICourseMapReadService, CourseMapReadService>();
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~CourseMapReadServiceTests"`
Expected: PASS — 4 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Learning/Learning.Application/GetCourseMap.cs \
        src/Modules/Learning/Learning.Infrastructure/CourseMapReadService.cs \
        src/Modules/Learning/Learning.Infrastructure/LearningInfrastructureExtensions.cs \
        tests/Learning.Integration.Tests/Infrastructure/CourseMapReadServiceTests.cs
git commit -m "feat(learning): add course-map read service (GetCourseMap) over attempt history"
```

---

## Task 3: Host endpoint + end-to-end tests

Expose `GET /me/courses/{courseId}/map` and verify the full stack, including the Learning→Engagement seam is untouched.

**Files:**
- Modify: `src/Host/Program.cs`
- Test: `tests/Learning.Integration.Tests/EndToEnd/LearningApiTests.cs`

**Interfaces:**
- Consumes: `GetCourseMap` + `CourseMapDto` (Task 2), `ICurrentUser`, `IMediator`, `LearningSeedIds`.
- Produces: HTTP route `GET /me/courses/{courseId:guid}/map` → `200` `CourseMapDto` / `404`.

- [ ] **Step 1: Write the failing e2e tests**

Modify `tests/Learning.Integration.Tests/EndToEnd/LearningApiTests.cs`.

First add these deserialization records to the class (next to the existing `private sealed record` declarations near the top):

```csharp
    private sealed record CourseMapView(Guid CourseId, string Title, List<UnitMapView> Units);
    private sealed record UnitMapView(Guid Id, string Title, int Position, List<LessonNodeView> Lessons);
    private sealed record LessonNodeView(Guid Id, string Title, int Position, string Status);
```

Then add these three facts to the class (e.g. after `Passing_the_same_lesson_twice_awards_xp_twice`):

```csharp
    [Fact]
    public async Task Course_map_for_a_new_learner_unlocks_only_the_first_lesson()
    {
        var client = ClientForLearner(Guid.NewGuid());

        var map = await client.GetFromJsonAsync<CourseMapView>($"/me/courses/{LearningSeedIds.SpanishCourse}/map");

        Assert.NotNull(map);
        Assert.Equal("Unlocked", map!.Units[0].Lessons[0].Status); // Greetings
        Assert.Equal("Locked", map.Units[0].Lessons[1].Status);    // The verb ser
        Assert.DoesNotContain(
            map.Units.SelectMany(u => u.Lessons),
            l => l.Id == LearningSeedIds.DessertLessonDraft);       // draft absent
    }

    [Fact]
    public async Task Passing_a_lesson_advances_the_map()
    {
        var client = ClientForLearner(Guid.NewGuid());
        var lesson = await GetGreetings(client);
        await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts",
            GreetingsAnswers(LearningSeedIds.GreetingsEx1Correct, LearningSeedIds.GreetingsEx2Correct, lesson));

        var map = await client.GetFromJsonAsync<CourseMapView>($"/me/courses/{LearningSeedIds.SpanishCourse}/map");

        var basics = map!.Units[0];
        Assert.Equal("Completed", basics.Lessons[0].Status);
        Assert.Equal("Unlocked", basics.Lessons[1].Status);
    }

    [Fact]
    public async Task Unknown_course_map_returns_404()
    {
        var resp = await ClientForLearner(Guid.NewGuid())
            .GetAsync($"/me/courses/{Guid.NewGuid()}/map");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LearningApiTests"`
Expected: FAIL — `Course_map_for_a_new_learner...` and `Passing_a_lesson_advances_the_map` fail (no route → `GetFromJsonAsync` throws / null), `Unknown_course_map_returns_404` fails (route missing returns 404 by luck may pass — the two positive tests must fail). The suite is red.

- [ ] **Step 3: Add the endpoint**

Modify `src/Host/Program.cs` — add the route in the endpoints section (immediately after the `POST /lessons/{lessonId:guid}/attempts` block):

```csharp
app.MapGet("/me/courses/{courseId:guid}/map",
    async (Guid courseId, ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        var dto = await mediator.SendAsync(new GetCourseMap(courseId, user.LearnerId), ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    });
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LearningApiTests"`
Expected: PASS — all `LearningApiTests` (existing + 3 new) pass.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: PASS — entire solution green (Engagement + Learning, unit + integration + architecture).

- [ ] **Step 6: Commit**

```bash
git add src/Host/Program.cs tests/Learning.Integration.Tests/EndToEnd/LearningApiTests.cs
git commit -m "feat(learning): expose GET /me/courses/{id}/map (progress & unlocking)"
```

---

## Task 4: Docs & status (post-merge, to `main`)

Per the repo's docs-hygiene convention, status/doc updates go **straight to `main`** (no PR) after the feature branch is merged. Do this once Tasks 1–3 are merged.

**Files:**
- Modify: `CLAUDE.md` (the duolingo project one) — Status section.

- [ ] **Step 1: Mark the slice complete**

In `duolingo/CLAUDE.md`, replace the `⏭️ Next` bullet's Slice-3 framing with a `✅` entry recording Slice 3a and restate the remaining Next work. Suggested entry:

```markdown
- ✅ **Sub-project 5 — Learning, Slice 3a (progress & unlocking)** (PR #9): a per-learner **course map**
  (`GET /me/courses/{id}/map`) classifying every published lesson node **Completed / Unlocked / Locked**,
  derived **read-only** from `Attempt` history — no new aggregate, no write path, no migration. The unlock
  rule (**Rule A** — linear-within-unit, sequential units, unit-gated) lives in `Learning.Domain` as the pure
  `LessonProgression` policy; drafts never appear as nodes or gate progression; only *passing* attempts
  complete a lesson. Deferred: `LearnerProgress` aggregate + **mastery** and the **completion economy**
  (once-per-lesson credit, reduced XP on repeat, dedup) → **Slice 4**; unlock *enforcement* on the attempt
  endpoint.
```

- [ ] **Step 2: Commit to main**

```bash
git commit -am "docs(learning): mark Slice 3a (progress & unlocking) complete"
```

---

## Self-Review

**Spec coverage** (against `docs/superpowers/specs/2026-07-21-learning-slice3-progress-unlocking-design.md`):
- `NodeStatus` enum → Task 1 Step 1. ✓
- `LessonProgression` pure policy (Rule A: frontier, unit gating, drafts-absent, out-of-order, vacuous-empty-unit) → Task 1 (all 7 edge cases as tests). ✓
- `GetCourseMap` query + handler (null → read convention) → Task 2 Step 1. ✓
- DTOs `CourseMapDto/UnitMapDto/LessonNodeDto` with string `Status` → Task 2 Step 1. ✓
- `ICourseMapReadService` + `CourseMapReadService` (fetch structure + distinct passed LessonIds, classify, assemble, omit empty units, null on unknown course) → Task 2 Steps 1/4. ✓
- DI registration → Task 2 Step 5. ✓
- Host `GET /me/courses/{courseId:guid}/map` (404 on null) → Task 3 Step 3. ✓
- Testing: domain (Task 1), integration incl. draft-absent + failing-attempt-not-completed + unknown-course-null (Task 2), e2e incl. new-learner map + advance-after-pass + 404 (Task 3), architecture auto-covered (Global Constraints note). ✓
- No migration / no aggregate / no contract or Engagement change → nothing in the plan adds them; full-suite run (Task 3 Step 5) guards the Engagement seam. ✓

**Placeholder scan:** No `TBD`/`TODO`/"handle edge cases"/"similar to Task N" — every step has literal code or an exact command. ✓

**Type consistency:** `Classify(IReadOnlyList<Unit>, IReadOnlyList<Lesson>, IReadOnlySet<LessonId>) → IReadOnlyDictionary<LessonId, NodeStatus>` used identically in Task 1 (def), Task 2 (call). `GetCourseMapAsync(Guid, Guid, CancellationToken)` and `CourseMapDto?` identical across Application port, Infrastructure impl, and Host call. `NodeStatus` values `{Locked, Unlocked, Completed}` and their `.ToString()` names match the string assertions in every test. `LearningSeedIds` members (`SpanishCourse`, `GreetingsLesson`, `DessertLessonDraft`, `GreetingsEx1Correct`, `GreetingsEx2Correct`) all exist. `Attempt.Create` / `GradingResult` / `Score` / `GradedAnswer` / `Outcome` signatures match the seeded-attempt helper. ✓
