using Microsoft.AspNetCore.Mvc;

namespace MinimalBankSystem.Api.Controllers;

[ApiController]
[Route("api/contract-verification")]
public sealed partial class ContractVerificationController : ControllerBase
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ContractVerificationController> _logger;

    public ContractVerificationController(
        TimeProvider timeProvider,
        ILogger<ContractVerificationController> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [HttpGet("time")]
    public IActionResult GetTime()
    {
        var now = _timeProvider.GetUtcNow();
        return Ok(new { utcNow = now, localNow = _timeProvider.GetLocalNow() });
    }

    [HttpGet("log-info")]
    public IActionResult LogInfo()
    {
        LogInfoMessage(_logger);
        return Ok(new { logged = true });
    }

    [HttpGet("throw-unmapped")]
    public IActionResult ThrowUnmapped()
    {
        throw new InvalidOperationException("Simulated infrastructure failure for contract verification");
    }

    [HttpGet("throw-api-exception")]
    public IActionResult ThrowApiException()
    {
        throw new ContractTestApiException();
    }

    [HttpGet("log-sensitive")]
    public IActionResult LogSensitive()
    {
        LogSensitiveMessage(
            _logger,
            "s3cr3t_p@ssw0rd",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test",
            "abcdef1234567890abcdef1234567890",
            "raw-idempotency-abc-123",
            "Host=db;Database=bank;Username=app;Password=secret",
            "this-is-fine");
        return Ok(new { logged = true });
    }

    [HttpGet("correlation-id")]
    public IActionResult GetCorrelationId()
    {
        return Ok(new
        {
            fromItems = HttpContext.Items["CorrelationId"]?.ToString() ?? "not-set",
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Contract verification informational log")]
    private static partial void LogInfoMessage(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attempting to log sensitive data: {Password} {JWT} {SigningKey} {IdempotencyKey} {ConnectionString} {NormalField}")]
    private static partial void LogSensitiveMessage(
        ILogger logger,
        string password,
        string jwt,
        string signingKey,
        string idempotencyKey,
        string connectionString,
        string normalField);
}

public sealed class ContractTestApiException : MinimalBankSystem.Domain.ApiException
{
    public ContractTestApiException()
        : base(422, "test_error", "A test API error for contract verification") { }
}
