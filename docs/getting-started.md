# Getting Started
**hybrID** is a tool designed to tell you where an identity (user or group) lives and which portal you need to use to manage it. It runs natively on Windows, macOS, and Linux.

## Launching the App
1.  Download the published build for your OS (or build it yourself — see [build](/docs/build.md)) and run it. No PowerShell, RSAT, or Graph module install is required.
2.  On first launch, hybrID seeds a per-user config file from its bundled defaults:
    - **Windows:** `%APPDATA%\hybrID\config.json`
    - **macOS:** `~/Library/Application Support/hybrID/config.json`
    - **Linux:** `~/.config/hybrID/config.json`
3.  Open **Settings** (bottom of the left navigation, or **Ctrl+,** / **⌘,**) and configure your environment before your first search. Until a domain controller is set, a banner on the Search page reminds you that only Entra ID is searched.

![](/assets/screenshot-main-presearch.png)

## Configuring Settings
Changes to everything except the theme are staged: an **Unsaved changes** bar appears with **Save** (Ctrl+S) and **Discard**. Each section's **Test connection** button checks the values on screen, even before you save.

| Setting | Notes |
|---|---|
| **Domain controller** | Your domain controller's hostname. |
| **Encryption** | **LDAPS** is the recommended, cross-platform default. **StartTLS** only works on Windows — the underlying .NET LDAP library (`System.DirectoryServices.Protocols`) does not support StartTLS on macOS/Linux, regardless of certificate trust. **None** sends credentials in plaintext and should only be used against a disposable lab DC. |
| **Port** | Defaults to `636` (LDAPS). Switching encryption moves it between 636 and 389 for you. |
| **Base DN** *(Advanced)* | Leave blank to auto-discover from the server's RootDSE. |
| **Skip certificate validation** *(Advanced)* | For self-signed lab DCs. **Windows only** — see the note below for macOS/Linux. |
| **On-premises Exchange server** | The FQDN used to build Exchange admin center (ECP) links. |
| **Tenant** | `organizations`, `common`, a tenant ID, or a verified domain. |
| **Client ID** *(Advanced)* | Defaults to the public Microsoft Graph Command Line Tools client. Change it only if Conditional Access requires your own app registration. |
| **App theme** | Use system setting, Light, or Dark. Applies and saves immediately. |
| **Link templates** | The deep-link URL templates. `{id}` is the object's ID and `{fqdn}` your Exchange server. |

### Self-signed certificates on macOS/Linux
The in-app "skip certificate validation" toggle only works on Windows (a limitation of the underlying .NET LDAP library on other platforms). On macOS/Linux, if you need to connect to a lab DC with a self-signed certificate, set the following in your shell **before** launching hybrID (it must be set before the process starts — an in-app setting can't apply it after the fact):

```bash
export LDAPTLS_REQCERT=never
```

Never do this against a production domain controller.

## Signing In
hybrID connects to two separate systems, each with its own sign-in. The **Active Directory** and **Entra ID** badges in the title bar show the state of each; click one for details and actions.

- **Active Directory:** On your first search each session, you'll be asked for a username and password. They're used only for that LDAP connection and are never written to disk. If sign-in fails, the dialog stays open so you can retry. Choose **Skip** to search Entra ID only for the rest of the session; the results then say that Active Directory was skipped, and **Sign in** on that banner (or the title-bar badge) brings it back.
- **Microsoft Graph:** On your first search, a dialog shows a sign-in code. **Copy code and open browser** copies it and opens `microsoft.com/devicelogin`; sign in there with your administrative account. You can **Hide** the dialog while you sign in; the search finishes when sign-in does, and the **Entra ID** badge can show the code again. After that, hybrID caches the token in your OS's secure storage (Windows Credential Manager, macOS Keychain, or the Linux Secret Service), so you won't be asked again until the token expires.

## Searching for an Object
You can search using any of the following formats:
* **sAMAccountName:** (e.g., `jsmith`)
* **UserPrincipalName (UPN):** (e.g., `jsmith@domain.com`)
* **ObjectGUID:** (e.g., `12345678-1234-5678-1234-567812345678`)

Type the identity into the search box and press **Enter** (or click **Locate**). Pasted values are tidied up for you: quotes, `mailto:`, and a `DOMAIN\` prefix are removed. If more than one object matches, pick the one you meant from the list.

## Interpreting the Results
The result leads with two cards: **Manage identity in** (name, password, group membership) and **Manage mailbox in** (email addresses, aliases, mailbox features). Each card names the system and, where there's a web portal, has a button that opens the object there. Below them, a **Guidance** note explains the routing, and detail cards show identity attributes, account status, mail addresses, licenses, or group details.

Under the name, a line shows whether **Active Directory** and **Entra ID** were each searched. If one was skipped, unreachable, or returned an error, a banner says so, and the verdict says what it can't confirm instead of guessing.

![](/assets/screenshot-main-postsearch.png)

### Scenario A: Fully On-Premises
* **Manage identity in:** Active Directory (no web portal: use Active Directory Users and Computers or PowerShell; **Copy DN** helps)
* **Manage mailbox in:** Exchange Server, with **Open on-prem ECP**

### Scenario B: Hybrid / Synced (Most Common)
* **Manage identity in:** Active Directory, with a **View in Entra ID** link
* **Manage mailbox in:** Exchange Server (tagged *Remote mailbox* when the mailbox lives in Exchange Online), with **Open on-prem ECP**
* **Action:** Because the object syncs to the cloud, you **must** manage proxy addresses and email aliases on-premises. Changes made directly in Microsoft 365 are overwritten by the next sync.

### Scenario C: Cloud-Only
* **Manage identity in:** Entra ID, with **Open in Entra admin center**
* **Manage mailbox in:** Exchange Online, with **Open Exchange admin center**

## Keyboard Shortcuts
On macOS, use ⌘ in place of Ctrl.

| Shortcut | Action |
|---|---|
| Ctrl+L | Focus the search box |
| Enter | Locate |
| Esc | Clear the search |
| Ctrl+Shift+C | Copy a plain-text summary for your ticket |
| Ctrl+1 / Ctrl+2 | Open the identity / mailbox portal |
| F5 | Search again |
| Ctrl+, | Settings |
