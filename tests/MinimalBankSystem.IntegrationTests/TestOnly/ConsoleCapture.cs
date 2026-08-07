namespace MinimalBankSystem.IntegrationTests.TestOnly;

internal static class ConsoleCapture
{
    public static async Task<string> CaptureAsync(Func<Task> action)
    {
        TextWriter original = Console.Out;
        StringWriter capture = new();
        Console.SetOut(capture);
        try
        {
            await action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return capture.ToString();
    }
}
