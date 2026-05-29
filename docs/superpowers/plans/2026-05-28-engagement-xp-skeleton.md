# Engagement XP Walking Skeleton — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the thinnest end-to-end slice of the Duolingo clone — a learner earns XP when a (stubbed) lesson is completed, and can read their XP total — validating the whole modular-monolith + Clean Architecture stack around the Engagement core.

**Architecture:** Evolutionary modular monolith. One ASP.NET Core host; the `Engagement` module is split into compiler-enforced layer projects (Domain/Application/Infrastructure). Modules talk only through a `Contracts` assembly + an in-process mediator. `Learning` is a single-project stub that raises `LessonCompleted`; Engagement reacts via an in-process notification handler. Persistence is EF Core on SQL Server, one schema (`engagement`) per module.

**Tech Stack:** C# / .NET 10 (`net10.0`), ASP.NET Core Minimal APIs, EF Core 10 (SQL Server / LocalDB), xUnit, hand-rolled mediator, NetArchTest for architecture rules.

**Reference specs:**
- Foundations: `docs/superpowers/specs/2026-05-28-architecture-foundations-design.md`
- Sub-project: `docs/superpowers/specs/2026-05-28-engagement-xp-skeleton-design.md`

**Conventions for this plan:**
- All commands run from the repo root `C:\Users\ysaldanha\source\repos\duolingo` unless stated. Shell is **PowerShell**.
- After each task, the solution must build (`dotnet build`) and all tests must pass (`dotnet test`).
- Commit after every task with the message shown.

**Resolved open choices (the spec deferred these to planning):**
- *Mediator shape:* request/response + notifications + request-only pipeline behaviors, hand-rolled with `dynamic` dispatch (Task 4).
- *Transaction / unit-of-work:* In slice 1, persistence happens inside the `LessonCompleted` **notification** handler, which sits *outside* the request pipeline — so a transaction-per-request *behavior* doesn't apply yet. We call `SaveChangesAsync` explicitly in the handler (one EngagementDbContext = one transaction), and use a `LoggingBehavior` to demonstrate the pipeline mechanism. A transaction/UoW behavior arrives when a *command* writes to the DB (a later sub-project).
- *Test DB:* SQL Server **LocalDB** (Docker unavailable); separate DBs for dev/design/test/e2e.
- *Endpoints:* Minimal APIs.

---

## File / project map (locked before tasks)

| Project | Responsibility | References |
|---|---|---|
| `src/BuildingBlocks/Domain` | Seedwork: `ValueObject`, `AggregateRoot`, `IDomainEvent` | — |
| `src/BuildingBlocks/Mediator` | Hand-rolled mediator: request/notification abstractions + impl + DI | MS.DI.Abstractions |
| `src/BuildingBlocks/Contracts` | Cross-module integration events (`LessonCompleted`) | Mediator |
| `src/Modules/Engagement/Engagement.Domain` | `LearnerEngagement`, value objects, `XpAwarded`, policy, repo **interface** | BuildingBlocks.Domain |
| `src/Modules/Engagement/Engagement.Application` | Use cases: notification handler + query + handlers | Domain, Contracts, Mediator |
| `src/Modules/Engagement/Engagement.Infrastructure` | `EngagementDbContext`, EF config, repo **impl**, migrations | Application, Domain, EF Core |
| `src/Modules/Learning/Learning.Stub` | `CompleteLesson` command → raises `LessonCompleted` | Contracts, Mediator |
| `src/Host` | Composition root, DI, endpoints, fake `ICurrentUser` | Engagement.Application, Engagement.Infrastructure, Learning.Stub, Mediator, Contracts |
| `tests/Engagement.Domain.Tests` | Pure domain unit tests | Engagement.Domain |
| `tests/Engagement.Integration.Tests` | End-to-end + architecture tests | Host, Engagement.Infrastructure, Engagement.Application, Contracts, Mediator |

