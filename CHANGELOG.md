# Changelog

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

## Unreleased

- Fondazione WPF/MVVM .NET 10.
- Scanner offline Registry, Startup folder, servizi e attività pianificate.
- SQLite, primo avvio con consenso, quattro lingue, scoring, change detection ed export.
- Backend Community locale self-hostable con health check e Docker Compose.
