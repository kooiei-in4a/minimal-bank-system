using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Verifies what the common API runtime contract registers, and what it deliberately does not.
/// </summary>
public sealed class ApiRuntimeCompositionTests
{
    [Fact]
    public void RuntimeContractRegistersTheSystemTimeProvider()
    {
        using ServiceProvider provider = BuildRuntimeContract();

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void RuntimeContractRegistersNoBusinessExceptionMapping()
    {
        using ServiceProvider provider = BuildRuntimeContract();

        Assert.Empty(provider.GetServices<IApiExceptionMapper>());
    }

    [Fact]
    public void RegisteredExceptionMappersAreResolvableThroughTheExtensionPoint()
    {
        ServiceCollection services = new();
        services.AddApiRuntimeContract();
        services.AddApiExceptionMapper<RuntimeContractTestExceptionMapper>();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<RuntimeContractTestExceptionMapper>(
            Assert.Single(provider.GetServices<IApiExceptionMapper>()));
    }

    [Theory]
    [InlineData("0123456789abcdef")]
    [InlineData("caller-supplied-id")]
    [InlineData("A")]
    public void CorrelationIdPolicyAcceptsShortAsciiTokens(string candidate)
    {
        Assert.True(CorrelationIdPolicy.IsAcceptable(candidate));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("has space")]
    [InlineData("has\ttab")]
    [InlineData("has\r\nnewline")]
    [InlineData("has/slash")]
    [InlineData("has_underscore")]
    [InlineData("識別子")]
    public void CorrelationIdPolicyRejectsUnsafeValues(string? candidate)
    {
        Assert.False(CorrelationIdPolicy.IsAcceptable(candidate));
    }

    [Fact]
    public void CorrelationIdPolicyRejectsOverlongValues()
    {
        Assert.True(CorrelationIdPolicy.IsAcceptable(new string('a', CorrelationIdPolicy.MaxLength)));
        Assert.False(CorrelationIdPolicy.IsAcceptable(new string('a', CorrelationIdPolicy.MaxLength + 1)));
    }

    [Fact]
    public void GeneratedCorrelationIdsSatisfyThePolicy()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Assert.True(CorrelationIdPolicy.IsAcceptable(CorrelationIdPolicy.Create()));
        }
    }

    private static ServiceProvider BuildRuntimeContract()
    {
        ServiceCollection services = new();
        services.AddApiRuntimeContract();

        return services.BuildServiceProvider();
    }
}
