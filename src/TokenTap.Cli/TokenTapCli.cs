using System.Diagnostics;
using TokenTap.Alerts;
using TokenTap.Core;
using TokenTap.Core.Config;
using TokenTap.Core.Models;
using TokenTap.Core.Privacy;
using TokenTap.Core.Retention;
using TokenTap.Counters;
using TokenTap.Export;
using TokenTap.Parsers;
using TokenTap.Storage;

namespace TokenTap.Cli;

public sealed class TokenTapCli
{
    private readonly IConsole _console;

    public TokenTapCli(IConsole console)
    {
        _console = console;
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                await PrintHelpAsync();
                return 0;
            }

            string command = args[0].ToLowerInvariant();
            CliArgs cliArgs = new(args.Skip(1).ToArray());
            return command switch
            {
                "init" => await InitAsync(cliArgs, cancellationToken),
                "detect" => await DetectAsync(cliArgs, cancellationToken),
                "watch-add" => await WatchAddAsync(cliArgs, cancellationToken),
                "watch" => await WatchAsync(cliArgs, cancellationToken),
                "import" => await ImportAsync(cliArgs, cancellationToken),
                "today" => await ReportAsync(new CliArgs(["--today", .. args.Skip(1)]), cancellationToken),
                "report" => await ReportAsync(cliArgs, cancellationToken),
                "top" => await TopAsync(cliArgs, cancellationToken),
                "export" => await ExportAsync(cliArgs, cancellationToken),
                "cleanup" => await CleanupAsync(cliArgs, cancellationToken),
                "db" => await DatabaseAsync(cliArgs, cancellationToken),
                "retention" => await RetentionAsync(cliArgs, cancellationToken),
                "config" => await ConfigAsync(cliArgs, cancellationToken),
                "counters" => await CountersAsync(cliArgs, cancellationToken),
                "alerts" => await AlertsAsync(cliArgs, cancellationToken),
                "smtp" => await SmtpAsync(cliArgs, cancellationToken),
                "run" => await RunWrapperAsync(cliArgs, cancellationToken),
                _ => await UnknownCommandAsync(command)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _console.ErrorOut.WriteLineAsync($"token-tap: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> InitAsync(CliArgs args, CancellationToken cancellationToken)
    {
        ConfigManager manager = CreateConfigManager(args);
        TokenTapConfig config = await manager.EnsureExistsAsync(cancellationToken);
        if (args.Value("--database") is { } databasePath)
        {
            config.DatabasePath = databasePath;
            await manager.SaveAsync(config, cancellationToken);
        }

        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        await database.UpsertModelsAsync(config.Models, cancellationToken);

        await _console.Out.WriteLineAsync($"Config: {manager.ConfigPath}");
        await _console.Out.WriteLineAsync($"Database: {database.DatabasePath}");
        await _console.Out.WriteLineAsync("Token-Tap initialized.");
        return 0;
    }

    private async Task<int> DetectAsync(CliArgs args, CancellationToken cancellationToken)
    {
        ConfigManager manager = CreateConfigManager(args);
        TokenTapConfig config = await manager.LoadOrDefaultAsync(cancellationToken);
        IReadOnlyList<string> folders = VsCodeSourceDetector.DetectExistingFolders(config.WatchFolders);

        foreach (string folder in folders)
        {
            await _console.Out.WriteLineAsync(folder);
        }

        if (folders.Count == 0)
        {
            await _console.Out.WriteLineAsync("No configured VS Code log folders exist yet.");
        }

        if (args.Has("--save") && folders.Count > 0)
        {
            config.WatchFolders = folders.ToList();
            await manager.SaveAsync(config, cancellationToken);
            await _console.Out.WriteLineAsync("Detected folders saved to config.");
        }

        return 0;
    }

    private async Task<int> WatchAddAsync(CliArgs args, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> positionals = args.Positionals();
        string? folder = positionals.Count > 0 ? positionals[0] : null;
        if (folder is null)
        {
            throw new ArgumentException("watch-add requires a folder path.");
        }

        ConfigManager manager = CreateConfigManager(args);
        TokenTapConfig config = await manager.LoadOrDefaultAsync(cancellationToken);
        if (!config.WatchFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            config.WatchFolders.Add(folder);
            await manager.SaveAsync(config, cancellationToken);
        }

        await _console.Out.WriteLineAsync($"Watching: {folder}");
        return 0;
    }

    private async Task<int> ImportAsync(CliArgs args, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> positionals = args.Positionals();
        if (positionals.Count < 2)
        {
            throw new ArgumentException("Use: token-tap import log <file> | folder <folder> | csv <file>");
        }

        ConfigManager manager = CreateConfigManager(args);
        TokenTapConfig config = await manager.LoadOrDefaultAsync(cancellationToken);
        if (args.Value("--model") is { } importModel)
        {
            config.DefaultModel = importModel;
        }

        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        string kind = positionals[0].ToLowerInvariant();
        string path = EnvironmentPathExpander.Expand(positionals[1]);
        string? agent = args.Value("--agent");

        int imported = kind switch
        {
            "log" => await ImportLogFileAsync(path, config, database, agent, cancellationToken),
            "folder" => await ImportFolderAsync(path, config, database, agent, cancellationToken),
            "csv" => await ImportCsvAsync(path, config, database, cancellationToken),
            _ => throw new ArgumentException($"Unknown import type '{kind}'.")
        };

        await _console.Out.WriteLineAsync($"Imported {imported} new event(s).");
        return 0;
    }

    private async Task<int> WatchAsync(CliArgs args, CancellationToken cancellationToken)
    {
        ConfigManager manager = CreateConfigManager(args);
        TokenTapConfig config = await manager.LoadOrDefaultAsync(cancellationToken);
        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        bool publishCounters = args.Has("--publish-counters");
        int intervalSeconds = Math.Max(1, args.IntValue("--interval", config.PerformanceCounters.PublishIntervalSeconds));

        do
        {
            int imported = 0;
            foreach (string folder in VsCodeSourceDetector.DetectExistingFolders(config.WatchFolders))
            {
                imported += await ImportFolderAsync(folder, config, database, null, cancellationToken);
            }

            if (publishCounters)
            {
                await PublishCountersAsync(config, database, cancellationToken);
            }

            await _console.Out.WriteLineAsync($"{DateTimeOffset.Now:HH:mm:ss} scan complete; {imported} new event(s).");
            if (args.Has("--once"))
            {
                return 0;
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
        }
        while (!cancellationToken.IsCancellationRequested);

        return 0;
    }

    private async Task<int> ReportAsync(CliArgs args, CancellationToken cancellationToken)
    {
        TokenTapConfig config = await CreateConfigManager(args).LoadOrDefaultAsync(cancellationToken);
        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        DateRange range = ResolveRange(args);
        UsageTotals totals = await database.GetTotalsAsync(range, cancellationToken);

        await _console.Out.WriteLineAsync($"Range: {range.Label}");
        await _console.Out.WriteLineAsync($"Events: {totals.EventCount}");
        await _console.Out.WriteLineAsync($"Input tokens: {totals.InputTokens:N0}");
        await _console.Out.WriteLineAsync($"Output tokens: {totals.OutputTokens:N0}");
        await _console.Out.WriteLineAsync($"Cached tokens: {totals.CachedTokens:N0}");
        await _console.Out.WriteLineAsync($"Estimated cost: {totals.EstimatedCostDollars:C}");
        return 0;
    }

    private async Task<int> TopAsync(CliArgs args, CancellationToken cancellationToken)
    {
        TokenTapConfig config = await CreateConfigManager(args).LoadOrDefaultAsync(cancellationToken);
        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        DateRange range = ResolveRange(args);
        IReadOnlyList<UsageEvent> events = await database.QueryEventsAsync(range, cancellationToken);
        string by = args.Value("--by") ?? "cost";

        var rows = events
            .GroupBy(e => new { e.AgentName, e.Model })
            .Select(group => new
            {
                group.Key.AgentName,
                group.Key.Model,
                Cost = group.Sum(e => e.EstimatedCostCents),
                Tokens = group.Sum(e => e.TotalTokens),
                Events = group.Count()
            })
            .OrderByDescending(row => by.Equals("tokens", StringComparison.OrdinalIgnoreCase) ? row.Tokens : (long)Math.Round(row.Cost))
            .Take(10);

        foreach (var row in rows)
        {
            await _console.Out.WriteLineAsync($"{row.AgentName,-12} {row.Model,-20} events={row.Events,4} tokens={row.Tokens,10:N0} cost={(row.Cost / 100m):C}");
        }

        return 0;
    }

    private async Task<int> ExportAsync(CliArgs args, CancellationToken cancellationToken)
    {
        TokenTapConfig config = await CreateConfigManager(args).LoadOrDefaultAsync(cancellationToken);
        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        DateRange range = ResolveRange(args);
        string format = (args.Value("--format") ?? "csv").ToLowerInvariant();
        string output = args.Value("--out") ?? $"token-tap-{range.Label}.{format}";

        if (format == "csv")
        {
            await CsvReportExporter.ExportAsync(database, range, output, cancellationToken);
        }
        else if (format is "xlsx" or "excel")
        {
            await ExcelReportExporter.ExportAsync(database, config, range, output, cancellationToken);
        }
        else
        {
            throw new ArgumentException("Export format must be csv or xlsx.");
        }

        await _console.Out.WriteLineAsync($"Exported {Path.GetFullPath(output)}");
        return 0;
    }

    private async Task<int> CleanupAsync(CliArgs args, CancellationToken cancellationToken)
    {
        TokenTapConfig config = await CreateConfigManager(args).LoadOrDefaultAsync(cancellationToken);
        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        TimeSpan? olderThan = args.Value("--older-than") is { } value ? DurationSpecParser.Parse(value) : null;
        CleanupResult result = await database.CleanupAsync(config.History, args.Has("--dry-run"), args.Has("--vacuum"), olderThan, cancellationToken);

        await _console.Out.WriteLineAsync(result.Message);
        await _console.Out.WriteLineAsync($"Events deleted: {result.EventsDeleted}");
        await _console.Out.WriteLineAsync($"Alerts deleted: {result.AlertsDeleted}");
        await _console.Out.WriteLineAsync($"Database: {result.DatabaseSizeBeforeMb:N3} MB -> {result.DatabaseSizeAfterMb:N3} MB");
        return 0;
    }

    private async Task<int> DatabaseAsync(CliArgs args, CancellationToken cancellationToken)
    {
        TokenTapConfig config = await CreateConfigManager(args).LoadOrDefaultAsync(cancellationToken);
        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        IReadOnlyList<string> positionals = args.Positionals();
        string subcommand = positionals.Count > 0 ? positionals[0].ToLowerInvariant() : "size";

        if (subcommand == "compact")
        {
            await database.CompactAsync(cancellationToken);
            await _console.Out.WriteLineAsync("Database compacted.");
            return 0;
        }

        DatabaseSize size = database.GetDatabaseSize();
        await _console.Out.WriteLineAsync($"{size.Path}");
        await _console.Out.WriteLineAsync($"{size.Bytes} bytes ({size.Megabytes:N3} MB)");
        return 0;
    }

    private async Task<int> RetentionAsync(CliArgs args, CancellationToken cancellationToken)
    {
        ConfigManager manager = CreateConfigManager(args);
        TokenTapConfig config = await manager.LoadOrDefaultAsync(cancellationToken);
        IReadOnlyList<string> positionals = args.Positionals();
        string subcommand = positionals.Count > 0 ? positionals[0].ToLowerInvariant() : "show";

        if (subcommand == "set")
        {
            if (positionals.Count < 3)
            {
                throw new ArgumentException("Use: token-tap retention set events 14d");
            }

            int days = (int)Math.Ceiling(DurationSpecParser.Parse(positionals[2]).TotalDays);
            switch (positionals[1].ToLowerInvariant())
            {
                case "events":
                    config.History.EventRetentionDays = days;
                    break;
                case "sessions":
                    config.History.RetentionDays = days;
                    break;
                case "aggregates":
                    config.History.AggregateRetentionDays = days;
                    break;
                case "alerts":
                    config.History.AlertRetentionDays = days;
                    break;
                case "anomalies":
                    config.History.AnomalyRetentionDays = days;
                    break;
                default:
                    throw new ArgumentException("Retention target must be events, sessions, aggregates, alerts, or anomalies.");
            }

            await manager.SaveAsync(config, cancellationToken);
        }

        await _console.Out.WriteLineAsync($"Events: {config.History.EventRetentionDays}d");
        await _console.Out.WriteLineAsync($"Sessions: {config.History.RetentionDays}d");
        await _console.Out.WriteLineAsync($"Aggregates: {config.History.AggregateRetentionDays}d");
        await _console.Out.WriteLineAsync($"Alerts: {config.History.AlertRetentionDays}d");
        await _console.Out.WriteLineAsync($"Anomalies: {config.History.AnomalyRetentionDays}d");
        return 0;
    }

    private async Task<int> ConfigAsync(CliArgs args, CancellationToken cancellationToken)
    {
        ConfigManager manager = CreateConfigManager(args);
        TokenTapConfig config = await manager.LoadOrDefaultAsync(cancellationToken);
        IReadOnlyList<string> positionals = args.Positionals();
        string subcommand = positionals.Count > 0 ? positionals[0].ToLowerInvariant() : "path";

        switch (subcommand)
        {
            case "path":
                await _console.Out.WriteLineAsync(manager.ConfigPath);
                break;
            case "set-default-model":
                if (positionals.Count < 2)
                {
                    throw new ArgumentException("Use: token-tap config set-default-model <model>");
                }

                config.DefaultModel = positionals[1];
                await manager.SaveAsync(config, cancellationToken);
                await _console.Out.WriteLineAsync($"Default model: {config.DefaultModel}");
                break;
            case "set-model":
                if (positionals.Count < 2)
                {
                    throw new ArgumentException("Use: token-tap config set-model <model> --input 2 --cached-input .5 --output 12");
                }

                config.Models[positionals[1]] = new ModelPricing
                {
                    Provider = args.Value("--provider") ?? "custom",
                    InputPerMillion = args.DecimalValue("--input", 0m),
                    CachedInputPerMillion = args.DecimalValue("--cached-input", 0m),
                    OutputPerMillion = args.DecimalValue("--output", 0m),
                    EffectiveDate = DateTimeOffset.UtcNow
                };
                await manager.SaveAsync(config, cancellationToken);
                await _console.Out.WriteLineAsync($"Model pricing saved: {positionals[1]}");
                break;
            default:
                throw new ArgumentException($"Unknown config command '{subcommand}'.");
        }

        return 0;
    }

    private async Task<int> CountersAsync(CliArgs args, CancellationToken cancellationToken)
    {
        TokenTapConfig config = await CreateConfigManager(args).LoadOrDefaultAsync(cancellationToken);
        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        IReadOnlyList<string> positionals = args.Positionals();
        string subcommand = positionals.Count > 0 ? positionals[0].ToLowerInvariant() : "list";

        switch (subcommand)
        {
            case "install":
                WindowsPerformanceCounterManager.Install(config.PerformanceCounters);
                await _console.Out.WriteLineAsync("Performance counters installed.");
                break;
            case "uninstall":
                WindowsPerformanceCounterManager.Uninstall(config.PerformanceCounters);
                await _console.Out.WriteLineAsync("Performance counters uninstalled.");
                break;
            case "test":
                WindowsPerformanceCounterManager.PublishTestValues(config.PerformanceCounters);
                await _console.Out.WriteLineAsync("Test values published.");
                break;
            case "reset":
                WindowsPerformanceCounterManager.Publish(config.PerformanceCounters, new CounterSnapshot());
                await _console.Out.WriteLineAsync("Counters reset.");
                break;
            case "publish":
                await PublishCountersAsync(config, database, cancellationToken);
                await _console.Out.WriteLineAsync("Current values published.");
                break;
            case "list":
                foreach (string row in WindowsPerformanceCounterManager.List(config.PerformanceCounters))
                {
                    await _console.Out.WriteLineAsync(row);
                }

                break;
            default:
                throw new ArgumentException($"Unknown counters command '{subcommand}'.");
        }

        return 0;
    }

    private async Task<int> AlertsAsync(CliArgs args, CancellationToken cancellationToken)
    {
        ConfigManager manager = CreateConfigManager(args);
        TokenTapConfig config = await manager.LoadOrDefaultAsync(cancellationToken);
        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        IReadOnlyList<string> positionals = args.Positionals();
        string subcommand = positionals.Count > 0 ? positionals[0].ToLowerInvariant() : "list";

        if (subcommand == "add")
        {
            if (positionals.Count < 2)
            {
                throw new ArgumentException("Use: token-tap alerts add daily_cost --threshold 25 --windows --email");
            }

            config.Alerts.Rules.Add(new AlertRuleConfig
            {
                Name = args.Value("--name") ?? $"{positionals[1]} threshold",
                Type = positionals[1],
                Threshold = args.DecimalValue("--threshold", 0m),
                NotifyWindows = args.Has("--windows") || !args.Has("--email"),
                NotifyEmail = args.Has("--email"),
                Severity = args.Value("--severity") ?? "warning",
                CooldownMinutes = args.IntValue("--cooldown-minutes", 60)
            });
            await manager.SaveAsync(config, cancellationToken);
            await _console.Out.WriteLineAsync("Alert rule added.");
            return 0;
        }

        if (subcommand == "test")
        {
            DateRange range = DateRange.Today();
            UsageTotals totals = await database.GetTotalsAsync(range, cancellationToken);
            IReadOnlyList<UsageEvent> events = await database.QueryEventsAsync(range, cancellationToken);
            IReadOnlyList<AlertDecision> decisions = AlertEvaluator.Evaluate(config, totals, events);
            ConsoleAlertNotifier notifier = new(_console.Out);
            foreach (AlertDecision decision in decisions)
            {
                await notifier.NotifyAsync(decision, cancellationToken);
                await database.InsertAlertHistoryAsync(ToHistory(decision, windowsSent: true, emailSent: false), cancellationToken);
            }

            await _console.Out.WriteLineAsync($"{decisions.Count} alert(s) triggered.");
            return 0;
        }

        foreach (AlertRuleConfig rule in config.Alerts.Rules)
        {
            await _console.Out.WriteLineAsync($"{(rule.Enabled ? "on " : "off")} {rule.Type,-24} {rule.Threshold,10} {rule.Severity,-8} {rule.Name}");
        }

        return 0;
    }

    private async Task<int> SmtpAsync(CliArgs args, CancellationToken cancellationToken)
    {
        TokenTapConfig config = await CreateConfigManager(args).LoadOrDefaultAsync(cancellationToken);
        IReadOnlyList<string> positionals = args.Positionals();
        string subcommand = positionals.Count > 0 ? positionals[0].ToLowerInvariant() : "show";

        if (subcommand == "test")
        {
            AlertDecision decision = new()
            {
                Rule = new AlertRuleConfig
                {
                    Name = "SMTP test",
                    Severity = "test",
                    NotifyEmail = true
                },
                Message = "This is a Token-Tap SMTP test message.",
                TriggerValue = 0
            };
            await new SmtpAlertNotifier(config.Alerts.Email).NotifyAsync(decision, cancellationToken);
            await _console.Out.WriteLineAsync("SMTP test completed.");
            return 0;
        }

        await _console.Out.WriteLineAsync($"Enabled: {config.Alerts.Email.Enabled}");
        await _console.Out.WriteLineAsync($"Host: {config.Alerts.Email.SmtpHost}:{config.Alerts.Email.SmtpPort}");
        await _console.Out.WriteLineAsync($"From: {config.Alerts.Email.From}");
        await _console.Out.WriteLineAsync($"Password env var: {config.Alerts.Email.PasswordSecretName}");
        return 0;
    }

    private async Task<int> RunWrapperAsync(CliArgs args, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> command = args.TailAfterDoubleDash();
        if (command.Count == 0)
        {
            throw new ArgumentException("Use: token-tap run --agent codex -- <command>");
        }

        TokenTapConfig config = await CreateConfigManager(args).LoadOrDefaultAsync(cancellationToken);
        TokenTapDatabase database = await OpenDatabaseAsync(config, cancellationToken);
        string executable = command[0];
        string arguments = string.Join(" ", command.Skip(1).Select(QuoteArgument));

        ProcessStartInfo startInfo = new(executable, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        DateTimeOffset started = DateTimeOffset.UtcNow;
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{executable}'.");
        string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        await _console.Out.WriteAsync(stdout);
        await _console.ErrorOut.WriteAsync(stderr);

        string transcript = string.Join(Environment.NewLine, command) + Environment.NewLine + stdout + stderr;
        UsageEvent usageEvent = new()
        {
            Timestamp = started,
            Source = "wrapper",
            AgentName = args.Value("--agent") ?? "wrapped-command",
            Model = args.Value("--model") ?? config.DefaultModel,
            InputTokens = Math.Max(1, command.Sum(part => part.Length) / Math.Max(1, config.Estimation.CharsPerToken)),
            OutputTokens = Math.Max(1, (stdout.Length + stderr.Length) / Math.Max(1, config.Estimation.CharsPerToken)),
            Confidence = ConfidenceLevel.Inferred,
            PromptHash = ContentHasher.Sha256Hex(string.Join(" ", command)),
            ResponseHash = ContentHasher.Sha256Hex(stdout + stderr),
            RawExcerptRedacted = $"exit={process.ExitCode}; command={executable}"
        };

        UsageEventFactory.FinalizeEvent(usageEvent, config, transcript);
        await database.InsertUsageEventsAsync([usageEvent], cancellationToken);
        await _console.Out.WriteLineAsync($"token-tap recorded wrapper event; exit code {process.ExitCode}.");
        return process.ExitCode;
    }

    private async Task<int> UnknownCommandAsync(string command)
    {
        await _console.ErrorOut.WriteLineAsync($"Unknown command '{command}'.");
        await PrintHelpAsync();
        return 1;
    }

    private static async Task<int> ImportLogFileAsync(string path, TokenTapConfig config, TokenTapDatabase database, string? agent, CancellationToken cancellationToken)
    {
        CompositeUsageParser parser = new();
        IReadOnlyList<UsageEvent> events = await parser.ParseFileAsync(path, config, agent, cancellationToken);
        int inserted = await database.InsertUsageEventsAsync(events, cancellationToken);
        await database.RecordWatchedSourceAsync(path, "file", "composite", cancellationToken);
        return inserted;
    }

    private static async Task<int> ImportFolderAsync(string folder, TokenTapConfig config, TokenTapDatabase database, string? agent, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException(folder);
        }

        int inserted = 0;
        foreach (string file in EnumerateCandidateFiles(folder))
        {
            try
            {
                inserted += await ImportLogFileAsync(file, config, database, agent, cancellationToken);
            }
            catch (IOException)
            {
                // Logs can rotate or be locked while VS Code writes them; the next scan will try again.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return inserted;
    }

    private static async Task<int> ImportCsvAsync(string path, TokenTapConfig config, TokenTapDatabase database, CancellationToken cancellationToken)
    {
        IReadOnlyList<UsageEvent> events = await CsvUsageImporter.ImportAsync(path, config, cancellationToken);
        int inserted = await database.InsertUsageEventsAsync(events, cancellationToken);
        await database.RecordWatchedSourceAsync(path, "csv", "csv", cancellationToken);
        return inserted;
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string folder)
    {
        string[] extensions = [".log", ".txt", ".json", ".jsonl", ".ndjson"];
        return Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<TokenTapDatabase> OpenDatabaseAsync(TokenTapConfig config, CancellationToken cancellationToken)
    {
        string path = EnvironmentPathExpander.ExpandToFullPath(config.DatabasePath);
        TokenTapDatabase database = new(path);
        await database.InitializeAsync(cancellationToken);
        return database;
    }

    private static ConfigManager CreateConfigManager(CliArgs args) =>
        new(args.Value("--config"));

    private static DateRange ResolveRange(CliArgs args)
    {
        if (args.Has("--week"))
        {
            return DateRange.ThisWeek();
        }

        if (args.Has("--month"))
        {
            return DateRange.ThisMonth();
        }

        if (args.Value("--days") is { } days && int.TryParse(days, out int parsed))
        {
            return DateRange.LastDays(parsed);
        }

        return DateRange.Today();
    }

    private static async Task PublishCountersAsync(TokenTapConfig config, TokenTapDatabase database, CancellationToken cancellationToken)
    {
        DateRange today = DateRange.Today();
        UsageTotals totals = await database.GetTotalsAsync(today, cancellationToken);
        IReadOnlyList<UsageEvent> events = await database.QueryEventsAsync(today, cancellationToken);
        Dictionary<string, UsageTotals> agentTotals = events
            .GroupBy(e => e.AgentName)
            .ToDictionary(
                group => group.Key,
                group => new UsageTotals
                {
                    InputTokens = group.Sum(e => e.InputTokens),
                    OutputTokens = group.Sum(e => e.OutputTokens),
                    CachedTokens = group.Sum(e => e.CachedTokens),
                    EstimatedCostCents = group.Sum(e => e.EstimatedCostCents),
                    EventCount = group.Count()
                },
                StringComparer.OrdinalIgnoreCase);

        long lastAge = events.Count == 0
            ? 0
            : (long)Math.Max(0, (DateTimeOffset.UtcNow - events.Max(e => e.Timestamp)).TotalSeconds);

        WindowsPerformanceCounterManager.Publish(config.PerformanceCounters, new CounterSnapshot
        {
            Totals = totals,
            LastEventAgeSeconds = lastAge,
            AgentTotals = agentTotals
        });
    }

    private static AlertHistoryEntry ToHistory(AlertDecision decision, bool windowsSent, bool emailSent) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Severity = decision.Rule.Severity,
            TriggerValue = decision.TriggerValue,
            Threshold = decision.Rule.Threshold,
            Message = decision.Message,
            WindowsSent = windowsSent,
            EmailSent = emailSent
        };

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ') ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : argument;

    private async Task PrintHelpAsync()
    {
        await _console.Out.WriteLineAsync("""
            Token-Tap - local AI coding-agent token usage and cost meter

            Usage:
              token-tap init [--database <path>]
              token-tap detect [--save]
              token-tap import log <file> [--agent codex] [--model gpt-5.4]
              token-tap import folder <folder>
              token-tap import csv <file>
              token-tap watch [--once] [--publish-counters]
              token-tap today
              token-tap report [--week|--month|--days N]
              token-tap top --by cost --today
              token-tap export [--today|--week|--month] --format csv|xlsx --out <path>
              token-tap cleanup [--dry-run] [--vacuum] [--older-than 30d]
              token-tap db size|compact
              token-tap retention show
              token-tap retention set events 14d
              token-tap counters install|uninstall|list|test|publish|reset
              token-tap alerts list|test
              token-tap alerts add daily_cost --threshold 25 --windows
              token-tap config set-model gpt-5.4 --input 2 --cached-input .5 --output 12
              token-tap run --agent codex -- codex

            Privacy default: stores summarized metrics, hashes, and short redacted excerpts only.
            """);
    }
}
