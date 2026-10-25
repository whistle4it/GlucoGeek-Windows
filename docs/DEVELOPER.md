# GlucoGeek developer guide

For installation and account setup, see the [README](../README.md).

## Build and test

Requires Windows and the .NET 9 SDK with its Windows Desktop targeting pack. GlucoGeek uses C# / Windows Forms and no third-party NuGet packages.

```powershell
.\build.ps1 -Test
```

The framework-dependent build is written to `dist`. Keep its files together; this build requires the .NET 9 Desktop Runtime x64.

For a self-contained build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o dist-self-contained
```

### Build the installer

Install the .NET 9 SDK and [Inno Setup 6](https://jrsoftware.org/isdl.php), then run:

```powershell
.\build-installer.ps1 -Iscc 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

This publishes a self-contained x64 app and produces the setup executable and SHA-256 file in `installer-output`. The stable application ID lets future installers upgrade the same installation.

The installer uses `%LOCALAPPDATA%\Programs\Librewin`. Settings remain in `%LOCALAPPDATA%\Librewin`. Existing startup registration is migrated to the installed executable. Older portable folders are not deleted. The installer is unsigned. Rebuild bundled runtime dependencies when runtime security updates are released.

The v0.4.0 Windows build, 55 offline checks, installer compilation and tray smoke check passed. Automated fresh install, upgrade, startup-command, uninstall and reinstall checks pass on a disposable Windows runner. See RELEASE_CHECKS.md for the remaining desktop/reboot checks.

### Test and preview modes

- `--self-test <absolute-report-path>` runs offline fixtures without contacting either glucose service or changing settings.
- `--smoke-test <absolute-report-path>` checks tray creation, rendering, the message loop and shutdown.
- `--preview <absolute-png-path>` renders a dashboard using demo data and exits.
- `--preview-settings <absolute-png-path>` renders empty LibreLinkUp settings; add `--dexcom` for Dexcom settings.
- `--preview-tray <absolute-png-path>` renders the tray states.

The README dashboard image uses the app's demo-data renderer with a wider window so its statistics are fully visible. No live account data is used.

## Integration details

LibreLinkUp uses an undocumented API that can change without notice. API version, product and User-Agent remain editable in Settings. Regional endpoints must use HTTPS on `libreview.io` or its subdomains. Unknown units and invalid timestamps are rejected; patient selection does not silently fall back to another person.

Dexcom G7 uses an unofficial Dexcom Share cloud integration, not direct Bluetooth access or the retrospective developer API. It requires the wearer's credentials and Share uploads. No API key or OAuth registration is needed. US, Outside US and Japan account regions are supported. Display bounds default to 70–180 mg/dL and are not imported from or written to Dexcom. Automatic units use mg/dL, and sensor age is unavailable.

Credentials for the two sources are stored separately. Invalid credentials pause automatic sign-in attempts until the user reconnects in Settings. Offline fixtures cover authentication, regional hosts, session renewal, failure redaction, units, trends, dates, malformed/empty readings and credential separation. Offline tests alone do not establish live service compatibility.

References: [Dexcom G7 Share setup](https://www.dexcom.com/en-us/m/faqs/how-do-i-share-my-dexcom-g7-glucose-data-with-followers), [pydexcom protocol reference](https://github.com/gagebenne/pydexcom). GlucoGeek implements the protocol in C# without depending on pydexcom.

## Display and settings

The chart shows three hours of available readings and does not join gaps longer than 20 minutes. Reading age, delta, average and percentage of available readings in range are shown. Sensor age is shown only when supplied by LibreLinkUp. Readings become stale after 11 minutes.

The tray always uses colour and trend. Window trend and Window colours apply to the main window. Refresh intervals are 1, 2, 5 or 10 minutes.

Settings are stored in `%LOCALAPPDATA%\Librewin\settings.json`. Account names and preferences are plain text; provider passwords are separately encrypted with Windows DPAPI. Session tokens and readings are in memory only. Sign out removes the selected source's saved credentials.

## Attribution and distribution

Derived from [GlucoGeek for macOS (formerly LibreMac)](https://github.com/whistle4it/GlucoGeek-macOS), source commit `2c4f81b4b2678f7d963932ec150b80acf50e86bd`. GlucoGeek for macOS's API implementation credits [DiaKEM/libre-link-up-api-client](https://github.com/DiaKEM/libre-link-up-api-client).

The application and installer reuse GlucoGeek for macOS's original `Resources/AppIcon.icns`, converted to `Resources/AppIcon.ico` with 16, 24, 32, 48, 64, 128 and 256 pixel sizes. `Resources/AppIcon.png` is a preview.

The project owner confirmed ownership or permission to release the inherited source and icon under MIT. See LICENSE and THIRD_PARTY_NOTICES.md. Repository visibility remains a separate maintainer decision.

## Rename compatibility

The app is GlucoGeek; the repositories use GlucoGeek-Windows and GlucoGeek-macOS. Windows keeps the original installer AppId, installation/profile folders and single-instance lock so the rename upgrades the existing app. Startup settings recognize the old Librewin entry; the installer replaces it with GlucoGeek and removes old named shortcuts and executable files. Automated upgrade checks pass; real reboot verification remains separate.
