using System.Reflection;
using System.Runtime.Versioning;

namespace MinimalBankSystem.IntegrationTests;

public sealed class BoundaryAssemblyTests
{
    private const string ExpectedTargetFramework = ".NETCoreApp,Version=v10.0";

    [Theory]
    [InlineData("MinimalBankSystem.Api")]
    [InlineData("MinimalBankSystem.Infrastructure")]
    public void FoundationAssembliesTargetNet10(string assemblyName)
    {
        Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
        TargetFrameworkAttribute? targetFramework =
            assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        Assert.NotNull(targetFramework);
        Assert.Equal(ExpectedTargetFramework, targetFramework!.FrameworkName);
    }
}
