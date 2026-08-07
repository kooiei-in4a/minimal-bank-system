namespace MinimalBankSystem.Api.Runtime;

public static class ApiErrorCatalog
{
    /// <summary>
    /// Foundation runtime code for exceptions with no registered mapper.
    /// Business-specific 500 codes are mapped by later feature Issues.
    /// </summary>
    public static ApiErrorDefinition UnmappedException { get; } = new(
        "internal_error",
        "処理を完了できませんでした。",
        StatusCodes.Status500InternalServerError);
}
