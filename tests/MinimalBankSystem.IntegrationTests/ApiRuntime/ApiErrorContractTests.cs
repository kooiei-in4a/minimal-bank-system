using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Verifies the common REST error contract of specification section 16 and AC-ERR-001.
/// </summary>
public sealed class ApiErrorContractTests
{
    [Fact]
    public async Task MappedExceptionUsesTheCommonErrorEnvelope()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync(
            services => services.AddApiExceptionMapper<RuntimeContractTestExceptionMapper>());

        using HttpResponseMessage response = await server.Client.GetAsync(
            $"/{RuntimeContractTestController.RouteBase}/failure/mapped");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        ApiErrorEnvelope envelope = await ApiErrorEnvelope.ReadAsync(response);

        Assert.Equal(RuntimeContractTestExceptionMapper.ErrorCode, envelope.Code);
        Assert.Equal(RuntimeContractTestExceptionMapper.Message, envelope.Message);
        Assert.DoesNotContain(RuntimeContractTestException.Detail, envelope.RawBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnmappedExceptionReturnsTheFixedInternalErrorWithoutInternalDetail()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        using HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            $"/{RuntimeContractTestController.RouteBase}/failure/unmapped",
            new RuntimeContractTestPayload());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        ApiErrorEnvelope envelope = await ApiErrorEnvelope.ReadAsync(response);

        Assert.Equal(ApiErrorCodes.InternalError, envelope.Code);
        Assert.DoesNotContain(
            RuntimeContractTestController.UnmappedExceptionDetail,
            envelope.RawBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), envelope.RawBody, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", envelope.RawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", envelope.RawBody, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(RuntimeContractTestController), envelope.RawBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidInputReturnsTheCommonErrorEnvelopeInsteadOfProblemDetails()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        using HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            $"/{RuntimeContractTestController.RouteBase}/validated",
            new ValidatedPayload());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ApiErrorEnvelope envelope = await ApiErrorEnvelope.ReadAsync(response);

        Assert.Equal(ApiErrorCodes.ValidationFailed, envelope.Code);
        Assert.DoesNotContain(nameof(ValidatedPayload.Name), envelope.RawBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FrameworkDoesNotSynthesiseAProblemDocumentForClientErrorResults()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        using HttpResponseMessage response = await server.Client.GetAsync(
            $"/{RuntimeContractTestController.RouteBase}/client-error");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EveryFailureResponseCarriesTheCorrelationHeader()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        using HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            $"/{RuntimeContractTestController.RouteBase}/failure/unmapped",
            new RuntimeContractTestPayload());

        Assert.True(response.Headers.TryGetValues(CorrelationIdPolicy.HeaderName, out IEnumerable<string>? values));
        Assert.True(CorrelationIdPolicy.IsAcceptable(Assert.Single(values!)));
    }
}
