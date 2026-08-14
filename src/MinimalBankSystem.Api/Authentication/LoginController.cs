using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.Api.Authentication;

/// <summary>
/// Credential verification / login admission / JWT issuance owned by WP2-AUTHN-01. Consumes the
/// Operator persistence and password-hashing semantics established by WP2-ID-01; it does not
/// invent a second password-persistence model, and it never rewrites the stored password hash
/// (including on <see cref="PasswordVerificationResult.SuccessRehashNeeded"/>).
/// </summary>
[ApiController]
public sealed class LoginController(
    BankDbContext dbContext,
    JwtTokenIssuer tokenIssuer) : ControllerBase
{
    private static readonly ApiErrorEnvelope AuthenticationRequired =
        new("authentication_required", "Authentication is required.");

    [HttpPost("/login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(AuthenticationRequired);
        }

        string normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        Operator? operatorEntity = await dbContext.Operators
            .SingleOrDefaultAsync(
                candidate => candidate.NormalizedUserName == normalizedUserName,
                cancellationToken);

        if (operatorEntity is null)
        {
            return Unauthorized(AuthenticationRequired);
        }

        // SuccessRehashNeeded is accepted as successful credential verification for login. AUTHN
        // does not rewrite the stored password hash; password persistence/rehash writes remain
        // outside this Issue's ownership.
        PasswordVerificationResult verification = IdentityPassword.Verify(operatorEntity, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(AuthenticationRequired);
        }

        // Login-time disabled-Operator rejection. Authenticated request-time state resolution
        // remains AUTHZ-owned; this check only gates JWT issuance at login.
        if (operatorEntity.State != OperatorState.Active)
        {
            return Unauthorized(AuthenticationRequired);
        }

        string accessToken = tokenIssuer.IssueAccessToken(operatorEntity);
        return Ok(new LoginResponse(
            accessToken,
            "Bearer",
            (int)JwtTokenSettings.AccessTokenLifetime.TotalSeconds));
    }
}

public sealed record LoginRequest(
    [property: Required] string? UserName,
    [property: Required] string? Password);

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresInSeconds);
