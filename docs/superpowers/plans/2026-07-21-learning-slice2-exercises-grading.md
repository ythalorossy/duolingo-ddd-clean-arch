# Learning Slice 2 — Exercises + Grading (earned completion) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Learning's *asserted* completion with *earned* completion — a learner submits answers to a lesson's multiple-choice exercises, the server grades them, records a persisted `Attempt`, and publishes the unchanged `LessonCompleted` only on a pass.

**Architecture:** `Exercise` becomes a child entity inside the `Lesson` aggregate (owned collection → 1 aggregate / 2 tables); `Lesson.Grade(answers)` produces a `GradingResult` (a `Score` compared against a global `PassThreshold`); a new persisted `Attempt` aggregate root is Learning's first write path. `POST /lessons/{id}/attempts` replaces `POST /complete`; a new `GET /lessons/{id}` read presents exercises without the answer key.

**Tech Stack:** C# / .NET 10, ASP.NET Core Minimal APIs, EF Core 10 on SQL Server LocalDB, xUnit, NetArchTest, hand-rolled mediator (`BuildingBlocks.Mediator`).

## Global Constraints

- **Framework-free Domain.** `Learning.Domain` must reference nothing infrastructural (no EF Core, no ASP.NET). Enforced by `ArchitectureTests`.
- **No cross-module type references.** `Learning.*` must not reference `Engagement.*`. Learner identity inside Learning is a Learning-owned `LearnerId` value object; the `LessonCompleted` contract crosses the boundary with a raw `Guid`.
- **Value objects inherit `BuildingBlocks.Domain.ValueObject`** and implement `GetEqualityComponents()`. Aggregates inherit `BuildingBlocks.Domain.AggregateRoot`, have private setters, and are built via a static `Create(...)` factory (EF uses a private parameterless ctor).
- **Typed ids reject `Guid.Empty`** (mirror `CourseId`). One domain type per file (repo convention), except tightly-coupled records/enums grouped with their primary type.
- **"Now" comes from the injected `TimeProvider`** in handlers — never `DateTimeOffset.UtcNow`.
- **EF value-object querying:** compare/order by the whole value object (`x.Id == id`); never reach into a converted member in a query.
- **Owned collections use a backing field** (`_exercises`, `_answers`) + `Navigation(...).HasField(...).UsePropertyAccessMode(PropertyAccessMode.Field)`, mirroring `XpAccountConfiguration`'s `AppliedAwards`.
- **`LessonCompleted` is unchanged / XP-free:** `record LessonCompleted(Guid EventId, Guid LearnerId, Guid LessonId, DateTimeOffset OccurredOn)`. Learning never learns XP amounts.
- **Global pass threshold** `PassThreshold = 0.8` (80%), a domain constant on `Lesson`.
- **Commits** use Conventional Commits and end with the repo's `Co-Authored-By` trailer. Work happens on branch `feat/learning-exercises-grading` (already created; the spec is its first commit).
- **Migrations** (run from repo root):
  ```powershell
  dotnet ef migrations add <Name> `
    -p src/Modules/Learning/Learning.Infrastructure `
    -s src/Modules/Learning/Learning.Infrastructure -o Migrations
  ```
- **Test DB naming:** one unique LocalDB name per persistence/e2e test class (xUnit runs classes in parallel); each self-manages via `EnsureDeleted()` + `Migrate()`.

## File Structure

**Domain (`src/Modules/Learning/Learning.Domain/`)**
- Create `ExerciseId.cs`, `AttemptId.cs`, `LearnerId.cs` — typed-id VOs (mirror `CourseId`).
- Create `Prompt.cs` — text VO (mirror `Title`, longer bound).
- Create `Choices.cs` — ordered non-empty option list VO.
- Create `Score.cs` — `(Correct, Total)` VO owning `MeetsThreshold(double)`.
- Create `Grading.cs` — `Outcome` enum, `SubmittedAnswer`, `GradedAnswer`, `GradingResult` (grading vocabulary that changes together).
- Create `Exercise.cs` — child entity of `Lesson` (`IsCorrect(int)`).
- Create `Attempt.cs` — aggregate root + owned `Answer` (they change together).
- Create `IAttemptRepository.cs` — write port.
- Modify `Lesson.cs` — owned `_exercises` collection, `PassThreshold` const, `Grade(...)`.

**Application (`src/Modules/Learning/Learning.Application/`)**
- Create `SubmitAttempt.cs` — command + handler + `AttemptResultDto` + input records.
- Create `GetLesson.cs` — query + handler + `LessonPresentationDto` + `ILessonPresentationRead` port.
- Delete `CompleteLesson.cs` (Task 11).

**Infrastructure (`src/Modules/Learning/Learning.Infrastructure/`)**
- Modify `LessonConfiguration.cs` — `OwnsMany` exercises + exercise seed.
- Modify `LearningSeedIds.cs` — exercise ids + correct-answer indices.
- Modify `LearningDbContext.cs` — `DbSet<Attempt>`, apply `AttemptConfiguration`.
- Create `AttemptConfiguration.cs`.
- Create `AttemptRepository.cs` — `IAttemptRepository` impl (Add + SaveChanges).
- Create `LessonPresentationRead.cs` — `ILessonPresentationRead` impl (omits the key).
- Modify `LearningInfrastructureExtensions.cs` — register the two new services.
- Create migrations `AddExercises`, `AddAttempts`.

**Host (`src/Host/Program.cs`)**
- Add `POST /lessons/{id}/attempts` and `GET /lessons/{id}`; remove `POST /complete` (Task 11); update mediator assembly marker to `typeof(SubmitAttempt)` (Task 11).

**Tests**
- `tests/Learning.Domain.Tests/` — extend `ValueObjectsTests.cs`, `AggregatesTests.cs`.
- `tests/Learning.Integration.Tests/` — new `Infrastructure/AttemptRepositoryTests.cs`, `Infrastructure/LessonPresentationReadTests.cs`; extend `Infrastructure/LearningSeedTests.cs`; rewrite `Application/SubmitAttemptHandlerTests.cs` (was `CompleteLessonHandlerTests.cs`); rewrite `EndToEnd/LearningApiTests.cs`; update `Architecture/ArchitectureTests.cs`.

> **Note (refines spec):** the spec mentions one migration `AddExercisesAndAttempts`; this plan delivers the same schema as two smaller migrations (`AddExercises`, `AddAttempts`) so each persistence task stays independently red→green. Harmless — the repo already has many migrations.

---

### Task 1: New simple value objects (ids, Prompt, Choices)

**Files:**
- Create: `src/Modules/Learning/Learning.Domain/ExerciseId.cs`, `AttemptId.cs`, `LearnerId.cs`, `Prompt.cs`, `Choices.cs`
- Test: `tests/Learning.Domain.Tests/ValueObjectsTests.cs` (extend)

**Interfaces:**
- Produces: `ExerciseId(Guid)`, `AttemptId(Guid)`, `LearnerId(Guid)` (all `.Value`, reject `Guid.Empty`); `Prompt(string)` (`.Value`, trims, non-empty, `MaxLength = 500`); `Choices(IReadOnlyList<string>)` (`.Values`, ≥2 non-empty trimmed options, indexer `this[int]`, `.Count`).

- [ ] **Step 1: Write the failing tests** — append to `ValueObjectsTests.cs`:

```csharp
[Fact]
public void Typed_ids_reject_empty_guid()
{
    Assert.Throws<ArgumentException>(() => new ExerciseId(Guid.Empty));
    Assert.Throws<ArgumentException>(() => new AttemptId(Guid.Empty));
    Assert.Throws<ArgumentException>(() => new LearnerId(Guid.Empty));
}

[Fact]
public void Prompt_trims_and_rejects_empty()
{
    Assert.Equal("Pick the greeting", new Prompt("  Pick the greeting  ").Value);
    Assert.Throws<ArgumentException>(() => new Prompt("   "));
    Assert.Throws<ArgumentException>(() => new Prompt(new string('x', Prompt.MaxLength + 1)));
}

[Fact]
public void Choices_requires_at_least_two_non_empty_options_and_indexes()
{
    var choices = new Choices(new[] { "Hola", "Adios" });
    Assert.Equal(2, choices.Count);
    Assert.Equal("Hola", choices[0]);
    Assert.Throws<ArgumentException>(() => new Choices(new[] { "only one" }));
    Assert.Throws<ArgumentException>(() => new Choices(new[] { "ok", "  " }));
}

[Fact]
public void Choices_has_value_equality()
{
    Assert.Equal(new Choices(new[] { "a", "b" }), new Choices(new[] { "a", "b" }));
    Assert.NotEqual(new Choices(new[] { "a", "b" }), new Choices(new[] { "b", "a" }));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~ValueObjectsTests"`
Expected: FAIL — `ExerciseId`/`Prompt`/`Choices` do not exist (compile error).

- [ ] **Step 3: Create the id VOs** — `ExerciseId.cs` (repeat for `AttemptId`, `LearnerId`, changing the class name and message):

```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class ExerciseId : ValueObject
{
    public Guid Value { get; }

    public ExerciseId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ExerciseId cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value.ToString();
}
```

