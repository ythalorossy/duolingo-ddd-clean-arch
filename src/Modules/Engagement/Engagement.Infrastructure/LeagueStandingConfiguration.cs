using Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Engagement.Infrastructure;

internal sealed class LeagueStandingConfiguration : IEntityTypeConfiguration<LeagueStanding>
{
    public void Configure(EntityTypeBuilder<LeagueStanding> builder)
    {
        builder.ToTable("LeagueStandings");

        // Composite key (LearnerId, WeekStart). Each member is a value-converted VO with
        // value-equality, so EF tracks the key correctly without a custom ValueComparer.
        builder.HasKey(s => new { s.Id, s.Week });

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new LearnerId(value))
            .HasColumnName("LearnerId")
            .ValueGeneratedNever();

        builder.Property(s => s.Week)
            .HasConversion(w => w.Start, value => new LeagueWeek(value))
            .HasColumnName("WeekStart")
            .ValueGeneratedNever();

        builder.Property(s => s.Tier)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(s => s.WeeklyXp)
            .HasConversion(xp => xp.Value, value => new Xp(value))
            .HasColumnName("WeeklyXp");

        builder.Ignore(s => s.DomainEvents);
    }
}
