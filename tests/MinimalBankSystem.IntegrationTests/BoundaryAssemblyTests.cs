using System.Reflection;
using System.Runtime.Versioning;

namespace MinimalBankSystem.IntegrationTests;

/// <summary>
/// Assembly-level proof that production code cannot reference the integration-test seed assembly.
/// The seed remains test-only by construction rather than by a runtime environment flag.
/// </summary>
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

    [Theory]
    [InlineData("MinimalBankSystem.Api")]
    [InlineData("MinimalBankSystem.Application")]
    [InlineData("MinimalBankSystem.Domain")]
    [InlineData("MinimalBankSystem.Infrastructure")]
    [InlineData("MinimalBankSystem.Migrator")]
    public void ProductionAssembliesNeverReferenceTheIntegrationTestAssembly(string assemblyName)
    {
        Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
        AssemblyName[] referenced = assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            referenced,
            reference => reference.Name!.Contains("IntegrationTests", StringComparison.Ordinal));
    }

    [Fact]
    public void DeterministicAuditFailureInjectionExistsOnlyInTheTestAssembly()
    {
        Assembly testAssembly = typeof(BoundaryAssemblyTests).Assembly;
        Type failureInjector = typeof(PostgreSql.RejectAuditSaveChangesInterceptor);

        Assert.Equal(testAssembly, failureInjector.Assembly);
        Assert.DoesNotContain(
            [
                Assembly.Load(new AssemblyName("MinimalBankSystem.Api")),
                Assembly.Load(new AssemblyName("MinimalBankSystem.Application")),
                Assembly.Load(new AssemblyName("MinimalBankSystem.Domain")),
                Assembly.Load(new AssemblyName("MinimalBankSystem.Infrastructure")),
                Assembly.Load(new AssemblyName("MinimalBankSystem.Migrator")),
            ],
            assembly => assembly.GetReferencedAssemblies()
                .Any(reference => reference.Name == testAssembly.GetName().Name));
    }
}
