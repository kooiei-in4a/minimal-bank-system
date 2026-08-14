using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Infrastructure.Authentication;

public sealed record AuthnOperatorCredential(
    Guid Id,
    string PersistedPasswordHash,
    OperatorState State,
    int AuthorizationStateVersion);
