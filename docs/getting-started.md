# Getting Started

**hybrID** helps you quickly determine where an identity object should be managed in a hybrid Microsoft environment.

## Launching the App

1. Launch **Windows PowerShell 5.1**.
2. Change directory to the repository root.
3. Run:
	- `./hybrID/public/hybrID.ps1`
4. On first launch, sign in to Microsoft Graph if prompted.

![](/assets/screenshot-main-ui.png)

## Search Inputs

Enter one of the following:

- **sAMAccountName** (for example: `jsmith`)
- **UserPrincipalName (UPN)** (for example: `jsmith@domain.com`)
- **ObjectGUID** (for example: `12345678-1234-5678-1234-567812345678`)

Select **Locate** to run the lookup.

## Understanding the Main Window

The main window is organized to keep on-prem and cloud context visible at the same time:

- **Left column:** On-premises context (Active Directory + Exchange Server)
- **Middle column:** Control actions and notes (including SOA state/actions)
- **Right column:** Cloud context (Entra ID + Exchange Online)

![](/assets/screenshot-main-ui-with-search.png)

## Common Result Patterns

### On-Premises Object

- Identity is managed on-prem.
- Exchange management appears as On-Premises Exchange (if mail-enabled).

### Synced / Hybrid Object

- Identity is synchronized between AD and Entra ID.
- Exchange management path depends on mailbox/group state.
- SOA state is shown in the middle column where applicable.

### Cloud-Only Object

- Identity management points to Entra ID.
- Exchange management points to Exchange Online when mail-enabled.

## Source of Authority (SOA) Actions

When the current object supports SOA transfer:

- A transfer button appears in the middle column.
- Button text updates to reflect direction (transfer vs revert).
- The current SOA state is displayed above the action.

## Deep Links

Blue, underlined values open the specific object management destination in a browser window. Link generation is driven by templates in `hybrID/config/config.json`.

## Next Steps

- See `docs/screenshots.md` for UI examples.
- See `docs/troubleshooting.md` for common issues.
- See `docs/contributing.md` for development standards.