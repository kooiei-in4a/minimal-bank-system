namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Extension point that turns an exception into the common API error contract.
/// </summary>
/// <remarks>
/// A feature registers its own mapper with
/// <see cref="ApiRuntimeServiceCollectionExtensions.AddApiExceptionMapper{TMapper}"/>.
/// Mappers are consulted in registration order and the first non-null result wins. When no mapper
/// owns the exception the runtime falls back to the fixed unmapped failure contract, so an
/// unregistered failure can never disclose internal detail.
/// </remarks>
public interface IApiExceptionMapper
{
    /// <summary>
    /// Returns the API error for <paramref name="exception"/>, or <see langword="null"/> when this
    /// mapper does not own the exception.
    /// </summary>
    ApiError? Map(Exception exception);
}
