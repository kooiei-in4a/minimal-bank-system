using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MinimalBankSystem.Api.ExceptionMapping;
using MinimalBankSystem.Api.Logging;
using MinimalBankSystem.Api.Middleware;
using MinimalBankSystem.Domain;

namespace MinimalBankSystem.IntegrationTests;

public sealed class ApiContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public async Task ExceptionHandlingMiddleware_MapsApiException()
    {
        var mapper = new DefaultExceptionMapper();
        var logger = NullLogger<ExceptionHandlingMiddleware>.Instance;
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ContractTestApiException(),
            mapper,
            logger);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(422, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal("test_error", doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("A test API error for contract verification", doc.RootElement.GetProperty("message").GetString());
        Assert.Equal(2, doc.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_MapsUnmappedExceptionTo500()
    {
        var mapper = new DefaultExceptionMapper();
        var logger = NullLogger<ExceptionHandlingMiddleware>.Instance;
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("test failure"),
            mapper,
            logger);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal("internal_error", doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("An internal error occurred.", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_DoesNotExposeExceptionDetails()
    {
        var mapper = new DefaultExceptionMapper();
        var logger = NullLogger<ExceptionHandlingMiddleware>.Instance;
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("SIMULATED_INFRASTRUCTURE_FAILURE"),
            mapper,
            logger);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.DoesNotContain("SIMULATED_INFRASTRUCTURE_FAILURE", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_GeneratesNewIdWhenNoneProvided()
    {
        var logger = NullLogger<CorrelationIdMiddleware>.Instance;
        var middleware = new CorrelationIdMiddleware(
            ctx => Task.CompletedTask,
            logger);

        var context = new DefaultHttpContext();
        var capturedHeaders = new HeaderDictionary();
        context.Response.OnStarting(state =>
        {
            var headers = (IHeaderDictionary)state!;
            foreach (var (key, value) in context.Response.Headers)
            {
                headers[key] = value;
            }
            return Task.CompletedTask;
        }, capturedHeaders);

        await middleware.InvokeAsync(context);

        Assert.True(context.Response.Headers.TryGetValue("X-Correlation-ID", out var values));
        var id = values.First()!;
        Assert.Equal(32, id.Length);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_AcceptsValidCallerSuppliedId()
    {
        var logger = NullLogger<CorrelationIdMiddleware>.Instance;
        var middleware = new CorrelationIdMiddleware(
            ctx => Task.CompletedTask,
            logger);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "client-supplied-abc123";

        await middleware.InvokeAsync(context);

        Assert.Equal("client-supplied-abc123", context.Response.Headers["X-Correlation-ID"].First());
    }

    [Fact]
    public void CorrelationIdMiddleware_RejectsOverlyLongValue()
    {
        var tooLong = new string('x', 129);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = tooLong;

        var result = CorrelationIdMiddleware.GetOrCreateCorrelationId(context);
        Assert.NotEqual(tooLong, result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void CorrelationIdMiddleware_RejectsControlCharacters()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "abc\n123";

        var result = CorrelationIdMiddleware.GetOrCreateCorrelationId(context);
        Assert.NotEqual("abc\n123", result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void CorrelationIdMiddleware_GeneratesUniqueFormattedIds()
    {
        var context1 = new DefaultHttpContext();
        var context2 = new DefaultHttpContext();

        var id1 = CorrelationIdMiddleware.GetOrCreateCorrelationId(context1);
        var id2 = CorrelationIdMiddleware.GetOrCreateCorrelationId(context2);

        Assert.NotEqual(id1, id2);
        Assert.Equal(32, id1.Length);
        Assert.Equal(32, id2.Length);
    }

    [Fact]
    public void DefaultExceptionMapper_MapsApiException()
    {
        var mapper = new DefaultExceptionMapper();
        var exception = new ContractTestApiException();

        Assert.True(mapper.TryMap(exception, out var statusCode, out var errorResponse));
        Assert.Equal(422, statusCode);
        Assert.Equal("test_error", errorResponse.Code);
        Assert.Equal("A test API error for contract verification", errorResponse.Message);
    }

    [Fact]
    public void DefaultExceptionMapper_DoesNotMapUnrecognizedException()
    {
        var mapper = new DefaultExceptionMapper();
        var exception = new InvalidOperationException("test");

        Assert.False(mapper.TryMap(exception, out _, out _));
    }

    [Fact]
    public void RedactingTextWriter_RedactsProhibitedFields()
    {
        var prohibited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Password", "password", "JWT", "SigningKey", "signing_key",
            "IdempotencyKey", "idempotency_key", "ConnectionString", "connection_string",
        };

        var inner = new StringWriter();
        var writer = new RedactingTextWriter(inner, prohibited);

        var jsonInput = @"{""Password"":""s3cr3t"",""NormalField"":""visible"",""connection_string"":""Host=db;Password=x""}";
        writer.Write(jsonInput);

        var output = inner.ToString();
        Assert.Contains("[REDACTED]", output, StringComparison.Ordinal);
        Assert.Contains("visible", output, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Host=db", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactingTextWriter_DoesNotModifySafeContent()
    {
        var prohibited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Password", "JWT",
        };

        var inner = new StringWriter();
        var writer = new RedactingTextWriter(inner, prohibited);

        var input = @"{""Name"":""test"",""Amount"":100}";
        writer.WriteLine(input);

        var output = inner.ToString();
        Assert.Contains(@"""Name"":""test""", output, StringComparison.Ordinal);
        Assert.Contains(@"""Amount"":100", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorResponse_MatchesSpecEnvelope()
    {
        var response = new ErrorResponse("test_code", "Test message");

        var json = JsonSerializer.Serialize(response, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("code", out var code));
        Assert.Equal("test_code", code.GetString());
        Assert.True(doc.RootElement.TryGetProperty("message", out var message));
        Assert.Equal("Test message", message.GetString());
    }
}

public sealed class ContractTestApiException : ApiException
{
    public ContractTestApiException()
        : base(422, "test_error", "A test API error for contract verification") { }
}
