using System.Text.Json;
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

        builder.HasData(
            Lesson.Create(new LessonId(LearningSeedIds.GreetingsLesson), new UnitId(LearningSeedIds.BasicsUnit), new Title("Greetings"),      1, isPublished: true),
            Lesson.Create(new LessonId(LearningSeedIds.SerLesson),       new UnitId(LearningSeedIds.BasicsUnit), new Title("The verb ser"),   2, isPublished: true),
            Lesson.Create(new LessonId(LearningSeedIds.CafeLesson),      new UnitId(LearningSeedIds.FoodUnit),   new Title("At the cafe"),     1, isPublished: true),
            Lesson.Create(new LessonId(LearningSeedIds.DessertLessonDraft), new UnitId(LearningSeedIds.FoodUnit), new Title("Ordering dessert"), 2, isPublished: false));
    }

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
}
