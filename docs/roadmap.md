# hybrID Roadmap

This document outlines the potential features, enhancements, and architectural goals for the **hybrID** application. As a tool built to streamline helpdesk workflows in a complex hybrid environment, the roadmap focuses heavily on speed, accuracy, and user experience.

## Foundation & Core Logic
- [x] **AD & Graph API Integration:** Query On-Premises AD and Entra ID simultaneously.
- [x] **Routing Engine:** Accurately determine identity and mailbox management locations based on hybrid sync rules.
- [x] **Dynamic Deep Linking:** Generate one-click URLs directly to specific user/group blades in Entra ID and Exchange Admin Centers.
- [x] **Modern UI:** Implement a dark-themed WPF GUI with hover tooltips and dynamic status bars.
- [x] **Modular Architecture:** Split the monolithic script into a standard public/private PowerShell module structure.
- [x] **Frictionless Auth:** Cache Microsoft Graph tokens to eliminate repetitive login prompts.

## Short-Term Enhancements
These are quick-win features designed to immediately improve the day-to-day quality of life for helpdesk technicians.
- [x] **Expanded Object Support:** Improve handling for Shared Mailboxes, Equipment/Room Mailboxes, and Microsoft 365 Groups.
- [x] **Graceful Error Logging:** Implement a hidden `logs\` directory that silently records Graph API timeouts or Active Directory connection failures for easier debugging.
- [x] **User Interface Improvements**: Improve UI to accommodate future feature additions.

## Medium-Term Goals
These features will require adding new capabilities to the UI and expanding our module dependencies.
- [x] **Source of Authority (SOA) Transfer**: Transfer user and group source of authority between on-prem and cloud via Microsoft Graph API.
- [ ] **Bulk Search Capabilities:** Add a method of bulk searching.

## Long-Term Vision
Big-picture goals to transition the tool from a helpful script to an enterprise-grade utility.
- [ ] **Pester Testing:** Write a comprehensive suite of unit tests for the logic layer (testing standard user, synced user, cloud-only user, etc.) to ensure future updates don't break the routing logic.
- [ ] **Native .NET Rewrite?** Transition the PowerShell/WPF hybrid into a compiled, standalone C# application for massive performance gains, true multi-threading (no UI freezing during heavy queries), and easier deployment. Not sure if this one will ever happen.
- [ ] **Test Harness**: Comprehensive test harness for automated testing of changes to the utility going forward.

## How to Influence the Roadmap
Do you have a feature request that isn't listed here? Please open an **Issue** in the repository.