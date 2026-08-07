using System.Diagnostics.CodeAnalysis;

namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Fixed policy for the correlation identifier that ties a request, its response and its technical
/// logs together.
/// </summary>
/// <remarks>
/// A caller supplied identifier is convenient for tracing but is untrusted input that is echoed
/// into a response header and into log output. It is therefore accepted only when it is a single
/// short token of ASCII letters, digits and hyphens, which excludes control characters, header and
/// log injection sequences, markup and unbounded values. Every other value is discarded rather than
/// sanitised, because a partially rewritten identifier is no longer the caller's identifier and
/// would silently break tracing.
/// </remarks>
public static class CorrelationIdPolicy
{
    /// <summary>
    /// Request and response header carrying the correlation identifier.
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// Maximum accepted length of a caller supplied correlation identifier.
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Returns whether a caller supplied value may be reused as the correlation identifier.
    /// </summary>
    public static bool IsAcceptable([NotNullWhen(true)] string? candidate)
    {
        if (string.IsNullOrEmpty(candidate) || candidate.Length > MaxLength)
        {
            return false;
        }

        foreach (char character in candidate)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Creates a new correlation identifier that satisfies <see cref="IsAcceptable"/>.
    /// </summary>
    public static string Create() => Guid.NewGuid().ToString("N");
}