**Dependency rule, made physical:** `Engagement.Domain` references *only* `BuildingBlocks.Domain` — no EF Core, no ASP.NET. The compiler enforces it. (Task 13 adds tests that enforce the rules the compiler can't.)

> **Note on `internal`:** because we split a module into separate assemblies, domain types must be `public` for the sibling Application/Infrastructure assemblies to use them. The real boundary — "nothing *outside* the Engagement module uses its domain types" — is enforced by Task 13's architecture tests, not by `internal`. Tightening to `internal` + `InternalsVisibleTo` is a documented later refinement.

---

## Task 1: Scaffold the solution, projects, references, packages

**Files:**
- Create: `Duolingo.sln` and all `.csproj` files in the map above.

- [ ] **Step 1: Create the solution and all projects**

Run (PowerShell, from repo root):

```powershell
dotnet new sln -n Duolingo

dotnet new classlib -n BuildingBlocks.Domain    -o src/BuildingBlocks/Domain    -f net10.0
dotnet new classlib -n BuildingBlocks.Mediator  -o src/BuildingBlocks/Mediator  -f net10.0
dotnet new classlib -n BuildingBlocks.Contracts -o src/BuildingBlocks/Contracts -f net10.0

dotnet new classlib -n Engagement.Domain         -o src/Modules/Engagement/Engagement.Domain         -f net10.0
dotnet new classlib -n Engagement.Application     -o src/Modules/Engagement/Engagement.Application     -f net10.0
dotnet new classlib -n Engagement.Infrastructure  -o src/Modules/Engagement/Engagement.Infrastructure  -f net10.0

dotnet new classlib -n Learning.Stub -o src/Modules/Learning/Learning.Stub -f net10.0

dotnet new web -n Host -o src/Host -f net10.0

dotnet new xunit -n Engagement.Domain.Tests      -o tests/Engagement.Domain.Tests      -f net10.0
dotnet new xunit -n Engagement.Integration.Tests -o tests/Engagement.Integration.Tests -f net10.0
```

- [ ] **Step 2: Delete the template `Class1.cs` placeholder files**

Run:

```powershell
Get-ChildItem -Recurse -Filter Class1.cs | Remove-Item
```

- [ ] **Step 3: Add every project to the solution**

Run:

```powershell
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { dotnet sln add $_.FullName }
```

- [ ] **Step 4: Wire project references (the dependency graph)**

Run:

```powershell
dotnet add src/BuildingBlocks/Contracts reference src/BuildingBlocks/Mediator

dotnet add src/Modules/Engagement/Engagement.Domain reference src/BuildingBlocks/Domain

dotnet add src/Modules/Engagement/Engagement.Application reference `
  src/Modules/Engagement/Engagement.Domain `
  src/BuildingBlocks/Contracts `
  src/BuildingBlocks/Mediator

dotnet add src/Modules/Engagement/Engagement.Infrastructure reference `
  src/Modules/Engagement/Engagement.Application `
  src/Modules/Engagement/Engagement.Domain

dotnet add src/Modules/Learning/Learning.Stub reference `
  src/BuildingBlocks/Contracts `
  src/BuildingBlocks/Mediator

dotnet add src/Host reference `
  src/Modules/Engagement/Engagement.Application `
  src/Modules/Engagement/Engagement.Infrastructure `
  src/Modules/Learning/Learning.Stub `
  src/BuildingBlocks/Mediator `
  src/BuildingBlocks/Contracts

dotnet add tests/Engagement.Domain.Tests reference src/Modules/Engagement/Engagement.Domain

dotnet add tests/Engagement.Integration.Tests reference `
  src/Host `
  src/Modules/Engagement/Engagement.Infrastructure `
  src/Modules/Engagement/Engagement.Application `
  src/BuildingBlocks/Contracts `
  src/BuildingBlocks/Mediator
```

- [ ] **Step 5: Add NuGet packages**

Run:

```powershell
dotnet add src/BuildingBlocks/Mediator package Microsoft.Extensions.DependencyInjection.Abstractions

dotnet add src/Modules/Engagement/Engagement.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/Modules/Engagement/Engagement.Infrastructure package Microsoft.EntityFrameworkCore.Design

dotnet add tests/Engagement.Integration.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/Engagement.Integration.Tests package Microsoft.EntityFrameworkCore.SqlServer
dotnet add tests/Engagement.Integration.Tests package NetArchTest.Rules
```

> Versions resolve to the latest 10.x automatically. If `dotnet add package` warns about a major mismatch, pin with `--version 10.*`.

- [ ] **Step 6: Install the EF Core CLI tool (for migrations)**

Run:

```powershell
dotnet tool install --global dotnet-ef
```

If it's already installed: `dotnet tool update --global dotnet-ef`. Verify: `dotnet ef --version` (expect 10.x).

- [ ] **Step 7: Build the empty solution**

Run: `dotnet build`
Expected: **Build succeeded**, 0 errors. (Projects are empty but must compile and the reference graph must be valid.)

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "chore: scaffold solution, projects, references, packages"
```

---

## Task 2: Seedwork — `ValueObject` base

**Why:** Value objects are compared by their contents, not identity. A reusable base gives us correct equality/hashing once, so every value object (`Xp`, `LearnerId`, ...) gets it for free. This is the foundation for killing primitive obsession.

**Files:**
- Test: `tests/Engagement.Domain.Tests/Seedwork/ValueObjectTests.cs`
- Create: `src/BuildingBlocks/Domain/ValueObject.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Engagement.Domain.Tests/Seedwork/ValueObjectTests.cs`:

```csharp
using BuildingBlocks.Domain;
using Xunit;

namespace Engagement.Domain.Tests.Seedwork;

public class ValueObjectTests
{
    private sealed class Money : ValueObject
    {
        public int Amount { get; }
        public string Currency { get; }
        public Money(int amount, string currency) { Amount = amount; Currency = currency; }
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Fact]
    public void Equal_when_all_components_match()
    {
        Assert.Equal(new Money(5, "USD"), new Money(5, "USD"));
        Assert.True(new Money(5, "USD") == new Money(5, "USD"));
    }

    [Fact]
    public void Not_equal_when_a_component_differs()
    {
        Assert.NotEqual(new Money(5, "USD"), new Money(5, "EUR"));
        Assert.True(new Money(5, "USD") != new Money(6, "USD"));
    }

    [Fact]
    public void Equal_values_share_hash_code()
    {
        Assert.Equal(new Money(5, "USD").GetHashCode(), new Money(5, "USD").GetHashCode());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: FAIL — `ValueObject` does not exist (compile error).

- [ ] **Step 3: Implement `ValueObject`**

Create `src/BuildingBlocks/Domain/ValueObject.cs`:

```csharp
namespace BuildingBlocks.Domain;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;
        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode() =>
        GetEqualityComponents()
            .Aggregate(0, (current, component) => HashCode.Combine(current, component));

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(seedwork): add ValueObject base type"
```

---

## Task 3: Seedwork — `AggregateRoot` and `IDomainEvent`

**Why:** Aggregates record *domain events* (facts that happened) without knowing who consumes them. The base type holds the event list and exposes raise/clear, keeping that plumbing out of each aggregate.

**Files:**
- Test: `tests/Engagement.Domain.Tests/Seedwork/AggregateRootTests.cs`
- Create: `src/BuildingBlocks/Domain/IDomainEvent.cs`, `src/BuildingBlocks/Domain/AggregateRoot.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Engagement.Domain.Tests/Seedwork/AggregateRootTests.cs`:

```csharp
using BuildingBlocks.Domain;
using Xunit;

namespace Engagement.Domain.Tests.Seedwork;

public class AggregateRootTests
{
    private sealed record SomethingHappened(DateTimeOffset OccurredOn) : IDomainEvent;

    private sealed class TestAggregate : AggregateRoot
    {
        public void DoSomething() => RaiseDomainEvent(new SomethingHappened(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Raised_events_are_exposed()
    {
        var agg = new TestAggregate();
        agg.DoSomething();
        Assert.Single(agg.DomainEvents);
        Assert.IsType<SomethingHappened>(agg.DomainEvents.First());
    }

    [Fact]
    public void Clear_empties_the_event_list()
    {
        var agg = new TestAggregate();
        agg.DoSomething();
        agg.ClearDomainEvents();
        Assert.Empty(agg.DomainEvents);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: FAIL — `AggregateRoot` / `IDomainEvent` do not exist.

- [ ] **Step 3: Implement the seedwork types**

Create `src/BuildingBlocks/Domain/IDomainEvent.cs`:

```csharp
namespace BuildingBlocks.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}
```

Create `src/BuildingBlocks/Domain/AggregateRoot.cs`:

```csharp
namespace BuildingBlocks.Domain;

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(seedwork): add AggregateRoot and IDomainEvent"
```

---

## Task 4: Hand-rolled mediator (abstractions + implementation + DI)

**Why:** CQRS-lite means every use case is an explicit message + handler. Building the ~80-line mediator ourselves removes the "magic": it's just resolving handlers from the DI container and threading pipeline behaviors around them.

**Files:**
- Create: `src/BuildingBlocks/Mediator/IMediator.cs`, `Abstractions.cs`, `Mediator.cs`, `MediatorServiceCollectionExtensions.cs`
- Test: `tests/Engagement.Integration.Tests/Mediator/MediatorTests.cs`

> The mediator test lives in the integration test project because it needs `Microsoft.Extensions.DependencyInjection`. Add that package first.

- [ ] **Step 1: Add the DI package to the test project**

Run: `dotnet add tests/Engagement.Integration.Tests package Microsoft.Extensions.DependencyInjection`

- [ ] **Step 2: Write the failing test**

Create `tests/Engagement.Integration.Tests/Mediator/MediatorTests.cs`:

```csharp
using BuildingBlocks.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.Mediator;

public class MediatorTests
{
    public record Ping(string Text) : IRequest<string>;

    public class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken ct) =>
            Task.FromResult($"pong:{request.Text}");
    }

    public class ShoutBehavior : IPipelineBehavior<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, RequestHandlerDelegate<string> next, CancellationToken ct)
        {
            var result = await next();
            return result.ToUpperInvariant();
        }
    }

    public record Notified(string Text) : INotification;

    public class RecordingHandler : INotificationHandler<Notified>
    {
        public static readonly List<string> Received = new();
        public Task HandleAsync(Notified notification, CancellationToken ct)
        {
            Received.Add(notification.Text);
            return Task.CompletedTask;
        }
    }

    private static IMediator Build(Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddMediator(typeof(MediatorTests).Assembly);
        extra?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Send_routes_to_the_matching_handler()
    {
        var mediator = Build();
        var result = await mediator.SendAsync(new Ping("hi"));
        Assert.Equal("pong:hi", result);
    }

    [Fact]
    public async Task Pipeline_behavior_wraps_the_handler()
    {
        var mediator = Build(s => s.AddScoped<IPipelineBehavior<Ping, string>, ShoutBehavior>());
        var result = await mediator.SendAsync(new Ping("hi"));
        Assert.Equal("PONG:HI", result);
    }

    [Fact]
    public async Task Publish_invokes_all_notification_handlers()
    {
        RecordingHandler.Received.Clear();
        var mediator = Build();
        await mediator.PublishAsync(new Notified("x"));
        Assert.Contains("x", RecordingHandler.Received);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Integration.Tests`
Expected: FAIL — mediator types don't exist.

- [ ] **Step 4: Create the abstractions**

Create `src/BuildingBlocks/Mediator/Abstractions.cs`:

```csharp
namespace BuildingBlocks.Mediator;

public interface IRequest<out TResponse> { }

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);
}

public interface INotification { }

public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    Task HandleAsync(TNotification notification, CancellationToken ct);
}
```

Create `src/BuildingBlocks/Mediator/IMediator.cs`:

```csharp
namespace BuildingBlocks.Mediator;

public interface IMediator
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
    Task PublishAsync(INotification notification, CancellationToken ct = default);
}
```

- [ ] **Step 5: Create the implementation**

Create `src/BuildingBlocks/Mediator/Mediator.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Mediator;

public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        var requestType = request.GetType();

        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        dynamic handler = serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for {requestType.Name}");

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = ((IEnumerable<object>)serviceProvider.GetServices(behaviorType)).Reverse().ToList();

        RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync((dynamic)request, ct);

        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            dynamic current = behavior;
            pipeline = () => current.HandleAsync((dynamic)request, next, ct);
        }

        return pipeline();
    }

    public async Task PublishAsync(INotification notification, CancellationToken ct = default)
    {
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notification.GetType());
        var handlers = (IEnumerable<object>)serviceProvider.GetServices(handlerType);

        foreach (dynamic handler in handlers)
            await handler.HandleAsync((dynamic)notification, ct);
    }
}
```

> We use `dynamic` to invoke the correctly-typed `HandleAsync` without manual `MethodInfo.Invoke` reflection — a pragmatic shortcut that keeps the dispatcher readable. The cost is that handler resolution errors surface at runtime, not compile time; the architecture/integration tests catch missing registrations.

- [ ] **Step 6: Create the DI registration (assembly scanning)**

Create `src/BuildingBlocks/Mediator/MediatorServiceCollectionExtensions.cs`:

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Mediator;

public static class MediatorServiceCollectionExtensions
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(IRequestHandler<,>),
        typeof(INotificationHandler<>)
    ];

    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<IMediator, Mediator>();

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

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Integration.Tests`
Expected: PASS (3 mediator tests).

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "feat(mediator): hand-rolled request/notification mediator with DI + pipeline"
```

---

## Task 5: Engagement domain value objects — `LearnerId`, `Xp`, `XpAward`

**Why:** These make illegal states unrepresentable. `Xp` cannot be negative; `XpAward` cannot be ≤ 0; `LearnerId` cannot be confused with any other `Guid`. Invariants live in the type, not scattered across services.

**Files:**
- Test: `tests/Engagement.Domain.Tests/ValueObjectsTests.cs`
- Create: `src/Modules/Engagement/Engagement.Domain/LearnerId.cs`, `Xp.cs`, `XpAward.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Engagement.Domain.Tests/ValueObjectsTests.cs`:

```csharp
using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class ValueObjectsTests
{
    [Fact]
    public void LearnerId_rejects_empty_guid()
    {
        Assert.Throws<ArgumentException>(() => new LearnerId(Guid.Empty));
    }

    [Fact]
    public void Xp_starts_at_zero_and_is_never_negative()
    {
        Assert.Equal(0, Xp.Zero.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Xp(-1));
    }

    [Fact]
    public void Xp_add_increases_by_award_amount()
    {
        var result = Xp.Zero.Add(new XpAward(10, "LessonCompleted", Guid.NewGuid()));
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void XpAward_rejects_non_positive_amounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new XpAward(0, "x", Guid.NewGuid()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new XpAward(-5, "x", Guid.NewGuid()));
    }

    [Fact]
    public void XpAward_rejects_empty_source_id()
    {
        Assert.Throws<ArgumentException>(() => new XpAward(10, "x", Guid.Empty));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Implement the value objects**

Create `src/Modules/Engagement/Engagement.Domain/LearnerId.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class LearnerId : ValueObject
{
    public Guid Value { get; }

    public LearnerId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("LearnerId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
```

Create `src/Modules/Engagement/Engagement.Domain/XpAward.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class XpAward : ValueObject
{
    public int Amount { get; }
    public string Reason { get; }
    public Guid SourceId { get; }

    public XpAward(int amount, string reason, Guid sourceId)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "XP award must be positive.");
        if (sourceId == Guid.Empty)
            throw new ArgumentException("SourceId cannot be empty.", nameof(sourceId));

        Amount = amount;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        SourceId = sourceId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Reason;
        yield return SourceId;
    }
}
```

Create `src/Modules/Engagement/Engagement.Domain/Xp.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class Xp : ValueObject
{
    public int Value { get; }

    public static Xp Zero => new(0);

    public Xp(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "XP can never be negative.");
        Value = value;
    }

    public Xp Add(XpAward award)
    {
        ArgumentNullException.ThrowIfNull(award);
        return new Xp(checked(Value + award.Amount)); // checked → overflow throws rather than wraps
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(engagement-domain): LearnerId, Xp, XpAward value objects"
```

---

## Task 6: `LearnerEngagement` aggregate + `XpAwarded` + policy + repository interface

**Why:** This is the core model. `AwardXp` is the *only* mutator (no public setter on `TotalXp`), it enforces idempotency per `SourceId`, and it records an `XpAwarded` fact. The repository **interface** lives here because it's a domain concept; Infrastructure implements it later.

**Files:**
- Test: `tests/Engagement.Domain.Tests/LearnerEngagementTests.cs`
- Create: `src/Modules/Engagement/Engagement.Domain/XpAwarded.cs`, `AppliedAward.cs`, `LearnerEngagement.cs`, `LessonCompletionXpPolicy.cs`, `ILearnerEngagementRepository.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Engagement.Domain.Tests/LearnerEngagementTests.cs`:

```csharp
using Engagement.Domain;
using Xunit;

namespace Engagement.Domain.Tests;

public class LearnerEngagementTests
{
    private static LearnerEngagement NewLearner() => LearnerEngagement.Create(new LearnerId(Guid.NewGuid()));
    private static XpAward Award(int amount = 10) => new(amount, "LessonCompleted", Guid.NewGuid());

    [Fact]
    public void New_learner_starts_with_zero_xp()
    {
        Assert.Equal(0, NewLearner().TotalXp.Value);
    }

    [Fact]
    public void Awarding_xp_increases_the_total()
    {
        var learner = NewLearner();
        learner.AwardXp(Award(10));
        Assert.Equal(10, learner.TotalXp.Value);
    }

    [Fact]
    public void Awarding_xp_raises_an_XpAwarded_event()
    {
        var learner = NewLearner();
        learner.AwardXp(Award(10));

        var evt = Assert.IsType<XpAwarded>(Assert.Single(learner.DomainEvents));
        Assert.Equal(learner.Id.Value, evt.LearnerId);
        Assert.Equal(10, evt.Amount);
        Assert.Equal(10, evt.NewTotal);
    }

    [Fact]
    public void Awarding_the_same_source_twice_is_idempotent()
    {
        var learner = NewLearner();
        var award = Award(10);

        learner.AwardXp(award);
        learner.AwardXp(award); // same SourceId

        Assert.Equal(10, learner.TotalXp.Value);              // awarded once
        Assert.Single(learner.DomainEvents);                  // event raised once
    }

    [Fact]
    public void Policy_returns_flat_ten_for_a_completed_lesson()
    {
        Assert.Equal(10, new LessonCompletionXpPolicy().XpForCompletedLesson());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Implement the domain event**

Create `src/Modules/Engagement/Engagement.Domain/XpAwarded.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed record XpAwarded(
    Guid LearnerId,
    int Amount,
    int NewTotal,
    DateTimeOffset OccurredOn) : IDomainEvent;
```

- [ ] **Step 4: Implement the owned `AppliedAward` (idempotency record)**

Create `src/Modules/Engagement/Engagement.Domain/AppliedAward.cs`:

```csharp
namespace Engagement.Domain;

// Owned by LearnerEngagement; persisted so idempotency survives reloads.
public sealed class AppliedAward
{
    public Guid SourceId { get; private set; }
    public int Amount { get; private set; }
    public DateTimeOffset AppliedAt { get; private set; }

    private AppliedAward() { } // EF

    public AppliedAward(Guid sourceId, int amount, DateTimeOffset appliedAt)
    {
        SourceId = sourceId;
        Amount = amount;
        AppliedAt = appliedAt;
    }
}
```

> **Caveat (documented):** storing every applied `SourceId` on the aggregate grows unbounded over a learner's lifetime. For the skeleton it keeps the invariant in the domain where it belongs. A later refinement moves dedup to an *inbox* table in Infrastructure.

- [ ] **Step 5: Implement the aggregate**

Create `src/Modules/Engagement/Engagement.Domain/LearnerEngagement.cs`:

```csharp
using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class LearnerEngagement : AggregateRoot
{
    private readonly List<AppliedAward> _appliedAwards = new();

    public LearnerId Id { get; private set; } = default!;
    public Xp TotalXp { get; private set; } = Xp.Zero;
    public IReadOnlyCollection<AppliedAward> AppliedAwards => _appliedAwards.AsReadOnly();

    private LearnerEngagement() { } // EF

    public static LearnerEngagement Create(LearnerId id) => new()
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)),
        TotalXp = Xp.Zero
    };

    public void AwardXp(XpAward award)
    {
        ArgumentNullException.ThrowIfNull(award);

        if (_appliedAwards.Any(a => a.SourceId == award.SourceId))
            return; // idempotent: this source was already applied

        TotalXp = TotalXp.Add(award);
        _appliedAwards.Add(new AppliedAward(award.SourceId, award.Amount, DateTimeOffset.UtcNow));

        RaiseDomainEvent(new XpAwarded(Id.Value, award.Amount, TotalXp.Value, DateTimeOffset.UtcNow));
    }
}
```

- [ ] **Step 6: Implement the XP policy**

Create `src/Modules/Engagement/Engagement.Domain/LessonCompletionXpPolicy.cs`:

```csharp
namespace Engagement.Domain;

// Core rule: how much a completed lesson is worth. Engagement owns this — Learning does not.
// Flat for the skeleton; will grow (combos, boosts, weekend XP) without changing callers.
public sealed class LessonCompletionXpPolicy
{
    public const int FlatLessonXp = 10;
    public int XpForCompletedLesson() => FlatLessonXp;
}
```

- [ ] **Step 7: Implement the repository interface**

Create `src/Modules/Engagement/Engagement.Domain/ILearnerEngagementRepository.cs`:

```csharp
namespace Engagement.Domain;

public interface ILearnerEngagementRepository
{
    Task<LearnerEngagement?> GetAsync(LearnerId id, CancellationToken ct);
    Task AddAsync(LearnerEngagement learner, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Domain.Tests`
Expected: PASS (all domain tests).

- [ ] **Step 9: Commit**

```powershell
git add -A
git commit -m "feat(engagement-domain): LearnerEngagement aggregate, XpAwarded, policy, repo interface"
```

---

## Task 7: Contracts — the `LessonCompleted` integration event

**Why:** This is the published language of the central seam. It lives in a shared `Contracts` assembly so any module can subscribe without depending on Learning's internals. It is an `INotification` so the mediator can publish it in-process.

**Files:**
- Create: `src/BuildingBlocks/Contracts/LessonCompleted.cs`
- Test: `tests/Engagement.Integration.Tests/Contracts/LessonCompletedTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Engagement.Integration.Tests/Contracts/LessonCompletedTests.cs`:

```csharp
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Xunit;

namespace Engagement.Integration.Tests.Contracts;

public class LessonCompletedTests
{
    [Fact]
    public void LessonCompleted_is_a_notification_with_the_expected_shape()
    {
        var evt = new LessonCompleted(
            EventId: Guid.NewGuid(),
            LearnerId: Guid.NewGuid(),
            LessonId: Guid.NewGuid(),
            OccurredOn: DateTimeOffset.UtcNow);

        Assert.IsAssignableFrom<INotification>(evt);
        Assert.NotEqual(Guid.Empty, evt.EventId);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Integration.Tests`
Expected: FAIL — `LessonCompleted` does not exist.

- [ ] **Step 3: Implement the contract**

Create `src/BuildingBlocks/Contracts/LessonCompleted.cs`:

```csharp
using BuildingBlocks.Mediator;

namespace BuildingBlocks.Contracts;

// Published by Learning when a learner finishes a lesson. EventId doubles as the
// idempotency key (SourceId) for any downstream award.
public sealed record LessonCompleted(
    Guid EventId,
    Guid LearnerId,
    Guid LessonId,
    DateTimeOffset OccurredOn) : INotification;
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Integration.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(contracts): add LessonCompleted integration event"
```

---

## Task 8: Engagement.Application — award handler + query

**Why:** Use cases live here. `AwardXpForLessonCompletedHandler` subscribes to `LessonCompleted`, applies the XP policy, and tells the aggregate to award. `GetLearnerEngagementHandler` answers the read side. Tested against an in-memory fake repo — no database needed.

**Files:**
- Create: `src/Modules/Engagement/Engagement.Application/AwardXpForLessonCompletedHandler.cs`, `GetLearnerEngagement.cs`
- Test: `tests/Engagement.Integration.Tests/Application/EngagementApplicationTests.cs` (+ an in-memory repo fake)

- [ ] **Step 1: Write the failing test (with an in-memory repository fake)**

Create `tests/Engagement.Integration.Tests/Application/EngagementApplicationTests.cs`:

```csharp
using BuildingBlocks.Contracts;
using Engagement.Application;
using Engagement.Domain;
using Xunit;

namespace Engagement.Integration.Tests.Application;

public class EngagementApplicationTests
{
    private sealed class InMemoryRepo : ILearnerEngagementRepository
    {
        private readonly Dictionary<Guid, LearnerEngagement> _store = new();
        public Task<LearnerEngagement?> GetAsync(LearnerId id, CancellationToken ct) =>
            Task.FromResult(_store.GetValueOrDefault(id.Value));
        public Task AddAsync(LearnerEngagement learner, CancellationToken ct)
        {
            _store[learner.Id.Value] = learner;
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task Completing_a_lesson_awards_flat_xp_to_a_new_learner()
    {
        var repo = new InMemoryRepo();
        var handler = new AwardXpForLessonCompletedHandler(repo, new LessonCompletionXpPolicy());
        var learnerId = Guid.NewGuid();

        await handler.HandleAsync(
            new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        var learner = await repo.GetAsync(new LearnerId(learnerId), CancellationToken.None);
        Assert.Equal(10, learner!.TotalXp.Value);
    }

    [Fact]
    public async Task Same_event_delivered_twice_awards_once()
    {
        var repo = new InMemoryRepo();
        var handler = new AwardXpForLessonCompletedHandler(repo, new LessonCompletionXpPolicy());
        var learnerId = Guid.NewGuid();
        var evt = new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, CancellationToken.None);
        await handler.HandleAsync(evt, CancellationToken.None);

        var learner = await repo.GetAsync(new LearnerId(learnerId), CancellationToken.None);
        Assert.Equal(10, learner!.TotalXp.Value);
    }

    [Fact]
    public async Task Query_returns_zero_for_an_unknown_learner()
    {
        var repo = new InMemoryRepo();
        var handler = new GetLearnerEngagementHandler(repo);

        var result = await handler.HandleAsync(new GetLearnerEngagement(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(0, result.TotalXp);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Integration.Tests`
Expected: FAIL — application types don't exist.

- [ ] **Step 3: Implement the award handler**

Create `src/Modules/Engagement/Engagement.Application/AwardXpForLessonCompletedHandler.cs`:

```csharp
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed class AwardXpForLessonCompletedHandler(
    ILearnerEngagementRepository repository,
    LessonCompletionXpPolicy policy) : INotificationHandler<LessonCompleted>
{
    public async Task HandleAsync(LessonCompleted notification, CancellationToken ct)
    {
        var learnerId = new LearnerId(notification.LearnerId);

        var learner = await repository.GetAsync(learnerId, ct);
        if (learner is null)
        {
            learner = LearnerEngagement.Create(learnerId);
            await repository.AddAsync(learner, ct);
        }

        // SourceId = the event id → re-delivery of the same event is idempotent.
        var award = new XpAward(policy.XpForCompletedLesson(), nameof(LessonCompleted), notification.EventId);
        learner.AwardXp(award);

        await repository.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Implement the query + handler + DTO**

Create `src/Modules/Engagement/Engagement.Application/GetLearnerEngagement.cs`:

```csharp
using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record GetLearnerEngagement(Guid LearnerId) : IRequest<LearnerEngagementDto>;

public sealed record LearnerEngagementDto(Guid LearnerId, int TotalXp);

public sealed class GetLearnerEngagementHandler(ILearnerEngagementRepository repository)
    : IRequestHandler<GetLearnerEngagement, LearnerEngagementDto>
{
    public async Task<LearnerEngagementDto> HandleAsync(GetLearnerEngagement request, CancellationToken ct)
    {
        var learner = await repository.GetAsync(new LearnerId(request.LearnerId), ct);
        return new LearnerEngagementDto(request.LearnerId, learner?.TotalXp.Value ?? 0);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Integration.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(engagement-application): award-on-lesson-completed handler + engagement query"
```

---

## Task 9: Engagement.Infrastructure — `EngagementDbContext`, EF config, repository, migration

**Why:** This layer makes the domain persistent without the domain knowing. Value objects map via converters; the owned `AppliedAwards` collection maps to its own table; domain events are cleared on save. We prove it with an integration test against LocalDB.

**Files:**
- Create: `src/Modules/Engagement/Engagement.Infrastructure/EngagementDbContext.cs`, `LearnerEngagementConfiguration.cs`, `LearnerEngagementRepository.cs`, `EngagementInfrastructureExtensions.cs`
- Test: `tests/Engagement.Integration.Tests/Infrastructure/EngagementPersistenceTests.cs`
- Migration: generated under `Engagement.Infrastructure/Migrations/`

- [ ] **Step 1: Write the failing test (round-trips the aggregate through LocalDB)**

Create `tests/Engagement.Integration.Tests/Infrastructure/EngagementPersistenceTests.cs`:

```csharp
using Engagement.Domain;
using Engagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Engagement.Integration.Tests.Infrastructure;

public class EngagementPersistenceTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static EngagementDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new EngagementDbContext(options);
    }

    public EngagementPersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate(); // applies the engagement schema migration to a clean DB
    }

    [Fact]
    public async Task Aggregate_round_trips_with_total_and_applied_awards()
    {
        var learnerId = new LearnerId(Guid.NewGuid());

        await using (var ctx = NewContext())
        {
            var repo = new LearnerEngagementRepository(ctx);
            var learner = LearnerEngagement.Create(learnerId);
            learner.AwardXp(new XpAward(10, "LessonCompleted", Guid.NewGuid()));
            await repo.AddAsync(learner, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var repo = new LearnerEngagementRepository(ctx);
            var reloaded = await repo.GetAsync(learnerId, CancellationToken.None);

            Assert.NotNull(reloaded);
            Assert.Equal(10, reloaded!.TotalXp.Value);
            Assert.Single(reloaded.AppliedAwards);
        }
    }

    [Fact]
    public async Task Domain_events_are_not_persisted_and_are_cleared_on_save()
    {
        var learnerId = new LearnerId(Guid.NewGuid());
        await using var ctx = NewContext();
        var repo = new LearnerEngagementRepository(ctx);

        var learner = LearnerEngagement.Create(learnerId);
        learner.AwardXp(new XpAward(10, "LessonCompleted", Guid.NewGuid()));
        await repo.AddAsync(learner, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        Assert.Empty(learner.DomainEvents);
    }
}
```

- [ ] **Step 2: Implement the DbContext + clear-events-on-save**

Create `src/Modules/Engagement/Engagement.Infrastructure/EngagementDbContext.cs`:

```csharp
using BuildingBlocks.Domain;
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class EngagementDbContext(DbContextOptions<EngagementDbContext> options) : DbContext(options)
{
    public const string Schema = "engagement";

    public DbSet<LearnerEngagement> Learners => Set<LearnerEngagement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new LearnerEngagementConfiguration());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var result = await base.SaveChangesAsync(ct);

        // Slice 1: no subscribers to XpAwarded yet (YAGNI). Clear raised events so a
        // long-lived context doesn't accumulate them. A real dispatcher arrives when a
        // subscriber (Notifications/Achievements) exists.
        foreach (var aggregate in ChangeTracker.Entries<AggregateRoot>().Select(e => e.Entity))
            aggregate.ClearDomainEvents();

        return result;
    }
}
```

- [ ] **Step 3: Implement the EF mapping (value-object converters + owned collection)**

Create `src/Modules/Engagement/Engagement.Infrastructure/LearnerEngagementConfiguration.cs`:

```csharp
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Engagement.Infrastructure;

internal sealed class LearnerEngagementConfiguration : IEntityTypeConfiguration<LearnerEngagement>
{
    public void Configure(EntityTypeBuilder<LearnerEngagement> builder)
    {
        builder.ToTable("Learners");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new LearnerId(value))
            .HasColumnName("LearnerId")
            .ValueGeneratedNever();

        builder.Property(l => l.TotalXp)
            .HasConversion(xp => xp.Value, value => new Xp(value))
            .HasColumnName("TotalXp");

        // Domain events are not persisted.
        builder.Ignore(l => l.DomainEvents);

        // Idempotency ledger as an owned collection → engagement.AppliedAwards.
        builder.OwnsMany(l => l.AppliedAwards, owned =>
        {
            owned.ToTable("AppliedAwards");
            owned.WithOwner().HasForeignKey("LearnerId");
            owned.HasKey("LearnerId", nameof(AppliedAward.SourceId));
            owned.Property(a => a.SourceId);
            owned.Property(a => a.Amount);
            owned.Property(a => a.AppliedAt);
        });

        builder.Navigation(l => l.AppliedAwards)
            .HasField("_appliedAwards")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

- [ ] **Step 4: Implement the repository**

Create `src/Modules/Engagement/Engagement.Infrastructure/LearnerEngagementRepository.cs`:

```csharp
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Engagement.Infrastructure;

public sealed class LearnerEngagementRepository(EngagementDbContext context) : ILearnerEngagementRepository
{
    public Task<LearnerEngagement?> GetAsync(LearnerId id, CancellationToken ct) =>
        context.Learners.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task AddAsync(LearnerEngagement learner, CancellationToken ct) =>
        await context.Learners.AddAsync(learner, ct);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
```

> EF translates `l => l.Id == id` using the `LearnerId` value converter, so the query compares on the underlying `Guid`.

- [ ] **Step 5: Implement the DI registration helper**

Create `src/Modules/Engagement/Engagement.Infrastructure/EngagementInfrastructureExtensions.cs`:

```csharp
using Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Engagement.Infrastructure;

public static class EngagementInfrastructureExtensions
{
    public static IServiceCollection AddEngagementInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<EngagementDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ILearnerEngagementRepository, LearnerEngagementRepository>();
        services.AddScoped<LessonCompletionXpPolicy>();
        return services;
    }
}
```

- [ ] **Step 6: Implement the design-time factory (so `dotnet ef` needs no Host)**

Create `src/Modules/Engagement/Engagement.Infrastructure/EngagementDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Engagement.Infrastructure;

// Used ONLY by the EF Core CLI at design time (migrations). The runtime app wires the
// DbContext through AddEngagementInfrastructure with the real connection string.
public sealed class EngagementDbContextFactory : IDesignTimeDbContextFactory<EngagementDbContext>
{
    public EngagementDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_Design;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new EngagementDbContext(options);
    }
}
```

- [ ] **Step 7: Create the EF migration**

Run (from repo root) — the Infrastructure project is its own startup thanks to the factory, so **this task no longer depends on the Host**:

```powershell
dotnet ef migrations add InitialEngagement `
  -p src/Modules/Engagement/Engagement.Infrastructure `
  -s src/Modules/Engagement/Engagement.Infrastructure `
  -o Migrations
```

Expected: a `Migrations/` folder with `*_InitialEngagement.cs` creating the `engagement` schema, `Learners`, and `AppliedAwards` tables.

- [ ] **Step 8: Run the persistence test**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~EngagementPersistenceTests"`
Expected: PASS (2 tests). LocalDB creates `DuolingoEngagement_Test`, migrates, round-trips.

- [ ] **Step 9: Commit**

```powershell
git add -A
git commit -m "feat(engagement-infrastructure): EF Core DbContext, VO mapping, repo, initial migration"
```

---

## Task 10: Learning.Stub — `CompleteLesson` command raises `LessonCompleted`

**Why:** The walking skeleton needs *something* to announce a lesson finished, without building the real Learning engine. The stub is the smallest honest producer of the central event.

**Files:**
- Create: `src/Modules/Learning/Learning.Stub/CompleteLesson.cs`, `LearningStubExtensions.cs`
- Test: `tests/Engagement.Integration.Tests/Learning/CompleteLessonHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Engagement.Integration.Tests/Learning/CompleteLessonHandlerTests.cs`:

```csharp
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Learning.Stub;
using Xunit;

namespace Engagement.Integration.Tests.Learning;

public class CompleteLessonHandlerTests
{
    private sealed class CapturingPublisher : IMediator
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

    [Fact]
    public async Task Completing_a_lesson_publishes_LessonCompleted()
    {
        var publisher = new CapturingPublisher();
        var handler = new CompleteLessonHandler(publisher);
        var learnerId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();

        await handler.HandleAsync(new CompleteLesson(learnerId, lessonId), CancellationToken.None);

        var evt = Assert.IsType<LessonCompleted>(Assert.Single(publisher.Published));
        Assert.Equal(learnerId, evt.LearnerId);
        Assert.Equal(lessonId, evt.LessonId);
        Assert.NotEqual(Guid.Empty, evt.EventId);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~CompleteLessonHandlerTests"`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Implement the command + handler**

Create `src/Modules/Learning/Learning.Stub/CompleteLesson.cs`:

```csharp
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;

namespace Learning.Stub;

public sealed record CompleteLesson(Guid LearnerId, Guid LessonId) : IRequest<Unit>;

// A tiny stand-in for "no meaningful return value" so the command fits IRequest<T>.
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}

public sealed class CompleteLessonHandler(IMediator mediator) : IRequestHandler<CompleteLesson, Unit>
{
    public async Task<Unit> HandleAsync(CompleteLesson request, CancellationToken ct)
    {
        await mediator.PublishAsync(
            new LessonCompleted(
                EventId: Guid.NewGuid(),
                LearnerId: request.LearnerId,
                LessonId: request.LessonId,
                OccurredOn: DateTimeOffset.UtcNow),
            ct);

        return Unit.Value;
    }
}
```

- [ ] **Step 4: Implement the DI registration helper**

Create `src/Modules/Learning/Learning.Stub/LearningStubExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Learning.Stub;

public static class LearningStubExtensions
{
    // Handlers are picked up by AddMediator(assembly); this marker keeps the
    // assembly easy to reference from the Host's mediator registration.
    public static readonly System.Reflection.Assembly Assembly = typeof(CompleteLesson).Assembly;
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~CompleteLessonHandlerTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(learning-stub): CompleteLesson command publishes LessonCompleted"
```

---

## Task 11: Host — composition root, fake current user, endpoints

**Why:** The host is the only place that knows about every module — it wires them together (composition root) and exposes HTTP. We fake the current user (Identity is a later sub-project) and expose the two skeleton endpoints.

**Files:**
- Create: `src/Host/CurrentUser.cs`, `src/Host/LoggingBehavior.cs`
- Modify: `src/Host/Program.cs` (replace template), `src/Host/appsettings.json`

- [ ] **Step 1: Set the connection string**

Replace `src/Host/appsettings.json` with:

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
  }
}
```

- [ ] **Step 2: Implement the fake current user**

Create `src/Host/CurrentUser.cs`:

```csharp
namespace Host;

public interface ICurrentUser
{
    Guid LearnerId { get; }
}

// Slice 1: no real auth. The learner comes from an "X-Learner-Id" header, or a fixed
// demo learner if absent. Replaced by the real Identity module later.
public sealed class HeaderCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public static readonly Guid DemoLearnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Guid LearnerId
    {
        get
        {
            var header = accessor.HttpContext?.Request.Headers["X-Learner-Id"].ToString();
            return Guid.TryParse(header, out var id) ? id : DemoLearnerId;
        }
    }
}
```

- [ ] **Step 3: Implement a logging pipeline behavior (demonstrates the pipeline)**

Create `src/Host/LoggingBehavior.cs`:

```csharp
using BuildingBlocks.Mediator;
using Microsoft.Extensions.Logging;

namespace Host;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next();
        logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}
```

- [ ] **Step 4: Replace `Program.cs` with the composition root + endpoints**

Replace `src/Host/Program.cs` with:

```csharp
using BuildingBlocks.Mediator;
using Engagement.Application;
using Engagement.Infrastructure;
using Host;
using Learning.Stub;

var builder = WebApplication.CreateBuilder(args);

// --- Composition root ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();

builder.Services.AddMediator(
    typeof(GetLearnerEngagement).Assembly,   // Engagement.Application handlers
    LearningStubExtensions.Assembly);         // Learning.Stub handlers

builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

builder.Services.AddEngagementInfrastructure(
    builder.Configuration.GetConnectionString("Engagement")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Engagement"));

var app = builder.Build();

// --- Endpoints ---
app.MapPost("/lessons/{lessonId:guid}/complete",
    async (Guid lessonId, ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        await mediator.SendAsync(new CompleteLesson(user.LearnerId, lessonId), ct);
        return Results.Accepted();
    });

app.MapGet("/me/engagement",
    async (ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        var dto = await mediator.SendAsync(new GetLearnerEngagement(user.LearnerId), ct);
        return Results.Ok(dto);
    });

app.Run();

// Exposes the implicit Program class to WebApplicationFactory in tests.
public partial class Program { }
```

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: Build succeeded. (The Task 9 migration already exists; the Host just consumes the wired-up infrastructure.)

- [ ] **Step 6: Manual smoke test (optional but recommended)**

Run: `dotnet run --project src/Host`
Then in another terminal:

```powershell
# apply the migration to the dev DB once
dotnet ef database update -p src/Modules/Engagement/Engagement.Infrastructure -s src/Host

curl -X POST http://localhost:5000/lessons/22222222-2222-2222-2222-222222222222/complete
curl http://localhost:5000/me/engagement
```

Expected: the GET returns `{"learnerId":"11111111-...","totalXp":10}`. Stop the app (Ctrl+C). (Port may differ — check the console output.)

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(host): composition root, fake current user, lesson-complete + engagement endpoints"
```

---

## Task 12: End-to-end integration test (the acceptance criteria)

**Why:** This proves the *whole slice* works through real HTTP and a real database — the walking skeleton is alive. It nails acceptance criteria 1, 2, and 3.

**Files:**
- Create: `tests/Engagement.Integration.Tests/EndToEnd/EngagementApiTests.cs`, `tests/Engagement.Integration.Tests/EndToEnd/EngagementApiFactory.cs`

- [ ] **Step 1: Create the test factory (points the app at a test DB, migrates it clean)**

Create `tests/Engagement.Integration.Tests/EndToEnd/EngagementApiFactory.cs`:

```csharp
using Engagement.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Engagement.Integration.Tests.EndToEnd;

public sealed class EngagementApiFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoEngagement_E2E;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Engagement", TestConnectionString);

        builder.ConfigureServices(services =>
        {
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EngagementDbContext>();
            db.Database.EnsureDeleted();
            db.Database.Migrate();
        });
    }
}
```

- [ ] **Step 2: Write the end-to-end test**

Create `tests/Engagement.Integration.Tests/EndToEnd/EngagementApiTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Engagement.Integration.Tests.EndToEnd;

public class EngagementApiTests(EngagementApiFactory factory) : IClassFixture<EngagementApiFactory>
{
    private sealed record EngagementResponse(Guid LearnerId, int TotalXp);

    private HttpClient ClientForLearner(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    [Fact] // Criterion 1 & 2
    public async Task Completing_a_lesson_then_reading_engagement_shows_ten_xp()
    {
        var learnerId = Guid.NewGuid();
        var client = ClientForLearner(learnerId);

        var post = await client.PostAsync($"/lessons/{Guid.NewGuid()}/complete", null);
        Assert.Equal(HttpStatusCode.Accepted, post.StatusCode);

        var dto = await client.GetFromJsonAsync<EngagementResponse>("/me/engagement");
        Assert.NotNull(dto);
        Assert.Equal(10, dto!.TotalXp);
    }

    [Fact] // Criterion 3: idempotency on re-delivery of the SAME event
    public async Task Same_lesson_completed_event_delivered_twice_awards_once()
    {
        var learnerId = Guid.NewGuid();
        var evt = new LessonCompleted(Guid.NewGuid(), learnerId, Guid.NewGuid(), DateTimeOffset.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.PublishAsync(evt);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.PublishAsync(evt); // same EventId → same SourceId
        }

        var dto = await ClientForLearner(learnerId).GetFromJsonAsync<EngagementResponse>("/me/engagement");
        Assert.Equal(10, dto!.TotalXp);
    }
}
```

- [ ] **Step 3: Run the full integration suite**

Run: `dotnet test tests/Engagement.Integration.Tests`
Expected: PASS (mediator, contracts, application, persistence, learning-stub, and the two end-to-end tests).

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "test(e2e): end-to-end XP slice through HTTP + LocalDB (criteria 1-3)"
```

---

## Task 13: Architecture tests — enforce the boundaries (criteria 5 & 6)

**Why:** The compiler enforces the *layer* dependency rule via project references. Architecture *tests* enforce the rules the compiler can't express — e.g., the Host must not reach into Engagement's domain types directly, and the domain must not reference EF Core. These make criteria 5 and 6 executable, not aspirational.

**Files:**
- Create: `tests/Engagement.Integration.Tests/Architecture/ArchitectureTests.cs`

- [ ] **Step 1: Write the architecture tests**

Create `tests/Engagement.Integration.Tests/Architecture/ArchitectureTests.cs`:

```csharp
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Engagement.Integration.Tests.Architecture;

public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(global::Engagement.Domain.LearnerEngagement).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(global::Engagement.Application.GetLearnerEngagement).Assembly;
    private static readonly Assembly HostAssembly = typeof(Program).Assembly;

    [Fact] // Criterion 5: domain depends on nothing infrastructural
    public void Domain_does_not_depend_on_EfCore_or_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact] // Application stays off infrastructure too
    public void Application_does_not_depend_on_EfCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact] // Criterion 6: nothing outside the Engagement module uses its DOMAIN types directly
    public void Host_does_not_depend_on_Engagement_Domain()
    {
        var result = Types.InAssembly(HostAssembly)
            .ShouldNot()
            .HaveDependencyOn("Engagement.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? "ok"
            : "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
```

> If `Host_does_not_depend_on_Engagement_Domain` fails, it means an endpoint or wiring touched a domain type directly. The fix is to go through Application DTOs/commands — which is exactly the boundary we want. (`Program.cs` as written only references `Engagement.Application` types, so this should pass.)

- [ ] **Step 2: Run the architecture tests**

Run: `dotnet test tests/Engagement.Integration.Tests --filter "FullyQualifiedName~ArchitectureTests"`
Expected: PASS (3 tests).

- [ ] **Step 3: Run the entire test suite**

Run: `dotnet test`
Expected: ALL tests pass across both test projects.

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "test(arch): enforce dependency-rule and module boundaries (criteria 5-6)"
```

---

## Done — definition of done checklist

- [ ] `dotnet build` succeeds with 0 warnings/errors.
- [ ] `dotnet test` passes (domain + integration projects).
- [ ] Criterion 1: POST complete → `TotalXp == 10`.
- [ ] Criterion 2: GET `/me/engagement` returns the total.
- [ ] Criterion 3: same `LessonCompleted` (same `EventId`) twice → awarded once.
- [ ] Criterion 4: `XpAward` amount ≤ 0 rejected (domain unit test).
- [ ] Criterion 5: `Engagement.Domain` references no EF Core/ASP.NET (arch test).
- [ ] Criterion 6: no cross-module references except via `Contracts` (arch test).

**Next sub-projects (future plans):** grow Engagement (streaks → leagues), then replace the Learning stub with the real Learning engine, then the real Identity module.
