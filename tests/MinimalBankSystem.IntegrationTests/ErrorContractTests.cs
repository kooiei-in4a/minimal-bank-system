using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MinimalBankSystem.Api.Middleware;
using MinimalBankSystem.Api.Models;
using MinimalBankSystem.Domain.Errors;

namespace MinimalBankSystem.IntegrationTests;

public sealed class ErrorContractTests
{
    [Fact]
    public void ErrorResponseContainsCodeAndMessage()
    {
        var error = new ErrorResponse("validation_failed", "Test validation error");

        Assert.Equal("validation_failed", error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public void DomainErrorsDefinesInternalErrorCode()
    {
        Assert.Equal("internal_error", DomainErrors.Common.InternalError);
    }

    [Fact]
    public void DomainErrorsDefinesValidationErrorCode()
    {
        Assert.Equal("validation_failed", DomainErrors.Common.ValidationError);
    }

    [Fact]
    public async Task ExceptionHandlingMiddlewareReturns500ForUnhandledException()
    {
        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = "test-id";
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            next: (ctx) => throw new InvalidOperationException("sensitive details"),
            logger: NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.DoesNotContain("sensitive details", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.Contains("internal_error", body);
    }

    [Fact]
    public async Task ExceptionHandlingMiddlewareDoesNotContainStackTrace()
    {
        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = "test-id";
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            next: (ctx) => throw new InvalidOperationException("test"),
            logger: NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.DoesNotContain("at ", body);
        Assert.DoesNotContain("Exception", body);
    }

    [Fact]
    public async Task ExceptionHandlingMiddlewarePassesThroughSuccessfulRequests()
    {
        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = "test-id";
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            next: (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            logger: NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }
}
