![](/assets/hybrID-logo-cropped.png)

# hybrID
A tool for knowing where to manage identity objects in hybrid Microsoft environments.

In environments utilizing On-Premises Active Directory synced to Entra ID (Azure AD) alongside a Hybrid Exchange deployment, technicians often waste time checking multiple portals to figure out where an object's source of authority lies. **hybrID** takes an identity (UPN, sAMAccountName, or GUID), queries both On-Prem AD and the Microsoft Graph API, and indicates where to manage the identity and its mail functionality, if any.

**hybrID runs on Windows, macOS, and Linux.** It ships as a single self-contained executable per platform — no PowerShell, RSAT, or Graph module install required to run it.

## Features
  - **Cross-Platform:** One codebase, native builds for Windows, macOS, and Linux.
  - **Single Pane of Glass:** Query an identity once to see its status across Active Directory, Entra ID, and Exchange Server/Online.
  - **Inbuilt Rules:** Automatically determines if a mailbox is On-Premises, Exchange Online (EXO), or an EXO mailbox managed via On-Premises Remote Mailbox rules.
  - **Deep Linking:** Generates dynamic, clickable URLs that drop the technician directly into the exact user or group profile blade in the Entra ID or Exchange Admin Centers.
  - **Verdict First:** Two cards answer "where do I manage this identity, and its mailbox?" with a button straight into the right portal, plus account status, sync state, proxy addresses, and licenses below.
  - **Honest About Coverage:** Every result says whether Active Directory and Entra ID were actually searched. If one was skipped or failed, hybrID says so instead of guessing, so a synced user never silently looks cloud-only.
  - **Copy Summary for Tickets:** One click (or Ctrl+Shift+C) copies a plain-text summary of the result to paste into your ticketing system.
  - **Modern, Keyboard-First UI:** A Windows 11-style interface on Avalonia with Light/Dark/System themes, keyboard shortcuts for every action, and in-window settings.
  - **Frictionless Authentication:** Device-code sign-in to Microsoft Graph, with tokens cached via each OS's native secure storage to prevent repetitive login prompts.

To view the roadmap/potential future features, check out the [roadmap](/docs/roadmap.md)!


## Prerequisites
**To run hybrID:** none. Download the published executable for your OS and run it.

**To build hybrID from source:** the [.NET SDK](https://dotnet.microsoft.com/download) (10 or later).

## Getting Started
1.  Download or build the executable for your platform (see [build](/docs/build.md)).
2.  Launch it.
3.  On first launch, open **Settings** (bottom of the left navigation) and configure your Active Directory domain controller and On-Premises Exchange server.
4.  Enter a **sAMAccountName**, **UserPrincipalName**, or **ObjectGUID** into the search box and press **Enter** (or click **Locate**). The first search signs you in to Active Directory and Microsoft Graph.

For more information, visit the [docs](/docs).

## Screenshots
### Main Window (Pre-Search)
![](/assets/screenshot-main-presearch.png)

For more screenshots, see [screenshots](/docs/screenshots.md).

## Repository Structure
hybrID is a C#/.NET Avalonia application, separating presentation (Views), business logic (Services), and data shapes (Models).

```text
hybrID/
├── assets/              # GitHub repository assets (readmes, badges)
├── build/               # build.ps1 (dotnet publish wrapper) and version.json
├── docs/                # Project documentation
├── src/
│   └── HybridID/        # Application source
│       ├── Assets/      # Static app resources (icons, fonts)
│       ├── Controls/    # The app's small composite controls
│       ├── Models/      # Plain data types (AppConfig, lookup results)
│       ├── Services/    # Business logic (LDAP/Graph queries, theming, config)
│       ├── Themes/      # Design tokens, typography, styles, control themes
│       └── Views/       # Main window, pages, and dialogs (AXAML + code-behind)
├── tools/               # Dev-only tooling (headless screenshot renderer)
├── hybrID.slnx           # Solution file
├── .gitignore
└── README.md
```

## How It Works
Input: The app accepts an identity string.

On-Premises Check: Queries Active Directory directly over LDAP (LDAPS by default) — no domain join or RSAT required. It evaluates `targetAddress`, `msExchHomeServerName`, and `mail` attributes to determine Exchange routing.

Cloud Check: Uses the Microsoft Graph SDK to query Entra ID for the user or group.

Evaluation:
  - If found only in AD: Flags as On-Premises only.
  - If found only in Graph: Flags as Cloud-only.
  - If found in both: Flags as Hybrid/Synced and provides appropriate management paths based on your organization's specific sync rules.
  - If either directory couldn't be searched (skipped, unreachable, or an error), the verdict says so rather than assuming the object isn't there.
