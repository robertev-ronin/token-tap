using System.Diagnostics;
using TokenTap.Core.Models;

namespace TokenTap.Counters;

#pragma warning disable CA1416 // This file is the Windows-specific adapter; public methods guard platform support.

public static class WindowsPerformanceCounterManager
{
    public static readonly string[] GlobalCounterNames =
    [
        "Estimated Daily Cost Cents",
        "Estimated Hourly Cost Cents",
        "Estimated Monthly Cost Cents",
        "Input Tokens Today",
        "Output Tokens Today",
        "Cached Tokens Today",
        "Total Tokens Today",
        "Events Processed Today",
        "Active Sessions",
        "Large Prompt Count Today",
        "Repeated Prompt Count Today",
        "Runaway Agent Warning Count Today",
        "Unknown Model Count Today",
        "Parser Error Count Today",
        "Last Event Age Seconds"
    ];

    public static readonly string[] AgentCounterNames =
    [
        "Estimated Daily Cost Cents",
        "Input Tokens Today",
        "Output Tokens Today",
        "Cached Tokens Today",
        "Total Tokens Today",
        "Active Sessions",
        "Large Prompt Count Today",
        "Repeated Prompt Count Today",
        "Runaway Agent Warning Count Today",
        "Parser Error Count Today",
        "Last Event Age Seconds"
    ];

    public static bool IsSupported => OperatingSystem.IsWindows();

    public static void Install(PerformanceCounterOptions options)
    {
        EnsureWindows();
        RecreateCategory(options.CategoryName, "Live Token-Tap totals", GlobalCounterNames, PerformanceCounterCategoryType.SingleInstance);
        RecreateCategory(options.AgentCategoryName, "Live Token-Tap per-agent totals", AgentCounterNames, PerformanceCounterCategoryType.MultiInstance);
    }

    public static void Uninstall(PerformanceCounterOptions options)
    {
        EnsureWindows();
        DeleteCategoryIfExists(options.CategoryName);
        DeleteCategoryIfExists(options.AgentCategoryName);
    }

    public static IReadOnlyList<string> List(PerformanceCounterOptions options)
    {
        if (!IsSupported)
        {
            return ["Windows Performance Counters are only supported on Windows."];
        }

        List<string> rows = [];
        rows.Add(CategoryExists(options.CategoryName)
            ? $"Category installed: {options.CategoryName}"
            : $"Category missing: {options.CategoryName}");
        rows.Add(CategoryExists(options.AgentCategoryName)
            ? $"Category installed: {options.AgentCategoryName}"
            : $"Category missing: {options.AgentCategoryName}");
        rows.AddRange(GlobalCounterNames.Select(name => $"\\{options.CategoryName}\\{name}"));
        rows.AddRange(AgentCounterNames.Select(name => $"\\{options.AgentCategoryName}(codex)\\{name}"));
        return rows;
    }

