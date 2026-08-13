using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests;

public sealed class BoundaryAssemblyTests
{
    private const string ExpectedTargetFramework = ".NETCoreApp,Version=v10.0";

    private static readonly string[] ProductionAssemblyNames =
        ["MinimalBankSystem.Api", "MinimalBankSystem.Infrastructure", "MinimalBankSystem.Migrator"];

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

    /// <summary>WP2-ID-01: preserves the single-<see cref="DbContext"/> topology required by Issue #165.</summary>
    [Fact]
    public void ExactlyOneDbContextExistsAcrossTheProductionAssemblies()
    {
        Type[] dbContextTypes =
        [
            .. ProductionAssemblyNames
                .Select(name => Assembly.Load(new AssemblyName(name)))
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(DbContext).IsAssignableFrom(type) && !type.IsAbstract),
        ];

        Type dbContextType = Assert.Single(dbContextTypes);
        Assert.Equal(typeof(BankDbContext), dbContextType);
    }

    /// <summary>
    /// WP2-ID-01 verification requirement #6: integration-test-only Operator seed data must be
    /// unreachable from production paths. No production assembly may reference the test assembly
    /// that owns that seed data, so unreachability holds by compile-time construction.
    /// </summary>
    [Theory]
    [InlineData("MinimalBankSystem.Api")]
    [InlineData("MinimalBankSystem.Infrastructure")]
    [InlineData("MinimalBankSystem.Migrator")]
    public void ProductionAssembliesNeverReferenceTheIntegrationTestAssembly(string assemblyName)
    {
        Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
        AssemblyName[] referenced = assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            referenced,
            reference => reference.Name!.Contains("IntegrationTests", StringComparison.Ordinal)
                || reference.Name!.Contains("Test", StringComparison.Ordinal));
    }
}
