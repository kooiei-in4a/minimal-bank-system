using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api;
using MinimalBankSystem.IntegrationTests.Infrastructure;

namespace MinimalBankSystem.IntegrationTests.Api;

public sealed class ApiContractWebApplicationFactory : WebApplicationFactory<Program>
{
    public CollectingLoggerProvider LoggerProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(LoggerProvider);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(FakeTimeProvider);
        });
    }

    public FakeTimeProvider FakeTimeProvider { get; } = new(
        new DateTimeOffset(2026, 8, 8, 0, 0, 0, TimeSpan.Zero));
}

public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
}
