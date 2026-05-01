using System.Text.Json;
using TokenTap.Core.Models;

namespace TokenTap.Parsers;

public sealed class AnthropicJsonParser : IUsageLogParser
{
    public string Name => "anthropic-json-parser";

    public IReadOnlyList<UsageEvent> Parse(string content, ParserContext context)
    {
        List<UsageEvent> events = [];
        foreach (JsonElement element in JsonElementReader.ReadObjects(content))
        {
            JsonElement usage = element.TryGetProperty("usage", out JsonElement usageElement)
                ? usageElement
                : element;

            long input = ReadLong(usage, "input_tokens") ?? 0;
            long output = ReadLong(usage, "output_tokens") ?? 0;
            long cacheRead = ReadLong(usage, "cache_read_input_tokens") ?? 0;
            long cacheCreated = ReadLong(usage, "cache_creation_input_tokens") ?? 0;
            long cached = cacheRead + cacheCreated;

            if (input == 0 && output == 0 && cached == 0)
            {
                continue;
            }

            string raw = element.GetRawText();
            events.Add(new UsageEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                EventType = "usage",
                Source = "anthropic-json",
                AgentName = "anthropic",
                Model = ReadString(element, "model") ?? ParserUtilities.DetectModel(raw, context.Config.DefaultModel),
                InputTokens = input,
                OutputTokens = output,
                CachedTokens = cached,
                Confidence = ConfidenceLevel.Exact,
                SourceFile = context.SourcePath,
                SourceFileHash = ParserUtilities.SourceFileHash(context.SourcePath),
                RawExcerptRedacted = raw
            });
        }

        return events;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? ReadLong(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number))
        {
            return number;
        }

        return null;
    }
}
