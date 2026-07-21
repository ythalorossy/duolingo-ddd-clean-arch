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
}
