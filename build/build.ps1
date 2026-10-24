#requires -version 7
<#
.SYNOPSIS
    Publishes hybrID as a self-contained, single-file executable for one or more platforms.

.DESCRIPTION
    Wraps `dotnet publish` for each target runtime identifier (RID). Output lands in
    build/releases/<rid>/, which is gitignored. Requires the .NET SDK (10+) - this script
    itself just needs PowerShell 7 to run cross-platform on the machine doing the building;
    it has no bearing on what the shipped app depends on (nothing - it's self-contained).

.PARAMETER Rid
    One or more .NET runtime identifiers to publish. Defaults to all four supported platforms.

.EXAMPLE
    ./build/build.ps1
    Publishes for win-x64, osx-x64, osx-arm64, and linux-x64.

.EXAMPLE
    ./build/build.ps1 -Rid osx-arm64
    Publishes only for Apple Silicon macOS.
#>
param(
    [ValidateSet('win-x64', 'osx-x64', 'osx-arm64', 'linux-x64')]
    [string[]]$Rid = @('win-x64', 'osx-x64', 'osx-arm64', 'linux-x64')
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'src/HybridID/HybridID.csproj'
$versionInfo = Get-Content (Join-Path $PSScriptRoot 'version.json') -Raw | ConvertFrom-Json
$version = $versionInfo[0].version

foreach ($r in $Rid) {
    $outDir = Join-Path $PSScriptRoot "releases/$version/$r"
    Write-Host "Publishing $r -> $outDir" -ForegroundColor Cyan

    dotnet publish $project `
        --configuration Release `
        --runtime $r `
        --self-contained true `
        --output $outDir `
        -p:PublishSingleFile=true `
        -p:Version=$version `
        -p:IncludeNativeLibrariesForSelfExtract=true

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $r"
    }
}

Write-Host "`nDone. Published builds are under build/releases/$version/" -ForegroundColor Green
