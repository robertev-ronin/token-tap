# Token-Tap 1.000

Token-Tap 1.000 is the first public standalone release of the local AI coding-agent token usage and cost meter.

## Highlights

- Local CLI for estimating and tracking AI coding-agent token usage and cost.
- Privacy-first SQLite history: metrics, hashes, and redacted excerpts by default.
- Parsers for OpenAI JSON, Anthropic JSON, generic text, Codex/Copilot-flavored logs, and CSV usage exports.
- Reports for today, week, month, and rolling day ranges.
- CSV and XLSX report export.
- Retention cleanup that rolls detailed events into daily/hourly aggregates before deletion.
- Windows Performance Counter support for live PerfMon and `Get-Counter` monitoring.
- Built-in alert rule evaluation and alert history.
- Optional SMTP notifier using an environment variable for the password.
- Wrapper mode for recording inferred usage around arbitrary commands.
- GitHub Actions CI on Windows and Ubuntu.

## Download

Use `token-tap-1.000-win-x64.zip` for Windows x64.

When the NuGet package is published, the preferred install path is:

```powershell
dotnet tool install --global token-tap
token-tap --help
```

```powershell
Expand-Archive .\token-tap-1.000-win-x64.zip -DestinationPath .\token-tap-1.000
.\token-tap-1.000\token-tap.exe --help
```

This package is framework-dependent and expects the .NET 8 runtime to be installed.

## Verify

```powershell
Get-FileHash .\token-tap-1.000-win-x64.zip -Algorithm SHA256
Get-Content .\token-tap-1.000-win-x64.zip.sha256
```

## First Run

```powershell
.\token-tap.exe init
.\token-tap.exe detect
.\token-tap.exe watch --once
.\token-tap.exe today
```

Performance counters require an elevated Windows shell:

```powershell
.\token-tap.exe counters install
.\token-tap.exe counters test
Get-Counter '\TokenTap\Estimated Daily Cost Cents'
```
