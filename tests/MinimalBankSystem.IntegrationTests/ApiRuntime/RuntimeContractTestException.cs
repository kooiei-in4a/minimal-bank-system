namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Exception used only by the contract tests to exercise the exception to HTTP extension point.
/// </summary>
public sealed class RuntimeContractTestException : Exception
{
    /// <summary>
    /// Internal detail that must never appear in an API response.
    /// </summary>
    public const string Detail = "mapped-exception-detail-must-not-reach-the-caller";

    public RuntimeContractTestException()
        : base(Detail)
    {
    }

    public RuntimeContractTestException(string message)
        : base(message)
    {
    }

    public RuntimeContractTestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
