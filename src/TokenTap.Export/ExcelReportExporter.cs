using System.Globalization;
using ClosedXML.Excel;
using TokenTap.Core.Models;
using TokenTap.Storage;

namespace TokenTap.Export;

public sealed class ExcelReportExporter
{
    public static async Task ExportAsync(
        TokenTapDatabase database,
        TokenTapConfig config,
        DateRange range,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        UsageTotals totals = await database.GetTotalsAsync(range, cancellationToken);
        IReadOnlyList<DailyUsageAggregate> daily = await database.GetDailyUsageAsync(range, cancellationToken);
        IReadOnlyList<UsageEvent> events = await database.QueryEventsAsync(range, cancellationToken);
        IReadOnlyList<AlertHistoryEntry> alerts = await database.GetAlertHistoryAsync(range, cancellationToken);
        var watchSources = await database.GetWatchedSourcesAsync(cancellationToken);

        using XLWorkbook workbook = new();
        BuildSummarySheet(workbook, range, totals, config);
        BuildDailyUsageSheet(workbook, daily);
        BuildEventsSheet(workbook, events);
        BuildAlertsSheet(workbook, alerts);
        BuildModelsSheet(workbook, config);
        BuildWatchSourcesSheet(workbook, watchSources);
        workbook.AddWorksheet("Sessions").Cell(1, 1).Value = "Session summaries are reserved for wrapper-mode sessions.";
        workbook.AddWorksheet("Anomalies").Cell(1, 1).Value = "Anomaly rows are populated as alert/anomaly rules mature.";

        workbook.SaveAs(outputPath);
    }

    private static void BuildSummarySheet(XLWorkbook workbook, DateRange range, UsageTotals totals, TokenTapConfig config)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Summary");
        object[,] rows =
        {
            { "Date Range", range.Label },
            { "Start", range.StartInclusive.ToString("O") },
            { "End", range.EndExclusive.ToString("O") },
            { "Total Estimated Cost", totals.EstimatedCostDollars },
            { "Currency", config.DefaultCurrency },
            { "Input Tokens", totals.InputTokens },
            { "Output Tokens", totals.OutputTokens },
            { "Cached Tokens", totals.CachedTokens },
            { "Total Tokens", totals.TotalTokens },
            { "Events", totals.EventCount },
            { "Large Prompts", totals.LargePromptCount },
            { "Unknown Models", totals.UnknownModelCount },
            { "Event Retention Days", config.History.EventRetentionDays },
            { "Aggregate Retention Days", config.History.AggregateRetentionDays }
        };

