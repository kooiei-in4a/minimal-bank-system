using System.Reflection;
using System.Runtime.Versioning;
using MinimalBankSystem.IntegrationTests.Authorization;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;

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
    public void AuditFailureInjectionExistsOnlyInTestCompositionByConstruction()
    {
        Type injectionType = typeof(ThrowOnAuditSaveChangesInterceptor);
        Assembly testAssembly = typeof(BoundaryAssemblyTests).Assembly;

        Assert.Same(testAssembly, injectionType.Assembly);

        foreach (string productionAssemblyName in new[]
                 {
                     "MinimalBankSystem.Api",
                     "MinimalBankSystem.Application",
                     "MinimalBankSystem.Domain",
                     "MinimalBankSystem.Infrastructure",
                     "MinimalBankSystem.Migrator",
                 })
        {
            Assembly productionAssembly = Assembly.Load(new AssemblyName(productionAssemblyName));
            Assert.DoesNotContain(
                productionAssembly.GetReferencedAssemblies(),
                reference => reference.Name == testAssembly.GetName().Name);
        }

        string apiComposition = File.ReadAllText(Path.Combine(
            RepositoryLayout.RepositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Api",
            "Program.cs"));

        Assert.DoesNotContain(nameof(ThrowOnAuditSaveChangesInterceptor), apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("AddInterceptors", apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("AUDIT_FAILURE", apiComposition, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AUTHZ (#168) Narrow Fix contract: the AUTHZ verification surface — its probe controller and
    /// each Critical Mutation's DI-substituted handler — is test-composition-only, never reachable
    /// from production. It is a distinct surface from the AUTHN probe (#167) and is not reused or
    /// modified here.
    /// </summary>
    [Fact]
    public void AuthorizationVerificationSurfaceExistsOnlyInTestCompositionByConstruction()
    {
        Assembly testAssembly = typeof(BoundaryAssemblyTests).Assembly;
        Type[] verificationOnlyTypes =
        [
            typeof(AuthorizationProbeController),
            typeof(ThrowingAuditWriter),
            Type.GetType(
                "MinimalBankSystem.IntegrationTests.PostgreSql.DisabledCheckBypassAuthorizationHandler, MinimalBankSystem.IntegrationTests")!,
            Type.GetType(
                "MinimalBankSystem.IntegrationTests.PostgreSql.VersionCheckBypassAuthorizationHandler, MinimalBankSystem.IntegrationTests")!,
            Type.GetType(
                "MinimalBankSystem.IntegrationTests.PostgreSql.RoleClaimAuthoritativeAuthorizationHandler, MinimalBankSystem.IntegrationTests")!,
        ];

        Assert.All(verificationOnlyTypes, type =>
        {
            Assert.NotNull(type);
            Assert.Same(testAssembly, type.Assembly);
        });

        string apiComposition = File.ReadAllText(Path.Combine(
            RepositoryLayout.RepositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Api",
            "Program.cs"));

        Assert.DoesNotContain(nameof(AuthorizationProbeController), apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain(AuthorizationProbeController.AdministratorOnlyPolicy, apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain(AuthorizationProbeController.AnyCurrentOperatorPolicy, apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("__authz-probe", apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("DisabledCheckBypassAuthorizationHandler", apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("VersionCheckBypassAuthorizationHandler", apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("RoleClaimAuthoritativeAuthorizationHandler", apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("ThrowingAuditWriter", apiComposition, StringComparison.Ordinal);
    }
}
