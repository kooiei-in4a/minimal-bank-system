using System.Diagnostics.CodeAnalysis;

namespace MinimalBankSystem.Api.ErrorHandling;

// Extension point for later issues; no implementations are registered here.
public interface IApiExceptionMapper
{
    bool TryMap(Exception exception, [NotNullWhen(true)] out ApiError? mappedError);
}
