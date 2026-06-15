using Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Engagement.Infrastructure;

internal sealed class LeagueWeekSettlementConfiguration : IEntityTypeConfiguration<LeagueWeekSettlement>
{
    public void Configure(EntityTypeBuilder<LeagueWeekSettlement> builder)
    {
        builder.ToTable("LeagueWeekSettlements");

        builder.HasKey(s => s.Week);
        builder.Property(s => s.Week)
            .HasConversion(w => w.Start, value => new LeagueWeek(value))
            .HasColumnName("WeekStart")
            .ValueGeneratedNever();

        builder.Property(s => s.SettledAt);

        builder.Ignore(s => s.DomainEvents);
    }
}
