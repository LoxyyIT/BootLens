# Local validation report

Date: 2026-09-20

```text
Build: PASS
Tests: 10 passed / 0 failed
Localization EN: 100%
Localization IT: 100%
Localization ES: 100%
Localization FR: 100%
Missing translations: 0
Portable build: PASS
Installer: FAIL - Inno Setup compiler is not installed locally
Community API local deployment: NOT PART OF CURRENT PRODUCT - Community temporarily disabled
Community Docker deployment: NOT RUN - Community is disabled and Docker is not installed locally
Online publication: NOT RUN by design
```

## Known limitations

- The optional agent currently persists a scan; ETW-based boot timing and the 120-second measurement window are not complete.
- Advanced persistence scanners for drivers, Winlogon, Shell and WMI are planned and are not presented as detected data when they are not scanned.
- Undo Center and Snapshot restore storage exist, but the complete restore UI is still a roadmap item.
- Delay rules and profiles are not enabled yet.
- Community functions are temporarily disabled in the desktop app; the isolated API and client contract are retained only as future scaffolding. Community Score is not implemented and is explicitly marked `Coming soon…`; it must not be interpreted as a security or malware verdict.
- Code signing, multi-version Windows E2E and installer verification remain release prerequisites.
- NuGet audit reports `SQLitePCLRaw.lib.e_sqlite3 2.1.11` as a High transitive advisory from `Microsoft.Data.Sqlite 10.0.0`; the stable package set available locally did not provide a fixed replacement. This must be resolved before public release.

## Manual data still required

- Replace `[YEAR] [COPYRIGHT HOLDER]` in `LICENSE`.
- Decide the final semantic version and code-signing certificate.
- Provide a VirusTotal API key only if that optional feature is later implemented; it is currently not required and no secret is committed.
- Do not operate a Community endpoint until the feature is re-enabled after privacy, retention, TLS, abuse protection and backup policies are approved.
