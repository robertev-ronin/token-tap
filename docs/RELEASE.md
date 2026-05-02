# Release Notes And Process

## Release 1.000

Release `1.000` is published from tag `v1.000`.

Release assets:

- `token-tap-1.000-win-x64.zip`
- `token-tap-1.000-win-x64.zip.sha256`

The Windows package is a framework-dependent .NET 8 `win-x64` publish. Install the .NET 8 runtime if it is not already present.

Quick install:

```powershell
Expand-Archive .\token-tap-1.000-win-x64.zip -DestinationPath .\token-tap-1.000
.\token-tap-1.000\token-tap.exe --help
```

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
git tag v1.000
git push origin v1.000
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
