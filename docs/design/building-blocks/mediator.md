# Building Block: the hand-rolled Mediator

**Where:** `src/BuildingBlocks/Mediator/`
**Why it exists:** CQRS-lite. Every use case is an explicit *message* + *handler*, decoupling the caller (an API endpoint) from the code that does the work. We hand-rolled it (~80 lines) so there is no "magic" — it's just resolving handlers from the DI container and threading pipeline behaviors around them.

There are **two** flows: **Send** (one request → one handler → one response) and **Publish** (one notification → many handlers → no response).

---

## The pieces

```mermaid
classDiagram
    class IRequest~TResponse~ {
        <<marker>>
    }
    class IRequestHandler~TRequest,TResponse~ {
        +HandleAsync(request, ct) Task~TResponse~
    }
    class IPipelineBehavior~TRequest,TResponse~ {
        +HandleAsync(request, next, ct) Task~TResponse~
    }
    class INotification {
        <<marker>>
    }
    class INotificationHandler~TNotification~ {
        +HandleAsync(notification, ct) Task
    }
    class IMediator {
        +SendAsync(request) Task~TResponse~
        +PublishAsync(notification) Task
    }
    class Mediator {
        -IServiceProvider serviceProvider
    }
    IMediator <|.. Mediator
    IRequestHandler ..> IRequest : handles
    INotificationHandler ..> INotification : handles
```

- `IRequest<TResponse>` / `INotification` are **marker interfaces** — they carry no methods, they just tag a message so the compiler and the dispatcher know its kind and (for requests) its response type.
- Handlers are resolved from DI; the `Mediator` never news them up.

---

## Flow 1 — `SendAsync` (request → handler, wrapped by behaviors)

This is the important one. A request flows *down* through each pipeline behavior to the handler, and the result flows *back up* — like an onion. With one behavior (`LoggingBehavior`):

```mermaid
sequenceDiagram
    autonumber
    participant C as Caller (e.g. endpoint)
    participant M as Mediator
    participant DI as ServiceProvider
    participant B as LoggingBehavior
    participant H as RequestHandler

    C->>M: SendAsync(GetLearnerEngagement)
    M->>DI: GetService(IRequestHandler<Query, Dto>)
    DI-->>M: handler instance
    M->>DI: GetServices(IPipelineBehavior<Query, Dto>)
    DI-->>M: [ LoggingBehavior ]
    Note over M: build pipeline = Logging( () => handler.Handle )
    M->>B: HandleAsync(request, next)
    B->>B: log "Handling Query"
    B->>H: next()  ⇒ HandleAsync(request)
    H-->>B: Dto
    B->>B: log "Handled Query"
    B-->>M: Dto
    M-->>C: Dto
```

### Why behaviors form an "onion"

In `Mediator.SendAsync`, we start with a delegate that calls the handler, then **wrap** it with each behavior (iterating the list reversed, so the first registered behavior ends up outermost):

```mermaid
flowchart LR
    subgraph Outermost["Behavior #1 (e.g. Logging)"]
        subgraph Inner["Behavior #2 (e.g. Validation)"]
            HH["Handler.HandleAsync"]
        end
    end
    Req["request"] --> Outermost
    Outermost -. "response bubbles back" .-> Req
```

Each behavior receives a `next` delegate. It can run code **before** calling `next()` (e.g. start a stopwatch, validate, open a transaction), call `next()` to descend, then run code **after** (e.g. log, commit). This is the decorator pattern, built by folding the list:

```text
pipeline      = () => handler.HandleAsync(request)        // innermost
pipeline      = () => behaviorN.HandleAsync(request, pipeline)
...
pipeline      = () => behavior1.HandleAsync(request, pipeline)   // outermost
return pipeline()                                          // kick it off
```

---

## Flow 2 — `PublishAsync` (notification → all handlers)

A notification has **no response** and **zero-to-many** handlers. The mediator resolves every `INotificationHandler<T>` and invokes each. This is the central seam: `Learning` publishes `LessonCompleted`; it does not know that `Engagement` (and later Notifications, Achievements...) are listening.

```mermaid
sequenceDiagram
    autonumber
    participant L as Learning.Stub
    participant M as Mediator
    participant DI as ServiceProvider
    participant E as Engagement handler
    participant X as (future) Achievements handler

    L->>M: PublishAsync(LessonCompleted)
    M->>DI: GetServices(INotificationHandler<LessonCompleted>)
    DI-->>M: [ Engagement, (future) Achievements ]
    loop each handler
        M->>E: HandleAsync(LessonCompleted)
        E-->>M: done
        M->>X: HandleAsync(LessonCompleted)
        X-->>M: done
    end
    M-->>L: done
```

> In slice 1 there is exactly one subscriber (Engagement). The point of choreography is that adding a second subscriber later requires **no change** to the publisher or the mediator — you just register another handler.

---

## How handlers get registered (`AddMediator`)

`AddMediator(params Assembly[])` scans the given assemblies, finds every concrete type implementing `IRequestHandler<,>` or `INotificationHandler<>`, and registers it in DI against that interface. Pipeline behaviors are registered separately (they're open generics) in the composition root.

```mermaid
flowchart TD
    A["AddMediator(EngagementApp, LearningStub)"] --> B["register IMediator → Mediator"]
    A --> C["for each assembly:<br/>scan concrete types"]
    C --> D{"implements<br/>IRequestHandler&lt;,&gt; or<br/>INotificationHandler&lt;&gt;?"}
    D -- yes --> E["services.AddScoped(interface, type)"]
    D -- no --> F["skip"]
    G["composition root (Host)"] -. "AddScoped(IPipelineBehavior&lt;,&gt;, LoggingBehavior&lt;,&gt;)" .-> B
```

At runtime, `Mediator` asks the `IServiceProvider` for the handler matching the message's runtime type (via `MakeGenericType`), then invokes it with `dynamic` so the correctly-typed `HandleAsync` overload is bound without manual reflection.

---

## Trade-offs we accepted

| Choice | Benefit | Cost |
|---|---|---|
| `dynamic` dispatch | Readable ~80-line dispatcher, no `MethodInfo.Invoke` | Missing-handler errors surface at **runtime**, not compile time |
| Marker interfaces | Compiler knows message kind + response type | A little ceremony per message |
| Assembly scanning | Handlers auto-register; no manual wiring | Reflection cost at startup (negligible) |

The missing-handler risk is mitigated by the integration and architecture tests, which exercise every real handler path.

---

*Related code:* `Mediator.cs`, `Abstractions.cs`, `IMediator.cs`, `MediatorServiceCollectionExtensions.cs`.
*Built in:* Task 4 of `docs/superpowers/plans/2026-05-28-engagement-xp-skeleton.md`.
