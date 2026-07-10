# Learning Slice 1 — Catalog + Real Completion — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `Learning.Stub` with a real `Learning` module (Domain · Application · Infrastructure) whose `LessonCompleted` is published from a validated, seeded content catalog (Course → Unit → Lesson).

**Architecture:** A new modular-monolith module mirroring Engagement's Clean-Architecture layout. Three aggregate roots referenced by id; the `CompleteLesson` handler loads a `Lesson`, enforces `EnsureCompletable()`, and publishes the unchanged `LessonCompleted` integration event. Learning owns a separate `DuolingoLearning` database + `learning` schema; the catalog is seeded via EF `HasData` and read-only at runtime.

**Tech Stack:** C# / .NET 10, ASP.NET Core Minimal APIs, EF Core 10 (SQL Server LocalDB), hand-rolled mediator, xUnit + NetArchTest.

## Global Constraints

Every task's requirements implicitly include this section.

- **Target framework:** `net10.0`; `<Nullable>enable</Nullable>`; `<ImplicitUsings>enable</ImplicitUsings>` on every project.
- **Package versions (copy verbatim):** `Microsoft.EntityFrameworkCore.SqlServer` **10.0.8**, `Microsoft.EntityFrameworkCore.Design` **10.0.8** (with `<PrivateAssets>all</PrivateAssets>`), `Microsoft.AspNetCore.Mvc.Testing` **10.0.8**, `Microsoft.Extensions.DependencyInjection` **10.0.8**, `Microsoft.Extensions.TimeProvider.Testing` **10.6.0**, `Microsoft.NET.Test.Sdk` **17.14.1**, `NetArchTest.Rules` **1.3.2**, `xunit` **2.9.3**, `xunit.runner.visualstudio` **3.1.4**, `coverlet.collector` **6.0.4**.
- **Mediator:** hand-rolled `BuildingBlocks.Mediator` only — no MediatR. Commands are `IRequest<Unit>`; queries `IRequest<TDto>`; handlers implement `IRequestHandler<,>.HandleAsync(request, ct)`. Integration events implement `INotification` and are published via `IMediator.PublishAsync`.
- **Dependency Rule:** `Learning.Domain` references only `BuildingBlocks.Domain` (no EF Core, no ASP.NET). `Learning.Application` → Domain + Contracts + Mediator. `Learning.Infrastructure` → Application + Domain + EF. No `Learning.* → Engagement.*` references anywhere in production code.
- **Value objects** inherit `BuildingBlocks.Domain.ValueObject` (implement `GetEqualityComponents`). **Aggregates** inherit `BuildingBlocks.Domain.AggregateRoot` (private parameterless ctor for EF, static `Create` factory, `private set` properties, null-guards via `?? throw new ArgumentNullException`).
- **"Now"** comes from the injected `TimeProvider` in handlers (`clock.GetUtcNow()`), never `DateTimeOffset.UtcNow`.
- **The `LessonCompleted` contract is unchanged and XP-free** (`EventId, LearnerId, LessonId, OccurredOn`). Engagement decides XP; the seam stays untouched.
- **Persistence:** database `DuolingoLearning`; schema `learning` via `HasDefaultSchema`. Test DBs use isolated names — **one unique DB name per persistence/e2e test class** (xUnit runs classes in parallel).
- **Name collision:** `Learning.Domain.Unit` (aggregate) vs `BuildingBlocks.Mediator.Unit` (command return). In any Application file importing both namespaces, add `using Unit = BuildingBlocks.Mediator.Unit;`.
- **Git:** Conventional Commits (`feat:`/`test:`/`docs:`/`chore:`), one commit per task. End every commit message with:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`
- **Branch:** `feat/learning-catalog-completion` (already created; the spec is already committed there).

---

## File Structure

**New — `src/Modules/Learning/Learning.Domain/`** (project references only `BuildingBlocks.Domain`)
- `CourseId.cs`, `UnitId.cs`, `LessonId.cs`, `Title.cs` — value objects
- `Course.cs`, `Unit.cs`, `Lesson.cs` — aggregate roots
- `ILessonRepository.cs` — read port (`GetByIdAsync`)

**New — `src/Modules/Learning/Learning.Application/`** (→ Domain, Contracts, Mediator)
- `CompleteLesson.cs` — command + handler (the migrated, now-real stub)
- `GetCatalog.cs` — query + `CatalogDto`/`CourseDto`/`UnitDto`/`LessonDto` + `ICatalogReadService` port + handler

**New — `src/Modules/Learning/Learning.Infrastructure/`** (→ Application, Domain, EF)
- `LearningDbContext.cs` — `learning` schema, three `DbSet`s
- `CourseConfiguration.cs`, `UnitConfiguration.cs`, `LessonConfiguration.cs` — EF maps + `HasData` seed
- `LearningSeedIds.cs` — fixed seed Guids (shared by configs + tests)
- `LessonRepository.cs`, `CatalogReadService.cs` — port implementations
- `LearningDbContextFactory.cs` — design-time factory (`DuolingoLearning_Design`)
- `LearningInfrastructureExtensions.cs` — `AddLearningInfrastructure(connectionString)`
- `Migrations/` — generated `InitialLearning`

**New — `tests/Learning.Domain.Tests/`** (→ Learning.Domain)
- `ValueObjectsTests.cs`, `AggregatesTests.cs`

**New — `tests/Learning.Integration.Tests/`** (→ Learning.Domain/Application/Infrastructure, Host, Engagement.Infrastructure, Contracts, Mediator)
- `Application/CompleteLessonHandlerTests.cs`
- `Infrastructure/LearningSeedTests.cs`, `Infrastructure/CatalogReadServiceTests.cs`, `Infrastructure/LessonRepositoryTests.cs`
- `EndToEnd/LearningApiFactory.cs`, `EndToEnd/LearningApiTests.cs`
- `Architecture/ArchitectureTests.cs`

**Modified**
- `Duolingo.slnx` — add the five new projects
- `src/Host/Host.csproj` — drop `Learning.Stub`, add `Learning.Application` + `Learning.Infrastructure`
- `src/Host/Program.cs` — register Learning, replace the completion endpoint, add `GET /courses`
- `src/Host/appsettings.json` — add `ConnectionStrings:Learning`
- `tests/Engagement.Integration.Tests/EndToEnd/EngagementApiTests.cs` — remove the stub-coupled test
- **Deleted:** `src/Modules/Learning/Learning.Stub/` and `tests/Engagement.Integration.Tests/Learning/CompleteLessonHandlerTests.cs`

---

### Task 1: Scaffold the Learning module (projects, references, solution)

**Files:**
- Create: `src/Modules/Learning/Learning.Domain/Learning.Domain.csproj`
- Create: `src/Modules/Learning/Learning.Application/Learning.Application.csproj`
- Create: `src/Modules/Learning/Learning.Infrastructure/Learning.Infrastructure.csproj`
- Create: `tests/Learning.Domain.Tests/Learning.Domain.Tests.csproj`
- Create: `tests/Learning.Integration.Tests/Learning.Integration.Tests.csproj`
- Modify: `Duolingo.slnx`

**Interfaces:**
- Consumes: nothing.
- Produces: five buildable projects wired into the solution. Later tasks add `.cs` files into them.

- [ ] **Step 1: Create the three production `.csproj` files**

`src/Modules/Learning/Learning.Domain/Learning.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\..\BuildingBlocks\Domain\BuildingBlocks.Domain.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

