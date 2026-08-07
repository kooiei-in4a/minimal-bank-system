using System.Text;
using System.Text.RegularExpressions;

namespace MinimalBankSystem.Api.Logging;

public sealed class RedactingTextWriter : TextWriter
{
    private readonly TextWriter _inner;
    private readonly ISet<string> _prohibitedNames;

    public RedactingTextWriter(TextWriter inner, ISet<string> prohibitedNames)
        : base(inner.FormatProvider)
    {
        _inner = inner;
        _prohibitedNames = prohibitedNames;
    }

    public override Encoding Encoding => _inner.Encoding;

    public override void Write(char value)
    {
        _inner.Write(value);
    }

    public override void Write(string? value)
    {
        _inner.Write(Redact(value ?? ""));
    }

    public override void WriteLine(string? value)
    {
        _inner.WriteLine(Redact(value ?? ""));
    }

    private string Redact(string input)
    {
        foreach (var name in _prohibitedNames)
        {
            input = Regex.Replace(
                input,
                $@"""{Regex.Escape(name)}""\s*:\s*""[^""]*""",
                $@"""{name}"":""[REDACTED]""",
                RegexOptions.IgnoreCase);
        }
        return input;
    }
}
