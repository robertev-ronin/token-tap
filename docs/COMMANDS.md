# Commands

The development form is:

```powershell
dotnet run --project src/TokenTap.Cli -- <command>
```

The installed form is:

```powershell
token-tap <command>
```

## Initialize

```powershell
token-tap init
token-tap init --config .\.token-tap\token-tap.json --database .\.token-tap\token-tap.db
```

Creates a config file and initializes the SQLite schema.

## Detect

```powershell
token-tap detect
token-tap detect --save
```

Checks configured VS Code and VS Code Insiders log/global storage paths and prints the folders that exist.

## Watch Sources

```powershell
token-tap watch-add "%APPDATA%\Code - Insiders\logs"
token-tap watch --once
token-tap watch --publish-counters
token-tap watch --interval 30
```

`watch` scans configured folders for `.log`, `.txt`, `.json`, `.jsonl`, and `.ndjson` files. Inserts are idempotent by event fingerprint, so repeated scans do not duplicate rows.

## Import

```powershell
token-tap import log .\codex-session.log --agent codex --model gpt-5.4
token-tap import folder .\logs
token-tap import csv .\usage.csv
```

CSV import recognizes common columns:

- `timestamp`
- `agent`
- `source`
- `model`
- `input_tokens`
- `output_tokens`
- `cached_tokens`
- `confidence`

## Reports

```powershell
token-tap today
token-tap report --week
token-tap report --month
token-tap report --days 30
token-tap top --by cost --today
token-tap top --by tokens --week
```

Cost is calculated from the model pricing table in config and displayed in the configured currency.

## Export

```powershell
token-tap export --today --format csv --out token-spend.csv
token-tap export --week --format xlsx --out token-spend.xlsx
token-tap export --month --format xlsx
```

XLSX output includes these sheets:

- Summary
- Daily Usage
- Events
- Alerts
- Models
- Watch Sources
- Sessions placeholder
- Anomalies placeholder

## Cleanup

```powershell
token-tap cleanup --dry-run
token-tap cleanup
token-tap cleanup --vacuum
token-tap cleanup --older-than 30d
token-tap db size
token-tap db compact
```

Cleanup rolls detailed events into hourly and daily aggregates before deleting old detail rows.

## Retention

```powershell
token-tap retention show
token-tap retention set events 14d
token-tap retention set sessions 90d
token-tap retention set aggregates 730d
token-tap retention set alerts 90d
token-tap retention set anomalies 90d
```

## Counters

```powershell
token-tap counters list
token-tap counters install
token-tap counters test
token-tap counters publish
token-tap counters reset
token-tap counters uninstall
```

`install` and `uninstall` require an elevated Windows shell.

Example PowerShell counter reads:

```powershell
Get-Counter '\TokenTap\Estimated Daily Cost Cents'
Get-Counter '\TokenTap Agent(codex)\Estimated Daily Cost Cents'
```

## Alerts

```powershell
token-tap alerts list
token-tap alerts test
token-tap alerts add daily_cost --threshold 25 --windows
token-tap alerts add daily_cost --threshold 50 --windows --email --severity critical
```

The current implementation evaluates configured rules and records alert history. Windows toast integration can be added behind the notifier abstraction without changing the rule model.

## SMTP

```powershell
token-tap smtp show
token-tap smtp test
```

SMTP is disabled by default. The sender reads the password from the environment variable named by `alerts.email.passwordSecretName`.

## Wrapper Mode

```powershell
token-tap run --agent codex -- codex
token-tap run --agent custom -- npm test
```

Wrapper mode runs the command, streams stdout/stderr, records an inferred usage event, and returns the wrapped command exit code.
