using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Infrastructure.Authentication;

public sealed class EfAuthnOperatorStore(BankDbContext dbContext) : IAuthnOperatorStore
{
    public Task<AuthnOperatorCredential?> FindByNormalizedUserNameAsync(
        string normalizedUserName,
        CancellationToken cancellationToken) =>
        dbContext.Operators
            .AsNoTracking()
            .Where(operatorEntity => operatorEntity.NormalizedUserName == normalizedUserName)
            .Select(operatorEntity => new AuthnOperatorCredential(
                operatorEntity.Id,
                operatorEntity.PasswordHash,
                operatorEntity.State,
                operatorEntity.AuthorizationStateVersion))
            .SingleOrDefaultAsync(cancellationToken);
}
