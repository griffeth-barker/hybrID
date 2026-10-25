# Contributing to hybrID
First off, thank you for considering contributing to hybrID! This tool is designed to make hybrid identity management frictionless across every platform, and community contributions are what keep it sharp.

This document outlines the architecture, coding standards, and workflow for contributing to the project.

## Project Architecture

hybrID is a C#/.NET 10 [Avalonia](https://avaloniaui.net/) desktop application. We separate presentation (Views), business logic (Services), and data shapes (Models) into distinct folders under `src/HybridID/`.

```text
hybrID/
├── assets/              # GitHub repository assets (readmes, badges)
├── build/                # build.ps1 (dotnet publish wrapper) and version.json
├── docs/                 # Project documentation
├── src/
│   └── HybridID/
│       ├── Assets/       # Static app resources (icons, fonts)
│       ├── Controls/     # Small composite controls (InfoBar, SettingsCard, ManagementCard, ...)
│       ├── Models/       # Plain data types - AppConfig, AdLookupResult, GraphLookupResult, IdentityLookupResult
│       ├── Services/     # Business logic (see below)
│       ├── Themes/       # Design tokens, typography, styles and control themes
│       └── Views/        # MainWindow, Pages/, Dialogs/ (AXAML + code-behind)
├── tools/
│   └── HybridID.Screenshots/  # Dev-only headless renderer for design review
├── hybrID.slnx
└── README.md
```

### Component Breakdown

#### `Views/*.axaml` + `*.axaml.cs`
Presentation layer. `MainWindow` is the only OS window: it hosts the nav rail, the cached pages (`Pages/SearchPage`, `SettingsPage`, `AboutPage`), in-window overlay dialogs (`Dialogs/AdCredentialDialog`, `DeviceCodeDialog`, `MessageDialog`) and toasts. Pages reach the window only through `IAppShell`. `LiveLookupSource` performs real searches for the Search page; `--preview` swaps in sample data (`Services/PreviewLookupSource`). The UI follows the design contract in [`docs/design/ui-spec.md`](/docs/design/ui-spec.md). We use plain code-behind (no MVVM framework) — this is intentional, matching the app's original simplicity. Keep it that way; don't introduce ReactiveUI/CommunityToolkit.Mvvm without a real need.

#### `Services/*.cs`
Where all logic lives:
  - `LdapIdentityService` — direct LDAP queries against AD via `System.DirectoryServices.Protocols` (replaces the old `Get-ADObject`/RSAT dependency; no domain join required).
  - `GraphAuthService` / `GraphIdentityService` — Microsoft Graph queries via the official `Microsoft.Graph` C# SDK with device-code sign-in (replaces the Graph PowerShell SDK).
  - `IdentityLookupService` — orchestrates the two above and contains the routing logic that decides where an identity's mailbox actually lives (on-prem, hybrid remote mailbox, or cloud-only).
  - `AppSession` — state shared by the pages: config, the LDAP connection, the Graph client and AD/Graph connection state.
  - `ConfigService` — loads/saves the per-user config file.
  - `ThemeService` — applies the Light/Dark/System theme via Avalonia's `RequestedThemeVariant`.
  - `UrlLauncher` — opens a URL in the OS default browser (`Process.Start` with `UseShellExecute`, works unmodified on all three platforms).

#### `Models/*.cs`
Plain, dependency-free data types: `AppConfig` (the config schema), `AdLookupResult`, `GraphLookupResult`, `IdentityLookupResult`, `ManagementLink`.

#### `Themes/`
`Theme.axaml` defines the design tokens (WinUI-style names such as `TextFillColorPrimaryBrush` and `CardBackgroundFillColorDefaultBrush`, plus app-specific `Location*` brushes) with `ThemeDictionaries` for `Light` and `Dark`. `Typography.axaml` holds the type ramp classes (`title`, `body`, `caption`, `mono`, ...), `AppStyles.axaml` the button variants and shared styles, and `Controls/*.axaml` the themes for the app's own controls. Everything builds on Avalonia's `FluentTheme`, and the brand accent color (`#007ACC`) flows through `SystemAccentColor`.

