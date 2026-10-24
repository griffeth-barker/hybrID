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
  - **Modern UI:** Built on Avalonia, featuring a dark/light/system theme, hover tooltips, and a dynamic status bar.
  - **Frictionless Authentication:** Device-code sign-in to Microsoft Graph, with tokens cached via each OS's native secure storage to prevent repetitive login prompts.

To view the roadmap/potential future features, check out the [roadmap](/docs/roadmap.md)!


## Prerequisites
**To run hybrID:** none. Download the published executable for your OS and run it.

**To build hybrID from source:** the [.NET SDK](https://dotnet.microsoft.com/download) (10 or later).

## Getting Started
1.  Download or build the executable for your platform (see [build](/docs/build.md)).
2.  Launch it.
3.  On first launch, open **Settings** and configure your On-Premises Exchange FQDN and Active Directory LDAP server.
4.  Enter a **sAMAccountName**, **UserPrincipalName**, or **ObjectGUID** into the search bar and click **Locate**. The first search against AD or Graph will prompt you to sign in.

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
│       ├── Assets/      # Static app resources (icons, images)
│       ├── Models/      # Plain data types (AppConfig, lookup results)
│       ├── Services/    # Business logic (LDAP/Graph queries, theming, config)
│       ├── Themes/      # Avalonia theme resource dictionaries
│       └── Views/       # Windows/dialogs (AXAML + code-behind)
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
