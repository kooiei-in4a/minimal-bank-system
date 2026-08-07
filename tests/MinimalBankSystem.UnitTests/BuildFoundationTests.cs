using System.Reflection;
using System.Runtime.Versioning;

namespace MinimalBankSystem.UnitTests;

/// <summary>
/// Guards the build foundation established by FND-01.
/// <para>
/// Each project this test project depends on must be built for the target
/// framework fixed by ADR-0001. Because the solution is still an empty skeleton,
/// the assemblies are resolved by name rather than through a type reference.
/// </para>
/// <para>
/// These tests also demonstrate that the test runner discovers and executes
/// tests, so a broken test setup fails the build instead of passing silently.
/// </para>
/// </summary>
public sealed class BuildFoundationTests
{
    private const string ExpectedTargetFramework = ".NETCoreApp,Version=v10.0";

    [Theory]
    [InlineData("MinimalBankSystem.Domain")]
    [InlineData("MinimalBankSystem.Application")]
    public void ReferencedProjectTargetsTheFrameworkFixedByAdr0001(string assemblyName)
    {
        Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));

        TargetFrameworkAttribute? targetFramework =
            assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        Assert.NotNull(targetFramework);
        Assert.Equal(ExpectedTargetFramework, targetFramework.FrameworkName);
    }
}
