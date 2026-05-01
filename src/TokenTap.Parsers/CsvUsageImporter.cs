using System.Globalization;
using TokenTap.Core;
using TokenTap.Core.Models;

namespace TokenTap.Parsers;

public sealed class CsvUsageImporter
{
    public static async Task<IReadOnlyList<UsageEvent>> ImportAsync(
        string path,
        TokenTapConfig config,
        CancellationToken cancellationToken = default)
    {
        string[] lines = await File.ReadAllLinesAsync(path, cancellationToken);
        if (lines.Length == 0)
        {
            return [];
        }

        string[] headers = SplitCsvLine(lines[0]);
        Dictionary<string, int> map = headers
            .Select((header, index) => new { Header = Normalize(header), Index = index })
            .ToDictionary(x => x.Header, x => x.Index, StringComparer.OrdinalIgnoreCase);

        List<UsageEvent> events = [];
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            string[] columns = SplitCsvLine(lines[i]);
            UsageEvent usageEvent = new()
            {
                Timestamp = ReadDate(columns, map, "timestamp") ?? ReadDate(columns, map, "date") ?? DateTimeOffset.UtcNow,
                Source = Read(columns, map, "source") ?? "csv",
                AgentName = Read(columns, map, "agent") ?? Read(columns, map, "agentname") ?? "manual",
                Model = Read(columns, map, "model") ?? config.DefaultModel,
                InputTokens = ReadLong(columns, map, "inputtokens"),
                OutputTokens = ReadLong(columns, map, "outputtokens"),
                CachedTokens = ReadLong(columns, map, "cachedtokens"),
                Confidence = ConfidenceLevelExtensions.Parse(Read(columns, map, "confidence")),
                SourceFile = path,
                SourceFileHash = TokenTap.Core.Privacy.ContentHasher.Sha256FilePath(path),
                RawExcerptRedacted = lines[i]
            };

            UsageEventFactory.FinalizeEvent(usageEvent, config, lines[i]);
            events.Add(usageEvent);
        }

        return events;
    }

    private static string? Read(string[] columns, IReadOnlyDictionary<string, int> map, string name)
    {
        if (!map.TryGetValue(name, out int index) || index >= columns.Length)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(columns[index]) ? null : columns[index];
    }

    private static long ReadLong(string[] columns, IReadOnlyDictionary<string, int> map, string name)
    {
        string? value = Read(columns, map, name);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0;
    }

    private static DateTimeOffset? ReadDate(string[] columns, IReadOnlyDictionary<string, int> map, string name)
    {
        string? value = Read(columns, map, name);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static string Normalize(string value) =>
        value.Replace(" ", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

    // This intentionally supports the common CSV subset used by usage exports without
    // bringing a full CSV dependency into the parser layer.
    private static string[] SplitCsvLine(string line)
    {
        List<string> fields = [];
        bool inQuotes = false;
        int start = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == ',' && !inQuotes)
            {
                fields.Add(Unquote(line[start..i]));
                start = i + 1;
            }
        }

        fields.Add(Unquote(line[start..]));
        return fields.ToArray();
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        }

        return value;
    }
}
