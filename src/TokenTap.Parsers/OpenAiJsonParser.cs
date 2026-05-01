using System.Text.Json;
using TokenTap.Core.Models;

namespace TokenTap.Parsers;

public sealed class OpenAiJsonParser : IUsageLogParser
{
    public string Name => "openai-json-parser";

    public IReadOnlyList<UsageEvent> Parse(string content, ParserContext context)
    {
        List<UsageEvent> events = [];
        foreach (JsonElement element in JsonElementReader.ReadObjects(content))
        {
            if (!TryReadUsage(element, out long inputTokens, out long outputTokens, out long cachedTokens))
            {
                continue;
            }

            string raw = element.GetRawText();
            events.Add(new UsageEvent
            {
                Timestamp = ReadTimestamp(element),
                EventType = "usage",
                Source = "openai-json",
                AgentName = ParserUtilities.DetectAgent(raw, context.SourcePath, context.DefaultAgent),
                Model = ReadString(element, "model") ?? context.Config.DefaultModel,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CachedTokens = cachedTokens,
                Confidence = ConfidenceLevel.Exact,
                SourceFile = context.SourcePath,
                SourceFileHash = ParserUtilities.SourceFileHash(context.SourcePath),
                RawExcerptRedacted = raw
            });
        }

        return events;
    }

    private static bool TryReadUsage(JsonElement element, out long inputTokens, out long outputTokens, out long cachedTokens)
    {
        inputTokens = 0;
        outputTokens = 0;
        cachedTokens = 0;

        JsonElement usage = element.TryGetProperty("usage", out JsonElement usageElement)
            ? usageElement
            : element;

        inputTokens = ReadLong(usage, "input_tokens") ??
            ReadLong(usage, "prompt_tokens") ??
            ReadLong(usage, "inputTokens") ??
            0;

        outputTokens = ReadLong(usage, "output_tokens") ??
            ReadLong(usage, "completion_tokens") ??
            ReadLong(usage, "outputTokens") ??
            0;

        cachedTokens = ReadLong(usage, "cached_tokens") ??
            ReadLong(usage, "cachedTokens") ??
            ReadCachedPromptTokens(usage) ??
            0;

        return inputTokens > 0 || outputTokens > 0 || cachedTokens > 0;
    }

    private static long? ReadCachedPromptTokens(JsonElement usage)
    {
        if (usage.TryGetProperty("prompt_tokens_details", out JsonElement details))
        {
            return ReadLong(details, "cached_tokens");
        }

        if (usage.TryGetProperty("input_token_details", out details))
        {
            return ReadLong(details, "cached_tokens");
        }

        return null;
    }

    private static DateTimeOffset ReadTimestamp(JsonElement element)
    {
        string? timestamp =
            ReadString(element, "timestamp") ??
            ReadString(element, "created_at") ??
            ReadString(element, "time");

        if (timestamp is not null && DateTimeOffset.TryParse(timestamp, out DateTimeOffset parsed))
        {
            return parsed.ToUniversalTime();
        }

        long? created = ReadLong(element, "created");
        if (created is not null)
        {
            return DateTimeOffset.FromUnixTimeSeconds(created.Value);
        }

        return DateTimeOffset.UtcNow;
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

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return null;
    }
}
