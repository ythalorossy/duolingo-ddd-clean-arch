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
