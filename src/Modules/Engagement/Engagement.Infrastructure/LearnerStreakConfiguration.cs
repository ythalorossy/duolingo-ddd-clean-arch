using Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Engagement.Infrastructure;

internal sealed class LearnerStreakConfiguration : IEntityTypeConfiguration<LearnerStreak>
{
    public void Configure(EntityTypeBuilder<LearnerStreak> builder)
    {
        builder.ToTable("LearnerStreaks");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new LearnerId(value))
            .HasColumnName("LearnerId")
            .ValueGeneratedNever();

        builder.Property(s => s.TimeZone)
            .HasConversion(tz => tz.IanaId, value => new LearnerTimeZone(value))
            .HasColumnName("TimeZoneId")
            .HasMaxLength(64);

        builder.Property(s => s.CurrentStreak);
        builder.Property(s => s.LongestStreak);
        builder.Property(s => s.LastQualifyingDate); // DateOnly? -> nullable date column

        builder.Ignore(s => s.DomainEvents);
    }
}
