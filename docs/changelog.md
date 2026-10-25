# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [0.2.0] - 2026-09-22

### Changed
  - **Full Cross-Platform Rewrite:** hybrID is now a C#/.NET 10 [Avalonia](https://avaloniaui.net/) application and runs natively on **Windows, macOS, and Linux**. The PowerShell 5.1/WPF, Windows-only implementation has been fully replaced.
  - **Active Directory Queries:** Moved from the RSAT `ActiveDirectory` PowerShell module (`Get-ADObject`) to direct LDAP queries via `System.DirectoryServices.Protocols`. No domain join or RSAT install is required anymore.
  - **Entra ID / Graph Queries:** Moved from the Microsoft Graph PowerShell SDK to the native Microsoft.Graph C# SDK, authenticating via device-code sign-in.
  - **Zero Runtime Prerequisites:** The app is now published as a self-contained, single-file executable per platform. Running it no longer requires PowerShell, RSAT, or the Graph PowerShell modules to be installed.
  - **Configuration Location:** Moved from an in-repo `config.json` (edited in place inside the install directory) to a per-user profile directory (`%APPDATA%\hybrID\config.json` on Windows, `~/Library/Application Support/hybrID/config.json` on macOS, `~/.config/hybrID/config.json` on Linux), seeded from a bundled default on first run.
  - **Live Theme Switching:** Theme changes now apply immediately across all open windows; the old restart-required mechanism is gone.
  - **Breaking Change:** Existing `config.json` files from 0.1.x are not compatible with the new schema (new LDAP server/port/encryption fields, Graph Tenant ID). Reconfigure via the Settings window after upgrading.

## [0.1.1] - 2026-03-06

### Fixed
  - Application icon missing from title bar in child windows ([#1](https://github.com/griffeth-barker/hybrID/issues/1))
    - Updated [contributing](/docs/contributing.md) to require child windows to inherit application icon from the parent window.


## [0.1.0] - 2026-03-06

### Added
  - **Initial Application Architecture:** Established a Model-View-Controller (MVC) directory structure (`public/`, `private/`, `ui/`, `config/`, `assets/`) to cleanly separate PowerShell logic from XAML presentation.
  - **Hybrid Identity Locator:** Core search engine capable of identifying objects via `sAMAccountName`, `UserPrincipalName` (UPN), or `ObjectGUID`.
  - **Dynamic URL Generation:** Programmatically builds direct management links for Entra ID (Users and Groups), Exchange Online, and On-Premises Exchange Admin Center (ECP) based on the located object's properties.
  - **Dynamic Theming:** Implemented a theme switcher (Light, Dark, and System default) utilizing XAML `DynamicResource` tokens for instant visual updates across all application windows.
  - **Configuration State Management:** Added persistent JSON-based settings storage (`config/config.json`) to track user preferences and environment variables like the On-Premises Exchange FQDN.
  - **Interactive Settings Interface:** Built a custom, fully themed WPF settings menu to allow users to update environment variables and visual themes without modifying code.
  - **Seamless Restart Mechanism:** Hidden background process launcher using the `WScript.Shell` COM object to gracefully restart the application when theme changes are applied.
  - **Custom Themed Alerts:** Replaced legacy Windows Forms `MessageBox` popups with completely custom, dynamically themed WPF dialog windows for "Success" and "Restart Required" alerts.
  - **UI/UX Polish:** Using native Windows Segoe MDL2 Assets for crisp vector icons, rounded corners (`CornerRadius`), and interactive hover states across all custom controls (TextBoxes, ComboBoxes, Buttons).
  - **Documentation:** Created standard docs including `README.md`, `CONTRIBUTING.md`, and `docs/Architecture.md`.

[unreleased]:  
[0.2.0]: https://github.com/griffeth-barker/hybrID/releases/tag/v0.2.0  
[0.1.1]: https://github.com/griffeth-barker/hybrID/releases/tag/v0.1.1  
[0.1.0]: https://github.com/griffeth-barker/hybrID/releases/tag/v0.1.0  