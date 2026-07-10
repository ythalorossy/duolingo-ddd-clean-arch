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
