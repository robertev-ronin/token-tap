# Configuration

Default path:

```text
%USERPROFILE%\.token-tap\token-tap.json
```

Use another config file with:

```powershell
token-tap --help
token-tap init --config .\.token-tap\token-tap.json --database .\.token-tap\token-tap.db
```

Most commands accept `--config <path>`.

## Model Pricing

Pricing is stored per million tokens:

```json
{
  "models": {
    "gpt-5.4": {
      "provider": "openai",
      "inputPerMillion": 2.0,
      "cachedInputPerMillion": 0.5,
      "outputPerMillion": 12.0
    }
  }
}
```

Update pricing:

```powershell
token-tap config set-model gpt-5.4 --input 2 --cached-input .5 --output 12 --provider openai
token-tap config set-default-model gpt-5.4
```

## Watch Folders

Defaults:

```text
%APPDATA%\Code\logs
%APPDATA%\Code - Insiders\logs
%APPDATA%\Code\User\globalStorage
%APPDATA%\Code - Insiders\User\globalStorage
```

Add folders:

```powershell
token-tap watch-add "C:\path\to\logs"
```

## Retention

Defaults:

- usage events: 14 days
- sessions: 90 days
- anomalies: 90 days
- alert history: 90 days
- daily aggregates: 730 days
- hourly aggregates: 180 days

Change settings:

```powershell
token-tap retention set events 14d
token-tap retention set sessions 90d
token-tap retention set aggregates 730d
```

## SMTP

SMTP settings live under `alerts.email`. Passwords should not be stored in config.

```json
{
  "alerts": {
    "email": {
      "enabled": true,
      "smtpHost": "smtp.example.com",
      "smtpPort": 587,
      "useSsl": true,
      "username": "alerts@example.com",
      "passwordSecretName": "TOKEN_TAP_SMTP_PASSWORD",
      "from": "alerts@example.com",
      "to": ["you@example.com"]
    }
  }
}
```

Set the password:

```powershell
$env:TOKEN_TAP_SMTP_PASSWORD = "app-password"
```

For persistent user-level storage:

```powershell
[Environment]::SetEnvironmentVariable("TOKEN_TAP_SMTP_PASSWORD", "app-password", "User")
```
