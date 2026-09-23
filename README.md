<p align="center">
  <img src="logo.png" alt="BootLens logo" width="112">
</p>

<h1 align="center">BootLens</h1>

<p align="center">
  <strong>See what really starts with Windows.</strong><br>
  An open-source Windows startup manager and boot analyzer for understanding, reviewing and carefully changing automatic startup entries.
</p>

<p align="center">
  <a href="https://github.com/LoxyyIT/BootLens">GitHub</a> ·
  <a href="https://loxyyit.github.io/BootLens">Project website</a> ·
  <a href="CONTRIBUTING.md">Contribute</a> ·
  <a href="SECURITY.md">Security</a>
</p>

<p align="center">
  <a href="https://github.com/LoxyyIT/BootLens"><img src="https://img.shields.io/badge/%E2%98%85%20Star%20on%20GitHub-LoxyyIT%2FBootLens-6f42c1?style=for-the-badge&logo=github" alt="Star BootLens on GitHub"></a>
  <a href="https://github.com/LoxyyIT/BootLens/issues/new"><img src="https://img.shields.io/badge/Open%20an%20issue-GitHub-d73a4a?style=for-the-badge&logo=github" alt="Open a BootLens issue"></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows-0b6cff" alt="Windows">
  <img src="https://img.shields.io/badge/UI-WPF-1677ff" alt="WPF">
  <img src="https://img.shields.io/badge/runtime-.NET%2010-512bd4" alt=".NET 10">
  <img src="https://img.shields.io/badge/license-MIT-18a06f" alt="MIT license">
  <img src="https://img.shields.io/badge/core-local--first-0e7490" alt="Local-first core">
</p>

## Why BootLens

Windows Startup Apps is useful, but it is only one user-facing view of automatic startup. Windows can load software through registry values, startup folders, scheduled tasks, services and lower-level system surfaces. BootLens collects those sources into one local interface and adds the context that is usually missing: the mechanism, source path, process, publisher, state, trust signals and the reason the entry is present.

BootLens is deliberately careful. Recommendations are advisory, changes require explicit confirmation by default, protected components stay protected, and the Community functions are temporarily disabled. It does not execute discovered command lines.

## A concrete comparison

On one real test machine, at one point in time:

| Windows Startup Apps | BootLens |
| ---: | ---: |
| **23** visible startup apps | **562** startup-related entries |

This is not a universal benchmark, an average or a promise. The numbers come from one machine and vary with installed software, Windows configuration and the categories being inspected. The difference exists because BootLens aggregates more startup mechanisms than the normal Startup Apps list; it does not mean Windows is incorrect.

## What is available now

### Discover

- Registry Run, RunOnce and related values, including 32-bit and 64-bit views.
- Current-user and all-users Startup folders.
- Automatic Windows services and driver-type services.
- Scheduled Tasks read through schtasks.exe.
- WMI `Win32_StartupCommand` records, with duplicate executable paths already represented by another scanned source suppressed from the result list.
- Advanced registry surfaces currently scanned by the Windows scanner: Boot Execute, Winlogon, AppInit, Known DLLs, Explorer shell hooks, Codecs/Drivers32, Image File Execution Options, Winsock providers, print monitors, LSA providers, network providers and Shell Load values.

### Understand

- Search across name, process, publisher, mechanism, state, path, command, source, trigger, version and identifier.
- Detail view with resolved file path, launch arguments, source, publisher metadata, Authenticode status (valid, unsigned, invalid or unavailable), hash, version, trigger and trust signals when available.
- Simple and advanced views, including quick filters for missing files, exact duplicate launch paths, unsigned files, invalid signatures and Microsoft items.
- “Why does this start?” context based on the detected mechanism and source.
- Coverage page with per-source counts, implemented and unsupported Autoruns categories, and a CSV comparison grouped by category and normalized executable path.

### Measure and track

