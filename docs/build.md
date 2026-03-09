# Build & Release Guide

This project does not currently have a fully automated packaging pipeline. Use this checklist to produce consistent, traceable releases.

## Release Readiness Checklist

- [ ] Roadmap and scope are aligned for the target version.
- [ ] `docs/changelog.md` includes release notes for the new version.
- [ ] `build/version.json` is updated to the target version.
- [ ] GitHub Actions CI is passing for the release commit.
- [ ] Core flows have been manually validated:
	- [ ] Search (AD-only, cloud-only, synced)
	- [ ] SOA transfer flow(s)
	- [ ] Settings/About windows
	- [ ] Theme behavior
- [ ] No sensitive data appears in logs/screenshots/docs.

## Local Validation

Run from repository root in Windows PowerShell:

1. `./hybrID/public/hybrID.ps1`
2. Exercise main troubleshooting paths listed above.
3. Confirm startup/auth behavior is resilient when Graph sign-in is canceled.
4. Run test and lint checks:
	- `Invoke-Pester -Path .\\tests -Verbose`
	- `Invoke-ScriptAnalyzer -Path .\hybrID -Recurse -Settings .\.psscriptanalyzer.psd1`

## Versioning

hybrID follows Semantic Versioning.

- Patch (`x.y.Z`): fixes, non-breaking maintenance
- Minor (`x.Y.z`): backward-compatible features
- Major (`X.y.z`): breaking changes

## Release Steps

1. Ensure release branch is up to date with `main`.
2. Update `docs/changelog.md` for the new version/date.
3. Update `build/version.json`.
4. Commit with a release-focused message.
5. Create and push a version tag (for example: `v0.2.0`).
6. Publish GitHub Release notes (copy from changelog entry).

## Post-Release

- [ ] Confirm tag and release page are correct.
- [ ] Verify links in `README.md` and docs reference the latest release as needed.
- [ ] Move remaining work items back to roadmap/backlog.

## Related Docs

- `docs/ci.md`
- `docs/troubleshooting.md`
- `docs/contributing.md`

