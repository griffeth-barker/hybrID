# Contributing to hybrID

Thanks for contributing to **hybrID**. This project helps IT professionals triage and route hybrid identity objects across Active Directory, Microsoft Entra ID, Exchange Server, and Exchange Online.

This guide defines architecture boundaries, coding standards, and pull request expectations.

## Project Principles

- Keep the app fast and dependable for helpdesk workflows.
- Prefer safe, reversible operations over destructive changes.
- Preserve clear on-prem vs cloud routing behavior.
- Optimize for readability and maintainability over cleverness.

## Architecture (Must Follow)

hybrID uses a modular MVC-like structure:

```text
hybrID/
├── docs/                # Project documentation
├── hybrID/
│   ├── assets/          # App icons/images
│   ├── config/          # Persistent app state (config.json)
│   ├── private/         # Feature functions (one focused function per file)
│   ├── public/          # Bootstrap/composition entrypoint (hybrID.ps1)
│   └── ui/              # XAML UI definitions
└── tests/               # Pester tests (as coverage grows)
```

### Responsibilities by Layer

- `public/hybrID.ps1`
  - Bootstrap only: load config/XAML, import private scripts, bind events, initialize globals.
  - Do **not** place business logic here.

- `private/*.ps1`
  - Business logic and integrations (AD, Graph, Exchange routing, SOA, etc.).
  - Keep functions focused and reusable.
  - Prefer one feature per file.

- `ui/*.xaml`
  - All layout and visual structure.
  - Do not generate UI markup in PowerShell.

- `config/config.json`
  - User-editable configuration only.
  - New settings must include safe defaults.

## PowerShell and Runtime Standards

- Target **Windows PowerShell 5.1** compatibility.
- Avoid PowerShell 7+ only syntax/features.
- Use approved module patterns already present in the project.
- Keep cmdlets explicit and readable.

## State and Scope Rules

- Use `$global:` only for cross-file shared state (for example, `$global:Config`, `$global:uiDir`, brush globals, current search context).
- Keep temporary and per-call values local.
- If new shared state is required, initialize it in `public/hybrID.ps1`.

## UI and Theming Standards

hybrID supports dynamic themes (Dark, Light, System). New UI must respect theme resources.

- Do not hardcode colors in XAML where a theme token exists.
- Use these existing resources where applicable:
  - `{DynamicResource WindowBg}`
  - `{DynamicResource PanelBg}`
  - `{DynamicResource InputBg}`
  - `{DynamicResource BorderBrush}`
  - `{DynamicResource PrimaryText}`
  - `{DynamicResource SecondaryText}`
  - `{DynamicResource NotesText}`
- Keep controls visually consistent (corner radius, spacing, typography).
- Use `Set-Link` for management hyperlinks so link styling/behavior remains centralized.
- New child windows/dialogs should inherit the main app icon and be themed via `Apply-Theme`.

## Error Handling and Logging

- Wrap external operations (AD, Graph, Exchange, browser/URL actions) in `try/catch`.
- Provide user-facing status updates with `Set-Status`.
- Log failures using `Write-HybrIDLog` with helpful context.
- Avoid raw console output for user-facing diagnostics.

## Feature Change Patterns

### Adding a New Setting

1. Add a default value in `config/config.json`.
2. Add/update controls in `ui/settings.xaml`.
3. Wire load/save behavior in `private/Show-Settings.ps1`.

### Adding a New Feature Function

1. Add a new focused file in `private/`.
2. Reuse existing shared utilities when possible (`Set-Status`, `Set-Link`, `Write-HybrIDLog`, `Apply-Theme`).
3. Bind UI events in `public/hybrID.ps1` only.
4. Keep search/routing semantics in sync with `Invoke-Search` behavior.

## Development Workflow

1. Fork and clone.
2. Create a feature branch (for example, `feature/add-export-json`).
3. Implement changes within the established folder boundaries.
4. Run the app locally from repo root:
   - `./hybrID/public/hybrID.ps1`
5. Validate core flows manually:
   - Search (AD-only, cloud-only, synced)
   - Settings and About windows
   - Theme behavior
   - SOA actions (if affected)
6. Add or update Pester tests when practical.
7. Submit a PR with clear scope and validation notes.

## Local Test & Lint Commands

From repository root in Windows PowerShell:

1. Run Pester tests:
  - `Invoke-Pester -Path .\tests -Verbose`
2. Run script analysis (error severity baseline):
  - `Invoke-ScriptAnalyzer -Path .\hybrID -Recurse -Settings .\.psscriptanalyzer.psd1`

If you do not have required modules installed locally:

- `Install-Module Pester -Scope CurrentUser`
- `Install-Module PSScriptAnalyzer -Scope CurrentUser`

## Continuous Integration (CI)

GitHub Actions runs CI on pushes and pull requests targeting `main`.

Current CI checks:

- PowerShell static analysis using PSScriptAnalyzer
- Pester tests in `tests/`

Contributors should treat CI failures as blockers and resolve them before merge.

## Pull Request Checklist

Before opening a PR, verify:

- [ ] Feature logic is in `private/`, not `public/hybrID.ps1`.
- [ ] XAML uses theme resources consistently.
- [ ] External calls are guarded with `try/catch` and logged when needed.
- [ ] New settings are wired end-to-end (config + UI + load/save).
- [ ] No unnecessary global variables were introduced.
- [ ] Manual validation steps are included in the PR description.
- [ ] CI checks are passing.

## Need Help?

If you're unsure about architecture, open a draft PR or an issue and include:

- the scenario you're solving,
- expected object behavior (on-prem/cloud/synced), and
- any API/module assumptions.