`AttemptId.cs` and `LearnerId.cs` are identical with the class name and error message changed to `AttemptId` / `LearnerId`.

- [ ] **Step 4: Create `Prompt.cs`**

```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Prompt : ValueObject
{
    public const int MaxLength = 500;

    public string Value { get; }

    public Prompt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Prompt cannot be empty.", nameof(value));

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Prompt cannot exceed {MaxLength} characters.", nameof(value));

        Value = trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value;
}
```

- [ ] **Step 5: Create `Choices.cs`**

```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Choices : ValueObject
{
    public IReadOnlyList<string> Values { get; }

    public Choices(IReadOnlyList<string> values)
    {
        if (values is null || values.Count < 2)
            throw new ArgumentException("A multiple-choice exercise needs at least two options.", nameof(values));
        if (values.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Choice options cannot be empty.", nameof(values));

        Values = values.Select(v => v.Trim()).ToArray();
    }

    public int Count => Values.Count;
    public string this[int index] => Values[index];
    public bool IsValidIndex(int index) => index >= 0 && index < Values.Count;

    protected override IEnumerable<object?> GetEqualityComponents() => Values;
    public override string ToString() => string.Join(" | ", Values);
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~ValueObjectsTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Learning/Learning.Domain tests/Learning.Domain.Tests/ValueObjectsTests.cs
git commit -m "feat(learning): add exercise/attempt/learner ids, Prompt, Choices value objects"
```

---

### Task 2: `Score` value object + `Outcome` enum

**Files:**
- Create: `src/Modules/Learning/Learning.Domain/Score.cs`
- Create: `src/Modules/Learning/Learning.Domain/Grading.cs` (the `Outcome` enum only in this task; other grading types added in Task 4)
- Test: `tests/Learning.Domain.Tests/ValueObjectsTests.cs` (extend)

**Interfaces:**
- Produces: `enum Outcome { Failed, Passed }`; `Score(int correct, int total)` with `.Correct`, `.Total`, `.Percentage` (double, `Correct/Total`), `bool MeetsThreshold(double threshold)`; guards `total >= 1`, `0 <= correct <= total`.

- [ ] **Step 1: Write the failing tests** — append to `ValueObjectsTests.cs`:

```csharp
[Fact]
public void Score_computes_percentage_and_rejects_invalid()
{
    Assert.Equal(0.5, new Score(1, 2).Percentage, 5);
    Assert.Throws<ArgumentException>(() => new Score(3, 2));   // correct > total
    Assert.Throws<ArgumentException>(() => new Score(-1, 2));  // negative
    Assert.Throws<ArgumentException>(() => new Score(0, 0));   // total < 1
}

[Theory]
[InlineData(4, 5, 0.8, true)]   // exactly at threshold passes
[InlineData(3, 5, 0.8, false)]  // below fails
[InlineData(5, 5, 0.8, true)]   // perfect passes
public void Score_MeetsThreshold_is_inclusive_at_the_boundary(int correct, int total, double threshold, bool expected)
{
    Assert.Equal(expected, new Score(correct, total).MeetsThreshold(threshold));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~ValueObjectsTests"`
Expected: FAIL — `Score` does not exist.

- [ ] **Step 3: Create `Grading.cs` with the `Outcome` enum**

```csharp
namespace Learning.Domain;

public enum Outcome { Failed, Passed }
```

- [ ] **Step 4: Create `Score.cs`**

```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Score : ValueObject
{
    public int Correct { get; }
    public int Total { get; }

    public Score(int correct, int total)
    {
        if (total < 1)
            throw new ArgumentException("A score needs at least one gradeable exercise.", nameof(total));
        if (correct < 0 || correct > total)
            throw new ArgumentException("Correct count must be between 0 and Total.", nameof(correct));

        Correct = correct;
        Total = total;
    }

    public double Percentage => (double)Correct / Total;

    public bool MeetsThreshold(double threshold) => Percentage >= threshold;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Correct;
        yield return Total;
    }

    public override string ToString() => $"{Correct}/{Total}";
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~ValueObjectsTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Learning/Learning.Domain/Score.cs src/Modules/Learning/Learning.Domain/Grading.cs tests/Learning.Domain.Tests/ValueObjectsTests.cs
git commit -m "feat(learning): add Score value object with inclusive pass threshold + Outcome enum"
```

---

### Task 3: `Exercise` child entity

**Files:**
- Create: `src/Modules/Learning/Learning.Domain/Exercise.cs`
- Test: `tests/Learning.Domain.Tests/AggregatesTests.cs` (extend)

**Interfaces:**
- Consumes: `ExerciseId`, `Prompt`, `Choices` (Task 1).
- Produces: `Exercise` with `Id`, `Position`, `Prompt`, `Choices`, and `bool IsCorrect(int selectedChoiceIndex)`. Factory `Exercise.Create(ExerciseId id, int position, Prompt prompt, Choices choices, int correctChoiceIndex)`; the correct index is stored privately (never a public getter — the answer key is sealed inside the aggregate). `Create` validates `correctChoiceIndex` is within `choices`.

- [ ] **Step 1: Write the failing tests** — append to `AggregatesTests.cs`:

```csharp
[Fact]
public void Exercise_IsCorrect_is_true_only_for_the_correct_index()
{
    var exercise = Exercise.Create(
        new ExerciseId(Guid.NewGuid()), 1,
        new Prompt("How do you say hello?"),
        new Choices(new[] { "Hola", "Adios", "Gracias" }),
        correctChoiceIndex: 0);

    Assert.True(exercise.IsCorrect(0));
    Assert.False(exercise.IsCorrect(1));
    Assert.False(exercise.IsCorrect(99)); // out-of-range selection is simply wrong, not an error
}

[Fact]
public void Exercise_Create_rejects_a_correct_index_outside_the_choices()
{
    Assert.Throws<ArgumentException>(() => Exercise.Create(
        new ExerciseId(Guid.NewGuid()), 1,
        new Prompt("Q"), new Choices(new[] { "a", "b" }), correctChoiceIndex: 2));
}

[Fact]
public void Exercise_does_not_expose_the_correct_answer()
{
    // Guards the server-authoritative rule: no public member reveals the key.
    Assert.DoesNotContain(typeof(Exercise).GetProperties(),
        p => p.Name.Contains("Correct", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~AggregatesTests"`
Expected: FAIL — `Exercise` does not exist.

- [ ] **Step 3: Create `Exercise.cs`**

```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

// A child ENTITY of the Lesson aggregate — reached only through its Lesson, never by a global id.
// The correct answer lives here, behind IsCorrect, and is never exposed as a public member.
public sealed class Exercise
{
    public ExerciseId Id { get; private set; } = default!;
    public int Position { get; private set; }
    public Prompt Prompt { get; private set; } = default!;
    public Choices Choices { get; private set; } = default!;

    private int _correctChoiceIndex;

    private Exercise() { } // EF

    public static Exercise Create(ExerciseId id, int position, Prompt prompt, Choices choices, int correctChoiceIndex)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(choices);
        if (!choices.IsValidIndex(correctChoiceIndex))
            throw new ArgumentException("Correct choice index must point at one of the choices.", nameof(correctChoiceIndex));

        return new Exercise
        {
            Id = id,
            Position = position,
            Prompt = prompt,
            Choices = choices,
            _correctChoiceIndex = correctChoiceIndex
        };
    }

    // Tell-don't-ask: the grader asks "is this choice right?" — the key never leaves the exercise.
    public bool IsCorrect(int selectedChoiceIndex) => selectedChoiceIndex == _correctChoiceIndex;
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~AggregatesTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Learning/Learning.Domain/Exercise.cs tests/Learning.Domain.Tests/AggregatesTests.cs
git commit -m "feat(learning): add Exercise child entity with sealed answer key (IsCorrect)"
```

---

### Task 4: `Lesson` grows exercises + `Grade`

**Files:**
- Modify: `src/Modules/Learning/Learning.Domain/Lesson.cs`
- Modify: `src/Modules/Learning/Learning.Domain/Grading.cs` (add `SubmittedAnswer`, `GradedAnswer`, `GradingResult`)
- Test: `tests/Learning.Domain.Tests/AggregatesTests.cs` (extend)

**Interfaces:**
- Consumes: `Exercise` (Task 3), `Score`, `Outcome` (Task 2).
- Produces:
  - `record SubmittedAnswer(ExerciseId ExerciseId, int SelectedChoiceIndex)` — grading input.
  - `record GradedAnswer(ExerciseId ExerciseId, int SelectedChoiceIndex, bool WasCorrect)`.
  - `record GradingResult(Score Score, Outcome Outcome, IReadOnlyList<GradedAnswer> Answers)`.
  - `Lesson.PassThreshold` (`const double = 0.8`), `Lesson.Exercises` (`IReadOnlyCollection<Exercise>`), `Lesson.Grade(IReadOnlyList<SubmittedAnswer>) → GradingResult`. `Create` gains an `IEnumerable<Exercise> exercises` parameter (defaulting to empty for existing callers/tests). `Grade` throws `ArgumentException` when the answer set does not cover exactly the lesson's exercises.

