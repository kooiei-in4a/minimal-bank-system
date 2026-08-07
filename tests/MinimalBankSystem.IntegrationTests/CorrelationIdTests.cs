using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MinimalBankSystem.Api.Middleware;

namespace MinimalBankSystem.IntegrationTests;

public sealed class CorrelationIdTests
{
    [Fact]
    public async Task CorrelationIdMiddlewareGeneratesIdWhenNoneProvided()
    {
        var context = new DefaultHttpContext();
        context.Response.Headers.Clear();

        var middleware = new CorrelationIdMiddleware(
            next: (ctx) => Task.CompletedTask,
            logger: NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(context.Items.ContainsKey("CorrelationId"));
        string? correlationId = context.Items["CorrelationId"] as string;
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public async Task CorrelationIdMiddlewarePropagatesSafeId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "test-correlation-123";

        var middleware = new CorrelationIdMiddleware(
            next: (ctx) => Task.CompletedTask,
            logger: NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("test-correlation-123", context.Items["CorrelationId"]);
    }

    [Fact]
    public async Task CorrelationIdMiddlewareSanitizesUnsafeId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "<script>alert('xss')</script>";

        var middleware = new CorrelationIdMiddleware(
            next: (ctx) => Task.CompletedTask,
            logger: NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        string? correlationId = context.Items["CorrelationId"] as string;
        Assert.NotEqual("<script>alert('xss')</script>", correlationId);
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public async Task CorrelationIdMiddlewareSanitizesExcessivelyLongId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = new string('a', 200);

        var middleware = new CorrelationIdMiddleware(
            next: (ctx) => Task.CompletedTask,
            logger: NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        string? correlationId = context.Items["CorrelationId"] as string;
        Assert.NotEqual(new string('a', 200), correlationId);
    }

    [Fact]
    public async Task CorrelationIdMiddlewareSetsResponseHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "my-id";

        var middleware = new CorrelationIdMiddleware(
            next: (ctx) => Task.CompletedTask,
            logger: NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(context.Response.Headers.ContainsKey("X-Correlation-Id"));
        Assert.Equal("my-id", context.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task CorrelationIdMiddlewareSafeIdAcceptsDotsAndDashes()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "abc-123.def_456";

        var middleware = new CorrelationIdMiddleware(
            next: (ctx) => Task.CompletedTask,
            logger: NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("abc-123.def_456", context.Items["CorrelationId"]);
    }
}
