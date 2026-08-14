using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Infrastructure.Authentication;
using System.IdentityModel.Tokens.Jwt;

namespace MinimalBankSystem.IntegrationTests.Authentication;

/// <summary>
/// AUTHN owns a test-host-only authentication-protected verification endpoint that proves the JWT
/// bearer authentication gate itself. It never resolves current Operator active/disabled state,
/// authorization-state-version comparison, or current DB role; those remain AUTHZ (#168) ownership.
/// This controller is never registered by production <c>Program.cs</c> — it is added as an
/// application part only by <see cref="AuthenticationProbeApiFactory"/>.
/// </summary>
[ApiController]
public sealed class AuthenticationProbeController : ControllerBase
{
    public const string ProbePath = "/__authn-probe/reached";

    [Authorize]
    [HttpGet(ProbePath)]
    public IActionResult Reached() =>
        Ok(new AuthenticationProbeResponse(
            HandlerReached: true,
            Subject: User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value));
}

public sealed record AuthenticationProbeResponse(bool HandlerReached, string? Subject);

internal sealed class AuthenticationProbeApiFactory(
    string signingKey,
    string? connectionString = null,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(JwtTokenSettings.SigningKeyEnvironmentVariable, signingKey);

        if (connectionString is not null)
        {
            builder.UseSetting("ConnectionStrings:Database", connectionString);
        }

        builder.ConfigureServices(services =>
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(AuthenticationProbeController).Assembly);

            configureServices?.Invoke(services);
        });
    }
}
