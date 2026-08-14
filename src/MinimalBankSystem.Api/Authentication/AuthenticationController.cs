using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.Api.Authentication;

[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController(
    BankDbContext dbContext,
    JwtTokenIssuer tokenIssuer) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        string normalizedUserName = request.UserName!.Trim().ToUpperInvariant();
        Operator? operatorEntity = await dbContext.Operators
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.NormalizedUserName == normalizedUserName,
                cancellationToken);

        PasswordVerificationResult verification = operatorEntity is null
            ? PasswordVerificationResult.Failed
            : IdentityPassword.Verify(operatorEntity, request.Password!);

        if (operatorEntity is null ||
            verification is not (PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded) ||
            operatorEntity.State != OperatorState.Active)
        {
            return Unauthorized(ApiErrorEnvelope.AuthenticationRequired);
        }

        IssuedJwtToken token = tokenIssuer.Issue(
            operatorEntity.Id,
            operatorEntity.AuthorizationStateVersion,
            operatorEntity.Role.ToString().ToLowerInvariant());

        return Ok(new LoginResponse(
            token.AccessToken,
            "Bearer",
            (long)JwtTokenParameters.AccessTokenLifetime.TotalSeconds,
            token.ExpiresAt));
    }
}

public sealed record LoginRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    string? UserName,
    [property: System.ComponentModel.DataAnnotations.Required]
    string? Password);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    long ExpiresIn,
    DateTimeOffset ExpiresAt);