- Local startup efficiency scoring and per-entry estimates when enough metadata exists.
- Real boot duration is read from Windows Diagnostics-Performance Operational event 100 when that event and its `BootTime` field are available. It stays “not measured” when Windows has no record.
- Measurements include the startup-configuration fingerprint seen by BootLens and can be compared only with prior measurements carrying the same fingerprint; this is observational and does not claim causation.
- Scan-to-scan change detection, optional 15-minute monitoring while BootLens remains open, snapshots and JSON/CSV/HTML export.

### Control and recover

- Explicit confirmation before enabling or disabling startup items, with a stronger warning for Windows-managed entries.
- Reversible operations for eligible Registry Run/RunOnce entries, Startup-folder entries, Scheduled Tasks and services; protected or unsupported mechanisms remain read-only.
- Backup and undo records where the operation supports them.
- The app requests administrator elevation at launch because some Windows startup sources require it.

### Community

- **Coming soon…** The Community functions are not available in the current desktop app and are temporarily disabled.
- The Community client and API code remain isolated as a future foundation, but they are not part of the supported workflow and must not be treated as active product functionality.
- **Community Score — Coming soon…** The aggregate popularity signal is not implemented and will never be a malware or security verdict. Community popularity is not safety.

## Scanners and modification boundaries

| Source | Detected | Modifiable | Notes |
| --- | :---: | :---: | --- |
| Registry Run / RunOnce | Yes | Limited | Reversible operations for eligible entries; protected items are guarded. |
| Startup folders | Yes | Limited | Entries are moved to a reversible .bootlens-disabled backup. |
| Scheduled Tasks | Yes | Limited | Uses Task Scheduler commands and verifies the resulting state. |
| Services | Yes | Limited | Changes service start configuration and require elevation. |
| Driver-type services | Yes | No | Detected as a separate mechanism; simple actions do not modify them. |
| Winlogon / Shell / AppInit | Yes | No | Advanced registry surfaces are read-only in the current modifier. |
| Other advanced registry surfaces | Yes | No | Boot Execute, Known DLLs, Explorer hooks, Codecs, Image hijacks, Winsock, print monitors, LSA and network providers are inspected read-only. |
| WMI `Win32_StartupCommand` | Yes | No | Read through the local CIM provider; matching paths already returned by another source are counted in WMI diagnostics and not duplicated in the main list. |
| Internet Explorer, Print Processors, Boot Verification and Sidebar Gadgets | No | No | Listed as unsupported in the Coverage page. |

The Autoruns comparison imports its CSV locally. Matching is based on normalized executable paths inside mapped categories, not on a claim that both tools use identical collection rules. Compare exports from the same PC and similar times; a difference is a lead to inspect, not proof that either scan is wrong.

## Scoring is not a verdict

BootLens keeps different signals separate:

- **Startup efficiency score**: a local estimate about startup impact based on local metadata and, where available, measurements. It is not a security rating.
- **Trust signals**: publisher, signature, Microsoft ownership, missing executable and related evidence. Missing evidence is not proof of malware.
- **Community behavior**: not available yet. The Community functions are temporarily disabled and planned for a future release. **Community Score is Coming soon…** and must not be interpreted as a safety, reputation or malware verdict. Community popularity is not security.

## Privacy and safety

- The core app works locally and does not require an account or a cloud service.
- Community functions, telemetry, crash upload and VirusTotal are currently disabled.
- The optional change monitor runs only while the desktop app is open; it is not a Windows background service.
- Multi-version Windows compatibility has not been established by the current local build. The Coverage page shows the Windows version used for the current scan.
- No remote shell, remote desktop or automatic execution of discovered command lines.
- Startup changes can break applications or Windows features. Review the path, publisher, mechanism and impact before changing anything.
- Protected system components are guarded, and the confirmation dialog explains the risk before an eligible change.
- Snapshots, backups and undo records reduce risk where supported; they cannot guarantee a perfect restore.

## Community status

Community is **Coming soon…**. The desktop entry is disabled temporarily, the app does not submit observations, and the Community Score is not available. The isolated client/API code is retained for future development only; do not start or publish the backend as part of the current product. Privacy, retention, authentication, TLS, abuse protection and backup policies must be finalized before Community is enabled.

## Installation and build

