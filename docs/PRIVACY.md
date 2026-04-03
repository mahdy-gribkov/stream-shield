# StreamShield Privacy Policy

**Last updated:** April 2026

## Summary

StreamShield does not collect, transmit, or store any user data. All processing happens entirely on your device.

## Data Collection

StreamShield collects **no data whatsoever**:

- No personal information
- No usage analytics or telemetry
- No crash reports
- No field values or content
- No browsing history or activity

## Network Activity

StreamShield makes **zero network calls**. The application and browser extension operate entirely offline. There are no servers, APIs, endpoints, or external services involved.

## How It Works

- **Desktop app (Windows):** Uses the Windows UI Automation API to detect focused input fields by their metadata (field name, type, automation ID). Field values are displayed locally in a mirror overlay and are never written to disk or transmitted.
- **Browser extension:** Uses DOM observation to detect sensitive input fields by their attributes (type, name, autocomplete, aria-label). A CSS overlay is applied client-side. No data leaves the page.

## Data Storage

- **Settings:** Stored locally in `%APPDATA%\StreamShield\settings.json` (Windows) or browser extension storage. Contains only UI preferences and custom keyword patterns.
- **Field values:** Never stored. Mirror overlays display values in real-time from the OS or DOM and do not persist them.

## Third-Party Services

StreamShield uses no third-party services, SDKs, libraries, or analytics platforms.

## Permissions

### Browser Extension
- `activeTab`: Required to detect input fields on the current page.
- `storage`: Required to save user preferences (enabled state, custom patterns).

No other permissions are requested.

## Open Source

StreamShield is fully open source under the MIT License. The complete source code is available at [github.com/medy-gribkov/stream-shield](https://github.com/medy-gribkov/stream-shield) for independent review.

## Contact

For questions about this privacy policy, open an issue on the [GitHub repository](https://github.com/medy-gribkov/stream-shield/issues).
