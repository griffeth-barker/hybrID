# CI Overview

hybrID uses GitHub Actions for continuous integration.

## Workflow File

- `.github/workflows/ci.yml`

## Trigger Conditions

- Push to `main`
- Pull request targeting `main`

## What CI Validates

1. **Static analysis** with PSScriptAnalyzer
2. **Pester tests** under `tests/`
3. **Test artifact** upload (`TestResults.xml`)

## Why Windows Runner

The project targets Windows PowerShell 5.1 behavior and modules, so CI runs on a Windows hosted runner.

## Local Parity Commands

Run these before opening a PR:

- `Invoke-Pester -Path .\tests -Verbose`
- `Invoke-ScriptAnalyzer -Path .\hybrID -Recurse -Settings .\.psscriptanalyzer.psd1`

## Branch Protection Recommendation

In GitHub settings, require the CI workflow status check for merges to `main`.

## Troubleshooting CI Failures

- **Pester failure:** inspect failing test output and reproduce locally
- **Analyzer failure:** fix reported issues or adjust baseline intentionally in `.psscriptanalyzer.psd1`
- **Module install failures:** rerun job; if persistent, pin module versions explicitly
