# hybrID Roadmap

This document outlines the potential features, enhancements, and architectural goals for the **hybrID** application. As a tool built to streamline IT workflows in hybrid Microsoft environments, the roadmap focuses heavily on speed, accuracy, and user experience.

## Completed
- [x] **AD & Graph API Integration:** Query On-Premises AD and Entra ID simultaneously.
- [x] **Routing Engine:** Accurately determine identity and mailbox management locations based on hybrid sync rules.
- [x] **Dynamic Deep Linking:** Generate one-click URLs directly to specific user/group blades in Entra ID and Exchange Admin Centers.
- [x] **Modern UI:** Implement a dark-themed WPF GUI with hover tooltips and dynamic status bars.
- [x] **Modular Architecture:** Split the monolithic script into a standard public/private PowerShell module structure.
- [x] **Frictionless Auth:** Use cached Microsoft Graph tokens.

## Active Development
- [ ] **Expanded Object Support:** Improve handling for Shared Mailboxes, Equipment/Room Mailboxes, and Microsoft 365 Groups.
- [ ] **Source of Authority (SOA) Transfer:** Transfer user and group source of authority between on-prem and cloud via Microsoft Graph API.
- [ ] **Graceful Error Logging:** Implement a hidden `logs\` directory that silently records Graph API timeouts or Active Directory connection failures for easier debugging.
- [ ] **User Interface Improvements:** Improve UI to accommodate future feature additions.

## Backlog
- [ ] **Bulk Search Capabilities:** Add a method of bulk searching.
- [ ] **Mailbox License Visibility (Lightweight):** Display the assigned license that provides the user's mailbox.
- [ ] **Export Object State:** Add export to JSON for complete object state.
- [ ] **Group Comparison View:** Add a Compare Groups experience.
- [ ] **Optional CSV Export:** Add CSV export options for object state.
- [ ] **Sync Error Detection:** Surface synchronization issues for the current object.
- [ ] **Key Attribute Comparison:** Compare common AD and cloud attributes used in troubleshooting.
- [ ] **Proxy Address Conflict Detection:** Identify whether the current object has proxy address conflicts.
- [ ] **Search History:** Add recent search history for faster repeat lookups.

## Quality & Reliability
- [ ] **Pester Testing:** Write a comprehensive suite of unit tests for the logic layer (standard user, synced user, cloud-only user, etc.) to prevent regressions.

## How to Influence the Roadmap
Do you have a feature request that isn't listed here? Please open an **Issue** in the repository.