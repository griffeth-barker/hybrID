## Summary

Describe what this PR changes and why.

## Related Issue(s)

- Closes #
- Related to #

## Type of Change

- [ ] Feature
- [ ] Bug fix
- [ ] Refactor
- [ ] Documentation
- [ ] Other (describe):

## Validation Performed

Describe how you validated this change.

- [ ] App launches successfully via `./hybrID/public/hybrID.ps1`
- [ ] Search flow validated (AD-only, cloud-only, synced as applicable)
- [ ] Settings/About flow validated (if impacted)
- [ ] Theme behavior validated (Dark/Light/System as applicable)
- [ ] SOA flow validated (if impacted)

## Architecture & Quality Checklist

- [ ] Feature logic is in `private/`, not `public/hybrID.ps1`
- [ ] XAML uses theme resources consistently
- [ ] External calls are wrapped with `try/catch`
- [ ] Failures are logged with `Write-HybrIDLog` where appropriate
- [ ] User-facing status updates use `Set-Status` where appropriate
- [ ] New settings are wired end-to-end (`config/config.json` + `ui/settings.xaml` + `private/Show-Settings.ps1`)
- [ ] No unnecessary global variables were introduced
- [ ] Management links use `Set-Link` where applicable

## Files/Areas Changed

List key files changed and a short note for each.

- 

## Notes for Reviewers

Anything specific reviewers should focus on, risks to watch for, or follow-up work.
