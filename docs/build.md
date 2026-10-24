# Building hybrID

hybrID is a C#/.NET 10 Avalonia application. You need the [.NET SDK](https://dotnet.microsoft.com/download) (10 or later) installed; nothing else.

## Build

From the repo root:

```bash
dotnet build hybrID.slnx
```

Or from the project directory:

```bash
cd src/HybridID
dotnet build
```

## Run from source

```bash
dotnet run --project src/HybridID
```

## Publish a self-contained release build

`build/build.ps1` wraps `dotnet publish` for each target platform, producing a self-contained, single-file executable that needs no .NET runtime installed on the machine that runs it. The script itself is PowerShell 7 (cross-platform `pwsh`), used purely as the build tooling — it has no bearing on what the shipped app depends on, which is nothing beyond the OS itself.

```powershell
# Publish for all four supported platforms (win-x64, osx-x64, osx-arm64, linux-x64)
pwsh build/build.ps1

# Publish for just one
pwsh build/build.ps1 -Rid osx-arm64
```

Output lands in `build/releases/<version>/<rid>/` (gitignored). The version number comes from `build/version.json`.

Each published folder contains a single native executable (e.g. `HybridID.exe` on Windows, `HybridID` on macOS/Linux) plus `config.default.json`. Copy the folder to the target machine and run it directly — no installer, no prerequisites.
