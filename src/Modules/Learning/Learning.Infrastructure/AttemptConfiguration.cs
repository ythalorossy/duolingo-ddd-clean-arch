using Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learning.Infrastructure;

internal sealed class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("Attempts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AttemptId(value))
            .HasColumnName("Id")
            .ValueGeneratedNever();

        builder.Property(a => a.LearnerId)
            .HasConversion(id => id.Value, value => new LearnerId(value))
            .HasColumnName("LearnerId");

        builder.Property(a => a.LessonId)
            .HasConversion(id => id.Value, value => new LessonId(value))
            .HasColumnName("LessonId"); // id reference — a column, not a navigation

        builder.Property(a => a.SubmittedAt).HasColumnName("SubmittedAt");
        builder.Property(a => a.Outcome).HasConversion<string>().HasColumnName("Outcome");

        builder.OwnsOne(a => a.Score, s =>
        {
            s.Property(x => x.Correct).HasColumnName("ScoreCorrect");
            s.Property(x => x.Total).HasColumnName("ScoreTotal");
        });

        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.Passed); // derived from Outcome

        builder.OwnsMany(a => a.Answers, ans =>
        {
            ans.ToTable("Answers");
            ans.WithOwner().HasForeignKey("AttemptId");

            // Store-generated surrogate key so freshly-added answers INSERT (mirrors AppliedAward).
            ans.Property<int>("Id").ValueGeneratedOnAdd();
            ans.HasKey("Id");

            ans.Property(x => x.ExerciseId)
                .HasConversion(id => id.Value, value => new ExerciseId(value))
                .HasColumnName("ExerciseId");
            ans.Property(x => x.SelectedChoiceIndex).HasColumnName("SelectedChoiceIndex");
            ans.Property(x => x.WasCorrect).HasColumnName("WasCorrect");
        });

        builder.Navigation(a => a.Answers)
            .HasField("_answers")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
