using System.Globalization;

namespace MinimalBankSystem.Api.Health;

/// <summary>Runs an endpoint probe using only the .NET runtime shipped in the API image.</summary>
internal static class HealthProbeCommand
{
    private const string Option = "--health-probe";
    private static readonly Uri LoopbackBaseAddress = new("http://127.0.0.1:8080");

    public static async Task<int?> TryRunAsync(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], Option, StringComparison.Ordinal))
        {
            return null;
        }

        if (args.Length != 2 || args[1] is not ("/health/live" or "/health/ready"))
        {
            Console.WriteLine("HEALTH_PROBE_STATUS=invalid");
            return 2;
        }

        using HttpClient client = new()
        {
            BaseAddress = LoopbackBaseAddress,
            Timeout = TimeSpan.FromSeconds(10),
        };

        try
        {
            using HttpResponseMessage response = await client.GetAsync(args[1]).ConfigureAwait(false);
            Console.WriteLine(
                "HEALTH_PROBE_STATUS={0}",
                ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));

            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception)
        {
            Console.WriteLine("HEALTH_PROBE_STATUS=unreachable");
            return 1;
        }
    }
}
