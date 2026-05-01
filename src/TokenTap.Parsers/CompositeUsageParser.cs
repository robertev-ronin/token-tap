using TokenTap.Core;
using TokenTap.Core.Models;
using TokenTap.Core.Privacy;

namespace TokenTap.Parsers;

public sealed class CompositeUsageParser
{
    private readonly IReadOnlyList<IUsageLogParser> _parsers;
    public CompositeUsageParser(IEnumerable<IUsageLogParser>? parsers = null)
    {
        _parsers = (parsers ?? CreateDefaultParsers()).ToArray();
    }

    public async Task<IReadOnlyList<UsageEvent>> ParseFileAsync(
        string path,
        TokenTapConfig config,
        string? defaultAgent = null,
        CancellationToken cancellationToken = default)
    {
        string content = await File.ReadAllTextAsync(path, cancellationToken);
        ParserContext context = new()
        {
            SourcePath = path,
            DefaultAgent = defaultAgent ?? ParserUtilities.DetectAgent("", path, "unknown"),
            DefaultSource = InferSource(path),
            Config = config
        };

        return Parse(content, context);
    }

    public IReadOnlyList<UsageEvent> Parse(string content, ParserContext context)
    {
        Dictionary<string, UsageEvent> byFingerprint = new(StringComparer.OrdinalIgnoreCase);
        foreach (IUsageLogParser parser in _parsers)
        {
            foreach (UsageEvent usageEvent in parser.Parse(content, context))
            {
                string raw = usageEvent.RawExcerptRedacted ?? content;
                usageEvent.RawExcerptRedacted = context.Config.Privacy.RedactSecrets
                    ? SecretRedactor.BuildExcerpt(raw, context.Config.Privacy)
                    : raw;

                UsageEventFactory.FinalizeEvent(usageEvent, context.Config, raw);
                byFingerprint.TryAdd(usageEvent.EventFingerprint, usageEvent);
            }
        }

        return byFingerprint.Values
            .OrderBy(e => e.Timestamp)
            .ToArray();
    }

    private static IReadOnlyList<IUsageLogParser> CreateDefaultParsers() =>
    [
        new OpenAiJsonParser(),
        new AnthropicJsonParser(),
        new CodexLogParser(),
        new CopilotLogParser(),
        new GenericTextParser()
    ];

    private static string InferSource(string path)
    {
        string lower = path.ToLowerInvariant();
        if (lower.Contains("copilot", StringComparison.Ordinal))
        {
            return "vscode-copilot";
        }

        if (lower.Contains("codex", StringComparison.Ordinal))
        {
            return "vscode-codex";
        }

        if (lower.Contains("code", StringComparison.Ordinal))
        {
            return "vscode";
        }

        return "log";
    }
}
