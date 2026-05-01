# Contributing

Thanks for helping improve Token-Tap.

## Local Setup

```powershell
dotnet restore TokenTap.sln
dotnet build TokenTap.sln
dotnet test TokenTap.sln
```

## Pull Request Expectations

- Keep changes scoped.
- Include tests for parser, storage, or command behavior changes.
- Update docs when commands or config change.
- Keep privacy defaults conservative.
- Do not commit sample logs containing private prompts, source code, or secrets.

## Parser Contributions

Parser changes should include:

- representative synthetic log samples
- expected token counts
- expected confidence level
- redaction behavior, if excerpts are involved

## Security

Report security issues privately. See [SECURITY.md](SECURITY.md).
