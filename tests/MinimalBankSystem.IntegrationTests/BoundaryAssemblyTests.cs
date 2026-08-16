using System.Reflection;
using System.Runtime.Versioning;
using MinimalBankSystem.IntegrationTests.Authorization;
using MinimalBankSystem.IntegrationTests.OperatorCreate;
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
    /// AUTHZ (#168) Narrow Fix contract: the AUTHZ verification surface — its probe controller,
    /// test-only Audit context, failure-injection writer, and each Critical Mutation's
    /// DI-substituted handler — is test-composition-only, never reachable from production.
    /// </summary>
    [Fact]
    public void AuthorizationVerificationSurfaceExistsOnlyInTestCompositionByConstruction()
    {
        Assembly testAssembly = typeof(BoundaryAssemblyTests).Assembly;
        Type[] verificationOnlyTypes =
        [
            typeof(AuthorizationProbeController),
            typeof(TestAuthorizationAuditContextAttribute),
            typeof(FailingAuthorizationAuditWriter),
            typeof(DisabledCheckBypassAuthorizationHandler),
            typeof(VersionCheckBypassAuthorizationHandler),
            typeof(RoleClaimAuthoritativeAuthorizationHandler),
        ];

        Assert.All(verificationOnlyTypes, type =>
        {
            Assert.NotNull(type);
            Assert.Same(testAssembly, type.Assembly);
        });

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

        Assert.DoesNotContain(nameof(AuthorizationProbeController), apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("__authz-probe", apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain(TestAuthorizationAuditContextAttribute.Operation, apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FailingAuthorizationAuditWriter), apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(DisabledCheckBypassAuthorizationHandler), apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(VersionCheckBypassAuthorizationHandler), apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(RoleClaimAuthoritativeAuthorizationHandler), apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEnvironment(\"Testing\")", apiComposition, StringComparison.Ordinal);
    }

    [Fact]
    public void OperatorCreateVerificationSurfaceExistsOnlyInTestCompositionByConstruction()
    {
        Assembly testAssembly = typeof(BoundaryAssemblyTests).Assembly;
        Type[] verificationOnlyTypes =
        [
            typeof(FailingOperatorCreateAuditWriter),
            typeof(CommitThenFailCreateAuditWriter),
            typeof(ThrowOnOperatorSaveChangesInterceptor),
            typeof(OperatorCreateAuditFailureInjectionException),
            typeof(OperatorCreateAuditAtomicityMutationException),
            typeof(OperatorCreatePersistenceInjectionException),
        ];

        Assert.All(verificationOnlyTypes, type =>
        {
            Assert.NotNull(type);
            Assert.Same(testAssembly, type.Assembly);
        });

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
        string executorPath = Path.Combine(
            RepositoryLayout.RepositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Api",
            "OperatorCreate",
            "OperatorCreateExecutor.cs");
        string executorComposition = File.ReadAllText(executorPath);

        Assert.DoesNotContain(nameof(FailingOperatorCreateAuditWriter), apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CommitThenFailCreateAuditWriter), apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ThrowOnOperatorSaveChangesInterceptor), apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("OPR-CREATE-AUD-01", apiComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEnvironment(\"Testing\")", apiComposition, StringComparison.Ordinal);
        Assert.Contains("AppendToCurrentTransactionAsync", executorComposition, StringComparison.Ordinal);
        Assert.Contains("AppendInSeparateTransactionBeforeResultAsync", executorComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("CommitThenFail", executorComposition, StringComparison.Ordinal);
    }
}
