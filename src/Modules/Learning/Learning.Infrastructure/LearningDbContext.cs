using Learning.Domain;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure;

// Plain DbContext: Learning aggregates raise no domain events, so (unlike EngagementDbContext)
// there is no dispatcher and no SaveChanges override. Slice 1 is read-only at runtime.
public sealed class LearningDbContext(DbContextOptions<LearningDbContext> options) : DbContext(options)
{
    public const string Schema = "learning";

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Lesson> Lessons => Set<Lesson>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new CourseConfiguration());
        modelBuilder.ApplyConfiguration(new UnitConfiguration());
        modelBuilder.ApplyConfiguration(new LessonConfiguration());
    }
}
