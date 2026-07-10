using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Learning.Infrastructure;

// Used ONLY by the EF CLI at design time (migrations). Runtime wiring uses AddLearningInfrastructure.
public sealed class LearningDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=DuolingoLearning_Design;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new LearningDbContext(options);
    }
}
