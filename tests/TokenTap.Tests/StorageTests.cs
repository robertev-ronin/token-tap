using TokenTap.Core;
using TokenTap.Core.Models;
using TokenTap.Storage;

namespace TokenTap.Tests;

public sealed class StorageTests
{
    [Fact]
    public async Task Database_InsertsEventsIdempotentlyAndReturnsTotals()
    {
        TokenTapConfig config = TokenTapConfig.CreateDefault();
        TokenTapDatabase database = await CreateDatabaseAsync();
        UsageEvent usageEvent = CreateEvent(DateTimeOffset.UtcNow, input: 100, output: 50);
        UsageEventFactory.FinalizeEvent(usageEvent, config, "test prompt");

        int firstInsert = await database.InsertUsageEventsAsync([usageEvent]);
        int secondInsert = await database.InsertUsageEventsAsync([usageEvent]);
        UsageTotals totals = await database.GetTotalsAsync(DateRange.Today());

        Assert.Equal(1, firstInsert);
        Assert.Equal(0, secondInsert);
        Assert.Equal(1, totals.EventCount);
        Assert.Equal(100, totals.InputTokens);
        Assert.Equal(50, totals.OutputTokens);
    }

    [Fact]
    public async Task Cleanup_RollsOldEventsIntoDailyAggregatesBeforeDeletingDetails()
    {
        TokenTapConfig config = TokenTapConfig.CreateDefault();
        TokenTapDatabase database = await CreateDatabaseAsync();
        DateTimeOffset oldTimestamp = DateTimeOffset.UtcNow.AddDays(-10);
        UsageEvent oldEvent = CreateEvent(oldTimestamp, input: 10, output: 5);
        UsageEventFactory.FinalizeEvent(oldEvent, config, "old prompt");
        await database.InsertUsageEventsAsync([oldEvent]);

        CleanupResult result = await database.CleanupAsync(
            config.History,
            dryRun: false,
            vacuum: false,
            olderThan: TimeSpan.FromDays(1));

        DateTimeOffset oldDayStart = new DateTimeOffset(oldTimestamp.Year, oldTimestamp.Month, oldTimestamp.Day, 0, 0, 0, TimeSpan.Zero);
        DateRange oldRange = new(oldDayStart, oldDayStart.AddDays(1), "old");
        UsageTotals totals = await database.GetTotalsAsync(oldRange);
        IReadOnlyList<UsageEvent> events = await database.QueryEventsAsync(oldRange);

        Assert.Equal(1, result.EventsDeleted);
        Assert.Empty(events);
        Assert.Equal(1, totals.EventCount);
        Assert.Equal(10, totals.InputTokens);
    }

    [Fact]
    public async Task Database_RecordsWatchedSourcesAndAlertHistory()
    {
        TokenTapDatabase database = await CreateDatabaseAsync();
        await database.RecordWatchedSourceAsync("C:\\logs\\codex.log", "file", "composite");
        await database.InsertAlertHistoryAsync(new AlertHistoryEntry
        {
            Severity = "warning",
            TriggerValue = 26,
            Threshold = 25,
            Message = "threshold"
        });

        var sources = await database.GetWatchedSourcesAsync();
        IReadOnlyList<AlertHistoryEntry> alerts = await database.GetAlertHistoryAsync(DateRange.Today());

        Assert.Single(sources);
        Assert.Single(alerts);
    }

    private static async Task<TokenTapDatabase> CreateDatabaseAsync()
    {
        string root = TestPaths.CreateDirectory();
        TokenTapDatabase database = new(Path.Combine(root, "token-tap.db"));
        await database.InitializeAsync();
        return database;
    }

    private static UsageEvent CreateEvent(DateTimeOffset timestamp, long input, long output) =>
        new()
        {
            Timestamp = timestamp,
            Source = "test",
            AgentName = "codex",
            Model = "gpt-5.4",
            InputTokens = input,
            OutputTokens = output,
            Confidence = ConfidenceLevel.Exact
        };
}
