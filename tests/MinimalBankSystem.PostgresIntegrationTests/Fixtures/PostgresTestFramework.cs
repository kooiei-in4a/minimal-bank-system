using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace MinimalBankSystem.PostgresIntegrationTests.Fixtures;

/// <summary>
/// The xUnit test framework for this assembly. It exists only to give the shared PostgreSQL
/// container a deterministic assembly-scoped teardown.
/// </summary>
/// <remarks>
/// xUnit v2 has no assembly fixture, and a collection fixture would force every test that shares
/// the container into one serialized collection. Wrapping the assembly run keeps the container
/// shared across parallel collections while still removing it exactly once, as soon as the last
/// test finishes, rather than leaving removal to process exit.
/// </remarks>
public sealed class PostgresTestFramework : XunitTestFramework
{
    public PostgresTestFramework(IMessageSink messageSink)
        : base(messageSink)
    {
    }

    /// <inheritdoc />
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName) =>
        new PostgresTestFrameworkExecutor(
            assemblyName,
            SourceInformationProvider,
            DiagnosticMessageSink);
}

/// <summary>
/// Runs the assembly and then removes the shared PostgreSQL container.
/// </summary>
internal sealed class PostgresTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    public PostgresTestFrameworkExecutor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

    /// <inheritdoc />
    protected override async void RunTestCases(
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
    {
        using XunitTestAssemblyRunner runner = new(
            TestAssembly,
            testCases,
            DiagnosticMessageSink,
            executionMessageSink,
            executionOptions);

        try
        {
            await runner.RunAsync();
        }
        finally
        {
            await ShutdownSharedServerAsync();
        }
    }

    private static async Task ShutdownSharedServerAsync()
    {
        try
        {
            await PostgresTestServer.ShutdownSharedAsync();
        }
        catch (Exception exception)
        {
            // Every test has already reported, so there is no test left to fail. Write the failure
            // where a CI log shows it and let it escape: a container that could not be removed
            // aborts the run instead of being silently left behind.
            Console.Error.WriteLine($"POSTGRES_TEST_CONTAINER_CLEANUP_FAILED: {exception}");
            throw;
        }
    }
}
