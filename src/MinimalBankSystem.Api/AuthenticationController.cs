using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Infrastructure.Authentication;

namespace MinimalBankSystem.Api;

[ApiController]
[Route("auth")]
public sealed class AuthenticationController(
    AuthnLoginService loginService,
    IJwtAccessTokenIssuer tokenIssuer) : ControllerBase
{
    // AUTHZ (#168) default-deny fallback protects every endpoint without explicit authorization
    // metadata. Login is explicitly anonymous: it is the one endpoint that establishes the
    // current Operator's authenticated state in the first place.
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthnLoginResult? login = await loginService
            .TryAuthenticateAsync(request.UserName, request.Password, cancellationToken)
            .ConfigureAwait(false);

        if (login is null)
        {
            return Unauthorized(ApiErrorEnvelope.AuthenticationRequired);
        }

        return Ok(new LoginResponse(tokenIssuer.Issue(login)));
    }
}

public sealed record LoginRequest(string? UserName, string? Password);

public sealed record LoginResponse(string AccessToken);
