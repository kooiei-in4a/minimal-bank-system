using Microsoft.AspNetCore.Mvc;
using MinimalBankSystem.Api.CorrelationId;
using MinimalBankSystem.Api.ErrorHandling;
using MinimalBankSystem.Api.Logging;

namespace MinimalBankSystem.Api.Testing;

[ApiController]
[Route("api/test")]
public sealed class TestContractController : ControllerBase
{
    private static readonly Action<ILogger, string?, Exception?> LogOk =
        LoggerMessage.Define<string?>(
            LogLevel.Information,
            new EventId(1, "TestOk"),
            "Test endpoint ok called. CorrelationId: {CorrelationId}");

    private static readonly Action<ILogger, string, string, string, string, string, Exception?> LogProhibited =
        LoggerMessage.Define<string, string, string, string, string>(
            LogLevel.Information,
            new EventId(2, "TestProhibitedFields"),
            "Test log with prohibited field names. Password: {Password}, JWT: {JWT}, SigningKey: {SigningKey}, IdempotencyKey: {IdempotencyKey}, ConnectionString: {ConnectionString}");

    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TestContractController> _logger;

    public TestContractController(
        ICorrelationIdAccessor correlationIdAccessor,
        TimeProvider timeProvider,
        ILogger<TestContractController> logger)
    {
        _correlationIdAccessor = correlationIdAccessor;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [HttpGet("ok")]
    public IActionResult Ping()
    {
        LogOk(_logger, _correlationIdAccessor.Current, null);
        return Ok(new { correlationId = _correlationIdAccessor.Current });
    }

    [HttpGet("time")]
    public IActionResult Time()
    {
        var now = _timeProvider.GetUtcNow();
        return Ok(new { utcNow = now.ToString("O") });
    }

    [HttpGet("problem")]
    public IActionResult Problem()
    {
        throw new ProblemException(400, "validation_failed", "Test validation error.");
    }

    [HttpGet("unhandled")]
    public IActionResult Unhandled()
    {
        throw new InvalidOperationException("Internal detail that must not leak.");
    }

    [HttpPost("log-prohibited-fields")]
    public IActionResult LogProhibitedFields([FromBody] ProhibitedFieldTestPayload payload)
    {
        LogProhibited(
            _logger,
            SensitiveFieldPolicy.IsProhibited("Password") ? "***" : payload.Password,
            SensitiveFieldPolicy.IsProhibited("JWT") ? "***" : payload.Jwt,
            SensitiveFieldPolicy.IsProhibited("SigningKey") ? "***" : payload.SigningKey,
            SensitiveFieldPolicy.IsProhibited("IdempotencyKey") ? "***" : payload.IdempotencyKey,
            SensitiveFieldPolicy.IsProhibited("ConnectionString") ? "***" : payload.ConnectionString,
            null);

        return Ok(new { logged = true });
    }
}

public sealed class ProhibitedFieldTestPayload
{
    public string Password { get; set; } = string.Empty;
    public string Jwt { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}
