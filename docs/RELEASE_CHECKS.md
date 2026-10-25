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

Executing the stored startup command is not an actual reboot or sign-out/sign-in.
That final real sign-in check remains pending with the maintainer.

## Repository and payload review

All 8 pre-preparation commits and 69 unique historical files were retrieved and
scanned locally as an additional check. The runner then scanned real Git history.
Committed screenshots use clearly labelled demo readings; PNG metadata contains
no account information. No saved account settings or credentials are tracked.

The local publish payload and historical portable ZIP (277 files, approximately
120 MB) were scanned. No credential patterns were found. Historical builds can contain compiler build-path metadata;
new builds map the developer path to /_/GlucoGeek. No history was rewritten.

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
- This repository remains private until the owner explicitly chooses to publish it.

See the latest Windows workflow run for validation of subsequent commits.
