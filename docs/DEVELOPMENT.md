# Development

## Build

```powershell
dotnet restore TokenTap.sln
dotnet build TokenTap.sln
```

The solution targets `net8.0`, enables nullable reference types, uses the latest recommended analyzers, and builds cleanly with zero warnings.

## Test

```powershell
dotnet test TokenTap.sln
```

Current coverage includes:

- cost calculation
- config roundtrip
- redaction and hashing
- OpenAI JSON parser
- Anthropic JSON parser
- generic text parser
- CSV importer
- SQLite idempotent inserts
- totals queries
- cleanup aggregate rollup
- watched sources
- alert history
- CSV and XLSX export
- alert evaluation
- counter metadata
- CLI init/help

## Manual Smoke Test

```powershell
$root = Join-Path $PWD ".token-tap-smoke"
New-Item -ItemType Directory -Force $root | Out-Null
$log = Join-Path $root "codex.log"
"2026-05-01T12:00:00Z codex model=gpt-5.4 input tokens: 120 output tokens: 30 cached tokens: 5" | Set-Content $log

dotnet run --project src/TokenTap.Cli -- init --config "$root\token-tap.json" --database "$root\token-tap.db"
dotnet run --project src/TokenTap.Cli -- import log "$log" --config "$root\token-tap.json"
dotnet run --project src/TokenTap.Cli -- today --config "$root\token-tap.json"
dotnet run --project src/TokenTap.Cli -- export --today --format xlsx --out "$root\report.xlsx" --config "$root\token-tap.json"
```

## Coding Standards

- Keep privacy defaults conservative.
- Add parser support behind `IUsageLogParser`.
- Keep raw log archival out of storage unless explicitly designed and documented.
- Keep SQLite writes parameterized.
- Prefer deterministic event fingerprints for idempotent imports.
- Do not make Windows-only APIs leak into core/parser/storage projects.
- Add tests for new parser patterns and storage migrations.

## Adding A Parser

1. Add a class in `TokenTap.Parsers` implementing `IUsageLogParser`.
2. Return normalized `UsageEvent` rows.
3. Set `Confidence` accurately.
4. Avoid storing full content in `RawExcerptRedacted`.
5. Add the parser to `CompositeUsageParser.CreateDefaultParsers`.
6. Add tests with representative log lines.

## Adding A Command

1. Add the command case in `TokenTapCli.RunAsync`.
2. Keep argument handling in the command method.
3. Use `ConfigManager` and `OpenDatabaseAsync`.
4. Print concise results suitable for PowerShell.
5. Add tests if the command changes behavior or touches storage.
