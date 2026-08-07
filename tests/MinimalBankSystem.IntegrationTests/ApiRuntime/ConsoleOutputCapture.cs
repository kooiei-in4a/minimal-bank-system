using System.Text;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Captures everything written to standard output so that the real console log stream can be
/// inspected.
/// </summary>
internal sealed class ConsoleOutputCapture : IDisposable
{
    private readonly TextWriter _originalOutput;
    private readonly StringBuilder _buffer = new();
    private readonly TextWriter _writer;
    private bool _stopped;

    private ConsoleOutputCapture()
    {
        _originalOutput = Console.Out;
        _writer = TextWriter.Synchronized(new StringWriter(_buffer));
        Console.SetOut(_writer);
    }

    /// <summary>
    /// Starts capturing. Must be called before the logging provider is created, because the console
    /// logger resolves the output writer once.
    /// </summary>
    public static ConsoleOutputCapture Start() => new();

    /// <summary>
    /// Restores standard output and returns everything captured. The caller is responsible for
    /// stopping the host first so that the console logger has drained its queue.
    /// </summary>
    public string StopAndRead()
    {
        Stop();
        return _buffer.ToString();
    }

    public void Dispose() => Stop();

    private void Stop()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        Console.SetOut(_originalOutput);
        _writer.Flush();
    }
}
