# Windows public beta checks

Checked on 25 October 2026. These results describe observed checks, not a guarantee
that every PC or live account will work.

## Automated Windows validation

[GitHub Actions run 40194068838](https://github.com/whistle4it/GlucoGeek-Windows/actions/runs/40194068838)
passed on a disposable Windows runner:

- All 55 offline application checks, including DPAPI credential protection and both providers' protocol fixtures.
- Full fetched Git-history scan with Gitleaks 8.30.1; no secrets detected.
- Installer build with bundled runtime legal notices.
- Fresh installation, Start menu/desktop shortcuts and installed executable.
- Execution of the registered background startup command.
- Upgrade from the actual v0.3.1 Librewin release installer.
- Removal of the old executable, old startup registry value and duplicate Startup shortcut.
- Preservation of a synthetic saved profile and decryptable DPAPI-protected credential.
- Uninstall, reinstall, and preservation of saved settings.

The final build also passed [run 40194478837](https://github.com/whistle4it/GlucoGeek-Windows/actions/runs/40194478837), including the new publish-payload privacy checks.

The interactive installer was then checked and completed on the maintainer’s
Windows 11 PC. GlucoGeek 0.4.1 launched from the installed folder, the old named
executable in that folder was removed, licence files were present, and the
existing settings file was byte-for-byte unchanged. The desktop app displayed
Dexcom G7 data without stale or refresh-failure indicators; no personal readings
were saved in this report. Live LibreLinkUp was not retested.

The first real reboot exposed an older portable Librewin startup entry outside
of the installed folder. That PC's effective startup entry was corrected and the
old portable executable was disabled. After a further reboot on 25 October 2026,
the maintainer confirmed successful automatic startup. Process inspection showed
only the installed GlucoGeek 0.4.1 executable running with --background after boot;
Windows' effective startup list pointed to that same executable. This verifies
startup on the repaired PC; the automated installer upgrade checks above cover
the standard installed upgrade path, not every manually configured portable copy.

## Repository and payload review

Source and historical file snapshots were retrieved and scanned locally, and the
runner scanned full Git history. The final pre-publication recheck covered 12
commits and 88 unique historical file snapshots before the workflow-only update.
No real credentials, saved account settings or personal glucose readings were
found. Test fixtures use synthetic accounts. Commit authors use GitHub no-reply
addresses. Committed screenshots show clearly labelled demo readings, and PNG
metadata contains no account information.

The original v0.3.0 portable ZIP contained a developer user-directory path in its
compiled debug metadata. Before publication, the v0.3.0, v0.3.1 and v0.4.0 downloads
were rebuilt with generic compiler paths. The application C# source and version
were preserved; project/upstream/runtime notices were included, and the portable
ZIP omits debug symbols. Release notes identify the source revisions and new
checksums. These are privacy rebuilds, not the original binary artifacts.

All three rebuilt applications passed 55 offline checks and tray launch/shutdown
smoke checks. Their publish payloads and the rebuilt portable ZIP contained no
Windows user-directory paths or matches to the maintainer's saved account fields;
secret scans also passed. GitHub's upload digests matched the local checksums.
The seven original release assets were removed before the repository became public;
source tags and commit history were not rewritten. The current v0.4.1 installer and
its checksum are unchanged.

[Run 40226888882](https://github.com/whistle4it/GlucoGeek-Windows/actions/runs/40226888882)
passed with the privacy-rebuilt v0.3.1 installer as its legacy upgrade fixture,
including fresh install, startup, upgrade, protected-credential preservation,
uninstall and reinstall.

## Licensing

The owner confirmed permission to release the inherited code and app icon under
MIT. LICENSE and THIRD_PARTY_NOTICES.md retain project and upstream notices.
The installer also includes legal notices from its exact bundled .NET runtime
packages.

## Remaining limits

- The installer is unsigned. Windows may warn about its publisher. A self-signed
  certificate would not remove that warning on other people's PCs.
- Offline tests do not establish live LibreLinkUp or Dexcom compatibility.
- The integrations are unofficial and can change without notice.
- Only the latest beta is maintained. Older downloads are historical builds.
- The owner approved publication after the privacy review; this Windows repository
  became public on 25 October 2026. The separate macOS repository was not changed.

See the latest Windows workflow run for validation of subsequent commits.