        sheet.Cell(1, 1).Value = "Metric";
        sheet.Cell(1, 2).Value = "Value";
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i, 0]?.ToString();
            sheet.Cell(i + 2, 2).Value = XLCellValue.FromObject(rows[i, 1], CultureInfo.InvariantCulture);
        }

        sheet.Columns().AdjustToContents();
    }

    private static void BuildDailyUsageSheet(XLWorkbook workbook, IReadOnlyList<DailyUsageAggregate> rows)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Daily Usage");
        string[] headers =
        [
            "Date", "Agent", "Source", "Repo Hash", "Model", "Input Tokens", "Output Tokens",
            "Cached Tokens", "Estimated Cost Cents", "Events", "Sessions", "Large Prompts",
            "Repeated Prompts", "Runaway Warnings", "Unknown Models", "Parser Errors"
        ];
        WriteHeaders(sheet, headers);

        for (int i = 0; i < rows.Count; i++)
        {
            DailyUsageAggregate row = rows[i];
            int r = i + 2;
            sheet.Cell(r, 1).Value = row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            sheet.Cell(r, 2).Value = row.AgentName;
            sheet.Cell(r, 3).Value = row.Source;
            sheet.Cell(r, 4).Value = row.RepoPathHash;
            sheet.Cell(r, 5).Value = row.Model;
            sheet.Cell(r, 6).Value = row.InputTokens;
            sheet.Cell(r, 7).Value = row.OutputTokens;
            sheet.Cell(r, 8).Value = row.CachedTokens;
            sheet.Cell(r, 9).Value = row.EstimatedCostCents;
            sheet.Cell(r, 10).Value = row.EventCount;
            sheet.Cell(r, 11).Value = row.SessionCount;
            sheet.Cell(r, 12).Value = row.LargePromptCount;
            sheet.Cell(r, 13).Value = row.RepeatedPromptCount;
            sheet.Cell(r, 14).Value = row.RunawayWarningCount;
            sheet.Cell(r, 15).Value = row.UnknownModelCount;
            sheet.Cell(r, 16).Value = row.ParserErrorCount;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void BuildEventsSheet(XLWorkbook workbook, IReadOnlyList<UsageEvent> events)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Events");
        string[] headers =
        [
            "Timestamp", "Agent", "Source", "Model", "Input Tokens", "Output Tokens",
            "Cached Tokens", "Estimated Cost Cents", "Confidence", "Source File", "Excerpt"
        ];
        WriteHeaders(sheet, headers);

        for (int i = 0; i < events.Count; i++)
        {
            UsageEvent usageEvent = events[i];
            int r = i + 2;
            sheet.Cell(r, 1).Value = usageEvent.Timestamp.ToString("O");
            sheet.Cell(r, 2).Value = usageEvent.AgentName;
            sheet.Cell(r, 3).Value = usageEvent.Source;
            sheet.Cell(r, 4).Value = usageEvent.Model;
            sheet.Cell(r, 5).Value = usageEvent.InputTokens;
            sheet.Cell(r, 6).Value = usageEvent.OutputTokens;
            sheet.Cell(r, 7).Value = usageEvent.CachedTokens;
            sheet.Cell(r, 8).Value = usageEvent.EstimatedCostCents;
            sheet.Cell(r, 9).Value = usageEvent.Confidence.ToStorageValue();
            sheet.Cell(r, 10).Value = usageEvent.SourceFile ?? "";
            sheet.Cell(r, 11).Value = usageEvent.RawExcerptRedacted ?? "";
        }

        sheet.Columns().AdjustToContents();
    }

    private static void BuildAlertsSheet(XLWorkbook workbook, IReadOnlyList<AlertHistoryEntry> alerts)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Alerts");
        string[] headers =
        [
            "Timestamp", "Severity", "Trigger Value", "Threshold", "Message",
            "Windows Sent", "Email Sent", "Suppressed", "Reason"
        ];
        WriteHeaders(sheet, headers);

        for (int i = 0; i < alerts.Count; i++)
        {
            AlertHistoryEntry alert = alerts[i];
            int r = i + 2;
            sheet.Cell(r, 1).Value = alert.Timestamp.ToString("O");
            sheet.Cell(r, 2).Value = alert.Severity;
            sheet.Cell(r, 3).Value = alert.TriggerValue;
            sheet.Cell(r, 4).Value = alert.Threshold;
            sheet.Cell(r, 5).Value = alert.Message;
            sheet.Cell(r, 6).Value = alert.WindowsSent;
            sheet.Cell(r, 7).Value = alert.EmailSent;
            sheet.Cell(r, 8).Value = alert.Suppressed;
            sheet.Cell(r, 9).Value = alert.SuppressionReason ?? "";
        }

        sheet.Columns().AdjustToContents();
    }

    private static void BuildModelsSheet(XLWorkbook workbook, TokenTapConfig config)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Models");
        WriteHeaders(sheet, ["Model", "Provider", "Input Per Million", "Cached Input Per Million", "Output Per Million"]);
        int row = 2;
        foreach ((string name, ModelPricing pricing) in config.Models.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            sheet.Cell(row, 1).Value = name;
            sheet.Cell(row, 2).Value = pricing.Provider;
            sheet.Cell(row, 3).Value = pricing.InputPerMillion;
            sheet.Cell(row, 4).Value = pricing.CachedInputPerMillion;
            sheet.Cell(row, 5).Value = pricing.OutputPerMillion;
            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void BuildWatchSourcesSheet(
        XLWorkbook workbook,
        IReadOnlyList<(string Path, string SourceType, string ParserName, DateTimeOffset? LastSeen)> rows)
    {
        IXLWorksheet sheet = workbook.AddWorksheet("Watch Sources");
        WriteHeaders(sheet, ["Path", "Source Type", "Parser", "Last Seen"]);
        for (int i = 0; i < rows.Count; i++)
        {
            int r = i + 2;
            sheet.Cell(r, 1).Value = rows[i].Path;
            sheet.Cell(r, 2).Value = rows[i].SourceType;
            sheet.Cell(r, 3).Value = rows[i].ParserName;
            sheet.Cell(r, 4).Value = rows[i].LastSeen?.ToString("O") ?? "";
        }

        sheet.Columns().AdjustToContents();
    }

    private static void WriteHeaders(IXLWorksheet sheet, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        sheet.Row(1).Style.Font.Bold = true;
    }
}
