using System.Reflection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MinimalBankSystem.Infrastructure.Auditing;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;

namespace MinimalBankSystem.IntegrationTests;

/// <summary>
/// Structural proof that deterministic Audit failure exists only in test composition. Production
/// has no interceptor, test-assembly reference, environment/configuration selector or request
/// surface capable of selecting the failure behavior.
/// </summary>
public sealed class AuditProductionBoundaryTests
{
    [Fact]
    public void ProductionCompositionHasNoFailureInterceptorOrSelectableFailureImplementation()
    {
        Assembly infrastructure = typeof(AuditWriter).Assembly;
        Type[] productionInterceptors =
        [
            .. infrastructure.GetTypes().Where(type =>
                typeof(IInterceptor).IsAssignableFrom(type) && !type.IsInterface),
        ];
        Assert.Empty(productionInterceptors);

        Assert.Contains(
            typeof(AuditFailureInterceptor),
            typeof(AuditFailureInterceptor).Assembly.GetTypes().Where(type =>
                typeof(IInterceptor).IsAssignableFrom(type) && !type.IsInterface));

        ConstructorInfo constructor = Assert.Single(typeof(AuditWriter).GetConstructors());
        Assert.Equal(
            [
                typeof(BankDbContext),
                typeof(AuditOperationRegistry),
                typeof(TimeProvider),
            ],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));

        string productionSource = string.Join(
            '\n',
            Directory.EnumerateFiles(
                Path.Combine(RepositoryLayout.RepositoryRoot.FullName, "src"),
                "*.cs",
                SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file))
                .Select(File.ReadAllText));
        Assert.DoesNotContain(nameof(AuditFailureInterceptor), productionSource, StringComparison.Ordinal);
        Assert.DoesNotContain(AuditFailureInjectionException.SemanticSignature, productionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ForceAuditFailure", productionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Audit:Failure", productionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AUDIT_FAILURE", productionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AddInterceptors", productionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyProductionRegistryFailsClosedUntilOwningFeaturesRegisterOperations()
    {
        AuditOperationRegistry registry = new([]);

        Assert.Equal(0, registry.Count);
        Assert.False(registry.IsRegistered("request.force.failure"));
        Assert.False(registry.IsRegistered("environment.force.failure"));
        Assert.False(registry.IsRegistered("configuration.force.failure"));
    }

    private static bool IsBuildOutput(string path)
    {
        char separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}bin{separator}", StringComparison.Ordinal);
    }
}
