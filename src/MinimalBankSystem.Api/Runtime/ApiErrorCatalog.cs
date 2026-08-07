namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Foundation-level safe defaults for unmapped failures.
/// Business-specific fixed codes from specification §16.3 are intentionally not registered here.
/// </summary>
public static class ApiErrorCatalog
{
    public const string InternalErrorCode = "internal_error";

    public const string InternalErrorMessage = "予期しないエラーが発生しました。";
}
