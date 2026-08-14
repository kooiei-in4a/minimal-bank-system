namespace MinimalBankSystem.Infrastructure.Authentication;

public interface IAuthnOperatorStore
{
    Task<AuthnOperatorCredential?> FindByNormalizedUserNameAsync(
        string normalizedUserName,
        CancellationToken cancellationToken);
}
