# Librewin

Project: [whistle4it/Librewin](https://github.com/whistle4it/Librewin). Version 0.2.0.

A Windows tray port of [whistle4it/libremac](https://github.com/whistle4it/libremac), based on source commit `2c4f81b4b2678f7d963932ec150b80acf50e86bd`.

## Run

1. Install the Microsoft .NET 9 **Desktop Runtime**, x64, if it is not already present.
2. Extract the release ZIP into a permanent folder and run `Librewin.exe`.
3. In Settings, enter your **LibreLinkUp follower account** email and password. If you follow multiple people, enter the exact full patient name.
4. Click **Save & connect**. Credentials stay on this PC, protected with Windows DPAPI for the current user. The app sends them only to LibreLinkUp for sign-in.

Click the tray arrow to open the chart. Closing the window keeps polling in the tray. Right-click the tray icon to refresh, open settings, or quit. Windows may initially hide the icon in the tray overflow. Pin it using Windows taskbar settings if desired.

## Features

- Large coloured trend arrow in the tray; exact glucose, trend and unit in the tooltip and main window.
- Three-hour chart with shaded target range; gaps longer than 20 minutes are not joined.
- Reading age, delta, average, percentage of available readings in range, and sensor age.
- Automatic units, mg/dL, or mmol/L; 1, 2, 5, or 10-minute refresh intervals.
- Out-of-range colours, stale indication after 11 minutes, and visible refresh failures.
- Optional launch at Windows sign-in (per-user registry entry; no administrator rights).
- Protected password storage, sign-out, bounded regional redirects, and one reauthentication attempt on HTTP 401.

The tray shows a large arrow: green in range, red low, amber high. Hover for the exact glucose value and units. A grey exclamation mark indicates a failed refresh; a grey clock indicates stale data; a question mark indicates unavailable trend or readings. The tray always uses colour and trend; the Window trend and Window colours settings control the main window. The main window shows these states explicitly. No alarms, insulin recommendations, or treatment calculations are implemented.

## Build and check

Requires Windows and the .NET 9 SDK, including its Windows Desktop targeting pack. No third-party NuGet packages are required.

```powershell
.\build.ps1 -Test
```

The application is written in C# / Windows Forms. `dist` contains the framework-dependent release. Keep all files in that directory together. To produce a self-contained x64 build on a machine with package download access:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o dist-self-contained
```

`--self-test <absolute-report-path>` runs offline fixture tests, including DPAPI, regional authentication, patient selection, conversions, timestamps, history, and HTTP 401 recovery. It does not contact LibreLinkUp or change settings.

`--preview <absolute-png-path>` renders a demo-data dashboard to an image and exits, without network access or saving settings.

## Settings and troubleshooting

Settings live in `%LOCALAPPDATA%\Librewin\settings.json`. The email and display preferences are plain text; the password is encrypted for the current Windows user. Session tokens and readings are held in memory and are not persisted. Sign out removes saved credentials. Launch at login points to the current executable path: disable it before moving the application, then enable it again afterward.

API version, product and User-Agent can be edited in Settings to match upstream LibreLinkUp changes without recompiling. Defaults are preserved from LibreMac. Regional endpoints must use HTTPS on `libreview.io` or its subdomains. Unknown units or invalid timestamps are rejected, and patient selection never silently falls back to another person. A sensor's rated lifetime is not assumed.

This is an initial Windows port. Offline tests do not establish compatibility with a live account. Verify sign-in, patient identity, units and timestamps against the official LibreLinkUp app before relying on the display. The release is unsigned.

## Attribution and distribution

Derived from the LibreMac repository above. Its API implementation credits [DiaKEM/libre-link-up-api-client](https://github.com/DiaKEM/libre-link-up-api-client). The inspected LibreMac revision includes no LICENSE file; this port does not invent or grant a license for that source. Confirm the original source's licensing before public redistribution.

LibreLinkUp is unofficially accessed through an undocumented API and can change without notice. Librewin is not affiliated with Abbott and is not a medical device. It is a companion display, not a basis for treatment decisions. Use the official app or meter for those decisions. Data depends on the sensor user's phone uploading and may lag the sensor.
