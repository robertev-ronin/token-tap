using System.Text.RegularExpressions;
using TokenTap.Core.Models;

namespace TokenTap.Parsers;

public partial class GenericTextParser : IUsageLogParser
{
    public virtual string Name => "generic-text-parser";

    public virtual IReadOnlyList<UsageEvent> Parse(string content, ParserContext context)
    {
        List<UsageEvent> events = [];
        using StringReader reader = new(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            UsageEvent? parsed = ParseLine(line, context);
            if (parsed is not null)
            {
                events.Add(parsed);
            }
        }

        return events;
    }

    protected virtual UsageEvent? ParseLine(string line, ParserContext context)
    {
        if (string.IsNullOrWhiteSpace(line) || !LooksRelevant(line, context.SourcePath))
        {
            return null;
        }

        long input = ReadTokenValue(InputTokenPattern(), line);
        long output = ReadTokenValue(OutputTokenPattern(), line);
        long cached = ReadTokenValue(CachedTokenPattern(), line);
        ConfidenceLevel confidence = ConfidenceLevel.Exact;

        if (input == 0 && output == 0 && cached == 0)
        {
            if (!ShouldEstimate(line))
            {
                return null;
            }

            input = ParserUtilities.EstimateTokens(line, context.Config.Estimation.CharsPerToken);
            output = Math.Max(1, (long)Math.Round(input * context.Config.Estimation.DefaultOutputToInputRatio));
            confidence = ConfidenceLevel.Inferred;
        }

        string agent = ParserUtilities.DetectAgent(line, context.SourcePath, context.DefaultAgent);
        string model = ParserUtilities.DetectModel(line, agent == "copilot" ? "copilot-estimated" : context.Config.DefaultModel);

        return new UsageEvent
        {
            Timestamp = ParserUtilities.ParseTimestampOrNow(line),
            EventType = "usage",
            Source = context.DefaultSource,
            AgentName = agent,
            Model = model,
            InputTokens = input,
            OutputTokens = output,
            CachedTokens = cached,
            Confidence = confidence,
            SourceFile = context.SourcePath,
            SourceFileHash = ParserUtilities.SourceFileHash(context.SourcePath),
            RawExcerptRedacted = line
        };
    }

    private static bool LooksRelevant(string line, string sourcePath)
    {
        string combined = $"{sourcePath} {line}";
        return combined.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("usage", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("prompt", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("completion", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("response", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("codex", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("copilot", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("openai", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("anthropic", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldEstimate(string line) =>
        line.Contains("prompt", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("completion", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("response", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("request", StringComparison.OrdinalIgnoreCase);

    private static long ReadTokenValue(Regex pattern, string line)
    {
        Match match = pattern.Match(line);
        return match.Success ? ParserUtilities.ParseTokenCount(match) : 0;
    }

    [GeneratedRegex(@"(?i)(?:input|prompt)[_\s-]*tokens?[""']?\s*[:=]\s*[""']?(?<value>[0-9][0-9,]*)", RegexOptions.CultureInvariant)]
    private static partial Regex InputTokenPattern();

    [GeneratedRegex(@"(?i)(?:output|completion|response)[_\s-]*tokens?[""']?\s*[:=]\s*[""']?(?<value>[0-9][0-9,]*)", RegexOptions.CultureInvariant)]
    private static partial Regex OutputTokenPattern();

    [GeneratedRegex(@"(?i)(?:cached|cache[_\s-]*read|cache[_\s-]*creation)[_\s-]*tokens?[""']?\s*[:=]\s*[""']?(?<value>[0-9][0-9,]*)", RegexOptions.CultureInvariant)]
    private static partial Regex CachedTokenPattern();
}
