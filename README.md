![](/assets/hybrID-logo-cropped.png)

# hybrID
A tool for knowing where to manage identity objects in hybrid Microsoft environments.

In environments utilizing On-Premises Active Directory synced to Entra ID (Azure AD) alongside a Hybrid Exchange deployment, technicians often waste time checking multiple portals to figure out where an object's source of authority lies. **hybrID** takes an identity (UPN, sAMAccountName, or GUID), queries both On-Prem AD and the Microsoft Graph API, and indicates where to manage the identity its mail functionality, if any.

## Features
  - **Single Pane of Glass:** Query an identity once to see its status across Active Directory, Entra ID, and Exchange Server/Online.
  - **Inbuilt Rules:** Automatically determines if a mailbox is On-Premises, Exchange Online (EXO), or an EXO mailbox managed via On-Premises Remote Mailbox rules.
  - **Deep Linking:** Generates dynamic, clickable URLs that drop the technician directly into the exact user or group profile blade in the Entra ID or Exchange Admin Centers.
  - **Modern UI:** Built on WPF featuring a dark theme, hover tooltips, and a dynamic status bar.
  - **Frictionless Authentication:** Silently caches Microsoft Graph API tokens to prevent repetitive login prompts.


## Prerequisites
To run this application, the technician's workstation must have the following installed:

1.  **Windows PowerShell 5.1** (Standard on Windows 10/11).
2.  **RSAT: Active Directory Domain Services and Lightweight Directory Services Tools** (Provides the ActiveDirectory PowerShell module).
3.  **Microsoft Graph PowerShell SDK** (Specifically the Authentication, Users, and Groups sub-modules).

## Getting Started
1.  Clone or download the repository to your local machine.
2.  Launch Windows PowerShell.
3.  Set your present working directory to the module root directory.
4.  Execute `hybrID.ps1` from your terminal.
5.  On first launch, you may be prompted to authenticate with Microsoft Graph. Sign in with your administrative credentials.
6.  Enter a **sAMAccountName**, **UserPrincipalName**, or **ObjectGUID** into the search bar and click **Locate**.

For more information, visit the [docs](/docs]).

## Screenshots
### Main Window (Pre-Search)
![](/assets/screenshot-main-ui.png)

For more screenshots, see [screenshots](/docs/screenshots.md).

## Repository Structure
This application is built using a modular PowerShell structure, separating the presentation layer (XAML) from the logic layer (private functions) and the execution layer. There is a public controller script to invoke the application.

```text
hybrID/
├── assets/              # GitHub repository assets (readmes, badges)
├── build/               # Build scripts (e.g., PS2EXE wrappers)
├── docs/                # Project documentation
├── hybrID/              # Core Application Directory
│   ├── assets/          # Static app resources (icons, images)
│   ├── config/          # Application state (config.json)
│   ├── private/         # Internal PowerShell logic & helper functions
│   ├── public/          # Main controller script (hybrID.ps1)
│   └── ui/              # XAML presentation layer files
├── tests/               # Pester tests
├── .gitignore
└── README.md
```

## How It Works
Input: The app accepts an identity string.

On-Premises Check: Uses Get-ADObject to search Active Directory. It evaluates targetAddress, msExchHomeServerName, and mail attributes to determine Exchange routing.

Cloud Check: Uses Get-MgUser and Get-MgGroup to search Entra ID.

Evaluation:  
  - If found only in AD: Flags as On-Premises only.
  - If found only in Graph: Flags as Cloud-only.
  - If found in both: Flags as Hybrid/Synced and provides appropriate management paths based on your organization's specific sync rules.