#### `config.json` (per-user, not in the repo)
Persistent state, seeded on first run from the bundled `config.default.json`: `settings` (On-Prem Exchange FQDN, LDAP connection details, Graph Tenant ID, Theme) and `managementUrls` (URL templates with `{id}`/`{fqdn}` placeholders for Entra/EXO/On-Prem ECP).

## Design & Theming Standards
  - **No hardcoded colors in Views.** Use the `DynamicResource` tokens from `Themes/Theme.axaml`, and change state by toggling `Classes` rather than assigning brushes from code, so the UI follows theme changes live. There is no restart mechanism, and there shouldn't need to be one.
  - **Rounded corners / brand accent**: handled by Fluent theme defaults plus `SystemAccentColor`; don't reintroduce custom `ControlTemplate`s for standard controls (buttons, text boxes, combo boxes) unless Fluent genuinely can't do what's needed.
  - **One window**: settings, about and every prompt live inside `MainWindow` (pages and overlay dialogs). Don't add child OS windows.

## Coding Standards
  - **Language/runtime**: C#, .NET 10, nullable reference types enabled (`<Nullable>enable</Nullable>`) — don't add `!` suppressions to silence a real null risk; fix the null-safety instead.
  - **Separation of Concerns**: business logic belongs in `Services/`, not in a View's code-behind. Code-behind should orchestrate (call a service, bind the result to controls) — see `MainWindow.axaml.cs` for the pattern.
  - **Error Handling**: external AD/Graph calls must not crash the app or the search, but they must not be silently read as "not found" either — a missing directory makes a synced object look cloud-only or on-prem-only. `IdentityLookupService` returns failures as `AdError`/`GraphError`, and the Search page tells the technician which directory wasn't searched.
  - **Platform quirks worth knowing**:
    - StartTLS is Windows-only — `System.DirectoryServices.Protocols` doesn't support it on macOS/Linux. LDAPS is the cross-platform default; `LdapIdentityService.Connect` throws a clear `PlatformNotSupportedException` if StartTLS is requested on a non-Windows OS.
    - The programmatic "skip certificate check" (`VerifyServerCertificate`) is also Windows-only; on macOS/Linux it must be done via the `LDAPTLS_REQCERT` environment variable set *before* the process launches (see `docs/getting-started.md`).
    - Graph auth uses the public "Microsoft Graph Command Line Tools" client ID (`14d82eec-204b-4c2f-b7e8-296a70dab67e`) — the same one the Graph PowerShell SDK uses — via device-code flow, so no app registration or redirect URI is required out of the box.

## Development Workflow
  1. Fork the repository and clone it to your local machine.
  2. Create a feature branch (`git checkout -b feature/Add-Export-Button`).
  3. Make your changes within `src/HybridID/`.
  4. Build and run: `dotnet build hybrID.slnx` from the repo root, or `dotnet run --project src/HybridID`. Add `-- --preview` to try the Search page with sample data instead of a real domain controller and tenant. Confirm your change respects both Dark and Light themes; `dotnet run --project tools/HybridID.Screenshots -- <outDir>` renders the main screens in both themes without a display.
     > There currently is not a test harness, but that may be an eventual addition (likely xUnit, given the move away from PowerShell/Pester).
  5. Commit your changes with clean, descriptive commit messages.
  6. Push to your fork and submit a PR to the main branch.

### Adding a New Setting
If you are adding a new user-configurable setting:

  1. Add the property to `AppSettings` in `src/HybridID/Models/AppConfig.cs`, and its default to `src/HybridID/config.default.json`.
  2. Add a `SettingsCard` with its input to the right section of `src/HybridID/Views/Pages/SettingsPage.axaml`.
  3. Wire it up in `SettingsPage.axaml.cs`: populate it in `Populate`, read it in `ReadSettings`, compare it in `IsDirty` (and `ConnectionChanged` if it affects the LDAP/Graph connection), and write it into `Session.Config` in `Save`.

## Need Help?
If you are unsure about the architecture or how to implement a specific feature, feel free to open a Draft PR or an Issue to discuss it!
