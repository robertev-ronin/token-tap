# Release Notes And Process

## Public Release Checklist

1. Update `CHANGELOG.md`.
2. Run:

```powershell
dotnet restore TokenTap.sln
dotnet build TokenTap.sln
dotnet test TokenTap.sln
```

3. Publish a self-contained build if desired:

```powershell
dotnet publish src/TokenTap.Cli -c Release -r win-x64 --self-contained false -o artifacts/token-tap-win-x64
```

4. Tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

5. Create a GitHub release with:

- release notes
- build artifacts
- checksum file, when artifacts are attached

## Versioning

Before API stabilization, use `0.x` versions:

- patch: bug fixes and parser refinements
- minor: new commands, new parser families, schema additions
- major: reserved for future stable CLI/API compatibility

## GitHub Actions

The included workflow runs build and tests on:

- Windows latest
- Ubuntu latest

Windows is included because Performance Counter code is Windows-specific.
