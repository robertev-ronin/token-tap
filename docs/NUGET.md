# NuGet Global Tool Publishing

Token-Tap is configured as a .NET global tool package with:

- Package ID: `token-tap`
- Command name: `token-tap`
- NuGet package version: `1.0.0`
- Product/release label: `1.000`

NuGet package versions follow SemVer, so release `1.000` is represented as package version `1.0.0`.

## Install

After the package is published to NuGet.org:

```powershell
dotnet tool install --global token-tap
token-tap --help
```

Install a specific version:

```powershell
dotnet tool install --global token-tap --version 1.0.0
```

Update:

```powershell
dotnet tool update --global token-tap
```

Uninstall:

```powershell
dotnet tool uninstall --global token-tap
```

## Local Package Test

```powershell
dotnet pack src/TokenTap.Cli -c Release -o artifacts/package
dotnet tool install --global token-tap --add-source artifacts/package --version 1.0.0
token-tap --help
dotnet tool uninstall --global token-tap
```

Use `--tool-path <folder>` instead of `--global` for isolated testing.

## Publish To NuGet.org

You need a NuGet.org account and API key.

Create the key:

```text
NuGet.org -> Account -> API Keys -> Create
Scope: Push
Packages: token-tap or *
```

Publish:

```powershell
dotnet nuget push artifacts/package/token-tap.1.0.0.nupkg --api-key <NUGET_API_KEY> --source https://api.nuget.org/v3/index.json
```

After NuGet finishes indexing, the public install command works:

```powershell
dotnet tool install --global token-tap
```

## Notes

- NuGet package IDs are globally unique.
- Published versions are immutable. If `1.0.0` is pushed, corrections require a new package version.
- Keep the GitHub release label `1.000` and NuGet package version `1.0.0` together in release notes.
