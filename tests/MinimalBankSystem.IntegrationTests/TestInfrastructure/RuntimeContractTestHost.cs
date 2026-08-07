using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api.Testing;

namespace MinimalBankSystem.IntegrationTests.TestInfrastructure;

public sealed class RuntimeContractTestHost : WebApplicationFactory<MinimalBankSystem.Api.Program>
{
    public CapturedJsonLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The host content root is pinned to a deterministic local directory so that
        // configuration probing never depends on the repository checkout location
        // (a Windows UNC checkout path blocks app startup in this environment).
        string contentRoot = Path.Combine(Path.GetTempPath(), "minimal-bank-system-test-host");
        Directory.CreateDirectory(contentRoot);
        builder.UseSetting(WebHostDefaults.ContentRootKey, contentRoot);
        builder.UseEnvironment(RuntimeContractTestEnvironment.Name);

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(RuntimeContractTestController).Assembly);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(FixedTimeProvider.Instance);
            services.AddLogging(logging => logging.AddProvider(Logs));
        });
    }
}
