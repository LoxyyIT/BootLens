# Changelog

## 0.2.6 - 2026-09-23

- Startup measurements now read the real Windows boot duration from Diagnostics-Performance event 100 and stay unavailable when the event is missing.
- Startup details distinguish unsigned files from invalid or unavailable Authenticode verification and show resolved file and argument information.
- Added missing-file and duplicate-path filters and optional 15-minute change checks while the app is open.
- Added WMI startup-command discovery, a source-coverage page and local Autoruns CSV comparison by category and executable path.
- Added SQLite migration support for the richer signature state and clarified current Windows compatibility limits.
- Refined the main workspace with denser entry rows, grouped filters, clearer search and sort controls, and an explicit empty selection state.
- Updated the native SQLite bundle to 2.1.13 and removed the warning suppression for its previous vulnerable version.

## 0.2.5 - 2026-09-20

- Signature status now uses Windows Authenticode verification instead of always reporting files as unsigned.
- The signer certificate is used when available to identify the publisher.
- Access-denied registry surfaces no longer stop the rest of the startup scan.

## 0.2.4 - 2026-09-20

- The change log now includes the entry name, action type, mechanism, publisher, source, state transition, path, command, result and timestamp.
- Manual enable and disable operations are recorded immediately after verification.
- Existing change records remain readable through a local database migration.

## 0.2.3 - 2026-09-20

- Protected startup entries are now locked before an action reaches Windows.
- The action label explains why a protected entry cannot be changed.
- The modifier keeps the same protection check as a second safety boundary.

## 0.2.2 - 2026-09-20

- Re-enabling Registry and Startup folder entries no longer depends only on a BootLens backup.
- Disabled entries can be restored from their saved command or `.bootlens-disabled` file when the backup record is unavailable.
- Restore failures now explain when the disabled entry is missing required information.

## 0.2.1 - 2026-09-20

- Startup rows update immediately after a verified change without forcing a full scan.
- Snapshot entries are loaded correctly and can be restored from the Snapshots page.
- Service access-denied responses now explain when Windows protects the service.
- Snapshot wording and actions are clearer in the interface.

## 0.2.0 - 2026-09-20

- Startup changes update immediately in the list and are verified again after the refresh.
- Driver entries can use the same guarded service controls as services.
- Registry changes keep their 32-bit or 64-bit view, so the right entry is modified.
- Scheduled-task scanning avoids one extra process query per task.
- Repeated scans reuse unchanged file inspections to reduce waiting time.
- The Windows app and installer request administrator rights by default.

## 0.1.9 - 2026-09-20

- First public source release with the WPF app, local scanner, reports, snapshots and startup change confirmation.
- Portable Windows x64 package published separately on GitHub Releases.
- Community functions remain disabled and are marked Coming soon.
