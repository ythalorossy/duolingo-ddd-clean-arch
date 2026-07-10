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
