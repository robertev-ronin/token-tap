# Security Policy

Token-Tap handles local development logs, so privacy bugs matter.

## Supported Versions

The current `main` branch is supported during early development.

## Reporting A Vulnerability

Please do not open a public issue for vulnerabilities involving:

- secret exposure
- prompt/response leakage
- unsafe raw log storage
- SMTP credential handling
- path traversal or unsafe file writes

Report privately to the repository owner through GitHub.

## Security Principles

- Store summarized metrics by default.
- Redact excerpts.
- Keep SMTP passwords out of config files.
- Use parameterized SQL.
- Watch logs read-only.
- Do not intercept network traffic.
