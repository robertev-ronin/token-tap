using System.Globalization;
using Microsoft.Data.Sqlite;
using TokenTap.Core.Models;

namespace TokenTap.Storage;

public sealed class TokenTapDatabase
{
    private readonly string _databasePath;

    public TokenTapDatabase(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath => _databasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await ExecuteNonQueryAsync(connection, Schema, cancellationToken);
    }

    public async Task UpsertModelsAsync(IReadOnlyDictionary<string, ModelPricing> models, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        foreach ((string name, ModelPricing pricing) in models)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO models (name, provider, input_per_million, cached_input_per_million, output_per_million, effective_date, created_at)
                VALUES ($name, $provider, $input, $cached, $output, $effective, $created)
                ON CONFLICT(name, effective_date) DO UPDATE SET
                  provider = excluded.provider,
                  input_per_million = excluded.input_per_million,
                  cached_input_per_million = excluded.cached_input_per_million,
                  output_per_million = excluded.output_per_million;
                """;
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$provider", pricing.Provider);
            command.Parameters.AddWithValue("$input", pricing.InputPerMillion);
            command.Parameters.AddWithValue("$cached", pricing.CachedInputPerMillion);
            command.Parameters.AddWithValue("$output", pricing.OutputPerMillion);
            command.Parameters.AddWithValue("$effective", ToDb(pricing.EffectiveDate));
            command.Parameters.AddWithValue("$created", ToDb(DateTimeOffset.UtcNow));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<int> InsertUsageEventsAsync(IEnumerable<UsageEvent> events, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        int inserted = 0;
        foreach (UsageEvent usageEvent in events)
        {
            inserted += await InsertUsageEventAsync(connection, transaction, usageEvent, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    public async Task<IReadOnlyList<UsageEvent>> QueryEventsAsync(DateRange range, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, session_id, timestamp, event_type, source, agent_name, model,
                   input_tokens, output_tokens, cached_tokens, estimated_cost_cents,
                   confidence, prompt_hash, response_hash, source_file_hash,
                   source_file, raw_excerpt_redacted, event_fingerprint, created_at
            FROM usage_events
            WHERE timestamp >= $start AND timestamp < $end
            ORDER BY timestamp ASC, id ASC;
            """;
        command.Parameters.AddWithValue("$start", ToDb(range.StartInclusive));
        command.Parameters.AddWithValue("$end", ToDb(range.EndExclusive));

        List<UsageEvent> events = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(ReadUsageEvent(reader));
        }

