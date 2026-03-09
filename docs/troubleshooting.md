# Troubleshooting

This guide covers common hybrID runtime and operator issues.

## App Won't Launch

### Symptoms

- Script exits immediately
- Error about missing XAML, config, or icon

### Checks

- Run from repo root: `./hybrID/public/hybrID.ps1`
- Confirm required files exist:
  - `hybrID/ui/main.xaml`
  - `hybrID/config/config.json`
  - `hybrID/assets/hybrID-icon-nobg-x128.ico`

## Active Directory Lookup Fails

### Symptoms

- Search returns cloud-only unexpectedly
- AD object not found when expected

### Checks

- Verify RSAT ActiveDirectory module is installed
- Confirm workstation/domain connectivity
- Validate search value format (sAMAccountName/UPN/GUID)

## Graph Authentication Issues

### Symptoms

- Prompt loops or authentication canceled state
- SOA actions fail with permission errors

### Checks

- Re-authenticate via app startup flow
- Confirm account roles/consent for required scopes
- Confirm tenant policies allow sign-in method used

## SOA Transfer Fails

### Symptoms

- Button visible but transfer fails
- State remains unknown

### Checks

- Confirm object is synced and supports SOA path
- Confirm Graph permissions include SOA scopes
- Retry after brief delay and re-run search to refresh state

## Links Open Incorrectly

### Symptoms

- Link opens wrong admin page
- Link missing placeholders

### Checks

- Review URL templates in `hybrID/config/config.json`
- Verify `{id}` and `{fqdn}` placeholders are valid for template

## Logs and Diagnostics

Operational errors are logged to the hidden logs folder under the app root.

- Path: `hybrID/logs/`
- Files rotate daily (date-based naming)

Use logs for correlation when API or routing behavior is unexpected.

## UI/Theme Issues

### Symptoms

- Controls look unthemed or inconsistent

### Checks

- Ensure new controls use DynamicResource theme tokens
- Ensure child windows are themed through `Apply-Theme`

## Still Stuck?

Open a GitHub issue with:

- What you searched for
- What you expected
- What happened instead
- Relevant redacted log snippets
- App version/commit
