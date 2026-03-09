# Security Policy

## Project Disclaimer

hybrID is an open-source side project maintained on a best-effort basis. Security fixes and response timelines are goals, not guarantees, and are delivered as maintainer time allows.

By using this project, you acknowledge that your organization is responsible for reviewing, testing, and approving any code, scripts, and configuration changes before use in your environment.

## Supported Versions

Security fixes are currently provided for the latest release on the `main` branch.

| Version | Supported |
| --- | --- |
| Latest release | :white_check_mark: |
| Older releases | :x: |

## Reporting a Vulnerability

Please do **not** report security vulnerabilities in public GitHub issues.

Instead, report vulnerabilities through one of the following methods:

1. Use GitHub's private vulnerability reporting for this repository (preferred), or
2. Contact the maintainer directly and include "Security Report" in the subject.

When reporting, include:

- A clear description of the issue
- Affected version/commit
- Reproduction steps or proof of concept
- Potential impact
- Suggested mitigation (if known)

## Response Expectations

- Initial acknowledgment target: within 5 business days
- Triage and impact assessment: as soon as practical
- Fix timeline: based on severity and complexity

## Scope Notes for hybrID

Given this project's hybrid identity/admin context:

- Never include tenant secrets, tokens, passwords, or private identifiers in reports.
- Redact screenshots/logs before sharing.
- Treat Graph permissions and management URL handling as security-sensitive areas.
