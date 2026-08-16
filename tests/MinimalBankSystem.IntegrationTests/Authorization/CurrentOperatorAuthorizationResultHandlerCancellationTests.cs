using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.IntegrationTests.Authorization;

/// <summary>
/// AUTHZ-H2-MIN-03-NOW-REACHABLE regression coverage. The mandatory authenticated-403 Product
/// Audit write must not depend on <see cref="HttpContext.RequestAborted"/>; a client disconnect
/// must never be able to suppress the required Audit record. These tests drive
/// <see cref="CurrentOperatorAuthorizationResultHandler"/> directly against a synthetic
/// <see cref="DefaultHttpContext"/> (no TestServer, no PostgreSQL) so cancellation timing is
/// fully controlled and deterministic: no sleeps, no timing-dependent races.
/// </summary>
public sealed class CurrentOperatorAuthorizationResultHandlerCancellationTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private const string TargetId = "cancellation-regression-target";
    private const string CorrelationId = "authz-h2-min-03-cancellation";
    private const string OperationIdentifier = "authz-h2-min-03.cancellation-regression";

    [Fact]
    public async Task MandatoryAuditPersistsExactlyOnceWhenRequestAbortedIsAlreadyCancelledBeforeHandling()
    {
        using CancellationTokenSource clientCts = new();
        clientCts.Cancel();

        RecordingAuditWriter auditWriter = new();
        DefaultHttpContext httpContext = CreateHttpContext(auditWriter, clientCts.Token);
        bool nextInvoked = false;

        // Response delivery is allowed to observe (and be cancelled by) an already-aborted
        // request; that must not be conflated with Audit durability, which is asserted below
        // regardless of whatever happens to the response write.
        await InvokeHandlerTolerantOfResponseCancellationAsync(httpContext, () => nextInvoked = true);

        AssertExactlyOneDurableAudit(auditWriter);
        Assert.False(
            auditWriter.TokenWasCancelledAtInvocation,
            "The mandatory 403 Audit token must not already be cancelled merely because " +
            "RequestAborted was cancelled before handling began.");
        Assert.False(nextInvoked, "The rejected request must never reach the feature handler.");
    }

    [Fact]
    public async Task MandatoryAuditCompletesDurablyWhenRequestAbortedIsCancelledWhileAuditIsInFlight()
    {
        using CancellationTokenSource clientCts = new();

        // Simulates a client disconnect landing exactly while the mandatory Audit persistence is
        // executing (the historical residual: RequestAborted was passed straight into the Audit
        // writer's begin/SaveChanges/commit).
        RecordingAuditWriter auditWriter = new(onInvoked: () => clientCts.Cancel());
        DefaultHttpContext httpContext = CreateHttpContext(auditWriter, clientCts.Token);
        bool nextInvoked = false;

        await InvokeHandlerTolerantOfResponseCancellationAsync(httpContext, () => nextInvoked = true);

        AssertExactlyOneDurableAudit(auditWriter);
        Assert.False(
            auditWriter.TokenWasCancelledAtInvocation,
            "The Audit token must not start out cancelled.");
        Assert.False(
            auditWriter.CapturedToken!.Value.IsCancellationRequested,
            "Cancelling RequestAborted while the mandatory 403 Audit write is in flight must not " +
            "cancel the independent bounded Audit token.");
        Assert.NotEqual(clientCts.Token, auditWriter.CapturedToken!.Value);
        Assert.False(nextInvoked, "The rejected request must never reach the feature handler.");
    }

    [Fact]
    public async Task RealAuditWriterFailureStillFailsClosedWhenRequestAbortedIsUncancelled()
    {
        using CancellationTokenSource clientCts = new();
        ThrowingAuditWriter auditWriter = new();
        DefaultHttpContext httpContext = CreateHttpContext(auditWriter, clientCts.Token);
        bool nextInvoked = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeHandlerAsync(
            httpContext,
            () => nextInvoked = true));

        Assert.Equal(1, auditWriter.InvocationCount);
        Assert.False(nextInvoked, "A required-Audit failure must fail closed without reaching the feature handler.");
    }

    private static void AssertExactlyOneDurableAudit(RecordingAuditWriter auditWriter)
    {
        Assert.Equal(1, auditWriter.InvocationCount);
        Assert.NotNull(auditWriter.CapturedRequest);
        Assert.Equal(ActorId, auditWriter.CapturedRequest!.ActorIdentifier);
        Assert.Equal(OperatorRole.Viewer, auditWriter.CapturedRequest.ActorRole);
        Assert.Equal(OperationIdentifier, auditWriter.CapturedRequest.OperationIdentifier);
        Assert.Equal(TargetId, auditWriter.CapturedRequest.TargetIdentifier);
        Assert.Equal(AuditResult.Failure, auditWriter.CapturedRequest.Result);
        Assert.Equal(CorrelationId, auditWriter.CapturedRequest.CorrelationId);
    }

    private static async Task InvokeHandlerTolerantOfResponseCancellationAsync(
        DefaultHttpContext httpContext,
        Action onNextInvoked)
    {
        try
        {
            await InvokeHandlerAsync(httpContext, onNextInvoked);
        }
        catch (OperationCanceledException)
        {
            // Expected when RequestAborted is cancelled: response delivery may observe the
            // cancellation. The Audit-durability assertions run independently of this.
        }
    }

    private static async Task InvokeHandlerAsync(DefaultHttpContext httpContext, Action onNextInvoked)
    {
        CurrentOperatorAuthorizationResultHandler handler = new();
        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        await handler.HandleAsync(
            _ =>
            {
                onNextInvoked();
                return Task.CompletedTask;
            },
            httpContext,
            policy,
            PolicyAuthorizationResult.Forbid());
    }

    private static DefaultHttpContext CreateHttpContext(IAuditWriter auditWriter, CancellationToken requestAborted)
    {
        CurrentOperatorRequestContext requestContext = new();
        requestContext.SetCurrent(new CurrentOperatorSnapshot(
            ActorId,
            OperatorState.Active,
            OperatorRole.Viewer,
            1));

        ServiceProvider services = new ServiceCollection()
            .AddSingleton(requestContext)
            .AddSingleton(auditWriter)
            .BuildServiceProvider();

        RouteEndpoint endpoint = new(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/__authz-cancellation-probe/{targetId}"),
            order: 0,
            metadata: new EndpointMetadataCollection(new StaticAuthorizationAuditContext()),
            displayName: "authz-cancellation-probe");

        DefaultHttpContext httpContext = new()
        {
            RequestServices = services,
            RequestAborted = requestAborted,
            TraceIdentifier = CorrelationId,
        };
        httpContext.Response.Body = Stream.Null;
        httpContext.SetEndpoint(endpoint);

        return httpContext;
    }

    private sealed class StaticAuthorizationAuditContext : IAuthorizationAuditContext
    {
        public string OperationIdentifier => CurrentOperatorAuthorizationResultHandlerCancellationTests.OperationIdentifier;

        public ValueTask<string?> ResolveTargetIdentifierAsync(HttpContext httpContext) =>
            ValueTask.FromResult<string?>(TargetId);
    }

    private sealed class RecordingAuditWriter(Action? onInvoked = null) : IAuditWriter
    {
        public int InvocationCount { get; private set; }

        public CancellationToken? CapturedToken { get; private set; }

        public AuditWriteRequest? CapturedRequest { get; private set; }

        public bool TokenWasCancelledAtInvocation { get; private set; }

        public Task AppendToCurrentTransactionAsync(
            AuditWriteRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "The AUTHZ policy-rejection path uses the separate-transaction primitive only.");

        public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
            AuditWriteRequest request,
            Func<CancellationToken, Task<TResult>> successResultFactory,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            CapturedRequest = request;
            CapturedToken = cancellationToken;
            TokenWasCancelledAtInvocation = cancellationToken.IsCancellationRequested;
            onInvoked?.Invoke();
            return successResultFactory(cancellationToken);
        }
    }

    private sealed class ThrowingAuditWriter : IAuditWriter
    {
        public int InvocationCount { get; private set; }

        public Task AppendToCurrentTransactionAsync(
            AuditWriteRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "The AUTHZ policy-rejection path uses the separate-transaction primitive only.");

        public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
            AuditWriteRequest request,
            Func<CancellationToken, Task<TResult>> successResultFactory,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            throw new InvalidOperationException("Deterministic test-only AUTHZ Product Audit failure.");
        }
    }
}
