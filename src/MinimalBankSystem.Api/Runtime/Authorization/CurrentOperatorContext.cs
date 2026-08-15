using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authorization;

namespace MinimalBankSystem.Api.Runtime.Authorization;

/// <summary>
/// Scoped per-request cache of the current Operator resolution. The authorization handler writes
/// it once; the authorization result handler reads the same snapshot when it composes the
/// policy-rejection Audit record.
/// </summary>
public sealed class CurrentOperatorContext
{
    public CurrentOperatorResolution? Resolution { get; private set; }

    public Operator? CurrentOperator => Resolution?.Operator;

    public void SetResolution(CurrentOperatorResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        Resolution = resolution;
    }
}