    public static void Publish(PerformanceCounterOptions options, CounterSnapshot snapshot)
    {
        EnsureWindows();
        if (!CategoryExists(options.CategoryName) || !CategoryExists(options.AgentCategoryName))
        {
            throw new InvalidOperationException("Performance counter categories are not installed. Run 'token-tap counters install' from an elevated shell.");
        }

        SetSingle(options.CategoryName, "Estimated Daily Cost Cents", snapshot.Totals.EstimatedCostCents);
        SetSingle(options.CategoryName, "Estimated Hourly Cost Cents", snapshot.Totals.EstimatedCostCents);
        SetSingle(options.CategoryName, "Estimated Monthly Cost Cents", snapshot.Totals.EstimatedCostCents);
        SetSingle(options.CategoryName, "Input Tokens Today", snapshot.Totals.InputTokens);
        SetSingle(options.CategoryName, "Output Tokens Today", snapshot.Totals.OutputTokens);
        SetSingle(options.CategoryName, "Cached Tokens Today", snapshot.Totals.CachedTokens);
        SetSingle(options.CategoryName, "Total Tokens Today", snapshot.Totals.TotalTokens);
        SetSingle(options.CategoryName, "Events Processed Today", snapshot.Totals.EventCount);
        SetSingle(options.CategoryName, "Active Sessions", snapshot.ActiveSessions);
        SetSingle(options.CategoryName, "Large Prompt Count Today", snapshot.Totals.LargePromptCount);
        SetSingle(options.CategoryName, "Repeated Prompt Count Today", snapshot.Totals.RepeatedPromptCount);
        SetSingle(options.CategoryName, "Runaway Agent Warning Count Today", snapshot.Totals.RunawayWarningCount);
        SetSingle(options.CategoryName, "Unknown Model Count Today", snapshot.Totals.UnknownModelCount);
        SetSingle(options.CategoryName, "Parser Error Count Today", snapshot.Totals.ParserErrorCount);
        SetSingle(options.CategoryName, "Last Event Age Seconds", snapshot.LastEventAgeSeconds);

        foreach ((string agent, UsageTotals totals) in snapshot.AgentTotals)
        {
            SetInstance(options.AgentCategoryName, agent, "Estimated Daily Cost Cents", totals.EstimatedCostCents);
            SetInstance(options.AgentCategoryName, agent, "Input Tokens Today", totals.InputTokens);
            SetInstance(options.AgentCategoryName, agent, "Output Tokens Today", totals.OutputTokens);
            SetInstance(options.AgentCategoryName, agent, "Cached Tokens Today", totals.CachedTokens);
            SetInstance(options.AgentCategoryName, agent, "Total Tokens Today", totals.TotalTokens);
            SetInstance(options.AgentCategoryName, agent, "Active Sessions", 0);
            SetInstance(options.AgentCategoryName, agent, "Large Prompt Count Today", totals.LargePromptCount);
            SetInstance(options.AgentCategoryName, agent, "Repeated Prompt Count Today", totals.RepeatedPromptCount);
            SetInstance(options.AgentCategoryName, agent, "Runaway Agent Warning Count Today", totals.RunawayWarningCount);
            SetInstance(options.AgentCategoryName, agent, "Parser Error Count Today", totals.ParserErrorCount);
            SetInstance(options.AgentCategoryName, agent, "Last Event Age Seconds", snapshot.LastEventAgeSeconds);
        }
    }

    public static void PublishTestValues(PerformanceCounterOptions options)
    {
        Publish(options, new CounterSnapshot
        {
            Totals = new UsageTotals
            {
                EstimatedCostCents = 42,
                InputTokens = 12_345,
                OutputTokens = 6_789,
                CachedTokens = 123,
                EventCount = 3
            },
            AgentTotals =
            {
                ["codex"] = new UsageTotals
                {
                    EstimatedCostCents = 42,
                    InputTokens = 12_345,
                    OutputTokens = 6_789,
                    CachedTokens = 123,
                    EventCount = 3
                }
            }
        });
    }

    private static void RecreateCategory(string name, string help, IEnumerable<string> counters, PerformanceCounterCategoryType type)
    {
        DeleteCategoryIfExists(name);
        CounterCreationDataCollection collection = [];
        foreach (string counter in counters)
        {
            collection.Add(new CounterCreationData(counter, counter, PerformanceCounterType.NumberOfItems64));
        }

        PerformanceCounterCategory.Create(name, help, type, collection);
    }

    private static void DeleteCategoryIfExists(string name)
    {
        if (CategoryExists(name))
        {
            PerformanceCounterCategory.Delete(name);
        }
    }

    private static bool CategoryExists(string name) =>
        OperatingSystem.IsWindows() && PerformanceCounterCategory.Exists(name);

    private static void SetSingle(string category, string counter, decimal value) =>
        SetSingle(category, counter, DecimalToRaw(value));

    private static void SetSingle(string category, string counter, long value)
    {
        using PerformanceCounter perfCounter = new(category, counter, readOnly: false);
        perfCounter.RawValue = value;
    }

    private static void SetInstance(string category, string instance, string counter, decimal value) =>
        SetInstance(category, instance, counter, DecimalToRaw(value));

    private static void SetInstance(string category, string instance, string counter, long value)
    {
        using PerformanceCounter perfCounter = new(category, counter, instance, readOnly: false);
        perfCounter.RawValue = value;
    }

    private static long DecimalToRaw(decimal value) =>
        (long)Math.Round(value, MidpointRounding.AwayFromZero);

    private static void EnsureWindows()
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("Windows Performance Counters are only supported on Windows.");
        }
    }
}

#pragma warning restore CA1416
