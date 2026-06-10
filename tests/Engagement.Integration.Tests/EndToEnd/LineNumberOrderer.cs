using Xunit.Abstractions;
using Xunit.Sdk;

namespace Engagement.Integration.Tests.EndToEnd;

/// <summary>
/// Orders test cases by their MetadataToken, which the C# compiler assigns in
/// declaration order for non-partial types. This guarantees that test methods
/// run in the order they appear in the source file — critical for test classes
/// that share a monotonically-advancing FakeTimeProvider (where each test's
/// date range must be strictly later than the previous test's).
/// </summary>
public sealed class LineNumberOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        return testCases
            .OrderBy(GetDeclarationOrder)
            .ThenBy(tc => tc.TestMethod.Method.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetDeclarationOrder<TTestCase>(TTestCase testCase) where TTestCase : ITestCase
    {
        // ReflectionMethodInfo is the concrete type xUnit uses at runtime.
        // MethodBase.MetadataToken is assigned by the C# compiler in declaration
        // order, making it a reliable proxy for "line number" within a type.
        if (testCase.TestMethod.Method is ReflectionMethodInfo reflMethod)
            return reflMethod.MethodInfo.MetadataToken;

        // Fallback (non-reflection runners only): return a constant so the ThenBy(name)
        // clause in OrderTestCases breaks ties alphabetically — deterministic, unlike
        // string.GetHashCode(), which is randomized per process on .NET Core.
        return 0;
    }
}
