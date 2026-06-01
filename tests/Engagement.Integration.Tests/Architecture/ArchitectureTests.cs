using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Engagement.Integration.Tests.Architecture;

public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(global::Engagement.Domain.LearnerEngagement).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(global::Engagement.Application.GetLearnerEngagement).Assembly;
    private static readonly Assembly HostAssembly = typeof(Program).Assembly;

    [Fact] // Criterion 5: domain depends on nothing infrastructural
    public void Domain_does_not_depend_on_EfCore_or_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact] // Application stays off infrastructure too
    public void Application_does_not_depend_on_EfCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact] // Criterion 6: nothing outside the Engagement module uses its DOMAIN types directly
    public void Host_does_not_depend_on_Engagement_Domain()
    {
        var result = Types.InAssembly(HostAssembly)
            .ShouldNot()
            .HaveDependencyOn("Engagement.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? "ok"
            : "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
