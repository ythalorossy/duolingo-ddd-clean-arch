using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Learning.Integration.Tests.Architecture;

public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(global::Learning.Domain.Lesson).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(global::Learning.Application.CompleteLesson).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(global::Learning.Infrastructure.LearningDbContext).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_EfCore_or_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_EfCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Learning_does_not_depend_on_Engagement()
    {
        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Engagement")
                .GetResult();

            Assert.True(result.IsSuccessful, $"{assembly.GetName().Name}: {Describe(result)}");
        }
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? "ok"
            : "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