A portable Windows x64 package is available from the [GitHub Releases](https://github.com/LoxyyIT/BootLens/releases/latest) page. To build the same package locally with the verified PowerShell script:

    dotnet restore BootLens.sln
    dotnet build BootLens.sln -c Release
    dotnet test BootLens.sln -c Release --no-build
    .\scripts\publish.ps1 -Version 0.2.5

The publish script creates artifacts/BootLens-0.2.5-win-x64.zip. An installer EXE is created when Inno Setup 6 is installed on the build machine. The app targets net10.0-windows, requests administrator rights at launch and the portable publish is self-contained for win-x64.

## Architecture

    BootLens.App                 WPF UI, MVVM, settings and confirmation flows
    BootLens.Core                domain models, analysis, scoring, localization and reports
    BootLens.Windows             Windows scanners, file inspection and startup modifiers
    BootLens.Data                SQLite storage and migrations
    BootLens.Agent               optional agent boundary for future boot measurements
    BootLens.Community.Client    disabled future Community contract/client
    server/                      disabled future Community API scaffold
    tests/                       Core and Windows parsing tests

## Repository map

    src/       application and libraries
    server/    disabled future Community API scaffold
    tests/     automated tests
    docs/      build, architecture, validation and self-hosting notes
    scripts/   build, publish and validation scripts
    deploy/    optional Inno Setup definition
    logo.png   BootLens logo source
    logo.ico   Windows application icon

## Screenshots

No application screenshots are committed yet. The public site deliberately does not show fabricated UI captures. Add real captures under docs/assets/images/ when available, ideally:

1. Overview after a real scan.
2. Startup list with filters and search.
3. Selected-entry details and “Why does this start?”.
4. Boot History, Snapshots and future Community screens.

## Roadmap

### Available now

- Local WPF application with four languages: English, Italian, Spanish and French.
- Offline startup discovery, details, filters, sorting, scoring, change tracking and exports.
- Confirmation, protected-item handling and reversible operations on supported mechanisms.
- Community functions disabled temporarily; planned for a future release.

### Planned

- Community Score aggregation and presentation in the app — **Coming soon…**
- Complete Undo Center and Snapshot restore UI.
- Deeper ETW measurements for per-startup-process CPU, disk I/O and memory attribution.
- Broader fixture coverage and verified scan behavior across Windows editions and versions.
- Delay-startup management for compatible applications.
- Code signing, verified installer and wider Windows-version testing.

## Current limitations

- The scanner is Windows-specific and advanced registry surfaces are read-only.
- Boot measurements depend on Windows recording Diagnostics-Performance event 100; BootLens does not fabricate a duration when the event is missing or unreadable.
- Change monitoring checks every 15 minutes only while the app is open; it does not watch continuously after the app closes.
- The Autoruns category comparison depends on a user-imported CSV from a comparable scan and matches executable paths, so category and timing differences can remain.
- Windows versions other than the current development environment have not been verified as compatible.
- There is no official Community endpoint.
- The Community Score is not implemented.
- Inno Setup is optional for local builds and is not installed in the current development environment.
- The current repository license still contains placeholder copyright fields that should be finalized before a formal public release.

## Contributing

Issues, pull requests, translations, application metadata, scanner coverage and reproducible bug reports are welcome. Before opening a pull request, run the Release build, all tests and localization validation. See CONTRIBUTING.md and CODE_OF_CONDUCT.md.

## Security

Do not publish exploitable vulnerability details in public issues. Follow SECURITY.md and include only a minimal reproduction, the version, non-sensitive logs and impact.

## License

BootLens is distributed under the MIT License. See LICENSE. The repository currently keeps the copyright holder fields in that file as placeholders and they should be completed by the maintainer before release.

<p align="center">
  <img src="logo.png" alt="BootLens" width="56"><br>
  <a href="https://github.com/LoxyyIT/BootLens">Star BootLens</a> ·
  <a href="https://github.com/LoxyyIT/BootLens/issues">Report a bug</a> ·
  <a href="CONTRIBUTING.md">Contribute</a>
</p>
