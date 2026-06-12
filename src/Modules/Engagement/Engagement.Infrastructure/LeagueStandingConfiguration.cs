using Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Engagement.Infrastructure;

internal sealed class LeagueStandingConfiguration : IEntityTypeConfiguration<LeagueStanding>
{
    public void Configure(EntityTypeBuilder<LeagueStanding> builder)
    {
        builder.ToTable("LeagueStandings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new LearnerId(value))
            .HasColumnName("LearnerId")
            .ValueGeneratedNever();

        builder.Property(s => s.Tier)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(s => s.Week)
            .HasConversion(w => w.Start, value => new LeagueWeek(value))
            .HasColumnName("WeekStart");

        builder.Property(s => s.WeeklyXp)
            .HasConversion(xp => xp.Value, value => new Xp(value))
            .HasColumnName("WeeklyXp");

        builder.Ignore(s => s.DomainEvents);
    }
}