`src/Modules/Learning/Learning.Application/Learning.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\Learning.Domain\Learning.Domain.csproj" />
    <ProjectReference Include="..\..\..\BuildingBlocks\Contracts\BuildingBlocks.Contracts.csproj" />
    <ProjectReference Include="..\..\..\BuildingBlocks\Mediator\BuildingBlocks.Mediator.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

`src/Modules/Learning/Learning.Infrastructure/Learning.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\Learning.Application\Learning.Application.csproj" />
    <ProjectReference Include="..\Learning.Domain\Learning.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.8" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

- [ ] **Step 2: Create the two test `.csproj` files**

`tests/Learning.Domain.Tests/Learning.Domain.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Modules\Learning\Learning.Domain\Learning.Domain.csproj" />
  </ItemGroup>

</Project>
```

`tests/Learning.Integration.Tests/Learning.Integration.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.8" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.8" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.8" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.6.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="NetArchTest.Rules" Version="1.3.2" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Host\Host.csproj" />
    <ProjectReference Include="..\..\src\Modules\Learning\Learning.Domain\Learning.Domain.csproj" />
    <ProjectReference Include="..\..\src\Modules\Learning\Learning.Application\Learning.Application.csproj" />
    <ProjectReference Include="..\..\src\Modules\Learning\Learning.Infrastructure\Learning.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\Modules\Engagement\Engagement.Infrastructure\Engagement.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\BuildingBlocks\Contracts\BuildingBlocks.Contracts.csproj" />
    <ProjectReference Include="..\..\src\BuildingBlocks\Mediator\BuildingBlocks.Mediator.csproj" />
  </ItemGroup>

</Project>
```

> The `Engagement.Infrastructure` reference is **test-only** (the e2e factory migrates the Engagement DB to prove the seam). The no-cross-module architecture rule targets the production `Learning.*` assemblies, not this test project.

- [ ] **Step 3: Register all five projects in `Duolingo.slnx`**

Replace the Learning folder block and add a test block. In `Duolingo.slnx`, change:
```xml
  <Folder Name="/src/Modules/Learning/">
    <Project Path="src/Modules/Learning/Learning.Stub/Learning.Stub.csproj" />
  </Folder>
```
to:
```xml
  <Folder Name="/src/Modules/Learning/">
    <Project Path="src/Modules/Learning/Learning.Stub/Learning.Stub.csproj" />
    <Project Path="src/Modules/Learning/Learning.Application/Learning.Application.csproj" />
    <Project Path="src/Modules/Learning/Learning.Domain/Learning.Domain.csproj" />
    <Project Path="src/Modules/Learning/Learning.Infrastructure/Learning.Infrastructure.csproj" />
  </Folder>
```
(Keep `Learning.Stub` for now — it is deleted in Task 8.) And in the `/tests/` folder, add:
```xml
    <Project Path="tests/Learning.Domain.Tests/Learning.Domain.Tests.csproj" />
    <Project Path="tests/Learning.Integration.Tests/Learning.Integration.Tests.csproj" />
```

- [ ] **Step 4: Build to verify the scaffold**

Run: `dotnet build Duolingo.slnx`
Expected: **Build succeeded.** (Empty projects compile; test projects report no tests.)

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Learning/Learning.Domain src/Modules/Learning/Learning.Application src/Modules/Learning/Learning.Infrastructure tests/Learning.Domain.Tests tests/Learning.Integration.Tests Duolingo.slnx
git commit -m "chore(learning): scaffold Learning module projects + solution wiring

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Value objects — `CourseId`, `UnitId`, `LessonId`, `Title`

**Files:**
- Create: `src/Modules/Learning/Learning.Domain/CourseId.cs`, `UnitId.cs`, `LessonId.cs`, `Title.cs`
- Test: `tests/Learning.Domain.Tests/ValueObjectsTests.cs`

**Interfaces:**
- Consumes: `BuildingBlocks.Domain.ValueObject`.
- Produces: `CourseId(Guid)`, `UnitId(Guid)`, `LessonId(Guid)` (each with `.Value`), `Title(string)` (`.Value`, `const int MaxLength = 120`). All reject invalid input with `ArgumentException`; all have value-equality.

- [ ] **Step 1: Write the failing tests**

`tests/Learning.Domain.Tests/ValueObjectsTests.cs`:
```csharp
using Learning.Domain;
using Xunit;

namespace Learning.Domain.Tests;

public class ValueObjectsTests
{
    [Fact]
    public void Typed_ids_reject_empty_guid()
    {
        Assert.Throws<ArgumentException>(() => new CourseId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new UnitId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new LessonId(Guid.Empty));
    }

    [Fact]
    public void Typed_ids_have_value_equality()
    {
        var g = Guid.NewGuid();
        Assert.Equal(new LessonId(g), new LessonId(g));
        Assert.NotEqual(new LessonId(g), new LessonId(Guid.NewGuid()));
    }

    [Fact]
    public void Title_rejects_empty_or_whitespace()
    {
        Assert.Throws<ArgumentException>(() => new Title(""));
        Assert.Throws<ArgumentException>(() => new Title("   "));
    }

    [Fact]
    public void Title_trims_and_bounds_length()
    {
        Assert.Equal("Greetings", new Title("  Greetings  ").Value);
        Assert.Throws<ArgumentException>(() => new Title(new string('x', Title.MaxLength + 1)));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Learning.Domain.Tests`
Expected: FAIL — build error, `CourseId`/`UnitId`/`LessonId`/`Title` do not exist.

- [ ] **Step 3: Implement the three typed ids**

`src/Modules/Learning/Learning.Domain/CourseId.cs`:
```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class CourseId : ValueObject
{
    public Guid Value { get; }

    public CourseId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("CourseId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
```

`src/Modules/Learning/Learning.Domain/UnitId.cs` — identical, substituting `UnitId`:
```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class UnitId : ValueObject
{
    public Guid Value { get; }

    public UnitId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("UnitId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
```

`src/Modules/Learning/Learning.Domain/LessonId.cs` — identical, substituting `LessonId`:
```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class LessonId : ValueObject
{
    public Guid Value { get; }

    public LessonId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("LessonId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
```

- [ ] **Step 4: Implement `Title`**

`src/Modules/Learning/Learning.Domain/Title.cs`:
```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Title : ValueObject
{
    public const int MaxLength = 120;

    public string Value { get; }

    public Title(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Title cannot be empty.", nameof(value));

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Title cannot exceed {MaxLength} characters.", nameof(value));

        Value = trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Learning.Domain.Tests`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Learning/Learning.Domain tests/Learning.Domain.Tests
