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
