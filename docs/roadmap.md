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
- [ ] **"Copy to Clipboard" Button:** Add a quick-copy button that formats the search results into a clean text block, making it easy to paste into a ticketing system (e.g., ServiceNow, Jira).
- [ ] **Expanded Object Support:** Improve handling for Shared Mailboxes, Equipment/Room Mailboxes, and Microsoft 365 Groups.
- [ ] **Graceful Error Logging:** Implement a hidden `logs\` directory that silently records Graph API timeouts or Active Directory connection failures for easier debugging.

## Medium-Term Goals
These features will require adding new capabilities to the UI and expanding our module dependencies.
- [ ] **M365 License Detection:** Leverage Graph API to display exactly which M365 licenses are currently assigned to the located user.
- [ ] **Last Sign-In Data:** Surface the user's `LastSignInDateTime` from Entra ID directly in the notes field to help techs instantly spot inactive or disabled accounts.
- [ ] **Bulk Search Capabilities:** Add a method of bulk searching.

## Long-Term Vision
Big-picture goals to transition the tool from a helpful script to an enterprise-grade utility.
- [ ] **Pester Testing:** Write a comprehensive suite of unit tests for the logic layer (testing standard user, synced user, cloud-only user, etc.) to ensure future updates don't break the routing logic.
- [ ] **Intune Integration:** Add a check to see if the user has a Primary Device registered in Intune, providing a deep link to the device blade.
- [ ] **Native .NET Rewrite?** Transition the PowerShell/WPF hybrid into a compiled, standalone C# application for massive performance gains, true multi-threading (no UI freezing during heavy queries), and easier deployment. Not sure if this one will ever happen.

## How to Influence the Roadmap
Do you have a feature request that isn't listed here? Please open an **Issue** in the repository.