git commit -m "feat(learning): add Course/Unit/Lesson id and Title value objects

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Aggregates — `Course`, `Unit`, `Lesson` (+ `EnsureCompletable`) and the `ILessonRepository` port

**Files:**
- Create: `src/Modules/Learning/Learning.Domain/Course.cs`, `Unit.cs`, `Lesson.cs`, `ILessonRepository.cs`
- Test: `tests/Learning.Domain.Tests/AggregatesTests.cs`

**Interfaces:**
- Consumes: `AggregateRoot`, `CourseId`, `UnitId`, `LessonId`, `Title` (Task 2).
- Produces:
  - `Course.Create(CourseId, Title, string? language = null)` → `Course { CourseId Id; Title Title; string? Language }`
  - `Unit.Create(UnitId, CourseId, Title, int position)` → `Unit { UnitId Id; CourseId CourseId; Title Title; int Position }`
  - `Lesson.Create(LessonId, UnitId, Title, int position, bool isPublished)` → `Lesson { LessonId Id; UnitId UnitId; Title Title; int Position; bool IsPublished }` + `void EnsureCompletable()` (throws `InvalidOperationException` when not published)
  - `interface ILessonRepository { Task<Lesson?> GetByIdAsync(LessonId id, CancellationToken ct); }`

- [ ] **Step 1: Write the failing tests**

`tests/Learning.Domain.Tests/AggregatesTests.cs`:
```csharp
using Learning.Domain;
using Xunit;

namespace Learning.Domain.Tests;

public class AggregatesTests
{
    [Fact]
    public void Course_create_sets_fields()
    {
        var id = new CourseId(Guid.NewGuid());
        var course = Course.Create(id, new Title("Spanish"), "es");

        Assert.Equal(id, course.Id);
        Assert.Equal("Spanish", course.Title.Value);
        Assert.Equal("es", course.Language);
    }

    [Fact]
    public void Unit_create_sets_fields_including_the_course_id_reference()
    {
        var courseId = new CourseId(Guid.NewGuid());
        var unit = Unit.Create(new UnitId(Guid.NewGuid()), courseId, new Title("Basics"), 1);

        Assert.Equal(courseId, unit.CourseId);
        Assert.Equal(1, unit.Position);
    }

    [Fact]
    public void Lesson_create_sets_fields_including_the_unit_id_reference()
    {
        var unitId = new UnitId(Guid.NewGuid());
        var lesson = Lesson.Create(new LessonId(Guid.NewGuid()), unitId, new Title("Greetings"), 2, isPublished: true);

        Assert.Equal(unitId, lesson.UnitId);
        Assert.Equal(2, lesson.Position);
        Assert.True(lesson.IsPublished);
    }

    [Fact]
    public void EnsureCompletable_passes_for_a_published_lesson()
    {
        var lesson = Lesson.Create(new LessonId(Guid.NewGuid()), new UnitId(Guid.NewGuid()), new Title("Greetings"), 1, isPublished: true);
        lesson.EnsureCompletable(); // does not throw
    }

    [Fact]
    public void EnsureCompletable_throws_for_an_unpublished_lesson()
    {
        var lesson = Lesson.Create(new LessonId(Guid.NewGuid()), new UnitId(Guid.NewGuid()), new Title("Greetings"), 1, isPublished: false);
        Assert.Throws<InvalidOperationException>(() => lesson.EnsureCompletable());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~AggregatesTests"`
Expected: FAIL — build error, `Course`/`Unit`/`Lesson` do not exist.

- [ ] **Step 3: Implement `Course`**

`src/Modules/Learning/Learning.Domain/Course.cs`:
```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Course : AggregateRoot
{
    public CourseId Id { get; private set; } = default!;
    public Title Title { get; private set; } = default!;
    public string? Language { get; private set; }

    private Course() { } // EF

    public static Course Create(CourseId id, Title title, string? language = null) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        Title = title ?? throw new ArgumentNullException(nameof(title)),
        Language = language
    };
}
```

- [ ] **Step 4: Implement `Unit`**

`src/Modules/Learning/Learning.Domain/Unit.cs`:
```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

// NOTE: shares its simple name with BuildingBlocks.Mediator.Unit. Domain never imports Mediator,
// so there is no clash here; Application files that touch both add a using-alias (see Task 4).
public sealed class Unit : AggregateRoot
{
    public UnitId Id { get; private set; } = default!;
    public CourseId CourseId { get; private set; } = default!; // reference by id, not a navigation
    public Title Title { get; private set; } = default!;
    public int Position { get; private set; }

    private Unit() { } // EF

    public static Unit Create(UnitId id, CourseId courseId, Title title, int position) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        CourseId = courseId ?? throw new ArgumentNullException(nameof(courseId)),
        Title = title ?? throw new ArgumentNullException(nameof(title)),
        Position = position
    };
}
```

- [ ] **Step 5: Implement `Lesson`**

`src/Modules/Learning/Learning.Domain/Lesson.cs`:
```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Lesson : AggregateRoot
{
    public LessonId Id { get; private set; } = default!;
    public UnitId UnitId { get; private set; } = default!; // reference by id, not a navigation
    public Title Title { get; private set; } = default!;
    public int Position { get; private set; }
    public bool IsPublished { get; private set; }

    private Lesson() { } // EF

    public static Lesson Create(LessonId id, UnitId unitId, Title title, int position, bool isPublished) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        UnitId = unitId ?? throw new ArgumentNullException(nameof(unitId)),
        Title = title ?? throw new ArgumentNullException(nameof(title)),
        Position = position,
        IsPublished = isPublished
    };

    // Tell-don't-ask: the handler tells the lesson it is being completed; the lesson enforces its rule.
    public void EnsureCompletable()
    {
        if (!IsPublished)
            throw new InvalidOperationException($"Lesson '{Id}' is not published and cannot be completed.");
    }
}
```

- [ ] **Step 6: Implement the `ILessonRepository` port**

`src/Modules/Learning/Learning.Domain/ILessonRepository.cs`:
```csharp
namespace Learning.Domain;

// Read port owned by the Domain; implemented in Infrastructure. Slice 1's completion path only reads.
public interface ILessonRepository
{
    Task<Lesson?> GetByIdAsync(LessonId id, CancellationToken ct);
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/Learning.Domain.Tests`
Expected: PASS (9 tests total across both test classes).

- [ ] **Step 8: Commit**

