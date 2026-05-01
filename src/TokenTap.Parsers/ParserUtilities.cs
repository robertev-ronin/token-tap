using System.Globalization;
using System.Text.RegularExpressions;
using TokenTap.Core.Models;
using TokenTap.Core.Privacy;

namespace TokenTap.Parsers;

internal static partial class ParserUtilities
{
    public static DateTimeOffset ParseTimestampOrNow(string input)
    {
        Match iso = IsoTimestampPattern().Match(input);
        if (iso.Success && DateTimeOffset.TryParse(iso.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset timestamp))
        {
            return timestamp.ToUniversalTime();
        }

        Match bracketed = BracketedTimestampPattern().Match(input);
        if (bracketed.Success && DateTimeOffset.TryParse(bracketed.Groups["value"].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out timestamp))
        {
            return timestamp.ToUniversalTime();
        }

        return DateTimeOffset.UtcNow;
    }

    public static string DetectModel(string input, string fallback)
    {
        Match match = ModelPattern().Match(input);
        return match.Success ? match.Value.Trim('"', '\'') : fallback;
    }

    public static string DetectAgent(string input, string path, string fallback)
    {
        string combined = $"{path} {input}";
        if (combined.Contains("copilot", StringComparison.OrdinalIgnoreCase))
        {
            return "copilot";
        }

        if (combined.Contains("codex", StringComparison.OrdinalIgnoreCase))
        {
            return "codex";
        }

        if (combined.Contains("openai", StringComparison.OrdinalIgnoreCase))
        {
            return "openai";
        }

        if (combined.Contains("anthropic", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("claude", StringComparison.OrdinalIgnoreCase))
        {
            return "anthropic";
        }

        return fallback;
    }

    public static long EstimateTokens(string text, int charsPerToken)
    {
        int divisor = Math.Max(1, charsPerToken);
        return Math.Max(1, (long)Math.Ceiling(text.Length / (decimal)divisor));
    }

    public static string SourceFileHash(string path) =>
        string.IsNullOrWhiteSpace(path) ? "" : ContentHasher.Sha256FilePath(path);

    public static long ParseTokenCount(Match match)
    {
        string value = match.Groups["value"].Value.Replace(",", "", StringComparison.Ordinal);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0;
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2})?", RegexOptions.CultureInvariant)]
    private static partial Regex IsoTimestampPattern();

    [GeneratedRegex(@"\[(?<value>\d{4}-\d{2}-\d{2}[^\]]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedTimestampPattern();

    [GeneratedRegex(@"(?i)\b(?:gpt-[A-Za-z0-9_.-]+|o[0-9][A-Za-z0-9_.-]*|claude-[A-Za-z0-9_.-]+|copilot[-A-Za-z0-9_.]*)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ModelPattern();
}