        return events;
    }

    public async Task<UsageTotals> GetTotalsAsync(DateRange range, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              COALESCE(SUM(input_tokens), 0),
              COALESCE(SUM(output_tokens), 0),
              COALESCE(SUM(cached_tokens), 0),
              COALESCE(SUM(estimated_cost_cents), 0),
              COALESCE(SUM(event_count), 0),
              COALESCE(SUM(session_count), 0),
              COALESCE(SUM(large_prompt_count), 0),
              COALESCE(SUM(repeated_prompt_count), 0),
              COALESCE(SUM(runaway_warning_count), 0),
              COALESCE(SUM(unknown_model_count), 0),
              COALESCE(SUM(parser_error_count), 0)
            FROM (
              SELECT input_tokens, output_tokens, cached_tokens, estimated_cost_cents,
                     1 AS event_count, 0 AS session_count,
                     CASE WHEN input_tokens >= 100000 THEN 1 ELSE 0 END AS large_prompt_count,
                     0 AS repeated_prompt_count, 0 AS runaway_warning_count,
                     CASE WHEN model = 'unknown' THEN 1 ELSE 0 END AS unknown_model_count,
                     0 AS parser_error_count
              FROM usage_events
              WHERE timestamp >= $start AND timestamp < $end
              UNION ALL
              SELECT input_tokens, output_tokens, cached_tokens, estimated_cost_cents,
                     event_count, session_count, large_prompt_count, repeated_prompt_count,
                     runaway_warning_count, unknown_model_count, parser_error_count
              FROM daily_usage_aggregates
              WHERE date >= $startDate AND date < $endDate
            );
            """;
        command.Parameters.AddWithValue("$start", ToDb(range.StartInclusive));
        command.Parameters.AddWithValue("$end", ToDb(range.EndExclusive));
        command.Parameters.AddWithValue("$startDate", range.StartInclusive.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$endDate", range.EndExclusive.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new UsageTotals();
        }

        return new UsageTotals
        {
            InputTokens = reader.GetInt64(0),
            OutputTokens = reader.GetInt64(1),
            CachedTokens = reader.GetInt64(2),
            EstimatedCostCents = reader.GetDecimal(3),
            EventCount = reader.GetInt64(4),
            SessionCount = reader.GetInt64(5),
            LargePromptCount = reader.GetInt64(6),
            RepeatedPromptCount = reader.GetInt64(7),
            RunawayWarningCount = reader.GetInt64(8),
            UnknownModelCount = reader.GetInt64(9),
            ParserErrorCount = reader.GetInt64(10)
        };
    }

    public async Task<IReadOnlyList<DailyUsageAggregate>> GetDailyUsageAsync(DateRange range, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, agent_name, source, repo_path_hash, model,
                   SUM(input_tokens), SUM(output_tokens), SUM(cached_tokens),
                   SUM(estimated_cost_cents), SUM(event_count), SUM(session_count),
                   SUM(large_prompt_count), SUM(repeated_prompt_count),
                   SUM(runaway_warning_count), SUM(unknown_model_count),
                   SUM(parser_error_count),
                   MIN(confidence_min), MAX(confidence_max)
            FROM (
              SELECT substr(timestamp, 1, 10) AS date, agent_name, source, '' AS repo_path_hash, model,
                     input_tokens, output_tokens, cached_tokens, estimated_cost_cents,
                     1 AS event_count, 0 AS session_count,
                     CASE WHEN input_tokens >= 100000 THEN 1 ELSE 0 END AS large_prompt_count,
                     0 AS repeated_prompt_count, 0 AS runaway_warning_count,
                     CASE WHEN model = 'unknown' THEN 1 ELSE 0 END AS unknown_model_count,
                     0 AS parser_error_count,
                     confidence AS confidence_min, confidence AS confidence_max
              FROM usage_events
              WHERE timestamp >= $start AND timestamp < $end
              UNION ALL
              SELECT date, agent_name, source, repo_path_hash, model,
                     input_tokens, output_tokens, cached_tokens, estimated_cost_cents,
                     event_count, session_count, large_prompt_count, repeated_prompt_count,
                     runaway_warning_count, unknown_model_count, parser_error_count,
                     confidence_min, confidence_max
              FROM daily_usage_aggregates
              WHERE date >= $startDate AND date < $endDate
            )
            GROUP BY date, agent_name, source, repo_path_hash, model
            ORDER BY date, agent_name, source, model;
            """;
        command.Parameters.AddWithValue("$start", ToDb(range.StartInclusive));
        command.Parameters.AddWithValue("$end", ToDb(range.EndExclusive));
        command.Parameters.AddWithValue("$startDate", range.StartInclusive.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$endDate", range.EndExclusive.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        List<DailyUsageAggregate> rows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DailyUsageAggregate
            {
                Date = DateOnly.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                AgentName = reader.GetString(1),
                Source = reader.GetString(2),
                RepoPathHash = reader.GetString(3),
                Model = reader.GetString(4),
                InputTokens = reader.GetInt64(5),
                OutputTokens = reader.GetInt64(6),
                CachedTokens = reader.GetInt64(7),
                EstimatedCostCents = reader.GetDecimal(8),
                EventCount = reader.GetInt64(9),
                SessionCount = reader.GetInt64(10),
                LargePromptCount = reader.GetInt64(11),
                RepeatedPromptCount = reader.GetInt64(12),
                RunawayWarningCount = reader.GetInt64(13),
                UnknownModelCount = reader.GetInt64(14),
                ParserErrorCount = reader.GetInt64(15),
                ConfidenceMin = ConfidenceLevelExtensions.Parse(reader.GetString(16)),
                ConfidenceMax = ConfidenceLevelExtensions.Parse(reader.GetString(17))
            });
        }

        return rows;
    }

    public async Task RecordWatchedSourceAsync(string path, string sourceType, string parserName, CancellationToken cancellationToken = default)
    {
        string hash = TokenTap.Core.Privacy.ContentHasher.Sha256FilePath(path);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO watched_sources (path_hash, display_path, source_type, enabled, last_seen_at, last_position, parser_name, parser_error_count)
            VALUES ($hash, $path, $sourceType, 1, $lastSeen, 0, $parserName, 0)
            ON CONFLICT(path_hash) DO UPDATE SET
              display_path = excluded.display_path,
              source_type = excluded.source_type,
              last_seen_at = excluded.last_seen_at,
              parser_name = excluded.parser_name;
            """;
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$sourceType", sourceType);
        command.Parameters.AddWithValue("$lastSeen", ToDb(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$parserName", parserName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(string Path, string SourceType, string ParserName, DateTimeOffset? LastSeen)>> GetWatchedSourcesAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT display_path, source_type, parser_name, last_seen_at
            FROM watched_sources
            ORDER BY display_path;
            """;

        List<(string, string, string, DateTimeOffset?)> rows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    public async Task InsertAlertHistoryAsync(AlertHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO alert_history (rule_id, timestamp, severity, trigger_value, threshold, message, windows_sent, email_sent, suppressed, suppression_reason)
            VALUES ($ruleId, $timestamp, $severity, $triggerValue, $threshold, $message, $windowsSent, $emailSent, $suppressed, $reason);
            """;
        command.Parameters.AddWithValue("$ruleId", (object?)entry.RuleId ?? DBNull.Value);
        command.Parameters.AddWithValue("$timestamp", ToDb(entry.Timestamp));
        command.Parameters.AddWithValue("$severity", entry.Severity);
        command.Parameters.AddWithValue("$triggerValue", entry.TriggerValue);
        command.Parameters.AddWithValue("$threshold", entry.Threshold);
        command.Parameters.AddWithValue("$message", entry.Message);
        command.Parameters.AddWithValue("$windowsSent", entry.WindowsSent ? 1 : 0);
        command.Parameters.AddWithValue("$emailSent", entry.EmailSent ? 1 : 0);
        command.Parameters.AddWithValue("$suppressed", entry.Suppressed ? 1 : 0);
        command.Parameters.AddWithValue("$reason", (object?)entry.SuppressionReason ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertHistoryEntry>> GetAlertHistoryAsync(DateRange range, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT rule_id, timestamp, severity, trigger_value, threshold, message, windows_sent, email_sent, suppressed, suppression_reason
            FROM alert_history
            WHERE timestamp >= $start AND timestamp < $end
            ORDER BY timestamp DESC;
            """;
        command.Parameters.AddWithValue("$start", ToDb(range.StartInclusive));
        command.Parameters.AddWithValue("$end", ToDb(range.EndExclusive));

        List<AlertHistoryEntry> entries = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new AlertHistoryEntry
            {
                RuleId = reader.IsDBNull(0) ? null : reader.GetInt64(0),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                Severity = reader.GetString(2),
                TriggerValue = reader.GetDecimal(3),
                Threshold = reader.GetDecimal(4),
                Message = reader.GetString(5),
                WindowsSent = reader.GetInt32(6) == 1,
                EmailSent = reader.GetInt32(7) == 1,
                Suppressed = reader.GetInt32(8) == 1,
                SuppressionReason = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }

        return entries;
    }

    public async Task<CleanupResult> CleanupAsync(
        HistoryOptions history,
        bool dryRun,
        bool vacuum,
        TimeSpan? olderThan = null,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset eventCutoff = now - (olderThan ?? TimeSpan.FromDays(history.EventRetentionDays));
        DateTimeOffset anomalyCutoff = now.AddDays(-history.AnomalyRetentionDays);
        DateTimeOffset alertCutoff = now.AddDays(-history.AlertRetentionDays);
        DateTimeOffset sessionCutoff = now.AddDays(-history.RetentionDays);

        DatabaseSize before = GetDatabaseSize();
        CleanupResult result = new()
        {
            DryRun = dryRun,
            DatabaseSizeBeforeMb = before.Megabytes
        };

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        result.EventsDeleted = await CountAsync(connection, transaction, "usage_events", "timestamp", eventCutoff, cancellationToken);
        result.SessionsDeleted = await CountAsync(connection, transaction, "sessions", "ended_at", sessionCutoff, cancellationToken);
        result.AnomaliesDeleted = await CountAsync(connection, transaction, "anomalies", "timestamp", anomalyCutoff, cancellationToken);
        result.AlertsDeleted = await CountAsync(connection, transaction, "alert_history", "timestamp", alertCutoff, cancellationToken);

        if (!dryRun)
        {
            await RollupEventsBeforeAsync(connection, transaction, eventCutoff, cancellationToken);
            await DeleteBeforeAsync(connection, transaction, "usage_events", "timestamp", eventCutoff, cancellationToken);
            await DeleteBeforeAsync(connection, transaction, "sessions", "ended_at", sessionCutoff, cancellationToken);
            await DeleteBeforeAsync(connection, transaction, "anomalies", "timestamp", anomalyCutoff, cancellationToken);
            await DeleteBeforeAsync(connection, transaction, "alert_history", "timestamp", alertCutoff, cancellationToken);
            await InsertCleanupHistoryAsync(connection, transaction, result, now, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        if (!dryRun && vacuum)
        {
            await CompactAsync(cancellationToken);
            result.VacuumPerformed = true;
        }

        result.DatabaseSizeAfterMb = GetDatabaseSize().Megabytes;
        result.Message = dryRun ? "Dry run complete. No rows were deleted." : "Cleanup complete.";
        return result;
    }

    public DatabaseSize GetDatabaseSize()
    {
        long bytes = File.Exists(_databasePath) ? new FileInfo(_databasePath).Length : 0;
        return new DatabaseSize(_databasePath, bytes);
    }

    public async Task CompactAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await ExecuteNonQueryAsync(connection, "VACUUM;", cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;", cancellationToken);
        return connection;
    }

    private static async Task<int> InsertUsageEventAsync(SqliteConnection connection, SqliteTransaction transaction, UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO usage_events (
              session_id, timestamp, event_type, source, agent_name, model,
              input_tokens, output_tokens, cached_tokens, estimated_cost_cents,
              confidence, prompt_hash, response_hash, source_file_hash, source_file,
              raw_excerpt_redacted, event_fingerprint, created_at)
            VALUES (
              $sessionId, $timestamp, $eventType, $source, $agentName, $model,
              $inputTokens, $outputTokens, $cachedTokens, $costCents,
              $confidence, $promptHash, $responseHash, $sourceFileHash, $sourceFile,
              $excerpt, $fingerprint, $createdAt);
            """;
        command.Parameters.AddWithValue("$sessionId", (object?)usageEvent.SessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$timestamp", ToDb(usageEvent.Timestamp));
        command.Parameters.AddWithValue("$eventType", usageEvent.EventType);
        command.Parameters.AddWithValue("$source", usageEvent.Source);
        command.Parameters.AddWithValue("$agentName", usageEvent.AgentName);
        command.Parameters.AddWithValue("$model", usageEvent.Model);
        command.Parameters.AddWithValue("$inputTokens", usageEvent.InputTokens);
        command.Parameters.AddWithValue("$outputTokens", usageEvent.OutputTokens);
        command.Parameters.AddWithValue("$cachedTokens", usageEvent.CachedTokens);
        command.Parameters.AddWithValue("$costCents", usageEvent.EstimatedCostCents);
        command.Parameters.AddWithValue("$confidence", usageEvent.Confidence.ToStorageValue());
        command.Parameters.AddWithValue("$promptHash", (object?)usageEvent.PromptHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$responseHash", (object?)usageEvent.ResponseHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceFileHash", (object?)usageEvent.SourceFileHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceFile", (object?)usageEvent.SourceFile ?? DBNull.Value);
        command.Parameters.AddWithValue("$excerpt", (object?)usageEvent.RawExcerptRedacted ?? DBNull.Value);
        command.Parameters.AddWithValue("$fingerprint", usageEvent.EventFingerprint);
        command.Parameters.AddWithValue("$createdAt", ToDb(usageEvent.CreatedAt));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static UsageEvent ReadUsageEvent(SqliteDataReader reader) =>
        new()
        {
            Id = reader.GetInt64(0),
            SessionId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
            Timestamp = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
            EventType = reader.GetString(3),
            Source = reader.GetString(4),
            AgentName = reader.GetString(5),
            Model = reader.GetString(6),
            InputTokens = reader.GetInt64(7),
            OutputTokens = reader.GetInt64(8),
            CachedTokens = reader.GetInt64(9),
            EstimatedCostCents = reader.GetDecimal(10),
            Confidence = ConfidenceLevelExtensions.Parse(reader.GetString(11)),
            PromptHash = reader.IsDBNull(12) ? null : reader.GetString(12),
            ResponseHash = reader.IsDBNull(13) ? null : reader.GetString(13),
            SourceFileHash = reader.IsDBNull(14) ? null : reader.GetString(14),
            SourceFile = reader.IsDBNull(15) ? null : reader.GetString(15),
            RawExcerptRedacted = reader.IsDBNull(16) ? null : reader.GetString(16),
            EventFingerprint = reader.GetString(17),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(18), CultureInfo.InvariantCulture)
        };

    private static async Task RollupEventsBeforeAsync(SqliteConnection connection, SqliteTransaction transaction, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using SqliteCommand hourly = connection.CreateCommand();
        hourly.Transaction = transaction;
        hourly.CommandText = """
            INSERT INTO hourly_usage_aggregates (
              hour_start, agent_name, source, repo_path_hash, model,
              input_tokens, output_tokens, cached_tokens, estimated_cost_cents,
              event_count, session_count, large_prompt_count, repeated_prompt_count,
              runaway_warning_count, unknown_model_count, parser_error_count)
            SELECT substr(timestamp, 1, 13) || ':00:00Z', agent_name, source, '', model,
                   SUM(input_tokens), SUM(output_tokens), SUM(cached_tokens), SUM(estimated_cost_cents),
                   COUNT(*), 0,
                   SUM(CASE WHEN input_tokens >= 100000 THEN 1 ELSE 0 END), 0, 0,
                   SUM(CASE WHEN model = 'unknown' THEN 1 ELSE 0 END), 0
            FROM usage_events
            WHERE timestamp < $cutoff
            GROUP BY substr(timestamp, 1, 13), agent_name, source, model
            ON CONFLICT(hour_start, agent_name, source, repo_path_hash, model) DO UPDATE SET
              input_tokens = excluded.input_tokens,
              output_tokens = excluded.output_tokens,
              cached_tokens = excluded.cached_tokens,
              estimated_cost_cents = excluded.estimated_cost_cents,
              event_count = excluded.event_count,
              large_prompt_count = excluded.large_prompt_count,
              unknown_model_count = excluded.unknown_model_count;
            """;
        hourly.Parameters.AddWithValue("$cutoff", ToDb(cutoff));
        await hourly.ExecuteNonQueryAsync(cancellationToken);

        await using SqliteCommand daily = connection.CreateCommand();
        daily.Transaction = transaction;
        daily.CommandText = """
            INSERT INTO daily_usage_aggregates (
              date, agent_name, source, repo_path_hash, model,
              input_tokens, output_tokens, cached_tokens, estimated_cost_cents,
              event_count, session_count, large_prompt_count, repeated_prompt_count,
              runaway_warning_count, unknown_model_count, parser_error_count,
              confidence_min, confidence_max)
            SELECT substr(timestamp, 1, 10), agent_name, source, '', model,
                   SUM(input_tokens), SUM(output_tokens), SUM(cached_tokens), SUM(estimated_cost_cents),
                   COUNT(*), 0,
                   SUM(CASE WHEN input_tokens >= 100000 THEN 1 ELSE 0 END), 0, 0,
                   SUM(CASE WHEN model = 'unknown' THEN 1 ELSE 0 END), 0,
                   MIN(confidence), MAX(confidence)
            FROM usage_events
            WHERE timestamp < $cutoff
            GROUP BY substr(timestamp, 1, 10), agent_name, source, model
            ON CONFLICT(date, agent_name, source, repo_path_hash, model) DO UPDATE SET
              input_tokens = excluded.input_tokens,
              output_tokens = excluded.output_tokens,
              cached_tokens = excluded.cached_tokens,
              estimated_cost_cents = excluded.estimated_cost_cents,
              event_count = excluded.event_count,
              large_prompt_count = excluded.large_prompt_count,
              unknown_model_count = excluded.unknown_model_count,
              confidence_min = excluded.confidence_min,
              confidence_max = excluded.confidence_max;
            """;
        daily.Parameters.AddWithValue("$cutoff", ToDb(cutoff));
        await daily.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> CountAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string column, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {column} < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", ToDb(cutoff));
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task DeleteBeforeAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string column, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {table} WHERE {column} < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", ToDb(cutoff));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCleanupHistoryAsync(SqliteConnection connection, SqliteTransaction transaction, CleanupResult result, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO cleanup_history (
              started_at, ended_at, events_deleted, sessions_deleted, anomalies_deleted, alerts_deleted,
              database_size_before_mb, database_size_after_mb, vacuum_performed, success, message)
            VALUES ($started, $ended, $events, $sessions, $anomalies, $alerts, $before, $after, $vacuum, $success, $message);
            """;
        command.Parameters.AddWithValue("$started", ToDb(startedAt));
        command.Parameters.AddWithValue("$ended", ToDb(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$events", result.EventsDeleted);
        command.Parameters.AddWithValue("$sessions", result.SessionsDeleted);
        command.Parameters.AddWithValue("$anomalies", result.AnomaliesDeleted);
        command.Parameters.AddWithValue("$alerts", result.AlertsDeleted);
        command.Parameters.AddWithValue("$before", result.DatabaseSizeBeforeMb);
        command.Parameters.AddWithValue("$after", result.DatabaseSizeAfterMb);
        command.Parameters.AddWithValue("$vacuum", result.VacuumPerformed ? 1 : 0);
        command.Parameters.AddWithValue("$success", result.Success ? 1 : 0);
        command.Parameters.AddWithValue("$message", result.Message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ToDb(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS sessions (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          started_at TEXT NOT NULL,
          ended_at TEXT,
          agent_name TEXT NOT NULL,
          source TEXT NOT NULL,
          repo_path TEXT,
          branch_name TEXT,
          command_hash TEXT,
          model TEXT NOT NULL,
          total_input_tokens INTEGER NOT NULL DEFAULT 0,
          total_output_tokens INTEGER NOT NULL DEFAULT 0,
          total_cached_tokens INTEGER NOT NULL DEFAULT 0,
          estimated_cost_cents REAL NOT NULL DEFAULT 0,
          confidence TEXT NOT NULL DEFAULT 'estimated',
          event_count INTEGER NOT NULL DEFAULT 0,
          large_prompt_count INTEGER NOT NULL DEFAULT 0,
          repeated_prompt_count INTEGER NOT NULL DEFAULT 0,
          parser_error_count INTEGER NOT NULL DEFAULT 0,
          notes TEXT
        );

        CREATE TABLE IF NOT EXISTS usage_events (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          session_id INTEGER NULL,
          timestamp TEXT NOT NULL,
          event_type TEXT NOT NULL,
          source TEXT NOT NULL,
          agent_name TEXT NOT NULL,
          model TEXT NOT NULL,
          input_tokens INTEGER NOT NULL DEFAULT 0,
          output_tokens INTEGER NOT NULL DEFAULT 0,
          cached_tokens INTEGER NOT NULL DEFAULT 0,
          estimated_cost_cents REAL NOT NULL DEFAULT 0,
          confidence TEXT NOT NULL,
          prompt_hash TEXT,
          response_hash TEXT,
          source_file_hash TEXT,
          source_file TEXT,
          raw_excerpt_redacted TEXT,
          event_fingerprint TEXT NOT NULL UNIQUE,
          created_at TEXT NOT NULL,
          FOREIGN KEY(session_id) REFERENCES sessions(id) ON DELETE SET NULL
        );

        CREATE INDEX IF NOT EXISTS ix_usage_events_timestamp ON usage_events(timestamp);
        CREATE INDEX IF NOT EXISTS ix_usage_events_agent_model ON usage_events(agent_name, model);

        CREATE TABLE IF NOT EXISTS hourly_usage_aggregates (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          hour_start TEXT NOT NULL,
          agent_name TEXT NOT NULL,
          source TEXT NOT NULL,
          repo_path_hash TEXT NOT NULL DEFAULT '',
          model TEXT NOT NULL,
          input_tokens INTEGER NOT NULL DEFAULT 0,
          output_tokens INTEGER NOT NULL DEFAULT 0,
          cached_tokens INTEGER NOT NULL DEFAULT 0,
          estimated_cost_cents REAL NOT NULL DEFAULT 0,
          event_count INTEGER NOT NULL DEFAULT 0,
          session_count INTEGER NOT NULL DEFAULT 0,
          large_prompt_count INTEGER NOT NULL DEFAULT 0,
          repeated_prompt_count INTEGER NOT NULL DEFAULT 0,
          runaway_warning_count INTEGER NOT NULL DEFAULT 0,
          unknown_model_count INTEGER NOT NULL DEFAULT 0,
          parser_error_count INTEGER NOT NULL DEFAULT 0,
          UNIQUE(hour_start, agent_name, source, repo_path_hash, model)
        );

        CREATE TABLE IF NOT EXISTS daily_usage_aggregates (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          date TEXT NOT NULL,
          agent_name TEXT NOT NULL,
          source TEXT NOT NULL,
          repo_path_hash TEXT NOT NULL DEFAULT '',
          model TEXT NOT NULL,
          input_tokens INTEGER NOT NULL DEFAULT 0,
          output_tokens INTEGER NOT NULL DEFAULT 0,
          cached_tokens INTEGER NOT NULL DEFAULT 0,
          estimated_cost_cents REAL NOT NULL DEFAULT 0,
          event_count INTEGER NOT NULL DEFAULT 0,
          session_count INTEGER NOT NULL DEFAULT 0,
          large_prompt_count INTEGER NOT NULL DEFAULT 0,
          repeated_prompt_count INTEGER NOT NULL DEFAULT 0,
          runaway_warning_count INTEGER NOT NULL DEFAULT 0,
          unknown_model_count INTEGER NOT NULL DEFAULT 0,
          parser_error_count INTEGER NOT NULL DEFAULT 0,
          confidence_min TEXT NOT NULL DEFAULT 'estimated',
          confidence_max TEXT NOT NULL DEFAULT 'estimated',
          UNIQUE(date, agent_name, source, repo_path_hash, model)
        );

        CREATE TABLE IF NOT EXISTS models (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          name TEXT NOT NULL,
          provider TEXT NOT NULL,
          input_per_million REAL NOT NULL,
          cached_input_per_million REAL NOT NULL,
          output_per_million REAL NOT NULL,
          effective_date TEXT NOT NULL,
          created_at TEXT NOT NULL,
          UNIQUE(name, effective_date)
        );

        CREATE TABLE IF NOT EXISTS anomalies (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          session_id INTEGER NULL,
          timestamp TEXT NOT NULL,
          anomaly_type TEXT NOT NULL,
          severity TEXT NOT NULL,
          message TEXT NOT NULL,
          agent_name TEXT NOT NULL,
          estimated_cost_cents REAL NOT NULL DEFAULT 0,
          related_prompt_hash TEXT,
          resolved INTEGER NOT NULL DEFAULT 0,
          created_at TEXT NOT NULL,
          FOREIGN KEY(session_id) REFERENCES sessions(id) ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS alert_rules (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          name TEXT NOT NULL,
          enabled INTEGER NOT NULL DEFAULT 1,
          type TEXT NOT NULL,
          threshold REAL NOT NULL,
          window_minutes INTEGER NOT NULL DEFAULT 60,
          severity TEXT NOT NULL,
          notify_windows INTEGER NOT NULL DEFAULT 1,
          notify_email INTEGER NOT NULL DEFAULT 0,
          cooldown_minutes INTEGER NOT NULL DEFAULT 60
        );

        CREATE TABLE IF NOT EXISTS alert_history (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          rule_id INTEGER NULL,
          timestamp TEXT NOT NULL,
          severity TEXT NOT NULL,
          trigger_value REAL NOT NULL,
          threshold REAL NOT NULL,
          message TEXT NOT NULL,
          windows_sent INTEGER NOT NULL DEFAULT 0,
          email_sent INTEGER NOT NULL DEFAULT 0,
          suppressed INTEGER NOT NULL DEFAULT 0,
          suppression_reason TEXT,
          FOREIGN KEY(rule_id) REFERENCES alert_rules(id) ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS watched_sources (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          path_hash TEXT NOT NULL UNIQUE,
          display_path TEXT NOT NULL,
          source_type TEXT NOT NULL,
          enabled INTEGER NOT NULL DEFAULT 1,
          last_seen_at TEXT,
          last_position INTEGER NOT NULL DEFAULT 0,
          parser_name TEXT NOT NULL,
          parser_error_count INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS cleanup_history (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          started_at TEXT NOT NULL,
          ended_at TEXT,
          events_deleted INTEGER NOT NULL DEFAULT 0,
          sessions_deleted INTEGER NOT NULL DEFAULT 0,
          anomalies_deleted INTEGER NOT NULL DEFAULT 0,
          alerts_deleted INTEGER NOT NULL DEFAULT 0,
          database_size_before_mb REAL NOT NULL DEFAULT 0,
          database_size_after_mb REAL NOT NULL DEFAULT 0,
          vacuum_performed INTEGER NOT NULL DEFAULT 0,
          success INTEGER NOT NULL DEFAULT 1,
          message TEXT
        );
        """;
}