```bash
git add src/Modules/Learning/Learning.Domain tests/Learning.Domain.Tests
git commit -m "feat(learning): add Course/Unit/Lesson aggregates and ILessonRepository port

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: `CompleteLesson` command + handler

**Files:**
- Create: `src/Modules/Learning/Learning.Application/CompleteLesson.cs`
- Test: `tests/Learning.Integration.Tests/Application/CompleteLessonHandlerTests.cs`

**Interfaces:**
- Consumes: `ILessonRepository`, `Lesson` (Task 3); `IMediator`, `IRequest<>`, `IRequestHandler<,>`, `Unit` (Mediator); `LessonCompleted` (Contracts); `TimeProvider`.
- Produces:
  - `record CompleteLesson(Guid LearnerId, Guid LessonId) : IRequest<Unit>`
  - `CompleteLessonHandler(ILessonRepository lessons, IMediator mediator, TimeProvider clock)` — loads the lesson (throws `KeyNotFoundException` if null), calls `EnsureCompletable()`, publishes `LessonCompleted` with `OccurredOn = clock.GetUtcNow()`, returns `Unit.Value`.

- [ ] **Step 1: Write the failing tests**

`tests/Learning.Integration.Tests/Application/CompleteLessonHandlerTests.cs`:
```csharp
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Learning.Application;
using Learning.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Learning.Integration.Tests.Application;

public class CompleteLessonHandlerTests
{
    private sealed class StubLessonRepository(Lesson? lesson) : ILessonRepository
    {
        public Task<Lesson?> GetByIdAsync(LessonId id, CancellationToken ct) => Task.FromResult(lesson);
    }

