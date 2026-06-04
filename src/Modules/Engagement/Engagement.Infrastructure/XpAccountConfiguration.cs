using Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Engagement.Infrastructure;

internal sealed class XpAccountConfiguration : IEntityTypeConfiguration<XpAccount>
{
    public void Configure(EntityTypeBuilder<XpAccount> builder)
    {
        builder.ToTable("XpAccounts");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new LearnerId(value))
            .HasColumnName("LearnerId")
            .ValueGeneratedNever();

        builder.Property(l => l.TotalXp)
            .HasConversion(xp => xp.Value, value => new Xp(value))
            .HasColumnName("TotalXp");

        // Domain events are not persisted.
        builder.Ignore(l => l.DomainEvents);

        // Idempotency ledger as an owned collection -> engagement.AppliedAwards.
        builder.OwnsMany(l => l.AppliedAwards, owned =>
        {
            owned.ToTable("AppliedAwards");
            owned.WithOwner().HasForeignKey("LearnerId");

            // Store-generated shadow surrogate key. A composite (LearnerId, SourceId) key made
            // EF treat a NEW award on a reloaded (existing) account as Modified -> UPDATE (0 rows
            // -> DbUpdateConcurrencyException). A generated key gives new dependents a temporary
            // key value, so EF correctly INSERTs them.
            owned.Property<int>("Id").ValueGeneratedOnAdd();
            owned.HasKey("Id");

            // Idempotency backstop in the database; the domain also guards in-memory in AwardXp.
            owned.HasIndex("LearnerId", nameof(AppliedAward.SourceId)).IsUnique();

            owned.Property(a => a.SourceId);
            owned.Property(a => a.Amount);
            owned.Property(a => a.AppliedAt);
        });

        builder.Navigation(l => l.AppliedAwards)
            .HasField("_appliedAwards")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
