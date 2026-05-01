# Privacy

Token-Tap is designed as a local meter, not a transcript archive.

## Default Storage

By default it stores:

- timestamp
- agent name
- source
- model
- input/output/cached token counts
- estimated cost
- confidence level
- prompt and response hashes when available
- source file hash
- short redacted excerpt

## Not Stored By Default

By default it does not store:

- full prompts
- full responses
- raw log files
- source code contents
- private chat history
- secrets
- SMTP passwords

## Redaction

Short excerpts pass through a redactor that targets common secret shapes:

- Authorization bearer headers
- `password`, `secret`, `token`, and API key assignments
- OpenAI-style `sk-...` keys
- GitHub-style `ghp_...`, `gho_...`, `ghu_...`, `ghs_...`, `ghr_...` tokens

Redaction is a defense-in-depth feature. Do not intentionally import logs containing secrets unless you understand the risk.

## Hashes

Hashes use SHA-256. They help detect repeated prompts and correlate events without storing the original content.

## Raw Logs

`privacy.storeRawLogLines` is present for future advanced scenarios, but the current implementation does not archive raw logs. Keep this off unless a later feature explicitly requires it.

## Local-Only Operation

Token-Tap does not:

- inject into VS Code
- intercept network traffic
- proxy agent calls
- upload usage to a remote service
- modify watched log files

SMTP sends alert emails only when explicitly configured.
