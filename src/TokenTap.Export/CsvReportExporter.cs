using System.Globalization;
using System.Text;
using TokenTap.Core.Models;
using TokenTap.Storage;

namespace TokenTap.Export;

public sealed class CsvReportExporter
{
    public static async Task ExportAsync(TokenTapDatabase database, DateRange range, string outputPath, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        IReadOnlyList<UsageEvent> events = await database.QueryEventsAsync(range, cancellationToken);
        await using StreamWriter writer = new(outputPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteLineAsync("Timestamp,Agent,Source,Model,InputTokens,OutputTokens,CachedTokens,EstimatedCostCents,Confidence,SourceFile,Excerpt");

        foreach (UsageEvent usageEvent in events)
        {
            string line = string.Join(
                ",",
                Csv(usageEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture)),
                Csv(usageEvent.AgentName),
                Csv(usageEvent.Source),
                Csv(usageEvent.Model),
                usageEvent.InputTokens.ToString(CultureInfo.InvariantCulture),
                usageEvent.OutputTokens.ToString(CultureInfo.InvariantCulture),
                usageEvent.CachedTokens.ToString(CultureInfo.InvariantCulture),
                usageEvent.EstimatedCostCents.ToString(CultureInfo.InvariantCulture),
                Csv(usageEvent.Confidence.ToStorageValue()),
                Csv(usageEvent.SourceFile ?? ""),
                Csv(usageEvent.RawExcerptRedacted ?? ""));
            await writer.WriteLineAsync(line);
        }
    }

    private static string Csv(string value)
    {
        bool mustQuote = value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('"', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal) ||
            value.Contains('\r', StringComparison.Ordinal);

        if (!mustQuote)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