    private sealed class CapturingMediator : IMediator
    {
        public readonly List<INotification> Published = new();
        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task PublishAsync(INotification notification, CancellationToken ct = default)
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }
    }

    private static Lesson Lesson(Guid id, bool published) =>
        Learning.Domain.Lesson.Create(new LessonId(id), new UnitId(Guid.NewGuid()), new Title("Greetings"), 1, published);

    [Fact]
    public async Task Completing_a_published_lesson_publishes_LessonCompleted_from_the_clock()
    {
        var lessonId = Guid.NewGuid();
        var learnerId = Guid.NewGuid();
        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var mediator = new CapturingMediator();
        var handler = new CompleteLessonHandler(new StubLessonRepository(Lesson(lessonId, published: true)), mediator, clock);

        await handler.HandleAsync(new CompleteLesson(learnerId, lessonId), CancellationToken.None);

        var evt = Assert.IsType<LessonCompleted>(Assert.Single(mediator.Published));
        Assert.Equal(learnerId, evt.LearnerId);
        Assert.Equal(lessonId, evt.LessonId);
        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.Equal(clock.GetUtcNow(), evt.OccurredOn);
    }

    [Fact]
    public async Task Completing_an_unpublished_lesson_throws_and_publishes_nothing()
    {
        var lessonId = Guid.NewGuid();
        var mediator = new CapturingMediator();
        var handler = new CompleteLessonHandler(new StubLessonRepository(Lesson(lessonId, published: false)), mediator, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new CompleteLesson(Guid.NewGuid(), lessonId), CancellationToken.None));
        Assert.Empty(mediator.Published);
    }

    [Fact]
    public async Task Completing_an_unknown_lesson_throws_KeyNotFound_and_publishes_nothing()
    {
        var mediator = new CapturingMediator();
        var handler = new CompleteLessonHandler(new StubLessonRepository(null), mediator, TimeProvider.System);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.HandleAsync(new CompleteLesson(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
        Assert.Empty(mediator.Published);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~CompleteLessonHandlerTests"`
Expected: FAIL — build error, `CompleteLesson`/`CompleteLessonHandler` do not exist.

- [ ] **Step 3: Implement the command + handler**

`src/Modules/Learning/Learning.Application/CompleteLesson.cs`:
```csharp
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Learning.Domain;
using Unit = BuildingBlocks.Mediator.Unit; // disambiguate from Learning.Domain.Unit (the aggregate)

namespace Learning.Application;

public sealed record CompleteLesson(Guid LearnerId, Guid LessonId) : IRequest<Unit>;

public sealed class CompleteLessonHandler(
    ILessonRepository lessons,
    IMediator mediator,
    TimeProvider clock) : IRequestHandler<CompleteLesson, Unit>
{
    public async Task<Unit> HandleAsync(CompleteLesson request, CancellationToken ct)
    {
        var lesson = await lessons.GetByIdAsync(new LessonId(request.LessonId), ct)
            ?? throw new KeyNotFoundException($"Lesson '{request.LessonId}' was not found.");

        lesson.EnsureCompletable();

        // Fresh EventId per completion → repeatable; the AppliedAward ledger still dedups true redelivery.
        // Contract unchanged / XP-free; OccurredOn from the injected clock (never DateTimeOffset.UtcNow).
        await mediator.PublishAsync(
            new LessonCompleted(
                EventId: Guid.NewGuid(),
                LearnerId: request.LearnerId,
                LessonId: request.LessonId,
                OccurredOn: clock.GetUtcNow()),
            ct);

        return Unit.Value;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~CompleteLessonHandlerTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Learning/Learning.Application tests/Learning.Integration.Tests
git commit -m "feat(learning): add CompleteLesson command + handler (validated, repeatable)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: `LearningDbContext`, EF configurations, seed, design-time factory, and the `InitialLearning` migration

**Files:**
- Create: `src/Modules/Learning/Learning.Infrastructure/LearningDbContext.cs`
- Create: `src/Modules/Learning/Learning.Infrastructure/LearningSeedIds.cs`
- Create: `src/Modules/Learning/Learning.Infrastructure/CourseConfiguration.cs`, `UnitConfiguration.cs`, `LessonConfiguration.cs`
- Create: `src/Modules/Learning/Learning.Infrastructure/LearningDbContextFactory.cs`
- Create (generated): `src/Modules/Learning/Learning.Infrastructure/Migrations/*_InitialLearning.cs`
- Test: `tests/Learning.Integration.Tests/Infrastructure/LearningSeedTests.cs`

**Interfaces:**
- Consumes: `Course`, `Unit`, `Lesson`, and the value objects (Task 3).
- Produces: `LearningDbContext` with `DbSet<Course> Courses`, `DbSet<Unit> Units`, `DbSet<Lesson> Lessons` on schema `learning`; `public static class LearningSeedIds` (fixed Guids); a migration that creates the three tables and seeds 1 course / 2 units / 4 lessons (one unpublished).

> **Prereq:** the EF tools must be available — `dotnet tool install --global dotnet-ef` if `dotnet ef` is missing (Engagement already uses it).

- [ ] **Step 1: Write the failing seed test**

`tests/Learning.Integration.Tests/Infrastructure/LearningSeedTests.cs`:
```csharp
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class LearningSeedTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Seed_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public LearningSeedTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task Migration_seeds_one_course_two_units_and_four_lessons_with_one_unpublished()
    {
        await using var ctx = NewContext();
        Assert.Equal(1, await ctx.Courses.CountAsync());
        Assert.Equal(2, await ctx.Units.CountAsync());
        Assert.Equal(4, await ctx.Lessons.CountAsync());
        Assert.Equal(1, await ctx.Lessons.CountAsync(l => !l.IsPublished));
    }
}
```

- [ ] **Step 2: Implement `LearningSeedIds`**

`src/Modules/Learning/Learning.Infrastructure/LearningSeedIds.cs`:
```csharp
namespace Learning.Infrastructure;

// Fixed ids keep HasData deterministic across migrations and let tests target real seeded rows.
public static class LearningSeedIds
{
    public static readonly Guid SpanishCourse       = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid BasicsUnit          = new("22222222-2222-2222-2222-222222222221");
    public static readonly Guid FoodUnit            = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid GreetingsLesson     = new("33333333-3333-3333-3333-333333333331"); // published
    public static readonly Guid SerLesson           = new("33333333-3333-3333-3333-333333333332"); // published
    public static readonly Guid CafeLesson          = new("33333333-3333-3333-3333-333333333333"); // published
    public static readonly Guid DessertLessonDraft  = new("33333333-3333-3333-3333-333333333334"); // UNPUBLISHED
}
```

- [ ] **Step 3: Implement `LearningDbContext`**

`src/Modules/Learning/Learning.Infrastructure/LearningDbContext.cs`:
```csharp
using Learning.Domain;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure;

// Plain DbContext: Learning aggregates raise no domain events, so (unlike EngagementDbContext)
// there is no dispatcher and no SaveChanges override. Slice 1 is read-only at runtime.
public sealed class LearningDbContext(DbContextOptions<LearningDbContext> options) : DbContext(options)
{
    public const string Schema = "learning";

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Lesson> Lessons => Set<Lesson>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new CourseConfiguration());
        modelBuilder.ApplyConfiguration(new UnitConfiguration());
        modelBuilder.ApplyConfiguration(new LessonConfiguration());
    }
}
```

- [ ] **Step 4: Implement `CourseConfiguration` (with seed)**

`src/Modules/Learning/Learning.Infrastructure/CourseConfiguration.cs`:
```csharp
using Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learning.Infrastructure;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new CourseId(value))
            .HasColumnName("Id")
            .ValueGeneratedNever();

        builder.Property(c => c.Title)
            .HasConversion(t => t.Value, value => new Title(value))
            .HasColumnName("Title")
            .HasMaxLength(Title.MaxLength);

        builder.Property(c => c.Language).HasMaxLength(16);

        builder.Ignore(c => c.DomainEvents);

        builder.HasData(Course.Create(new CourseId(LearningSeedIds.SpanishCourse), new Title("Spanish"), "es"));
    }
}
```

- [ ] **Step 5: Implement `UnitConfiguration` (with seed)**

`src/Modules/Learning/Learning.Infrastructure/UnitConfiguration.cs`:
```csharp
using Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learning.Infrastructure;

internal sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => new UnitId(value))
            .HasColumnName("Id")
            .ValueGeneratedNever();

        builder.Property(u => u.CourseId)
            .HasConversion(id => id.Value, value => new CourseId(value))
            .HasColumnName("CourseId"); // id reference — a column, not a navigation

        builder.Property(u => u.Title)
            .HasConversion(t => t.Value, value => new Title(value))
            .HasColumnName("Title")
            .HasMaxLength(Title.MaxLength);

        builder.Property(u => u.Position).HasColumnName("Position");

        builder.Ignore(u => u.DomainEvents);

        builder.HasData(
            Unit.Create(new UnitId(LearningSeedIds.BasicsUnit), new CourseId(LearningSeedIds.SpanishCourse), new Title("Basics"), 1),
            Unit.Create(new UnitId(LearningSeedIds.FoodUnit),   new CourseId(LearningSeedIds.SpanishCourse), new Title("Food"),   2));
    }
}
```

- [ ] **Step 6: Implement `LessonConfiguration` (with seed — one unpublished)**

`src/Modules/Learning/Learning.Infrastructure/LessonConfiguration.cs`:
```csharp
using Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learning.Infrastructure;

internal sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new LessonId(value))
            .HasColumnName("Id")
            .ValueGeneratedNever();

        builder.Property(l => l.UnitId)
            .HasConversion(id => id.Value, value => new UnitId(value))
            .HasColumnName("UnitId"); // id reference — a column, not a navigation

        builder.Property(l => l.Title)
            .HasConversion(t => t.Value, value => new Title(value))
            .HasColumnName("Title")
            .HasMaxLength(Title.MaxLength);

        builder.Property(l => l.Position).HasColumnName("Position");
        builder.Property(l => l.IsPublished).HasColumnName("IsPublished");

        builder.Ignore(l => l.DomainEvents);

        builder.HasData(
            Lesson.Create(new LessonId(LearningSeedIds.GreetingsLesson), new UnitId(LearningSeedIds.BasicsUnit), new Title("Greetings"),      1, isPublished: true),
            Lesson.Create(new LessonId(LearningSeedIds.SerLesson),       new UnitId(LearningSeedIds.BasicsUnit), new Title("The verb ser"),   2, isPublished: true),
            Lesson.Create(new LessonId(LearningSeedIds.CafeLesson),      new UnitId(LearningSeedIds.FoodUnit),   new Title("At the cafe"),     1, isPublished: true),
            Lesson.Create(new LessonId(LearningSeedIds.DessertLessonDraft), new UnitId(LearningSeedIds.FoodUnit), new Title("Ordering dessert"), 2, isPublished: false));
    }
}
```

- [ ] **Step 7: Implement the design-time factory**

`src/Modules/Learning/Learning.Infrastructure/LearningDbContextFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Learning.Infrastructure;

// Used ONLY by the EF CLI at design time (migrations). Runtime wiring uses AddLearningInfrastructure.
public sealed class LearningDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Design;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new LearningDbContext(options);
    }
}
```

- [ ] **Step 8: Generate the migration**

Run:
```bash
dotnet ef migrations add InitialLearning -p src/Modules/Learning/Learning.Infrastructure -s src/Modules/Learning/Learning.Infrastructure -o Migrations
```
Expected: creates `Migrations/<timestamp>_InitialLearning.cs` (+ `.Designer.cs` and the model snapshot).

- [ ] **Step 9: Verify the generated migration seeds the catalog**

Open `Migrations/<timestamp>_InitialLearning.cs`; confirm `CreateTable` for `Courses`/`Units`/`Lessons` under schema `learning`, and `InsertData` for **1** course, **2** units, and **4** lessons (with `IsPublished = false` on "Ordering dessert").
> If `dotnet ef migrations add` fails on the value-converted seed values: HasData with scalar value-converters is supported in EF Core 10, so this is not expected — but the robust fallback is to drop `HasData` and seed at runtime (a method that `AddRange`s the `Create(...)` instances and `SaveChanges` when the tables are empty, invoked right after `Migrate()` in the factory/test setup). Prefer HasData.

- [ ] **Step 10: Run the seed test to verify it passes**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LearningSeedTests"`
Expected: PASS (1 test) — the migration creates and seeds `DuolingoLearning_Seed_Test`.

- [ ] **Step 11: Commit**

```bash
git add src/Modules/Learning/Learning.Infrastructure tests/Learning.Integration.Tests
git commit -m "feat(learning): add LearningDbContext, EF maps, seeded catalog + migration

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Catalog read — `GetCatalog` query, DTOs, `ICatalogReadService`, `CatalogReadService`

**Files:**
- Create: `src/Modules/Learning/Learning.Application/GetCatalog.cs`
- Create: `src/Modules/Learning/Learning.Infrastructure/CatalogReadService.cs`
- Test: `tests/Learning.Integration.Tests/Infrastructure/CatalogReadServiceTests.cs`

**Interfaces:**
- Consumes: `LearningDbContext` (Task 5); `IRequest<>`, `IRequestHandler<,>` (Mediator).
- Produces:
  - `record GetCatalog() : IRequest<CatalogDto>`
  - `record CatalogDto(IReadOnlyList<CourseDto> Courses)`; `record CourseDto(Guid Id, string Title, string? Language, IReadOnlyList<UnitDto> Units)`; `record UnitDto(Guid Id, string Title, int Position, IReadOnlyList<LessonDto> Lessons)`; `record LessonDto(Guid Id, string Title, int Position, bool IsPublished)`
  - `interface ICatalogReadService { Task<CatalogDto> GetCatalogAsync(CancellationToken ct); }`
  - `GetCatalogHandler` (delegates to the port); `CatalogReadService` (materializes + assembles, ordered by `Position`).

- [ ] **Step 1: Write the failing test**

`tests/Learning.Integration.Tests/Infrastructure/CatalogReadServiceTests.cs`:
```csharp
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class CatalogReadServiceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Catalog_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public CatalogReadServiceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task Returns_the_seeded_catalog_nested_and_ordered_by_position()
    {
        await using var ctx = NewContext();

        var catalog = await new CatalogReadService(ctx).GetCatalogAsync(CancellationToken.None);

        var course = Assert.Single(catalog.Courses);
        Assert.Equal("Spanish", course.Title);
        Assert.Equal("es", course.Language);
        Assert.Equal(2, course.Units.Count);
        Assert.Equal("Basics", course.Units[0].Title); // Position 1 sorts first
        Assert.Equal("Food", course.Units[1].Title);   // Position 2
        Assert.Equal(2, course.Units[0].Lessons.Count);
        Assert.Equal("Greetings", course.Units[0].Lessons[0].Title);
        Assert.Contains(course.Units.SelectMany(u => u.Lessons), l => !l.IsPublished);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~CatalogReadServiceTests"`
Expected: FAIL — build error, `CatalogReadService`/`CatalogDto` do not exist.

- [ ] **Step 3: Implement the query, DTOs, port, and handler**

`src/Modules/Learning/Learning.Application/GetCatalog.cs`:
```csharp
using BuildingBlocks.Mediator;

namespace Learning.Application;

public sealed record GetCatalog() : IRequest<CatalogDto>;

public sealed record CatalogDto(IReadOnlyList<CourseDto> Courses);
public sealed record CourseDto(Guid Id, string Title, string? Language, IReadOnlyList<UnitDto> Units);
public sealed record UnitDto(Guid Id, string Title, int Position, IReadOnlyList<LessonDto> Lessons);
public sealed record LessonDto(Guid Id, string Title, int Position, bool IsPublished);

// Read-model port (returns a DTO) — distinct from the aggregate repository. Implemented in Infrastructure.
public interface ICatalogReadService
{
    Task<CatalogDto> GetCatalogAsync(CancellationToken ct);
}

public sealed class GetCatalogHandler(ICatalogReadService catalog) : IRequestHandler<GetCatalog, CatalogDto>
{
    public Task<CatalogDto> HandleAsync(GetCatalog request, CancellationToken ct) => catalog.GetCatalogAsync(ct);
}
```

- [ ] **Step 4: Implement `CatalogReadService`**

`src/Modules/Learning/Learning.Infrastructure/CatalogReadService.cs`:
```csharp
using Learning.Application;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure;

public sealed class CatalogReadService(LearningDbContext context) : ICatalogReadService
{
    public async Task<CatalogDto> GetCatalogAsync(CancellationToken ct)
    {
        // The catalog is small (seeded); materialize all three tables and assemble in memory.
        // In-memory ordering/joins on the value objects avoids any EF value-converter translation limits.
        var courses = await context.Courses.ToListAsync(ct);
        var units = await context.Units.ToListAsync(ct);
        var lessons = await context.Lessons.ToListAsync(ct);

        var courseDtos = courses
            .OrderBy(c => c.Title.Value)
            .Select(c => new CourseDto(
                c.Id.Value,
                c.Title.Value,
                c.Language,
                units.Where(u => u.CourseId == c.Id)
                    .OrderBy(u => u.Position)
                    .Select(u => new UnitDto(
                        u.Id.Value,
                        u.Title.Value,
                        u.Position,
                        lessons.Where(l => l.UnitId == u.Id)
                            .OrderBy(l => l.Position)
                            .Select(l => new LessonDto(l.Id.Value, l.Title.Value, l.Position, l.IsPublished))
                            .ToList()))
                    .ToList()))
            .ToList();

        return new CatalogDto(courseDtos);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~CatalogReadServiceTests"`
Expected: PASS (1 test).

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Learning/Learning.Application src/Modules/Learning/Learning.Infrastructure tests/Learning.Integration.Tests
git commit -m "feat(learning): add catalog read model (GetCatalog + CatalogReadService)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: `LessonRepository` + `AddLearningInfrastructure` DI

**Files:**
- Create: `src/Modules/Learning/Learning.Infrastructure/LessonRepository.cs`
- Create: `src/Modules/Learning/Learning.Infrastructure/LearningInfrastructureExtensions.cs`
- Test: `tests/Learning.Integration.Tests/Infrastructure/LessonRepositoryTests.cs`

**Interfaces:**
- Consumes: `ILessonRepository` (Task 3), `ICatalogReadService` (Task 6), `LearningDbContext` (Task 5).
- Produces: `LessonRepository : ILessonRepository`; `AddLearningInfrastructure(this IServiceCollection, string connectionString)` registering the DbContext, `ILessonRepository`, `ICatalogReadService`, and (via `TryAddSingleton`) `TimeProvider.System`.

- [ ] **Step 1: Write the failing test**

`tests/Learning.Integration.Tests/Infrastructure/LessonRepositoryTests.cs`:
```csharp
using Learning.Domain;
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class LessonRepositoryTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Lesson_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public LessonRepositoryTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task GetByIdAsync_returns_the_seeded_published_lesson()
    {
        await using var ctx = NewContext();
        var lesson = await new LessonRepository(ctx)
            .GetByIdAsync(new LessonId(LearningSeedIds.GreetingsLesson), CancellationToken.None);

        Assert.NotNull(lesson);
        Assert.True(lesson!.IsPublished);
        Assert.Equal("Greetings", lesson.Title.Value);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_an_unknown_lesson()
    {
        await using var ctx = NewContext();
        var lesson = await new LessonRepository(ctx)
            .GetByIdAsync(new LessonId(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(lesson);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LessonRepositoryTests"`
Expected: FAIL — build error, `LessonRepository` does not exist.

- [ ] **Step 3: Implement `LessonRepository`**

`src/Modules/Learning/Learning.Infrastructure/LessonRepository.cs`:
```csharp
using Learning.Domain;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure;

public sealed class LessonRepository(LearningDbContext context) : ILessonRepository
{
    // Whole-VO equality translates (== on the converted Id column); never reach into id.Value in the query.
    public Task<Lesson?> GetByIdAsync(LessonId id, CancellationToken ct) =>
        context.Lessons.FirstOrDefaultAsync(l => l.Id == id, ct);
}
```

- [ ] **Step 4: Implement `AddLearningInfrastructure`**

`src/Modules/Learning/Learning.Infrastructure/LearningInfrastructureExtensions.cs`:
```csharp
using Learning.Application;
using Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Learning.Infrastructure;

public static class LearningInfrastructureExtensions
{
    public static IServiceCollection AddLearningInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LearningDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ICatalogReadService, CatalogReadService>();

        // TimeProvider is shared with Engagement; TryAdd avoids a duplicate registration when both modules load.
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LessonRepositoryTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Learning/Learning.Infrastructure tests/Learning.Integration.Tests
git commit -m "feat(learning): add LessonRepository + AddLearningInfrastructure DI

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: Replace the stub — wire the real module into the Host and prove it end-to-end

This is the integration keystone: the e2e tests drive the Host rewiring. Because the stub and the real
module both define `CompleteLesson`, the swap is atomic (you cannot half-wire it).

**Files:**
- Create: `tests/Learning.Integration.Tests/EndToEnd/LearningApiFactory.cs`, `EndToEnd/LearningApiTests.cs`
- Modify: `src/Host/Host.csproj`, `src/Host/Program.cs`, `src/Host/appsettings.json`, `Duolingo.slnx`
- Modify: `tests/Engagement.Integration.Tests/EndToEnd/EngagementApiTests.cs` (remove the stub-coupled test)
- Delete: `src/Modules/Learning/Learning.Stub/` (whole project), `tests/Engagement.Integration.Tests/Learning/CompleteLessonHandlerTests.cs`

**Interfaces:**
- Consumes: `AddLearningInfrastructure` (Task 7), `CompleteLesson`/`GetCatalog` (Tasks 4/6), `LearningSeedIds` (Task 5), `ICurrentUser` (Host, existing: `Guid LearnerId`).
- Produces: real `POST /lessons/{lessonId:guid}/complete` (200 / 404 / 409) and `GET /courses`; the Host running the real Learning module.

- [ ] **Step 1: Write the e2e factory (boots the real Host, provisions both DBs)**

`tests/Learning.Integration.Tests/EndToEnd/LearningApiFactory.cs`:
```csharp
using Engagement.Infrastructure;
using Learning.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learning.Integration.Tests.EndToEnd;

public sealed class LearningApiFactory : WebApplicationFactory<Program>
{
    private const string EngagementDb =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_E2E_Engagement;Trusted_Connection=True;TrustServerCertificate=True";
    private const string LearningDb =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_E2E;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Engagement", EngagementDb);
        builder.UseSetting("ConnectionStrings:Learning", LearningDb);
        builder.UseSetting("Leagues:Settlement:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            using var scope = services.BuildServiceProvider().CreateScope();

            var engagement = scope.ServiceProvider.GetRequiredService<EngagementDbContext>();
            engagement.Database.EnsureDeleted();
            engagement.Database.Migrate();

            var learning = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            learning.Database.EnsureDeleted();
            learning.Database.Migrate(); // applies the learning schema + seed
        });
    }
}
```

- [ ] **Step 2: Write the e2e tests**

`tests/Learning.Integration.Tests/EndToEnd/LearningApiTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using Learning.Infrastructure;
using Xunit;

namespace Learning.Integration.Tests.EndToEnd;

public class LearningApiTests(LearningApiFactory factory) : IClassFixture<LearningApiFactory>
{
    private sealed record XpResponse(Guid LearnerId, int TotalXp);
    private sealed record CatalogView(List<CourseView> Courses);
    private sealed record CourseView(Guid Id, string Title, string? Language, List<UnitView> Units);
    private sealed record UnitView(Guid Id, string Title, int Position, List<LessonView> Lessons);
    private sealed record LessonView(Guid Id, string Title, int Position, bool IsPublished);

    private HttpClient ClientForLearner(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    [Fact]
    public async Task Get_courses_returns_the_seeded_catalog()
    {
        var catalog = await factory.CreateClient().GetFromJsonAsync<CatalogView>("/courses");

        Assert.NotNull(catalog);
        var course = Assert.Single(catalog!.Courses);
        Assert.Equal("Spanish", course.Title);
        Assert.Equal(2, course.Units.Count);
        Assert.Equal(4, course.Units.Sum(u => u.Lessons.Count));
    }

    [Fact]
    public async Task Completing_a_published_lesson_returns_200_and_awards_xp()
    {
        var learnerId = Guid.NewGuid();
        var client = ClientForLearner(learnerId);

        var post = await client.PostAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/complete", null);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var xp = await client.GetFromJsonAsync<XpResponse>("/me/xp");
        Assert.Equal(10, xp!.TotalXp);
    }

    [Fact]
    public async Task Completing_an_unknown_lesson_returns_404()
    {
        var post = await ClientForLearner(Guid.NewGuid())
            .PostAsync($"/lessons/{Guid.NewGuid()}/complete", null);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task Completing_an_unpublished_lesson_returns_409()
    {
        var post = await ClientForLearner(Guid.NewGuid())
            .PostAsync($"/lessons/{LearningSeedIds.DessertLessonDraft}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, post.StatusCode);
    }

    [Fact]
    public async Task Completing_the_same_lesson_twice_awards_xp_twice()
    {
        var learnerId = Guid.NewGuid();
        var client = ClientForLearner(learnerId);

        await client.PostAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/complete", null);
        await client.PostAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/complete", null);

        var xp = await client.GetFromJsonAsync<XpResponse>("/me/xp");
        Assert.Equal(20, xp!.TotalXp);
    }
}
```

- [ ] **Step 3: Run the e2e tests to verify they fail**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LearningApiTests"`
Expected: FAIL — the Host is not wired for Learning yet (the factory can't resolve `LearningDbContext`, and `/courses` 404s).

- [ ] **Step 4: Swap the Host project references**

In `src/Host/Host.csproj`, replace:
```xml
    <ProjectReference Include="..\Modules\Learning\Learning.Stub\Learning.Stub.csproj" />
```
with:
```xml
    <ProjectReference Include="..\Modules\Learning\Learning.Application\Learning.Application.csproj" />
    <ProjectReference Include="..\Modules\Learning\Learning.Infrastructure\Learning.Infrastructure.csproj" />
```

- [ ] **Step 5: Rewire `src/Host/Program.cs`**

Change the `using` (top of file): replace `using Learning.Stub;` with:
```csharp
using Learning.Application;
using Learning.Infrastructure;
```

Replace the mediator registration:
```csharp
builder.Services.AddMediator(
    typeof(GetXpAccount).Assembly,   // Engagement.Application handlers
    LearningStubExtensions.Assembly);         // Learning.Stub handlers
```
with:
```csharp
builder.Services.AddMediator(
    typeof(GetXpAccount).Assembly,        // Engagement.Application handlers
    typeof(CompleteLesson).Assembly);     // Learning.Application handlers
```

Add the Learning infrastructure registration immediately after the `AddEngagementInfrastructure(...)` block:
```csharp
builder.Services.AddLearningInfrastructure(
    builder.Configuration.GetConnectionString("Learning")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Learning"));
```

Replace the completion endpoint:
```csharp
app.MapPost("/lessons/{lessonId:guid}/complete",
    async (Guid lessonId, ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        await mediator.SendAsync(new CompleteLesson(user.LearnerId, lessonId), ct);
        return Results.Accepted();
    });
```
with:
```csharp
app.MapPost("/lessons/{lessonId:guid}/complete",
    async (Guid lessonId, ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        try
        {
            await mediator.SendAsync(new CompleteLesson(user.LearnerId, lessonId), ct);
            return Results.Ok(); // work (incl. the XP award) runs in-process before we return
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message }); // lesson exists but is not completable
        }
    });
```

Add the catalog read endpoint (next to the other `MapGet`s):
```csharp
app.MapGet("/courses",
    async (IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.SendAsync(new GetCatalog(), ct)));
```

- [ ] **Step 6: Add the Learning connection string to `src/Host/appsettings.json`**

Change the `ConnectionStrings` block to:
```json
  "ConnectionStrings": {
    "Engagement": "Server=(localdb)\\MSSQLLocalDB;Database=DuolingoEngagement;Trusted_Connection=True;TrustServerCertificate=True",
    "Learning": "Server=(localdb)\\MSSQLLocalDB;Database=DuolingoLearning;Trusted_Connection=True;TrustServerCertificate=True"
  },
```

- [ ] **Step 7: Delete the stub and its stub-coupled tests**

- Delete the directory `src/Modules/Learning/Learning.Stub/`.
- Remove its line from `Duolingo.slnx`:
  ```xml
    <Project Path="src/Modules/Learning/Learning.Stub/Learning.Stub.csproj" />
  ```
- Delete `tests/Engagement.Integration.Tests/Learning/CompleteLessonHandlerTests.cs` (its behavior is re-covered by Task 4 in `Learning.Integration.Tests`).
- In `tests/Engagement.Integration.Tests/EndToEnd/EngagementApiTests.cs`, delete the `Completing_a_lesson_then_reading_engagement_shows_ten_xp` test (it posts a random guid and asserts 202 — pure stub behavior, now superseded by `LearningApiTests`). Keep `Same_lesson_completed_event_delivered_twice_awards_once` and the class's helpers unchanged.

- [ ] **Step 8: Confirm no dangling references to the stub**

Run: `git grep -n "Learning.Stub\|LearningStubExtensions"`
Expected: **no matches**. (If any remain, remove them.)

- [ ] **Step 9: Build, then run the full suite**

Run: `dotnet build Duolingo.slnx`
Expected: **Build succeeded.**

Run: `dotnet test Duolingo.slnx`
Expected: PASS — `LearningApiTests` (5) green, and every existing Engagement/streak/league test still green. (`EngagementApiFactory` boots because `appsettings.json` now supplies `ConnectionStrings:Learning`; those tests never open that connection.)

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat(learning): replace stub with real module; validated completion + GET /courses

Deletes Learning.Stub; wires Learning.Application/Infrastructure into the Host.
POST /lessons/{id}/complete now validates against the seeded catalog (200/404/409);
adds GET /courses. Migrates the two stub-coupled Engagement tests.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 9: Architecture tests — enforce the module boundaries

**Files:**
- Create: `tests/Learning.Integration.Tests/Architecture/ArchitectureTests.cs`

**Interfaces:**
- Consumes: the production Learning assemblies (via a type from each) + `NetArchTest.Rules`.
- Produces: tests asserting `Learning.Domain` depends on nothing infrastructural, and no production `Learning.*` assembly depends on `Engagement.*`.

- [ ] **Step 1: Write the failing tests**

`tests/Learning.Integration.Tests/Architecture/ArchitectureTests.cs`:
```csharp
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Learning.Integration.Tests.Architecture;

public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(global::Learning.Domain.Lesson).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(global::Learning.Application.CompleteLesson).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(global::Learning.Infrastructure.LearningDbContext).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_EfCore_or_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_EfCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Learning_does_not_depend_on_Engagement()
    {
        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Engagement")
                .GetResult();

            Assert.True(result.IsSuccessful, $"{assembly.GetName().Name}: {Describe(result)}");
        }
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? "ok"
            : "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~Architecture"`
Expected: PASS (3 tests). They pass on first run because the projects were built correctly — that is the point: they lock the boundaries so a *future* accidental `Engagement`/EF reference turns red.

- [ ] **Step 3: Commit**

```bash
git add tests/Learning.Integration.Tests
git commit -m "test(learning): enforce module boundaries with NetArchTest

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review

**1. Spec coverage** — every spec section maps to a task:
- Course→Unit→Lesson, 3 roots by id → Tasks 2–3. Typed-id + `Title` VOs, `IsPublished`, `EnsureCompletable` → Tasks 2–3.
- Validated completion (404/409/200), repeatable, XP-free contract, `OccurredOn` from clock → Tasks 4 & 8.
- `learning` schema + `DuolingoLearning` DB + own migration; `HasData` seed (one unpublished) → Task 5.
- `GET /courses` read model → Tasks 6 & 8. `ILessonRepository` / `ICatalogReadService` split → Tasks 3/6/7.
- Delete stub; wire real module → Task 8. Test migration (superseded EngagementApiTests test + moved handler test) → Tasks 4 & 8.
- Architecture (Domain pure; no cross-module refs) → Task 9.
- All 8 acceptance criteria are exercised by tests in Tasks 3, 4, 5, 6, 7, 8, 9.

**2. Placeholder scan** — no `TBD`/`TODO`/"add error handling"/"similar to Task N". Every code step shows complete code; the one risk note (HasData) names a concrete pivot, not a gap.

**3. Type consistency** — checked across tasks: `Lesson.Create(LessonId, UnitId, Title, int, bool)`, `EnsureCompletable()`, `ILessonRepository.GetByIdAsync(LessonId, CancellationToken)`, `CompleteLesson(Guid, Guid)`, `GetCatalog()` → `CatalogDto`, `ICatalogReadService.GetCatalogAsync(CancellationToken)`, `AddLearningInfrastructure(string)`, and `LearningSeedIds.*` names are identical everywhere they appear. The `Unit` alias is applied in the one Application file (Task 4) that imports both namespaces.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-10-learning-catalog-completion.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
