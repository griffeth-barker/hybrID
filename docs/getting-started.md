# Getting Started
**hybrID** is a tool designed to tell you where an identity (user or group) lives and which portal you need to use to manage it. It runs natively on Windows, macOS, and Linux.

## Launching the App
1.  Download the published build for your OS (or build it yourself — see [build](/docs/build.md)) and run it. No PowerShell, RSAT, or Graph module install is required.
2.  On first launch, hybrID seeds a per-user config file from its bundled defaults:
    - **Windows:** `%APPDATA%\hybrID\config.json`
    - **macOS/Linux:** `~/.config/hybrID/config.json`
3.  Open **Settings** (gear icon in the status bar) and configure your environment before your first search.

![](/assets/screenshot-main-ui.png)

## Configuring Settings
| Setting | Notes |
|---|---|
| **On-Premises Exchange FQDN** | The hostname used to build On-Prem ECP deep links. |
| **LDAP Server** | Your domain controller's hostname. |
| **LDAP Port** | Defaults to `636` (LDAPS). |
| **Encryption** | **LDAPS** is the recommended, cross-platform default. **StartTLS** only works on Windows — the underlying .NET LDAP library (`System.DirectoryServices.Protocols`) does not support StartTLS on macOS/Linux, regardless of certificate trust. **None** sends credentials in plaintext and should only be used against a disposable lab DC. |
| **Base DN** | Leave blank to auto-discover from the server's RootDSE. |
| **Skip certificate validation** | For self-signed lab DCs. **Windows only** — see the note below for macOS/Linux. |
| **Graph Tenant ID** | `organizations`, `common`, or a specific tenant GUID. |
| **Theme** | System, Light, or Dark. Applies immediately — no restart needed. |

### Self-signed certificates on macOS/Linux
The in-app "skip certificate validation" toggle only works on Windows (a limitation of the underlying .NET LDAP library on other platforms). On macOS/Linux, if you need to connect to a lab DC with a self-signed certificate, set the following in your shell **before** launching hybrID (it must be set before the process starts — an in-app setting can't apply it after the fact):

```bash
export LDAPTLS_REQCERT=never
```

Never do this against a production domain controller.

## Signing In
hybrID connects to two separate systems, each with its own sign-in:

- **Active Directory:** On your first search each session, you'll be prompted for a username and password. These credentials are used only for that LDAP connection and are never written to disk.
- **Microsoft Graph:** On your first search each session, a dialog shows a device code and a URL (`microsoft.com/devicelogin`). Sign in there with your administrative credentials. After that, hybrID caches the token using your OS's native secure storage (Windows Credential Manager, macOS Keychain, or the Linux Secret Service), so you won't be prompted again until the token expires.

## Searching for an Object
You can search using any of the following formats:
* **sAMAccountName:** (e.g., `jsmith`)
* **UserPrincipalName (UPN):** (e.g., `jsmith@domain.com`)
* **ObjectGUID:** (e.g., `12345678-1234-5678-1234-567812345678`)

Type the identity into the search bar and click **Locate**.

## Interpreting the Results
The app splits management into two distinct categories: **Identity** (name, password, group membership) and **Exchange** (email addresses, aliases, mailbox features).

![](/assets/screenshot-main-ui-with-search.png)

### Scenario A: Fully On-Premises
* **Manage Identity In:** `Active Directory` (Gray Text)
* **Manage Exchange In:** `On-Premises Exchange` (Blue Link)
* **Action:** Click the blue link to open the On-Premises ECP. Use Active Directory Users & Computers (ADUC) to reset passwords or change names.

### Scenario B: Hybrid / Synced (Most Common)
* **Manage Identity In:** `Active Directory` (Gray Text)
* **Manage Exchange In:** `On-Premises Exchange` (Blue Link)
* **Action:** Because the user syncs to the cloud, you **must** manage their proxy addresses and email aliases from the On-Premises ECP link provided. Do not attempt to change email aliases directly in M365, as the sync will overwrite them.

### Scenario C: Cloud-Only
* **Manage Identity In:** `Entra ID` (Blue Link)
* **Manage Exchange In:** `Exchange Online` (Blue Link)
* **Action:** This object does not exist in our local Active Directory. Click the links to manage them entirely within the Microsoft 365 web portals.

## Using the Deep Links
Whenever a field appears as blue, underlined text, it is a direct link to that specific user or group's profile. Clicking it will automatically open a new browser window directly to that object's management page.
