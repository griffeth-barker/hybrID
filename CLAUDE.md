# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

hybrID is a cross-platform (Windows/macOS/Linux) desktop app for helpdesk technicians in hybrid Microsoft environments (On-Prem Active Directory synced to Entra ID, with Exchange Hybrid). Given an identity (sAMAccountName, UPN, or ObjectGUID), it queries both AD (direct LDAP) and Microsoft Graph, determines where the identity and its mailbox are actually managed (On-Prem AD/Exchange, EXO, or a hybrid remote-mailbox combination), and produces deep links directly into the correct admin portal blade.

It's a C#/.NET 10 [Avalonia](https://avaloniaui.net/) application — no PowerShell involved in the shipped app (the app was originally a Windows-only PowerShell/WPF script; it was fully rewritten to be cross-platform, see `docs/changelog.md` 0.2.0).

## Commands

```bash
# Build (from repo root or the project directory)
dotnet build hybrID.slnx
cd src/HybridID && dotnet build

# Run from source
dotnet run --project src/HybridID

# Run with sample data instead of AD/Graph (Search page design review; also HYBRID_PREVIEW=1)
dotnet run --project src/HybridID -- --preview

# Render preview-mode screenshots headlessly, Light + Dark (default out: ./screenshots)
dotnet run --project tools/HybridID.Screenshots -- <outDir> [w=640] [identity ...]

# Publish self-contained single-file binaries for all platforms (or -Rid <one>)
pwsh build/build.ps1
```

There is no automated test suite yet (see `docs/roadmap.md`). Verify changes by running the app and confirming both Dark and Light themes render correctly; for anything touching `LdapIdentityService`, verify against a real (or disposable Samba) AD DC — LDAP behavior genuinely differs by OS (see Platform Quirks below), so don't assume Windows behavior carries over.

## Architecture

`src/HybridID/` separates presentation, logic, and data:

- **`Views/*.axaml` + `*.axaml.cs`** — `MainWindow` (search UI), `SettingsWindow`, `AboutWindow`, `AlertDialog` (generic message dialog), `AdCredentialDialog` (LDAP username/password prompt), `DeviceCodeDialog` (Graph device-code sign-in prompt). Plain code-behind, no MVVM framework — this is intentional; don't introduce ReactiveUI/CommunityToolkit.Mvvm. Code-behind orchestrates (call a service, bind results to controls); it should not contain query/routing logic itself.
- **`Services/*.cs`** — where all logic lives:
  - `LdapIdentityService` — direct LDAP queries via `System.DirectoryServices.Protocols`. No domain join required.
  - `GraphAuthService` / `GraphIdentityService` — Microsoft Graph queries via the official `Microsoft.Graph` C# SDK, device-code auth.
  - `IdentityLookupService` — orchestrates the two above; contains the actual routing logic (on-prem / hybrid remote-mailbox / cloud-only determination, deep-link URL building). This is the C# port of the original app's `Invoke-Search.ps1` — if you're changing routing behavior, this is the file.
  - `ConfigService` — loads/saves the per-user config file (see below).
  - `ThemeService` — applies Light/Dark/System via Avalonia's `RequestedThemeVariant`.
  - `UrlLauncher` — opens a URL in the OS default browser (`Process.Start` + `UseShellExecute`, works unmodified on all three platforms).
- **`Models/*.cs`** — plain data types: `AppConfig` (config schema), `AdLookupResult`, `GraphLookupResult`, `IdentityLookupResult`, `ManagementLink`.
- **`Themes/Theme.axaml`** — the app's palette as an Avalonia `ResourceDictionary` with `Light`/`Dark` `ThemeDictionaries` (`WindowBg`, `PanelBg`, `InputBg`, `AppBorderBrush`, `PrimaryText`, `SecondaryText`, `NotesText`, `LinkText`, `StatusBarBg`, plus theme-invariant `StatusInfoBrush`/`StatusSuccessBrush`/`StatusErrorBrush`/`StatusReadyBrush`). Built on Avalonia's `FluentTheme` rather than hand-rolled `ControlTemplate`s — only the app-specific palette is overridden; the brand accent (`#007ACC`) flows through `SystemAccentColor`.

### Config

Per-user JSON file, not in the repo: `%APPDATA%\hybrID\config.json` (Windows) or `~/Library/Application Support/hybrID/config.json` (macOS) or `~/.config/hybrID/config.json` (Linux), all via `Environment.SpecialFolder.ApplicationData`, seeded on first run from `src/HybridID/config.default.json`. Schema: `settings` (On-Prem Exchange FQDN, LDAP server/port/encryption/base DN, Graph Tenant ID, Theme) and `managementUrls` (URL templates with `{id}`/`{fqdn}` placeholders).

### Adding a new setting

1. Add the property to `AppSettings` in `Models/AppConfig.cs`, and its default to `config.default.json`.
2. Add the corresponding label/input to `Views/SettingsWindow.axaml`.
3. Wire it up in `SettingsWindow.axaml.cs` — populate on load, persist via `ConfigService.Save` on Apply.

## Platform quirks (learned the hard way — verify before "fixing")

- **StartTLS is Windows-only.** `System.DirectoryServices.Protocols` does not support `StartTransportLayerSecurity` on macOS/Linux — confirmed by live testing against a real DC, fails regardless of certificate trust. LDAPS (implicit TLS) is the cross-platform default; `LdapIdentityService.Connect` throws `PlatformNotSupportedException` if StartTLS is requested on a non-Windows OS. Don't try to "fix" this by debugging StartTLS further on Unix — switch to LDAPS.
- **Programmatic cert-skip is also Windows-only.** Setting `LdapConnection.SessionOptions.VerifyServerCertificate` throws immediately on Unix. On macOS/Linux, self-signed cert trust must be set via the `LDAPTLS_REQCERT` environment variable **before the process starts** (an in-app runtime `Environment.SetEnvironmentVariable` call does not work — the native OpenLDAP-backed layer reads it at process init, confirmed by testing).
- **Graph auth** uses the public "Microsoft Graph Command Line Tools" client ID (`14d82eec-204b-4c2f-b7e8-296a70dab67e`, same one the Graph PowerShell SDK uses) via device-code flow — no app registration or redirect URI needed out of the box. Token cache is `Azure.Identity`'s `TokenCachePersistenceOptions`, using each OS's native secure storage.
- **Theme changes apply live** across all open windows via `ThemeService.Apply` — there's no restart mechanism, and none should be reintroduced; the old WPF app needed one, Avalonia doesn't.

## Versioning / changelog

`build/version.json` holds the current version. Changes are logged in `docs/changelog.md` following Keep a Changelog + SemVer conventions.
