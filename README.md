# Librewin

Project: [whistle4it/Librewin](https://github.com/whistle4it/Librewin). Version 0.3.0.

A Windows tray companion for LibreLinkUp and Dexcom G7 (via Dexcom Share), originally a port of [whistle4it/libremac](https://github.com/whistle4it/libremac), based on source commit `2c4f81b4b2678f7d963932ec150b80acf50e86bd`.

## Run

1. Install the Microsoft .NET 9 **Desktop Runtime**, x64, if it is not already present.
2. Extract the release ZIP into a permanent folder and run `Librewin.exe`.
3. In Settings, enter your **LibreLinkUp follower account** email and password. If you follow multiple people, enter the exact full patient name.
4. Click **Save & connect**. Credentials are stored on this PC, protected with Windows DPAPI for the current user. The selected provider receives only its own credentials for sign-in.

Click the tray arrow to open the chart. Closing the window keeps polling in the tray. Right-click the tray icon to refresh, open settings, or quit. Windows may initially hide the icon in the tray overflow. Pin it using Windows taskbar settings if desired.

## Dexcom G7 setup

1. In the official G7 app, enable **Connections > Share** and complete its setup. Keep the G7 app uploading readings to the internet.
2. In Librewin Settings choose **Dexcom G7** as the glucose source.
3. Enter the **G7 wearer's Dexcom username and password**, not a Dexcom Follow account. These are separate from your saved LibreLinkUp credentials.
4. Select your Dexcom account region: **US**, **Outside US** (including UK/Europe), or **Japan**. Librewin does not guess your region or try your credentials against other regions.
5. Choose **mmol/L** or **mg/dL**. For Dexcom, Automatic uses mg/dL because Share does not supply your preferred units.
6. Review the low/high display bounds (entered in mg/dL regardless of display units) and click **Save & connect**.

Dexcom readings use the existing chart, coloured tray arrows, tooltip, and stale/error indicators. Double-up and double-down arrows are preserved. The last three hours are requested; no available readings or malformed readings produce an error. The bounds default to 70–180 mg/dL and affect only Librewin's display; they are not read from or written to the G7 app. Sensor age is omitted because Share does not provide it.

This is an **unofficial Dexcom Share cloud integration**, not direct Bluetooth sensor access or the retrospective Dexcom developer API. It requires internet access and Share data from the wearer's account. No API key or OAuth application registration is required. Existing LibreLinkUp accounts remain configured when switching sources. Invalid credentials pause further authentication attempts until you reconnect in Settings, avoiding repeated automatic sign-in attempts.

Offline fixtures cover authentication, US/Outside US/Japan hosts, session renewal, failure redaction, units, trend mappings, dates, empty/malformed readings and credential separation. **Live G7 account compatibility has not been verified in this build environment.** Windows DPAPI also needs a normal signed-in Windows session for verification.

References: [Dexcom G7 Share setup](https://www.dexcom.com/en-us/m/faqs/how-do-i-share-my-dexcom-g7-glucose-data-with-followers); [pydexcom protocol reference](https://github.com/gagebenne/pydexcom). Librewin implements the protocol in C# without adding pydexcom as a dependency.

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

`--self-test <absolute-report-path>` runs offline fixture tests, including DPAPI, regional authentication, patient selection, conversions, timestamps, history, and HTTP 401 recovery. It does not contact LibreLinkUp or Dexcom or change settings.

`--preview <absolute-png-path>` renders a demo-data dashboard to an image and exits, without network access or saving settings.

## Settings and troubleshooting

Settings live in `%LOCALAPPDATA%\Librewin\settings.json`. Account names and display preferences are plain text; both provider passwords are separately encrypted for the current Windows user. Session tokens and readings are held in memory and are not persisted. Sign out removes saved credentials for the selected source. Launch at login points to the current executable path: disable it before moving the application, then enable it again afterward.

API version, product and User-Agent can be edited in Settings to match upstream LibreLinkUp changes without recompiling. Defaults are preserved from LibreMac. Regional endpoints must use HTTPS on `libreview.io` or its subdomains. Unknown units or invalid timestamps are rejected, and patient selection never silently falls back to another person. A sensor's rated lifetime is not assumed.

This is an initial Windows port. Offline tests do not establish compatibility with a live account. Verify sign-in, patient identity, units and timestamps against the official LibreLinkUp app before relying on the display. The release is unsigned.

## Attribution and distribution

Derived from the LibreMac repository above. Its API implementation credits [DiaKEM/libre-link-up-api-client](https://github.com/DiaKEM/libre-link-up-api-client). The inspected LibreMac revision includes no LICENSE file; this port does not invent or grant a license for that source. Confirm the original source's licensing before public redistribution.

LibreLinkUp is unofficially accessed through an undocumented API and can change without notice. Librewin is not affiliated with Abbott or Dexcom and is not a medical device. It is a companion display, not a basis for treatment decisions. Use the official app or meter for those decisions. Data depends on the sensor user's phone uploading and may lag the sensor.
