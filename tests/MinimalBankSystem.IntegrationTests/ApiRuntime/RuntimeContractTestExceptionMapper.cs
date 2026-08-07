using Microsoft.AspNetCore.Http;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Test-only mapper that demonstrates the exception to HTTP extension point.
/// </summary>
/// <remarks>
/// The code is deliberately outside the specification's business error table, because Issue #40 must
/// not pre-empt business error mapping. It only proves that a feature can attach its own exception
/// to the shared error contract.
/// </remarks>
internal sealed class RuntimeContractTestExceptionMapper : IApiExceptionMapper
{
    public const string ErrorCode = "runtime_contract_test_error";

    public const string Message = "The representative test operation was rejected.";

    public ApiError? Map(Exception exception) => exception is RuntimeContractTestException
        ? new ApiError(StatusCodes.Status409Conflict, ErrorCode, Message)
        : null;
}
