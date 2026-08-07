using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.IntegrationTests.Infrastructure;

namespace MinimalBankSystem.IntegrationTests.Api;

public sealed class ProhibitedTechnicalLogFieldTests : IClassFixture<ApiContractWebApplicationFactory>
{
    private readonly ApiContractWebApplicationFactory _factory;

    public ProhibitedTechnicalLogFieldTests(ApiContractWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TechnicalLogs_DoNotContainProhibitedSentinelValues()
    {
        _factory.LoggerProvider.Clear();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/__contract__/log-sentinel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string combinedLogText = BuildCombinedLogText(_factory.LoggerProvider.Entries);

        Assert.DoesNotContain(ContractSentinels.Password, combinedLogText, StringComparison.Ordinal);
        Assert.DoesNotContain(ContractSentinels.Jwt, combinedLogText, StringComparison.Ordinal);
        Assert.DoesNotContain(ContractSentinels.SigningKey, combinedLogText, StringComparison.Ordinal);
        Assert.DoesNotContain(ContractSentinels.IdempotencyKey, combinedLogText, StringComparison.Ordinal);
        Assert.DoesNotContain(ContractSentinels.ConnectionString, combinedLogText, StringComparison.Ordinal);
        Assert.Contains("allowed", combinedLogText, StringComparison.Ordinal);
    }

    private static string BuildCombinedLogText(IReadOnlyList<CollectedLogEntry> entries)
    {
        StringBuilder builder = new();
        foreach (CollectedLogEntry entry in entries)
        {
            builder.AppendLine(entry.Message);
            foreach (KeyValuePair<string, object?> property in entry.Properties)
            {
                builder.Append(property.Key);
                builder.Append('=');
                builder.Append(property.Value);
                builder.AppendLine();
            }

            if (entry.Exception is not null)
            {
                builder.AppendLine(entry.Exception.ToString());
            }
        }

        return builder.ToString();
    }
}
