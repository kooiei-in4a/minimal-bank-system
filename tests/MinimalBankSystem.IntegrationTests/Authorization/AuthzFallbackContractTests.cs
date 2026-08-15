extern alias api;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Api.Runtime.Authorization;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.IntegrationTests.Authentication;

namespace MinimalBankSystem.IntegrationTests.Authorization;

/// <summary>
/// Verification items 1, 3, 9, 10 and the production wiring scan: the deny-by-default fallback
/// policy rejects anonymous requests before any feature handler, routing errors keep their
/// contract, explicitly anonymous endpoints stay reachable, the AUTHN probe still performs
/// authentication only, and production Program registers no test-only surface.
/// </summary>
public sealed class AuthzFallbackContractTests
{
    [Fact]
    public async Task AnonymousRequestToDefaultProtectedEndpointIsRejectedWith401BeforeTheHandler()
    {
        using AuthorizationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(AuthzFeatureController.DefaultDenyPath);

        await AssertRejectedAsync(response, HttpStatusCode.Unauthorized, "anonymous-default-deny");
    }

    [Fact]
    public async Task AnonymousRequestToRoleProtectedEndpointIsRejectedWith401BeforeTheHandler()
    {
        using AuthorizationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(AuthzFeatureController.AdministratorOnlyPath);

        await AssertRejectedAsync(response, HttpStatusCode.Unauthorized, "anonymous-role-protected");
    }

    [Fact]
    public async Task ValidJwtWithoutCurrentOperatorResolutionCanNeverReachTheDefaultProtectedEndpoint()
    {
        // Fail closed: a valid JWT alone is not authorization. Without a database the current
        // Operator cannot be resolved and the request must become a safe internal error, never
        // a handler-reached success.
        using AuthorizationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(Guid.NewGuid(), 1);

        using HttpRequestMessage request = new(HttpMethod.Get, AuthzFeatureController.DefaultDenyPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("handlerReached", body, StringComparison.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("internal_error", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("An internal error occurred.", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ValidJwtReachesTheAuthenticationOnlyProbeWithoutAnyOperatorResolution()
    {
        // The AUTHN probe uses the default [Authorize] policy: an authenticated principal passes
        // without current-Operator resolution. This factory has no database connection, so a
        // handler that attempted operator resolution would fail closed with HTTP 500.
        using AuthenticationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(Guid.NewGuid(), 1);

        using HttpRequestMessage request = new(HttpMethod.Get, AuthenticationProbeController.ProbePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
    }

    [Theory]
    [InlineData("POST", AuthzFeatureController.DefaultDenyPath, 405, "method_not_allowed")]
    [InlineData("DELETE", AuthzFeatureController.AdministratorOnlyPath, 405, "method_not_allowed")]
    [InlineData("GET", "/__authz/does-not-exist", 404, "endpoint_not_found")]
    public async Task RoutingErrorsKeepTheirContractUnderTheFallbackPolicy(
        string requestMethod,
        string path,
        int expectedStatusCode,
        string expectedCode)
    {
        using AuthorizationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();

        using HttpRequestMessage request = new(new HttpMethod(requestMethod), path);
        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)expectedStatusCode, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExplicitlyAnonymousEndpointsRemainAnonymousUnderTheFallbackPolicy()
    {
        using AuthorizationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage health = await client.GetAsync(HealthContract.LivePath);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        using HttpResponseMessage feature = await client.GetAsync(AuthzFeatureController.AnonymousPath);
        Assert.Equal(HttpStatusCode.OK, feature.StatusCode);
    }

    [Fact]
    public async Task ProductionProgramWiresDenyByDefaultFallbackAndNamedRolePolicies()
    {
        using AuthorizationProbeApiFactory factory = new(TestJwtConfiguration.SigningKey);
        IAuthorizationPolicyProvider policies =
            factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        AuthorizationPolicy fallback = (await policies.GetFallbackPolicyAsync())!;
        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(fallback.Requirements, requirement => requirement is CurrentOperatorRequirement);

        AuthorizationPolicy administrator = (await policies.GetPolicyAsync(AuthorizationPolicies.AdministratorOnly))!;
        Assert.NotNull(administrator);
        Assert.Contains(administrator.Requirements, requirement => requirement is CurrentOperatorRequirement);
        OperatorRoleRequirement administratorRole = Assert.IsType<OperatorRoleRequirement>(
            Assert.Single(administrator.Requirements.OfType<OperatorRoleRequirement>()));
        Assert.Equal([OperatorRole.Administrator], administratorRole.AllowedRoles);

        AuthorizationPolicy tellerOrAdministrator =
            (await policies.GetPolicyAsync(AuthorizationPolicies.TellerOrAdministrator))!;
        Assert.NotNull(tellerOrAdministrator);
        OperatorRoleRequirement tellerOrAdministratorRole = Assert.IsType<OperatorRoleRequirement>(
            Assert.Single(tellerOrAdministrator.Requirements.OfType<OperatorRoleRequirement>()));
        Assert.Equal([OperatorRole.Teller, OperatorRole.Administrator], tellerOrAdministratorRole.AllowedRoles);
    }

    [Fact]
    public void ProductionProgramExposesNoTestOnlyFeatureEndpoints()
    {
        using WebApplicationFactory<api::Program> factory = new ProductionProgramApiFactory();
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
                template.StartsWith("__", StringComparison.Ordinal),
                $"Production Program exposes a test-only route template '{template}'.");
        }
    }

    private static async Task AssertRejectedAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string scenario)
    {
        string body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            throw new Xunit.Sdk.XunitException(
                $"AUTHZ fallback rejection returned HTTP 500; production error contract was weakened. " +
                $"Scenario: {scenario}. Body: {body}");
        }

        Assert.Equal(expectedStatusCode, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(body);
        string expectedCode = expectedStatusCode == HttpStatusCode.Unauthorized
            ? "authentication_required"
            : "operation_not_permitted";
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("handlerReached", body, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateToken(Guid subject, int authorizationStateVersion)
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            "minimal-bank-system",
            "minimal-bank-system-api",
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject.ToString("D")),
                new Claim(
                    AuthnClaimTypes.AuthorizationStateVersion,
                    authorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
            ],
            now.AddMinutes(-1),
            now.AddMinutes(5),
            new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

internal sealed class ProductionProgramApiFactory : WebApplicationFactory<api::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(JwtAuthnOptions.SigningKeyConfigurationKey, TestJwtConfiguration.SigningKey);
    }
}
