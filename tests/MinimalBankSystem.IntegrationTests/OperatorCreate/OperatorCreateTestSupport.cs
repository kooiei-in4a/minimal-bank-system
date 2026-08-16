extern alias api;

using System.Collections.Concurrent;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.OperatorCreate;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.IntegrationTests.OperatorCreate;

internal static class OperatorCreateDisclosureOracle
{
    public const string PasswordSentinel = "OPR-CREATE-PASSWORD-SENTINEL-c3-7f41e2a9";
    public const string LoginSentinel = "opr-create-login-sentinel-c3-91ab";
    public const string HashSentinel = "AQAAAAIAAYagAAAAEOPRCREATEHASHCONTROL";

    public static bool Detects(string surface, params string[] materials)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(materials);

        foreach (string material in materials)
        {
            if (!string.IsNullOrEmpty(material)
                && surface.Contains(material, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string LeakingProjection(string password, string login, string hash) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["operatorIdentifier"] = Guid.CreateVersion7().ToString("D"),
            ["state"] = "active",
            ["role"] = "teller",
            ["password"] = password,
            ["loginIdentifier"] = login,
            ["passwordHash"] = hash,
            ["securityStamp"] = "stamp-leak",
        });
}

internal sealed class OperatorCreateApiFactory : WebApplicationFactory<api::Program>
{
    private readonly string connectionString;
    private readonly Action<IServiceCollection>? configureServices;
    private readonly OperatorCreateLogCapture logCapture;

    public OperatorCreateApiFactory(
        string connectionString,
        Action<IServiceCollection>? configureServices = null,
        OperatorCreateLogCapture? logCapture = null)
    {
        this.connectionString = connectionString;
        this.configureServices = configureServices;
        this.logCapture = logCapture ?? new OperatorCreateLogCapture();
    }

    public OperatorCreateLogCapture LogCapture => logCapture;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(JwtAuthnOptions.SigningKeyConfigurationKey, TestJwtConfiguration.SigningKey);
        builder.UseSetting(
            $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
            connectionString);
        builder.ConfigureLogging(logging => logging.AddProvider(logCapture));
        builder.ConfigureServices(services => configureServices?.Invoke(services));
    }
}

internal sealed class OperatorCreateLogCapture : ILoggerProvider
{
    private readonly ConcurrentBag<string> messages = [];

    public IReadOnlyCollection<string> Messages => messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(messages);

    public void Dispose()
    {
    }

    public void EmitPositiveControl(string sentinel) =>
        messages.Add($"positive-control:{sentinel}");

    public void Clear()
    {
        while (messages.TryTake(out _))
        {
        }
    }

    private sealed class CaptureLogger(ConcurrentBag<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
            if (exception is not null)
            {
                messages.Add(exception.GetType().FullName ?? exception.GetType().Name);
            }
        }
    }
}

internal sealed class OperatorCreateExecutionSignals
{
    private int actionReachedCount;

    public int ActionReachedCount => Volatile.Read(ref actionReachedCount);

    public void RecordActionReached() => Interlocked.Increment(ref actionReachedCount);
}

internal sealed class OperatorCreateExecutionFilter(OperatorCreateExecutionSignals signals) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.Controller is OperatorCreateController)
        {
            signals.RecordActionReached();
        }

        await next().ConfigureAwait(false);
    }
}

internal sealed class OperatorCreateAuditFailureProbe
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);
}

internal sealed class FailingOperatorCreateAuditWriter(
    OperatorCreateAuditFailureProbe failureProbe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        failureProbe.RecordInvocation();
        throw new OperatorCreateAuditFailureInjectionException();
    }

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = successResultFactory;
        _ = cancellationToken;
        failureProbe.RecordInvocation();
        throw new OperatorCreateAuditFailureInjectionException();
    }
}

internal sealed class CommitThenFailCreateAuditWriter(
    BankDbContext persistence,
    OperatorCreateAuditFailureProbe failureProbe) : IAuditWriter
{
    public async Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        failureProbe.RecordInvocation();
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            persistence.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "OPR-CREATE-AUD-01 mutation expected an ambient caller transaction.");

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        throw new OperatorCreateAuditAtomicityMutationException();
    }

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = successResultFactory;
        _ = cancellationToken;
        throw new NotSupportedException(
            "OPR-CREATE-AUD-01 mutates the same-transaction success path only.");
    }
}

internal sealed class OperatorCreateAuditFailureInjectionException()
    : InvalidOperationException("Deterministic test-only Operator create Audit persistence failure.");

internal sealed class OperatorCreateAuditAtomicityMutationException()
    : InvalidOperationException(
        "Test-only mutation committed Operator creation before the required success Audit.");

internal sealed class ThrowOnOperatorSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowForOperator(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ThrowForOperator(eventData);
        return ValueTask.FromResult(result);
    }

    private static void ThrowForOperator(DbContextEventData eventData)
    {
        if (eventData.Context?.ChangeTracker.Entries<Operator>()
            .Any(entry => entry.State == EntityState.Added) == true)
        {
            throw new OperatorCreatePersistenceInjectionException();
        }
    }
}

internal sealed class OperatorCreatePersistenceInjectionException()
    : InvalidOperationException("Deterministic test-only Operator persistence failure.");

internal static class OperatorCreateTestAuthentication
{
    public static string CreateToken(Operator operatorEntity)
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer: "minimal-bank-system",
            audience: "minimal-bank-system-api",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, operatorEntity.Id.ToString("D")),
                new Claim(
                    AuthnClaimTypes.AuthorizationStateVersion,
                    operatorEntity.AuthorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void AddExecutionSignal(
        IServiceCollection services,
        OperatorCreateExecutionSignals signals)
    {
        services.AddSingleton(signals);
        services.AddSingleton<OperatorCreateExecutionFilter>();
        services.Configure<MvcOptions>(options =>
            options.Filters.AddService<OperatorCreateExecutionFilter>());
    }

    public static void ReplaceAuditWriter<TWriter>(IServiceCollection services)
        where TWriter : class, IAuditWriter
    {
        services.RemoveAll<IAuditWriter>();
        services.AddScoped<IAuditWriter, TWriter>();
    }

    public static void AddSaveChangesInterceptor(
        IServiceCollection services,
        string connectionString,
        IInterceptor interceptor)
    {
        ServiceDescriptor[] descriptors = [.. services];
        foreach (ServiceDescriptor descriptor in descriptors)
        {
            Type serviceType = descriptor.ServiceType;
            if (serviceType == typeof(BankDbContext)
                || serviceType == typeof(DbContextOptions<BankDbContext>)
                || (serviceType.IsGenericType
                    && serviceType.GenericTypeArguments.Length == 1
                    && serviceType.GenericTypeArguments[0] == typeof(BankDbContext)
                    && serviceType.Name.Contains("DbContext", StringComparison.Ordinal)))
            {
                services.Remove(descriptor);
            }
        }

        services.AddDbContext<BankDbContext>((_, options) =>
        {
            options.UseBankPostgreSql(connectionString);
            options.AddInterceptors(interceptor);
        });
    }

    public static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    public static async Task<HttpResponseMessage> SendCreateAsync(
        HttpClient client,
        object? payload,
        string? token,
        string correlationId)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/operators");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        request.Content = payload is null
            ? null
            : JsonBody(payload);
        return await client.SendAsync(request);
    }
}