- [ ] **Step 1: Write the failing tests** — append to `AggregatesTests.cs`:

```csharp
private static Lesson PublishedLessonWith(params (int correct, string[] options)[] exercises)
{
    var built = exercises.Select((e, i) => Exercise.Create(
        new ExerciseId(Guid.NewGuid()), i + 1,
        new Prompt($"Q{i + 1}"), new Choices(e.options), e.correct)).ToList();
    return Lesson.Create(new LessonId(Guid.NewGuid()), new UnitId(Guid.NewGuid()),
        new Title("Greetings"), 1, isPublished: true, exercises: built);
}

private static SubmittedAnswer Answer(Lesson lesson, int exerciseIndex, int choice) =>
    new(lesson.Exercises.ElementAt(exerciseIndex).Id, choice);

[Fact]
public void Grade_all_correct_passes()
{
    var lesson = PublishedLessonWith((0, new[] { "a", "b" }), (1, new[] { "a", "b" }));
    var result = lesson.Grade(new[] { Answer(lesson, 0, 0), Answer(lesson, 1, 1) });

    Assert.Equal(Outcome.Passed, result.Outcome);
    Assert.Equal(2, result.Score.Correct);
    Assert.Equal(2, result.Score.Total);
    Assert.All(result.Answers, a => Assert.True(a.WasCorrect));
}

[Fact]
public void Grade_below_threshold_fails_and_records_per_exercise_correctness()
{
    var lesson = PublishedLessonWith((0, new[] { "a", "b" }), (1, new[] { "a", "b" }));
    var result = lesson.Grade(new[] { Answer(lesson, 0, 0), Answer(lesson, 1, 0) }); // 1/2 = 50%

    Assert.Equal(Outcome.Failed, result.Outcome);
    Assert.Equal(1, result.Score.Correct);
    Assert.Contains(result.Answers, a => !a.WasCorrect);
}

[Fact]
public void Grade_exactly_at_threshold_passes()
{
    var lesson = PublishedLessonWith(
        (0, new[] { "a", "b" }), (0, new[] { "a", "b" }), (0, new[] { "a", "b" }),
        (0, new[] { "a", "b" }), (1, new[] { "a", "b" }));
    // answer the first four correctly, last one wrong -> 4/5 = 80%
    var answers = new[]
    {
        Answer(lesson, 0, 0), Answer(lesson, 1, 0), Answer(lesson, 2, 0),
        Answer(lesson, 3, 0), Answer(lesson, 4, 0)
    };
    Assert.Equal(Outcome.Passed, lesson.Grade(answers).Outcome);
}

[Fact]
public void Grade_rejects_an_answer_set_that_does_not_cover_the_exercises()
{
    var lesson = PublishedLessonWith((0, new[] { "a", "b" }), (1, new[] { "a", "b" }));

    // missing one answer
    Assert.Throws<ArgumentException>(() => lesson.Grade(new[] { Answer(lesson, 0, 0) }));
    // unknown exercise id
    Assert.Throws<ArgumentException>(() => lesson.Grade(new[]
    {
        Answer(lesson, 0, 0), new SubmittedAnswer(new ExerciseId(Guid.NewGuid()), 0)
    }));
    // choice index outside the exercise's options
    Assert.Throws<ArgumentException>(() => lesson.Grade(new[] { Answer(lesson, 0, 5), Answer(lesson, 1, 0) }));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~AggregatesTests"`
Expected: FAIL — `Grade`, `SubmittedAnswer`, `Lesson.Exercises`, and the new `Create` overload do not exist.

- [ ] **Step 3: Add the grading records to `Grading.cs`**

```csharp
namespace Learning.Domain;

public enum Outcome { Failed, Passed }

public sealed record SubmittedAnswer(ExerciseId ExerciseId, int SelectedChoiceIndex);
public sealed record GradedAnswer(ExerciseId ExerciseId, int SelectedChoiceIndex, bool WasCorrect);
public sealed record GradingResult(Score Score, Outcome Outcome, IReadOnlyList<GradedAnswer> Answers);
```

- [ ] **Step 4: Rewrite `Lesson.cs`**

