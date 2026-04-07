# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.1.x   | Yes       |
| < 1.1   | No        |

## Reporting a Vulnerability

**Do not open a public issue for security vulnerabilities.**

Instead, use [GitHub Security Advisories](https://github.com/medy-gribkov/stream-shield/security/advisories/new) to report privately.

### What to include

- Description of the vulnerability
- Steps to reproduce
- Impact assessment (what data could be exposed, what could an attacker do)
- Affected version(s)

### Response timeline

- **72 hours**: Initial acknowledgment
- **7 days**: Triage and severity assessment
- **90 days**: Fix released (critical issues prioritized)

## Scope

The following are considered security vulnerabilities:

- **Data exposure**: Field values visible to screen capture despite protection being active
- **Capture bypass**: Methods to circumvent `WDA_EXCLUDEFROMCAPTURE` protection
- **Privilege escalation**: App performing actions beyond its intended scope
- **Sensitive data leakage**: Field values written to disk, network, clipboard, or logs
- **Registry/file tampering**: Settings file or registry entries used to execute arbitrary code

## Out of Scope

- Social engineering attacks
- Denial of service against the local application
- Issues requiring physical access to the machine
- Vulnerabilities in third-party screen capture tools themselves
- Fields that are not detected as sensitive (feature request, not security issue)

## Security Design

- Zero network calls. Zero telemetry. Zero data collection.
- Field values are read via UI Automation, displayed in a local mirror window, and never persisted.
- Log files record only timestamps and event names, never field values or names.
- Settings file stores only UI preferences and detection keywords.
- Chrome extension uses Manifest V3 with minimal permissions (`activeTab`, `storage`).
- All dependencies are built-in .NET / Win32 APIs. Zero NuGet packages. Zero npm packages.
