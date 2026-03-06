# Getting Started
**hybrID** is a tool designed to tell you where an identity (user or group) lives and which portal you need to use to manage it.

## Launching the App
1.  Launch Windows PowerShell.
2.  Set your present working directory to the module root directory.
3.  Execute `hybrID.ps1` from your terminal.
4.  On first launch, you may be prompted to authenticate with Microsoft Graph. Sign in with your administrative credentials.

![](/assets/screenshot-main-ui.png)


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