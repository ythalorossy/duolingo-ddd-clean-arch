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