```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Lesson : AggregateRoot
{
    public const double PassThreshold = 0.8;

    private readonly List<Exercise> _exercises = new();

    public LessonId Id { get; private set; } = default!;
    public UnitId UnitId { get; private set; } = default!; // reference by id, not a navigation
    public Title Title { get; private set; } = default!;
    public int Position { get; private set; }
    public bool IsPublished { get; private set; }

    public IReadOnlyCollection<Exercise> Exercises => _exercises.AsReadOnly();

    private Lesson() { } // EF

    public static Lesson Create(
        LessonId id, UnitId unitId, Title title, int position, bool isPublished,
        IEnumerable<Exercise>? exercises = null)
    {
        var lesson = new Lesson
        {
            Id = id ?? throw new ArgumentNullException(nameof(id)),
            UnitId = unitId ?? throw new ArgumentNullException(nameof(unitId)),
            Title = title ?? throw new ArgumentNullException(nameof(title)),
            Position = position,
            IsPublished = isPublished
        };
        if (exercises is not null)
            lesson._exercises.AddRange(exercises);
        return lesson;
    }

    // Tell-don't-ask: the handler tells the lesson it is being completed; the lesson enforces its rule.
    public void EnsureCompletable()
    {
        if (!IsPublished)
            throw new InvalidOperationException($"Lesson '{Id}' is not published and cannot be completed.");
    }

    // Grading lives on the aggregate that owns every input (exercises, keys, threshold).
    // Extract to a GradingService only when a rule spans beyond a single lesson's own data.
    public GradingResult Grade(IReadOnlyList<SubmittedAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        if (_exercises.Count == 0)
            throw new InvalidOperationException($"Lesson '{Id}' has no exercises to grade.");

        if (answers.Count != _exercises.Count ||
            answers.Select(a => a.ExerciseId).Distinct().Count() != answers.Count)
            throw new ArgumentException("Answers must cover each exercise exactly once.", nameof(answers));

        var graded = new List<GradedAnswer>(_exercises.Count);
        foreach (var answer in answers)
        {
            var exercise = _exercises.SingleOrDefault(e => e.Id == answer.ExerciseId)
                ?? throw new ArgumentException($"Answer references unknown exercise '{answer.ExerciseId}'.", nameof(answers));
            if (!exercise.Choices.IsValidIndex(answer.SelectedChoiceIndex))
                throw new ArgumentException($"Selected choice {answer.SelectedChoiceIndex} is out of range.", nameof(answers));

            graded.Add(new GradedAnswer(answer.ExerciseId, answer.SelectedChoiceIndex,
                exercise.IsCorrect(answer.SelectedChoiceIndex)));
        }

        var score = new Score(graded.Count(g => g.WasCorrect), _exercises.Count);
        var outcome = score.MeetsThreshold(PassThreshold) ? Outcome.Passed : Outcome.Failed;
        return new GradingResult(score, outcome, graded);
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~AggregatesTests"`
Expected: PASS. (Existing `EnsureCompletable`/`Create` tests still pass — `exercises` is optional.)

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Learning/Learning.Domain/Lesson.cs src/Modules/Learning/Learning.Domain/Grading.cs tests/Learning.Domain.Tests/AggregatesTests.cs
git commit -m "feat(learning): Lesson owns exercises and grades a submission (Grade -> GradingResult)"
```

---

### Task 5: `Attempt` aggregate + `Answer` + `IAttemptRepository`

**Files:**
- Create: `src/Modules/Learning/Learning.Domain/Attempt.cs`
- Create: `src/Modules/Learning/Learning.Domain/IAttemptRepository.cs`
- Test: `tests/Learning.Domain.Tests/AggregatesTests.cs` (extend)

**Interfaces:**
- Consumes: `AttemptId`, `LearnerId`, `LessonId`, `Score`, `Outcome`, `GradingResult`, `GradedAnswer` (earlier tasks).
- Produces:
  - `Answer` (owned): `.ExerciseId`, `.SelectedChoiceIndex`, `.WasCorrect`.
  - `Attempt : AggregateRoot`: `.Id`, `.LearnerId`, `.LessonId`, `.SubmittedAt`, `.Score`, `.Outcome`, `.Answers` (`IReadOnlyCollection<Answer>`), `.Passed` (`=> Outcome == Outcome.Passed`). Factory `Attempt.Create(AttemptId id, LearnerId learnerId, LessonId lessonId, DateTimeOffset submittedAt, GradingResult result)`.
  - `interface IAttemptRepository { Task AddAsync(Attempt attempt, CancellationToken ct); }`.

- [ ] **Step 1: Write the failing test** — append to `AggregatesTests.cs`:

```csharp
[Fact]
public void Attempt_Create_records_the_grading_result()
{
    var lesson = PublishedLessonWith((0, new[] { "a", "b" }), (1, new[] { "a", "b" }));
    var result = lesson.Grade(new[] { Answer(lesson, 0, 0), Answer(lesson, 1, 0) }); // 1/2 -> Failed

    var learnerId = new LearnerId(Guid.NewGuid());
    var at = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
    var attempt = Attempt.Create(new AttemptId(Guid.NewGuid()), learnerId, lesson.Id, at, result);

    Assert.Equal(learnerId, attempt.LearnerId);
    Assert.Equal(lesson.Id, attempt.LessonId);
    Assert.Equal(at, attempt.SubmittedAt);
    Assert.Equal(Outcome.Failed, attempt.Outcome);
    Assert.False(attempt.Passed);
    Assert.Equal(2, attempt.Answers.Count);
    Assert.Equal(result.Answers.Select(a => a.WasCorrect), attempt.Answers.Select(a => a.WasCorrect));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~AggregatesTests"`
Expected: FAIL — `Attempt` does not exist.

- [ ] **Step 3: Create `Attempt.cs`**

```csharp
using BuildingBlocks.Domain;

namespace Learning.Domain;

// Owned child of Attempt — the durable record of one graded answer.
public sealed class Answer
{
    public ExerciseId ExerciseId { get; private set; } = default!;
    public int SelectedChoiceIndex { get; private set; }
    public bool WasCorrect { get; private set; }

    private Answer() { } // EF

    internal Answer(ExerciseId exerciseId, int selectedChoiceIndex, bool wasCorrect)
    {
        ExerciseId = exerciseId;
        SelectedChoiceIndex = selectedChoiceIndex;
        WasCorrect = wasCorrect;
    }
}

public sealed class Attempt : AggregateRoot
{
    private readonly List<Answer> _answers = new();

    public AttemptId Id { get; private set; } = default!;
    public LearnerId LearnerId { get; private set; } = default!;
    public LessonId LessonId { get; private set; } = default!; // reference by id
    public DateTimeOffset SubmittedAt { get; private set; }
    public Score Score { get; private set; } = default!;
    public Outcome Outcome { get; private set; }

    public IReadOnlyCollection<Answer> Answers => _answers.AsReadOnly();
    public bool Passed => Outcome == Outcome.Passed;

    private Attempt() { } // EF

    public static Attempt Create(
        AttemptId id, LearnerId learnerId, LessonId lessonId, DateTimeOffset submittedAt, GradingResult result)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(learnerId);
        ArgumentNullException.ThrowIfNull(lessonId);
        ArgumentNullException.ThrowIfNull(result);

        var attempt = new Attempt
        {
            Id = id,
            LearnerId = learnerId,
            LessonId = lessonId,
            SubmittedAt = submittedAt,
            Score = result.Score,
            Outcome = result.Outcome
        };
        foreach (var g in result.Answers)
            attempt._answers.Add(new Answer(g.ExerciseId, g.SelectedChoiceIndex, g.WasCorrect));
        return attempt;
    }
}
```

- [ ] **Step 4: Create `IAttemptRepository.cs`**

```csharp
namespace Learning.Domain;

// Write port owned by the Domain; implemented in Infrastructure. Slice 2 is Learning's first write path.
public interface IAttemptRepository
{
    Task AddAsync(Attempt attempt, CancellationToken ct);
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/Learning.Domain.Tests --filter "FullyQualifiedName~AggregatesTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Learning/Learning.Domain/Attempt.cs src/Modules/Learning/Learning.Domain/IAttemptRepository.cs tests/Learning.Domain.Tests/AggregatesTests.cs
git commit -m "feat(learning): add persisted Attempt aggregate + owned Answer + IAttemptRepository"
```

---

### Task 6: Persist exercises (owned collection + seed + migration)

**Files:**
- Modify: `src/Modules/Learning/Learning.Infrastructure/LearningSeedIds.cs`
- Modify: `src/Modules/Learning/Learning.Infrastructure/LessonConfiguration.cs`
- Create migration: `src/Modules/Learning/Learning.Infrastructure/Migrations/*_AddExercises.cs`
- Test: `tests/Learning.Integration.Tests/Infrastructure/LearningSeedTests.cs` (extend), `tests/Learning.Integration.Tests/Infrastructure/LessonRepositoryTests.cs` (extend)

**Interfaces:**
- Consumes: `Lesson`, `Exercise`, `Prompt`, `Choices` (domain).
- Produces: `learning.Exercises` table (owned by `Lesson`, auto-loaded with the lesson); exercise seed ids on `LearningSeedIds`. Correct-answer indices exposed as `LearningSeedIds` constants **for tests/seed only** (never surfaced by the API).

- [ ] **Step 1: Write the failing tests**

Extend `LessonRepositoryTests.cs` (owned exercises auto-load with the lesson — no `Include` needed):

```csharp
[Fact]
public async Task GetByIdAsync_loads_the_seeded_exercises()
{
    await using var ctx = NewContext();
    var lesson = await new LessonRepository(ctx)
        .GetByIdAsync(new LessonId(LearningSeedIds.GreetingsLesson), CancellationToken.None);

    Assert.NotNull(lesson);
    Assert.NotEmpty(lesson!.Exercises);
    Assert.All(lesson.Exercises, e => Assert.True(e.Choices.Count >= 2));
}
```

Extend `LearningSeedTests.cs` with an assertion that the published lessons carry exercises and the draft does not. (Open `LearningSeedTests.cs` first to match its existing context/fixture style; add:)

```csharp
[Fact]
public async Task Seed_gives_published_lessons_exercises_and_leaves_the_draft_empty()
{
    await using var ctx = NewContext();
    var greetings = await ctx.Lessons.FirstAsync(l => l.Id == new LessonId(LearningSeedIds.GreetingsLesson));
    var draft = await ctx.Lessons.FirstAsync(l => l.Id == new LessonId(LearningSeedIds.DessertLessonDraft));

    Assert.NotEmpty(greetings.Exercises);
    Assert.Empty(draft.Exercises);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LessonRepositoryTests"`
Expected: FAIL — `Exercises` is empty (no mapping/seed yet).

- [ ] **Step 3: Add exercise seed ids + correct indices** to `LearningSeedIds.cs`:

```csharp
// --- Slice 2: exercises (two per published lesson) ---
public static readonly Guid GreetingsEx1 = new("44444444-4444-4444-4444-000000000001");
public static readonly Guid GreetingsEx2 = new("44444444-4444-4444-4444-000000000002");
public static readonly Guid SerEx1       = new("44444444-4444-4444-4444-000000000003");
public static readonly Guid SerEx2       = new("44444444-4444-4444-4444-000000000004");
public static readonly Guid CafeEx1      = new("44444444-4444-4444-4444-000000000005");
public static readonly Guid CafeEx2      = new("44444444-4444-4444-4444-000000000006");

// Correct-choice indices for the seeded exercises — used by seeding and by tests to build a
// passing/failing submission. The HTTP API never returns these.
public const int GreetingsEx1Correct = 0;
public const int GreetingsEx2Correct = 1;
public const int SerEx1Correct = 0;
public const int SerEx2Correct = 1;
public const int CafeEx1Correct = 0;
public const int CafeEx2Correct = 1;
```

- [ ] **Step 4: Map + seed exercises as an owned collection** in `LessonConfiguration.cs`. Add `using System.Text.Json;`, then inside `Configure`, after the existing `builder.Ignore(l => l.DomainEvents);` and before the lesson `HasData`, add:

```csharp
builder.OwnsMany(l => l.Exercises, ex =>
{
    ex.ToTable("Exercises");
    ex.WithOwner().HasForeignKey("LessonId");
    ex.HasKey(e => e.Id);

    ex.Property(e => e.Id)
        .HasConversion(id => id.Value, value => new ExerciseId(value))
        .HasColumnName("Id")
        .ValueGeneratedNever();

    ex.Property(e => e.Position).HasColumnName("Position");

    ex.Property(e => e.Prompt)
        .HasConversion(p => p.Value, value => new Prompt(value))
        .HasColumnName("Prompt")
        .HasMaxLength(Prompt.MaxLength);

    // Choices -> a single JSON column (keeps the Exercises table flat and HasData simple).
    ex.Property(e => e.Choices)
        .HasConversion(
            c => JsonSerializer.Serialize(c.Values, (JsonSerializerOptions?)null),
            s => new Choices(JsonSerializer.Deserialize<List<string>>(s, (JsonSerializerOptions?)null)!))
        .HasColumnName("Choices");

    // The answer key: a private field mapped as a shadow-ish backing property.
    ex.Property<int>("_correctChoiceIndex").HasColumnName("CorrectChoiceIndex");

    ex.HasData(
        SeedExercise(LearningSeedIds.GreetingsEx1, LearningSeedIds.GreetingsLesson, 1, "How do you say hello?",     new[] { "Hola", "Adios", "Gracias" }, LearningSeedIds.GreetingsEx1Correct),
        SeedExercise(LearningSeedIds.GreetingsEx2, LearningSeedIds.GreetingsLesson, 2, "How do you say goodbye?",   new[] { "Hola", "Adios", "Gracias" }, LearningSeedIds.GreetingsEx2Correct),
        SeedExercise(LearningSeedIds.SerEx1,       LearningSeedIds.SerLesson,       1, "Yo ___ estudiante.",        new[] { "soy", "es", "eres" },        LearningSeedIds.SerEx1Correct),
        SeedExercise(LearningSeedIds.SerEx2,       LearningSeedIds.SerLesson,       2, "Ella ___ profesora.",       new[] { "soy", "es", "eres" },        LearningSeedIds.SerEx2Correct),
        SeedExercise(LearningSeedIds.CafeEx1,      LearningSeedIds.CafeLesson,      1, "How do you order a coffee?",new[] { "Un cafe, por favor", "Adios", "Gracias" }, LearningSeedIds.CafeEx1Correct),
        SeedExercise(LearningSeedIds.CafeEx2,      LearningSeedIds.CafeLesson,      2, "How do you say water?",     new[] { "leche", "agua", "pan" },     LearningSeedIds.CafeEx2Correct));

    builder.Navigation(l => l.Exercises)
        .HasField("_exercises")
        .UsePropertyAccessMode(PropertyAccessMode.Field);
});
```

Add this private static helper at the bottom of the `LessonConfiguration` class (owned `HasData` needs anonymous rows carrying the owner FK and the backing field; `Choices`/`Prompt`/`Id` go through the configured converters, `_correctChoiceIndex` is the shadow column):

```csharp
private static object SeedExercise(Guid id, Guid lessonId, int position, string prompt, string[] choices, int correct) =>
    new
    {
        Id = new ExerciseId(id),
        LessonId = new LessonId(lessonId),
        Position = position,
        Prompt = new Prompt(prompt),
        Choices = new Choices(choices),
        _correctChoiceIndex = correct
    };
```

> **HasData fallback (spec-flagged risk):** if `OwnsMany(...).HasData` with the value-converted `Choices`/`Prompt`/`Id` proves awkward at `migrations add` time, replace the `ex.HasData(...)` block with a one-time startup/`EnsureSeeded` seeder invoked from `AddLearningInfrastructure` (idempotent upsert by id) and delete the exercise rows from the migration. Keep the seed **ids and correct indices** identical so tests are unaffected.

- [ ] **Step 5: Generate the migration**

Run:
```powershell
dotnet ef migrations add AddExercises `
  -p src/Modules/Learning/Learning.Infrastructure `
  -s src/Modules/Learning/Learning.Infrastructure -o Migrations
```
Expected: a new `Migrations/*_AddExercises.cs` creating `learning.Exercises` (columns `Id`, `LessonId`, `Position`, `Prompt`, `Choices`, `CorrectChoiceIndex`) with the six seed rows.

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LessonRepositoryTests|FullyQualifiedName~LearningSeedTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Learning/Learning.Infrastructure tests/Learning.Integration.Tests/Infrastructure/LessonRepositoryTests.cs tests/Learning.Integration.Tests/Infrastructure/LearningSeedTests.cs
git commit -m "feat(learning): persist exercises as an owned collection with seed (AddExercises)"
```

---

### Task 7: Persist attempts (Attempt/Answer mapping + repository + DI + migration)

**Files:**
- Modify: `src/Modules/Learning/Learning.Infrastructure/LearningDbContext.cs`
- Create: `src/Modules/Learning/Learning.Infrastructure/AttemptConfiguration.cs`
- Create: `src/Modules/Learning/Learning.Infrastructure/AttemptRepository.cs`
- Modify: `src/Modules/Learning/Learning.Infrastructure/LearningInfrastructureExtensions.cs`
- Create migration: `src/Modules/Learning/Learning.Infrastructure/Migrations/*_AddAttempts.cs`
- Test: `tests/Learning.Integration.Tests/Infrastructure/AttemptRepositoryTests.cs` (new)

**Interfaces:**
- Consumes: `Attempt`, `Answer`, `IAttemptRepository`, `GradingResult` (domain).
- Produces: `learning.Attempts` (+ `learning.Answers`) tables; `AttemptRepository : IAttemptRepository`; DI registration.

- [ ] **Step 1: Write the failing test** — create `tests/Learning.Integration.Tests/Infrastructure/AttemptRepositoryTests.cs`:

```csharp
using Learning.Domain;
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class AttemptRepositoryTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Attempt_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public AttemptRepositoryTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    private static Attempt SampleAttempt()
    {
        var ex = Exercise.Create(new ExerciseId(Guid.NewGuid()), 1,
            new Prompt("Q"), new Choices(new[] { "a", "b" }), 0);
        var lesson = Lesson.Create(new LessonId(Guid.NewGuid()), new UnitId(Guid.NewGuid()),
            new Title("L"), 1, isPublished: true, exercises: new[] { ex });
        var result = lesson.Grade(new[] { new SubmittedAnswer(ex.Id, 0) });
        return Attempt.Create(new AttemptId(Guid.NewGuid()), new LearnerId(Guid.NewGuid()),
            lesson.Id, new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public async Task AddAsync_persists_the_attempt_and_its_answers()
    {
        var attempt = SampleAttempt();

        await using (var ctx = NewContext())
            await new AttemptRepository(ctx).AddAsync(attempt, CancellationToken.None);

        await using var read = NewContext();
        var reloaded = await read.Attempts.FirstOrDefaultAsync(a => a.Id == attempt.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(Outcome.Passed, reloaded!.Outcome);
        Assert.Equal(1, reloaded.Score.Correct);
        Assert.Single(reloaded.Answers);
        Assert.True(reloaded.Answers.First().WasCorrect);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~AttemptRepositoryTests"`
Expected: FAIL — `AttemptRepository`, `ctx.Attempts` do not exist.

- [ ] **Step 3: Add the `DbSet` + config registration** in `LearningDbContext.cs`:

```csharp
public DbSet<Attempt> Attempts => Set<Attempt>();
```
and inside `OnModelCreating`, after the existing `ApplyConfiguration` calls:
```csharp
modelBuilder.ApplyConfiguration(new AttemptConfiguration());
```

- [ ] **Step 4: Create `AttemptConfiguration.cs`** (mirrors `XpAccountConfiguration`'s owned-collection + backing-field pattern; `Score` is an owned single, `Outcome` stored as text):

```csharp
using Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learning.Infrastructure;

internal sealed class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("Attempts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AttemptId(value))
            .HasColumnName("Id")
            .ValueGeneratedNever();

        builder.Property(a => a.LearnerId)
            .HasConversion(id => id.Value, value => new LearnerId(value))
            .HasColumnName("LearnerId");

        builder.Property(a => a.LessonId)
            .HasConversion(id => id.Value, value => new LessonId(value))
            .HasColumnName("LessonId"); // id reference — a column, not a navigation

        builder.Property(a => a.SubmittedAt).HasColumnName("SubmittedAt");
        builder.Property(a => a.Outcome).HasConversion<string>().HasColumnName("Outcome");

        builder.OwnsOne(a => a.Score, s =>
        {
            s.Property(x => x.Correct).HasColumnName("ScoreCorrect");
            s.Property(x => x.Total).HasColumnName("ScoreTotal");
        });

        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.Passed); // derived from Outcome

        builder.OwnsMany(a => a.Answers, ans =>
        {
            ans.ToTable("Answers");
            ans.WithOwner().HasForeignKey("AttemptId");

            // Store-generated surrogate key so freshly-added answers INSERT (mirrors AppliedAward).
            ans.Property<int>("Id").ValueGeneratedOnAdd();
            ans.HasKey("Id");

            ans.Property(x => x.ExerciseId)
                .HasConversion(id => id.Value, value => new ExerciseId(value))
                .HasColumnName("ExerciseId");
            ans.Property(x => x.SelectedChoiceIndex).HasColumnName("SelectedChoiceIndex");
            ans.Property(x => x.WasCorrect).HasColumnName("WasCorrect");
        });

        builder.Navigation(a => a.Answers)
            .HasField("_answers")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

- [ ] **Step 5: Create `AttemptRepository.cs`**

```csharp
using Learning.Domain;

namespace Learning.Infrastructure;

// Learning's first write path: add the attempt and commit in one unit of work.
public sealed class AttemptRepository(LearningDbContext context) : IAttemptRepository
{
    public async Task AddAsync(Attempt attempt, CancellationToken ct)
    {
        context.Attempts.Add(attempt);
        await context.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 6: Register the repository** in `LearningInfrastructureExtensions.cs`, after the `ICatalogReadService` registration:

```csharp
services.AddScoped<IAttemptRepository, AttemptRepository>();
```

- [ ] **Step 7: Generate the migration**

Run:
```powershell
dotnet ef migrations add AddAttempts `
  -p src/Modules/Learning/Learning.Infrastructure `
  -s src/Modules/Learning/Learning.Infrastructure -o Migrations
```
Expected: `Migrations/*_AddAttempts.cs` creating `learning.Attempts` (`Id`, `LearnerId`, `LessonId`, `SubmittedAt`, `Outcome`, `ScoreCorrect`, `ScoreTotal`) and `learning.Answers` (`Id` identity, `AttemptId` FK, `ExerciseId`, `SelectedChoiceIndex`, `WasCorrect`).

- [ ] **Step 8: Run to verify it passes**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~AttemptRepositoryTests"`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/Modules/Learning/Learning.Infrastructure tests/Learning.Integration.Tests/Infrastructure/AttemptRepositoryTests.cs
git commit -m "feat(learning): persist Attempt + owned Answers, add AttemptRepository (AddAttempts)"
```

---

### Task 8: `GetLesson` presentation read (application port + infra impl)

**Files:**
- Create: `src/Modules/Learning/Learning.Application/GetLesson.cs`
- Create: `src/Modules/Learning/Learning.Infrastructure/LessonPresentationRead.cs`
- Modify: `src/Modules/Learning/Learning.Infrastructure/LearningInfrastructureExtensions.cs`
- Test: `tests/Learning.Integration.Tests/Infrastructure/LessonPresentationReadTests.cs` (new)

**Interfaces:**
- Consumes: `LearningDbContext`, `Lesson`/`Exercise` (owned, auto-loaded).
- Produces:
  - `record GetLesson(Guid LessonId) : IRequest<LessonPresentationDto?>`.
  - `record LessonPresentationDto(Guid Id, string Title, bool IsPublished, IReadOnlyList<ExercisePresentationDto> Exercises)`.
  - `record ExercisePresentationDto(Guid Id, int Position, string Prompt, IReadOnlyList<string> Choices)` — **no correct index**.
  - `interface ILessonPresentationRead { Task<LessonPresentationDto?> GetLessonAsync(Guid lessonId, CancellationToken ct); }`.

- [ ] **Step 1: Write the failing test** — create `LessonPresentationReadTests.cs`:

```csharp
using Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Learning.Integration.Tests.Infrastructure;

public class LessonPresentationReadTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Present_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static LearningDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LearningDbContext>().UseSqlServer(ConnectionString).Options);

    public LessonPresentationReadTests()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task GetLessonAsync_returns_prompts_and_choices_without_the_key()
    {
        await using var ctx = NewContext();
        var dto = await new LessonPresentationRead(ctx)
            .GetLessonAsync(LearningSeedIds.GreetingsLesson, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.Exercises.Count);
        Assert.All(dto.Exercises, e => Assert.True(e.Choices.Count >= 2));
        // The DTO type has no member that could carry the answer key.
        Assert.DoesNotContain(typeof(ExercisePresentationDto).GetProperties(),
            p => p.Name.Contains("Correct", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetLessonAsync_returns_null_for_an_unknown_lesson()
    {
        await using var ctx = NewContext();
        var dto = await new LessonPresentationRead(ctx).GetLessonAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(dto);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LessonPresentationReadTests"`
Expected: FAIL — `LessonPresentationRead`/`ExercisePresentationDto` do not exist.

- [ ] **Step 3: Create `GetLesson.cs`**

```csharp
using BuildingBlocks.Mediator;

namespace Learning.Application;

public sealed record GetLesson(Guid LessonId) : IRequest<LessonPresentationDto?>;

public sealed record LessonPresentationDto(
    Guid Id, string Title, bool IsPublished, IReadOnlyList<ExercisePresentationDto> Exercises);
public sealed record ExercisePresentationDto(
    Guid Id, int Position, string Prompt, IReadOnlyList<string> Choices); // no correct answer

// Read-model port (returns a DTO) — distinct from the aggregate repositories.
public interface ILessonPresentationRead
{
    Task<LessonPresentationDto?> GetLessonAsync(Guid lessonId, CancellationToken ct);
}

public sealed class GetLessonHandler(ILessonPresentationRead read) : IRequestHandler<GetLesson, LessonPresentationDto?>
{
    public Task<LessonPresentationDto?> HandleAsync(GetLesson request, CancellationToken ct) =>
        read.GetLessonAsync(request.LessonId, ct);
}
```

- [ ] **Step 4: Create `LessonPresentationRead.cs`** (project in memory to avoid value-converter translation limits, mirroring `CatalogReadService`):

```csharp
using Learning.Application;
using Learning.Domain;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure;

public sealed class LessonPresentationRead(LearningDbContext context) : ILessonPresentationRead
{
    public async Task<LessonPresentationDto?> GetLessonAsync(Guid lessonId, CancellationToken ct)
    {
        var id = new LessonId(lessonId);
        var lesson = await context.Lessons.FirstOrDefaultAsync(l => l.Id == id, ct); // owned exercises auto-load
        if (lesson is null)
            return null;

        var exercises = lesson.Exercises
            .OrderBy(e => e.Position)
            .Select(e => new ExercisePresentationDto(e.Id.Value, e.Position, e.Prompt.Value, e.Choices.Values))
            .ToList();

        return new LessonPresentationDto(lesson.Id.Value, lesson.Title.Value, lesson.IsPublished, exercises);
    }
}
```

- [ ] **Step 5: Register the read service** in `LearningInfrastructureExtensions.cs`:

```csharp
services.AddScoped<ILessonPresentationRead, LessonPresentationRead>();
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LessonPresentationReadTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Learning/Learning.Application/GetLesson.cs src/Modules/Learning/Learning.Infrastructure tests/Learning.Integration.Tests/Infrastructure/LessonPresentationReadTests.cs
git commit -m "feat(learning): add GetLesson presentation read (exercises without the answer key)"
```

---

### Task 9: `SubmitAttempt` command + handler (added alongside `CompleteLesson`)

**Files:**
- Create: `src/Modules/Learning/Learning.Application/SubmitAttempt.cs`
- Create (replaces old test file): `tests/Learning.Integration.Tests/Application/SubmitAttemptHandlerTests.cs`

**Interfaces:**
- Consumes: `ILessonRepository`, `IAttemptRepository`, `IMediator`, `TimeProvider`; `Lesson.Grade`, `Attempt.Create`, `LessonCompleted`.
- Produces:
  - `record SubmitAttempt(Guid LearnerId, Guid LessonId, IReadOnlyList<SubmittedAnswerInput> Answers) : IRequest<AttemptResultDto>`.
  - `record SubmittedAnswerInput(Guid ExerciseId, int SelectedChoiceIndex)`.
  - `record AttemptResultDto(Guid AttemptId, int ScoreCorrect, int ScoreTotal, string Outcome, IReadOnlyList<PerExerciseResultDto> PerExercise)`.
  - `record PerExerciseResultDto(Guid ExerciseId, bool WasCorrect)`.
  - `SubmitAttemptHandler`.

- [ ] **Step 1: Write the failing tests** — create `SubmitAttemptHandlerTests.cs`:

```csharp
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Learning.Application;
using Learning.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Learning.Integration.Tests.Application;

public class SubmitAttemptHandlerTests
{
    private sealed class StubLessonRepository(Lesson? lesson) : ILessonRepository
    {
        public Task<Lesson?> GetByIdAsync(LessonId id, CancellationToken ct) => Task.FromResult(lesson);
    }

    private sealed class RecordingAttemptRepository : IAttemptRepository
    {
        public Attempt? Saved;
        public Task AddAsync(Attempt attempt, CancellationToken ct) { Saved = attempt; return Task.CompletedTask; }
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

    private static Lesson PublishedLesson(bool published = true)
    {
        var e1 = Exercise.Create(new ExerciseId(Guid.NewGuid()), 1, new Prompt("Q1"), new Choices(new[] { "a", "b" }), 0);
        var e2 = Exercise.Create(new ExerciseId(Guid.NewGuid()), 2, new Prompt("Q2"), new Choices(new[] { "a", "b" }), 1);
        return Lesson.Create(new LessonId(Guid.NewGuid()), new UnitId(Guid.NewGuid()),
            new Title("Greetings"), 1, published, exercises: new[] { e1, e2 });
    }

    private static SubmitAttempt SubmissionFor(Lesson lesson, Guid learnerId, params int[] picks) =>
        new(learnerId, lesson.Id.Value,
            lesson.Exercises.Select((e, i) => new SubmittedAnswerInput(e.Id.Value, picks[i])).ToList());

    private static (SubmitAttemptHandler handler, RecordingAttemptRepository attempts, CapturingMediator mediator) Build(Lesson? lesson)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var attempts = new RecordingAttemptRepository();
        var mediator = new CapturingMediator();
        return (new SubmitAttemptHandler(new StubLessonRepository(lesson), attempts, mediator, clock), attempts, mediator);
    }

    [Fact]
    public async Task Passing_attempt_persists_and_publishes_LessonCompleted_from_the_clock()
    {
        var learnerId = Guid.NewGuid();
        var lesson = PublishedLesson();
        var (handler, attempts, mediator) = Build(lesson);

        var dto = await handler.HandleAsync(SubmissionFor(lesson, learnerId, 0, 1), CancellationToken.None);

        Assert.Equal("Passed", dto.Outcome);
        Assert.NotNull(attempts.Saved);
        var evt = Assert.IsType<LessonCompleted>(Assert.Single(mediator.Published));
        Assert.Equal(learnerId, evt.LearnerId);
        Assert.Equal(lesson.Id.Value, evt.LessonId);
        Assert.Equal(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero), evt.OccurredOn);
    }

    [Fact]
    public async Task Failing_attempt_persists_but_publishes_nothing()
    {
        var lesson = PublishedLesson();
        var (handler, attempts, mediator) = Build(lesson);

        var dto = await handler.HandleAsync(SubmissionFor(lesson, Guid.NewGuid(), 0, 0), CancellationToken.None); // 1/2

        Assert.Equal("Failed", dto.Outcome);
        Assert.NotNull(attempts.Saved);
        Assert.Empty(mediator.Published);
    }

    [Fact]
    public async Task Unknown_lesson_throws_KeyNotFound_and_writes_nothing()
    {
        var (handler, attempts, mediator) = Build(lesson: null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(
            new SubmitAttempt(Guid.NewGuid(), Guid.NewGuid(), new List<SubmittedAnswerInput>()), CancellationToken.None));
        Assert.Null(attempts.Saved);
        Assert.Empty(mediator.Published);
    }

    [Fact]
    public async Task Unpublished_lesson_throws_InvalidOperation_and_writes_nothing()
    {
        var lesson = PublishedLesson(published: false);
        var (handler, attempts, mediator) = Build(lesson);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            SubmissionFor(lesson, Guid.NewGuid(), 0, 1), CancellationToken.None));
        Assert.Null(attempts.Saved);
        Assert.Empty(mediator.Published);
    }

    [Fact]
    public async Task Malformed_answer_set_throws_ArgumentException_and_writes_nothing()
    {
        var lesson = PublishedLesson();
        var (handler, attempts, mediator) = Build(lesson);

        // only one answer for a two-exercise lesson
        var bad = new SubmitAttempt(Guid.NewGuid(), lesson.Id.Value,
            new[] { new SubmittedAnswerInput(lesson.Exercises.First().Id.Value, 0) });

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(bad, CancellationToken.None));
        Assert.Null(attempts.Saved);
        Assert.Empty(mediator.Published);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~SubmitAttemptHandlerTests"`
Expected: FAIL — `SubmitAttempt` does not exist.

- [ ] **Step 3: Create `SubmitAttempt.cs`**

```csharp
using BuildingBlocks.Contracts;
using BuildingBlocks.Mediator;
using Learning.Domain;

namespace Learning.Application;

public sealed record SubmitAttempt(Guid LearnerId, Guid LessonId, IReadOnlyList<SubmittedAnswerInput> Answers)
    : IRequest<AttemptResultDto>;

public sealed record SubmittedAnswerInput(Guid ExerciseId, int SelectedChoiceIndex);
public sealed record AttemptResultDto(
    Guid AttemptId, int ScoreCorrect, int ScoreTotal, string Outcome, IReadOnlyList<PerExerciseResultDto> PerExercise);
public sealed record PerExerciseResultDto(Guid ExerciseId, bool WasCorrect);

public sealed class SubmitAttemptHandler(
    ILessonRepository lessons,
    IAttemptRepository attempts,
    IMediator mediator,
    TimeProvider clock) : IRequestHandler<SubmitAttempt, AttemptResultDto>
{
    public async Task<AttemptResultDto> HandleAsync(SubmitAttempt request, CancellationToken ct)
    {
        var lesson = await lessons.GetByIdAsync(new LessonId(request.LessonId), ct)
            ?? throw new KeyNotFoundException($"Lesson '{request.LessonId}' was not found.");

        lesson.EnsureCompletable(); // unpublished -> InvalidOperationException -> 409

        var submitted = request.Answers
            .Select(a => new SubmittedAnswer(new ExerciseId(a.ExerciseId), a.SelectedChoiceIndex))
            .ToList();

        var result = lesson.Grade(submitted); // malformed set -> ArgumentException -> 400

        var attempt = Attempt.Create(
            new AttemptId(Guid.NewGuid()),
            new LearnerId(request.LearnerId),
            lesson.Id,
            clock.GetUtcNow(),
            result);

        await attempts.AddAsync(attempt, ct); // persist first — the Attempt is the source of truth

        if (attempt.Passed)
        {
            // Integration event, XP-free; the AppliedAward ledger dedups true redelivery.
            await mediator.PublishAsync(
                new LessonCompleted(
                    EventId: Guid.NewGuid(),
                    LearnerId: request.LearnerId,
                    LessonId: request.LessonId,
                    OccurredOn: clock.GetUtcNow()),
                ct);
        }

        return new AttemptResultDto(
            attempt.Id.Value,
            result.Score.Correct,
            result.Score.Total,
            result.Outcome.ToString(),
            result.Answers.Select(a => new PerExerciseResultDto(a.ExerciseId.Value, a.WasCorrect)).ToList());
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~SubmitAttemptHandlerTests"`
Expected: PASS.

- [ ] **Step 5: Delete the superseded handler test** (its behavior is now covered by `SubmitAttemptHandlerTests`; `CompleteLesson` itself is removed in Task 11):

```bash
git rm tests/Learning.Integration.Tests/Application/CompleteLessonHandlerTests.cs
```

- [ ] **Step 6: Run the full Learning suite to confirm nothing else broke**

Run: `dotnet test tests/Learning.Integration.Tests`
Expected: PASS (the old e2e `/complete` tests still pass — that endpoint still exists until Task 11).

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Learning/Learning.Application/SubmitAttempt.cs tests/Learning.Integration.Tests/Application
git commit -m "feat(learning): add SubmitAttempt command/handler (grade, persist, publish on pass)"
```

---

### Task 10: Host endpoints for attempts + lesson presentation (e2e)

**Files:**
- Modify: `src/Host/Program.cs`
- Rewrite: `tests/Learning.Integration.Tests/EndToEnd/LearningApiTests.cs`

**Interfaces:**
- Consumes: `SubmitAttempt`, `GetLesson` (Application); `LearningSeedIds` (test seed + correct indices).
- Produces: `POST /lessons/{id}/attempts`, `GET /lessons/{id}`. (The old `POST /complete` stays until Task 11, so this task's build is green.)

- [ ] **Step 1: Write the failing e2e tests** — replace the body of `LearningApiTests.cs` with (keeps the catalog test; swaps completion for attempts; the factory `LearningApiFactory` is unchanged and already provisions both DBs + disables the league scheduler):

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
    private sealed record UnitView(Guid Id, string Title, int Position, List<LessonNode> Lessons);
    private sealed record LessonNode(Guid Id, string Title, int Position, bool IsPublished);

    private sealed record LessonPresentation(Guid Id, string Title, bool IsPublished, List<ExercisePresentation> Exercises);
    private sealed record ExercisePresentation(Guid Id, int Position, string Prompt, List<string> Choices);
    private sealed record AttemptResult(Guid AttemptId, int ScoreCorrect, int ScoreTotal, string Outcome, List<PerExercise> PerExercise);
    private sealed record PerExercise(Guid ExerciseId, bool WasCorrect);
    private sealed record AnswerInput(Guid ExerciseId, int SelectedChoiceIndex);
    private sealed record AttemptRequest(List<AnswerInput> Answers);

    private HttpClient ClientForLearner(Guid learnerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Learner-Id", learnerId.ToString());
        return client;
    }

    // The Greetings lesson's correct answers, from the seed (the API never returns them).
    private static AttemptRequest GreetingsAnswers(int ex1Pick, int ex2Pick, LessonPresentation lesson) =>
        new(new List<AnswerInput>
        {
            new(lesson.Exercises[0].Id, ex1Pick),
            new(lesson.Exercises[1].Id, ex2Pick)
        });

    private async Task<LessonPresentation> GetGreetings(HttpClient client) =>
        (await client.GetFromJsonAsync<LessonPresentation>($"/lessons/{LearningSeedIds.GreetingsLesson}"))!;

    [Fact]
    public async Task Get_courses_returns_the_seeded_catalog()
    {
        var catalog = await factory.CreateClient().GetFromJsonAsync<CatalogView>("/courses");
        Assert.NotNull(catalog);
        var course = Assert.Single(catalog!.Courses);
        Assert.Equal(4, course.Units.Sum(u => u.Lessons.Count));
    }

    [Fact]
    public async Task Get_lesson_returns_exercises_without_the_answer_key()
    {
        var lesson = await GetGreetings(factory.CreateClient());
        Assert.Equal(2, lesson.Exercises.Count);
        Assert.All(lesson.Exercises, e => Assert.True(e.Choices.Count >= 2));
    }

    [Fact]
    public async Task Passing_attempt_returns_200_Passed_and_awards_xp()
    {
        var client = ClientForLearner(Guid.NewGuid());
        var lesson = await GetGreetings(client);

        var post = await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts",
            GreetingsAnswers(LearningSeedIds.GreetingsEx1Correct, LearningSeedIds.GreetingsEx2Correct, lesson));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var result = await post.Content.ReadFromJsonAsync<AttemptResult>();
        Assert.Equal("Passed", result!.Outcome);

        var xp = await client.GetFromJsonAsync<XpResponse>("/me/xp");
        Assert.Equal(10, xp!.TotalXp);
    }

    [Fact]
    public async Task Failing_attempt_returns_200_Failed_and_awards_no_xp()
    {
        var client = ClientForLearner(Guid.NewGuid());
        var lesson = await GetGreetings(client);

        var wrong = LearningSeedIds.GreetingsEx1Correct == 0 ? 1 : 0;
        var post = await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts",
            GreetingsAnswers(wrong, wrong, lesson));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var result = await post.Content.ReadFromJsonAsync<AttemptResult>();
        Assert.Equal("Failed", result!.Outcome);

        var xp = await client.GetFromJsonAsync<XpResponse>("/me/xp");
        Assert.Equal(0, xp!.TotalXp);
    }

    [Fact]
    public async Task Unknown_lesson_returns_404()
    {
        var post = await ClientForLearner(Guid.NewGuid())
            .PostAsJsonAsync($"/lessons/{Guid.NewGuid()}/attempts", new AttemptRequest(new List<AnswerInput>()));
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task Unpublished_lesson_returns_409()
    {
        var post = await ClientForLearner(Guid.NewGuid())
            .PostAsJsonAsync($"/lessons/{LearningSeedIds.DessertLessonDraft}/attempts",
                new AttemptRequest(new List<AnswerInput>()));
        Assert.Equal(HttpStatusCode.Conflict, post.StatusCode);
    }

    [Fact]
    public async Task Malformed_answer_set_returns_400()
    {
        var client = ClientForLearner(Guid.NewGuid());
        var lesson = await GetGreetings(client);

        // only one answer for a two-exercise lesson
        var bad = new AttemptRequest(new List<AnswerInput> { new(lesson.Exercises[0].Id, 0) });
        var post = await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts", bad);
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    [Fact]
    public async Task Passing_the_same_lesson_twice_awards_xp_twice()
    {
        var client = ClientForLearner(Guid.NewGuid());
        var lesson = await GetGreetings(client);
        var pass = GreetingsAnswers(LearningSeedIds.GreetingsEx1Correct, LearningSeedIds.GreetingsEx2Correct, lesson);

        await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts", pass);
        await client.PostAsJsonAsync($"/lessons/{LearningSeedIds.GreetingsLesson}/attempts", pass);

        var xp = await client.GetFromJsonAsync<XpResponse>("/me/xp");
        Assert.Equal(20, xp!.TotalXp);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LearningApiTests"`
Expected: FAIL — `/lessons/{id}/attempts` and `/lessons/{id}` return 404 (not mapped yet).

- [ ] **Step 3: Add the endpoints** in `Program.cs`. Add `GetLesson`/`SubmitAttempt` are already in `Learning.Application` (imported via `using Learning.Application;`). Insert after the existing `GET /courses` endpoint:

```csharp
app.MapGet("/lessons/{lessonId:guid}",
    async (Guid lessonId, IMediator mediator, CancellationToken ct) =>
    {
        var dto = await mediator.SendAsync(new GetLesson(lessonId), ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    });

app.MapPost("/lessons/{lessonId:guid}/attempts",
    async (Guid lessonId, AttemptBody body, ICurrentUser user, IMediator mediator, CancellationToken ct) =>
    {
        try
        {
            var result = await mediator.SendAsync(
                new SubmitAttempt(user.LearnerId, lessonId,
                    body.Answers.Select(a => new SubmittedAnswerInput(a.ExerciseId, a.SelectedChoiceIndex)).ToList()),
                ct);
            return Results.Ok(result); // grading + any XP award ran in-process before we return
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message }); // lesson exists but is not completable
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message }); // answer set does not match the exercises
        }
    });
```

And add the request-body records near the bottom of `Program.cs` (next to `SetTimeZoneRequest`, before `public partial class Program { }`):

```csharp
public sealed record AttemptBody(List<AnswerBody> Answers);
public sealed record AnswerBody(Guid ExerciseId, int SelectedChoiceIndex);
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Learning.Integration.Tests --filter "FullyQualifiedName~LearningApiTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Host/Program.cs tests/Learning.Integration.Tests/EndToEnd/LearningApiTests.cs
git commit -m "feat(learning): expose POST /lessons/{id}/attempts and GET /lessons/{id}"
```

---

### Task 11: Remove the superseded `CompleteLesson` path (cleanup)

**Files:**
- Delete: `src/Modules/Learning/Learning.Application/CompleteLesson.cs`
- Modify: `src/Host/Program.cs` (remove `POST /complete`; update mediator assembly marker)
- Modify: `tests/Learning.Integration.Tests/Architecture/ArchitectureTests.cs` (marker type)

**Interfaces:**
- Removes `CompleteLesson`/`CompleteLessonHandler`. `Learning.Application`'s architecture marker becomes `SubmitAttempt`.

- [ ] **Step 1: Delete the command + handler**

```bash
git rm src/Modules/Learning/Learning.Application/CompleteLesson.cs
```

- [ ] **Step 2: Remove the old endpoint** in `Program.cs` — delete the entire `app.MapPost("/lessons/{lessonId:guid}/complete", ...)` block (lines mapping the Slice-1 completion).

- [ ] **Step 3: Update the mediator assembly marker** in `Program.cs` — the handler-scan marker currently references the deleted type. Change:

```csharp
builder.Services.AddMediator(
    typeof(GetXpAccount).Assembly,        // Engagement.Application handlers
    typeof(SubmitAttempt).Assembly);      // Learning.Application handlers
```

- [ ] **Step 4: Update the architecture-test marker** in `ArchitectureTests.cs`:

```csharp
private static readonly Assembly ApplicationAssembly = typeof(global::Learning.Application.SubmitAttempt).Assembly;
```

- [ ] **Step 5: Build to confirm no dangling references**

Run: `dotnet build`
Expected: SUCCEED — no references to `CompleteLesson` remain.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: PASS — all Learning domain/integration/e2e/architecture tests, plus untouched Engagement streak/league suites (they publish `LessonCompleted` directly).

- [ ] **Step 7: Commit**

```bash
git add src/Host/Program.cs src/Modules/Learning/Learning.Application tests/Learning.Integration.Tests/Architecture/ArchitectureTests.cs
git commit -m "refactor(learning): remove asserted CompleteLesson path, superseded by attempts"
```

---

### Task 12: Full-suite verification + branch finish

**Files:** none (verification + integration).

- [ ] **Step 1: Clean build + full test run from a cold state**

Run: `dotnet build` then `dotnet test`
Expected: build succeeds; every test passes. If any Engagement e2e factory shares a host that now needs the Learning schema, confirm it still provisions both DBs (it did in Slice 1).

- [ ] **Step 2: Confirm the acceptance criteria** (from the spec) are each covered by a passing test:
  1. Passing attempt → `LessonCompleted` + XP (`Passing_attempt_returns_200_Passed_and_awards_xp`).
  2. Failing attempt → 200 Failed, no event (`Failing_attempt_returns_200_Failed_and_awards_no_xp`).
  3. 404 / 409 / 400 (`Unknown_lesson_returns_404`, `Unpublished_lesson_returns_409`, `Malformed_answer_set_returns_400`).
  4. Server-authoritative (`Get_lesson_returns_exercises_without_the_answer_key`, `Exercise_does_not_expose_the_correct_answer`).
  5. Passing twice awards twice (`Passing_the_same_lesson_twice_awards_xp_twice`).
  6. `Score`/threshold/`TimeProvider`/unchanged contract (`Score_*`, `Passing_attempt_persists_and_publishes_LessonCompleted_from_the_clock`).
  7. Own schema + migrations, no cross-module refs (`AttemptRepositoryTests`, `Learning_does_not_depend_on_Engagement`).
  8. Domain purity + `/complete` removed (`Domain_does_not_depend_on_EfCore_or_AspNetCore`, build has no `CompleteLesson`).

- [ ] **Step 3: Finish the branch** — invoke `superpowers:finishing-a-development-branch` to open the PR (`feat/learning-exercises-grading` → `main`). After merge, mark the slice complete in `CLAUDE.md` and `README` with a docs-straight-to-main commit (per repo hygiene), and delete the merged branch locally + remotely.

---

## Self-Review

**Spec coverage:** Every spec section maps to a task — exercise model/child entity (T3), `Lesson.Grade` + `Score` + global threshold (T2, T4), persisted `Attempt` (T5, T7), presentation read hiding the key (T8), `SubmitAttempt` replacing `CompleteLesson` (T9, T11), endpoints + error mapping 404/409/400 (T10), seed + migrations (T6, T7), test migration (T9 removes old handler test; T10 rewrites e2e; T11 updates arch marker), and the deferred items stay deferred (no progress/dedup/second-type work). ✔

**Placeholder scan:** No TBD/TODO; every code step shows complete code; the one risk (owned `HasData`) has a concrete documented fallback, not a placeholder. ✔

**Type consistency:** `SubmittedAnswer` (domain) vs `SubmittedAnswerInput` (application) vs `AnswerBody`/`AnswerInput` (host/test) are intentionally distinct DTOs at each boundary; `AttemptResultDto` fields (`ScoreCorrect`, `ScoreTotal`, `Outcome`, `PerExercise`) match the handler's construction and the e2e `AttemptResult` record; `Lesson.Create(..., IEnumerable<Exercise>? exercises = null)` optional param keeps Slice-1 callers/tests compiling; `IAttemptRepository.AddAsync` name matches impl and handler. ✔

> **One behavioral note surfaced during planning:** `Lesson.Grade` throws `InvalidOperationException` if a lesson has zero exercises. The seeded published lessons always have exercises, and the draft lesson is caught earlier by `EnsureCompletable()` (409). This edge is not surfaced as an endpoint path and is left untested by design.
