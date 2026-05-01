# Architecture

Token-Tap is split into small projects so parser, storage, reporting, alerting, and Windows integration can evolve independently.

## Projects

`TokenTap.Core`

Shared models and policy:

- config model
- date ranges
- token pricing
- privacy redaction
- hashing
- retention parsing
- usage event finalization

`TokenTap.Parsers`

Parser plugins normalize logs into `UsageEvent` rows:

- OpenAI JSON usage
- Anthropic JSON usage
- Codex/generic text
- Copilot/generic text
- CSV import

`TokenTap.Storage`

SQLite schema and persistence:

- idempotent event insert by fingerprint
- reporting totals
- daily aggregate queries
- cleanup rollups
- watched sources
- alert history

`TokenTap.Export`

Report exporters:

- CSV event export
- XLSX workbook export with summary, daily usage, events, alerts, models, and watched sources

`TokenTap.Alerts`

Alert rule evaluation and notifiers:

- daily spend
- input token threshold
- session cost threshold
- repeated prompt count
- console notifier
- SMTP notifier

`TokenTap.Counters`

Windows Performance Counter adapter:

- global `TokenTap` category
- per-agent `TokenTap Agent` category
- install/uninstall/list/test/publish

`TokenTap.Cli`

Command router and workflows.

## Data Flow

```text
VS Code or imported logs
        |
        v
CompositeUsageParser
        |
        v
UsageEventFactory: pricing, hashes, excerpt, fingerprint
        |
        v
TokenTapDatabase SQLite
        |
        +--> reports and exports
        +--> cleanup rollups
        +--> alert evaluation
        +--> Windows Performance Counters
```

## Storage Model

Detailed events are short-lived. Cleanup rolls old `usage_events` rows into hourly and daily aggregate tables before deletion. Daily aggregates are the long-term reporting source.

The schema includes the full roadmap tables from the plan so future phases do not require a disruptive migration:

- `sessions`
- `usage_events`
- `hourly_usage_aggregates`
- `daily_usage_aggregates`
- `models`
- `anomalies`
- `alert_rules`
- `alert_history`
- `watched_sources`
- `cleanup_history`

## Parser Strategy

Parsers are intentionally permissive:

- exact counts from JSON usage blocks when available
- exact counts from text when token fields are present
- inferred counts for request/response-style lines that mention relevant agent activity

Every parsed event carries a confidence level.

## Windows Counter Boundary

Performance counters are isolated in `TokenTap.Counters`. This keeps the rest of the app portable and testable. Counter installation is Windows-only and generally requires elevation.
