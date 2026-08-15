extern alias api;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.Authorization;

/// <summary>
/// Production composition ownership for AUTHZ named role policies and absence of test-only
/// verification surfaces from production Program.
/// </summary>
public sealed class AuthzProductionCompositionTests
{
    [Fact]
    public async Task ProductionProgramWiresDenyByDefaultFallbackAndNamedRolePolicies()
    {
        await using ProductionProgramApiFactory factory = new();
        IAuthorizationPolicyProvider policies =
            factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        AuthorizationPolicy fallback = (await policies.GetFallbackPolicyAsync())!;
        Assert.NotNull(fallback);
        Assert.Contains(
            fallback.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(
            fallback.Requirements,
            requirement => requirement is CurrentOperatorRequirement);
        Assert.DoesNotContain(
            fallback.Requirements,
            requirement => requirement is CurrentOperatorRoleRequirement);

        AuthorizationPolicy administrator =
            (await policies.GetPolicyAsync(CurrentOperatorPolicyNames.Administrator))!;
        Assert.NotNull(administrator);
        Assert.Contains(
            administrator.Requirements,
            requirement => requirement is CurrentOperatorRequirement);
        CurrentOperatorRoleRequirement administratorRole = Assert.IsType<CurrentOperatorRoleRequirement>(
            Assert.Single(administrator.Requirements.OfType<CurrentOperatorRoleRequirement>()));
        Assert.Equal([OperatorRole.Administrator], administratorRole.PermittedRoles);

        AuthorizationPolicy administratorOrTeller =
            (await policies.GetPolicyAsync(CurrentOperatorPolicyNames.AdministratorOrTeller))!;
        Assert.NotNull(administratorOrTeller);
        CurrentOperatorRoleRequirement tellerOrAdministratorRole =
            Assert.IsType<CurrentOperatorRoleRequirement>(
                Assert.Single(administratorOrTeller.Requirements.OfType<CurrentOperatorRoleRequirement>()));
        Assert.Equal(
            [OperatorRole.Administrator, OperatorRole.Teller],
            tellerOrAdministratorRole.PermittedRoles.ToArray());
    }

    [Fact]
    public void ProductionProgramExposesNoTestOnlyAuthzEndpoints()
    {
        using ProductionProgramApiFactory factory = new();
        IActionDescriptorCollectionProvider descriptors =
            factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>();

        Assert.NotEmpty(descriptors.ActionDescriptors.Items);

        foreach (ActionDescriptor action in descriptors.ActionDescriptors.Items)
        {
            string? template = action.AttributeRouteInfo?.Template;
            if (template is null)
            {
                continue;
            }

            Assert.False(
                template.Contains("__authz-probe", StringComparison.Ordinal) ||
                template.StartsWith("__", StringComparison.Ordinal),
                $"Production Program exposes a test-only route template '{template}'.");
        }
    }

    private sealed class ProductionProgramApiFactory : WebApplicationFactory<api::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting(JwtAuthnOptions.SigningKeyConfigurationKey, TestJwtConfiguration.SigningKey);
            // Persistence is registered lazily; a syntactically valid placeholder is enough for
            // composition assertions that never open a connection.
            builder.UseSetting(
                $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
                "Host=127.0.0.1;Port=1;Database=composition-check;Username=composition;Password=composition");
        }
    }
}
