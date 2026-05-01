using System.Text.RegularExpressions;
using TokenTap.Core.Models;

namespace TokenTap.Core.Privacy;

public static partial class SecretRedactor
{
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string redacted = AuthorizationHeaderPattern().Replace(value, "$1[redacted]");
        redacted = AssignmentSecretPattern().Replace(redacted, "$1[redacted]");
        redacted = ApiKeyPattern().Replace(redacted, "[redacted-api-key]");
        redacted = GithubTokenPattern().Replace(redacted, "[redacted-github-token]");
        return redacted;
    }

    public static string BuildExcerpt(string value, PrivacyOptions privacy)
    {
        if (!privacy.StoreShortExcerpts || privacy.MaxExcerptChars <= 0)
        {
            return string.Empty;
        }

        string excerpt = privacy.RedactSecrets ? Redact(value) : value;
        excerpt = excerpt.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        excerpt = WhitespacePattern().Replace(excerpt, " ").Trim();
        return excerpt.Length <= privacy.MaxExcerptChars ? excerpt : excerpt[..privacy.MaxExcerptChars];
    }

    [GeneratedRegex(@"(?i)(authorization\s*:\s*bearer\s+)[A-Za-z0-9._~+/=-]+", RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderPattern();

    [GeneratedRegex(@"(?i)\b(password|secret|token|api[_-]?key)\s*[:=]\s*[""']?[^,\s;""']+", RegexOptions.CultureInvariant)]
    private static partial Regex AssignmentSecretPattern();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9_-]{20,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"\bgh[pousr]_[A-Za-z0-9_]{20,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex GithubTokenPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
