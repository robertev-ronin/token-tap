using TokenTap.Alerts;
using TokenTap.Core;
using TokenTap.Core.Models;
using TokenTap.Counters;
using TokenTap.Export;
using TokenTap.Storage;

namespace TokenTap.Tests;

public sealed class ExportAlertCounterTests
{
    [Fact]
    public async Task Exporters_CreateCsvAndXlsxFiles()
    {
        string root = TestPaths.CreateDirectory();
        TokenTapConfig config = TokenTapConfig.CreateDefault();
        TokenTapDatabase database = new(Path.Combine(root, "token-tap.db"));
        await database.InitializeAsync();

        UsageEvent usageEvent = new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Source = "test",
            AgentName = "codex",
            Model = "gpt-5.4",
            InputTokens = 20,
            OutputTokens = 10
        };
        UsageEventFactory.FinalizeEvent(usageEvent, config, "export prompt");
        await database.InsertUsageEventsAsync([usageEvent]);

        string csv = Path.Combine(root, "report.csv");
        string xlsx = Path.Combine(root, "report.xlsx");
        await CsvReportExporter.ExportAsync(database, DateRange.Today(), csv);
        await ExcelReportExporter.ExportAsync(database, config, DateRange.Today(), xlsx);

        Assert.True(new FileInfo(csv).Length > 0);
        Assert.True(new FileInfo(xlsx).Length > 0);
    }

    [Fact]
    public void AlertEvaluator_FiresDailyCostRule()
    {
        TokenTapConfig config = TokenTapConfig.CreateDefault();
        UsageTotals totals = new()
        {
            EstimatedCostCents = 2_600
        };

        IReadOnlyList<AlertDecision> decisions = AlertEvaluator.Evaluate(config, totals, []);

        Assert.Contains(decisions, decision => decision.Rule.Type == "daily_cost" && decision.Rule.Severity == "warning");
    }

    [Fact]
    public void CounterManager_ListsExpectedCounterNames()
    {
        IReadOnlyList<string> rows = WindowsPerformanceCounterManager.List(new PerformanceCounterOptions());

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains(rows, row => row.Contains("Estimated Daily Cost Cents", StringComparison.Ordinal));
            Assert.Contains(rows, row => row.Contains("TokenTap", StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(rows, row => row.Contains("only supported on Windows", StringComparison.Ordinal));
        }
    }
}
