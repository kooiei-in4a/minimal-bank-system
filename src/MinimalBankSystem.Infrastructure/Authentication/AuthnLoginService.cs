using Microsoft.AspNetCore.Identity;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.Infrastructure.Authentication;

public sealed class AuthnLoginService(IAuthnOperatorStore operatorStore)
{
    public async Task<AuthnLoginResult?> TryAuthenticateAsync(
        string? userName,
        string? plaintextPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(plaintextPassword))
        {
            return null;
        }

        string normalizedUserName = userName.Trim().ToUpperInvariant();
        AuthnOperatorCredential? candidate =
            await operatorStore.FindByNormalizedUserNameAsync(normalizedUserName, cancellationToken)
                .ConfigureAwait(false);

        if (candidate is null)
        {
            return null;
        }

        PasswordVerificationResult verification =
            IdentityPassword.VerifyHash(candidate.PersistedPasswordHash, plaintextPassword);

        if (verification is not (PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded) ||
            candidate.State is not OperatorState.Active)
        {
            return null;
        }

        return new AuthnLoginResult(candidate.Id, candidate.AuthorizationStateVersion);
    }
}

public sealed record AuthnLoginResult(Guid OperatorId, int AuthorizationStateVersion);
