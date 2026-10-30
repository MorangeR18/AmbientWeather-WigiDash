# Ambient Weather Station for G.SKILL WigiDash

[![Latest release](https://img.shields.io/github/v/release/MorangeR18/AmbientWeather-WigiDash?display_name=tag&sort=semver)](https://github.com/MorangeR18/AmbientWeather-WigiDash/releases/latest)
[![Release downloads](https://img.shields.io/github/downloads/MorangeR18/AmbientWeather-WigiDash/total)](https://github.com/MorangeR18/AmbientWeather-WigiDash/releases)
[![Widget verification](https://github.com/MorangeR18/AmbientWeather-WigiDash/actions/workflows/verify.yml/badge.svg)](https://github.com/MorangeR18/AmbientWeather-WigiDash/actions/workflows/verify.yml)
[![MIT License](https://img.shields.io/github/license/MorangeR18/AmbientWeather-WigiDash)](LICENSE)

An unofficial, open-source widget that turns an Ambient Weather personal weather station into a live G.SKILL WigiDash touchscreen dashboard. Use the full 5×4 page or choose a compact layout for an existing dashboard.

![Full 5×4 Ambient Weather dashboard rendered with synthetic sample data](docs/images/weather-full-5x4.png)

*Rendered preview using synthetic station data; no real station identity, location, or credentials are shown.*

> [!IMPORTANT]
> This community project is not affiliated with or endorsed by G.SKILL or Ambient Weather. Every user supplies their own Ambient Weather API Key and Application Key; no shared credentials are included.

## Download and install

**Most users do not need Visual Studio or the source code.** Download the prepared widget package from the Releases page:

### [Download the latest release](https://github.com/MorangeR18/AmbientWeather-WigiDash/releases/latest)

Under **Assets**, download `AmbientWeather-WigiDash-v1.0.1.zip`—not GitHub's automatically generated “Source code” archives.

Version 1.0.1 is a maintenance release covering source-build installation, safe connection-error messages, and reliable widget shutdown. See the [release notes](docs/RELEASE_NOTES_v1.0.1.md) for the changes, validation record, and upgrade instructions.

### Requirements

- A G.SKILL WigiDash with WigiDash Manager on Windows 10 or 11
- An AmbientWeather.net account with a reporting weather station
- Your own Ambient Weather **API Key** and **Application Key**

### Installation

1. Download and open `AmbientWeather-WigiDash-v1.0.1.zip`.
2. Close WigiDash Manager completely, including its notification-area icon.
3. Press <kbd>Win</kbd>+<kbd>R</kbd>, paste the following path, and select **OK**:

   ```text
   %APPDATA%\G.SKILL\WigiDashManager\Widgets
   ```

4. Copy the entire folder below from the ZIP into the `Widgets` folder:

   ```text
   7A2E4F11-3D92-4F65-9B23-C8B6D451A907
   ```

5. Start WigiDash Manager and find **Ambient Weather Station** in the widget list.
6. Create an empty page, select the **5×4** widget size, and drag the widget onto the page.

The custom widget does not belong in WigiDash Manager's protected `DefaultWidgets` directory under Program Files.

### Connect your weather station

1. Sign in to [AmbientWeather.net](https://ambientweather.net/) and open **Account**.
2. Create or copy both your **API Key** and **Application Key**. They are different values and both are required.
3. Select the widget in WigiDash Manager and open its settings.
4. Enter both keys and select **Test connection / load stations**.
5. Confirm the expected station appears, choose it, select your units and refresh interval, then select **Save and refresh**.

Tap the live widget once to request a manual refresh.

> [!CAUTION]
> Treat both keys as secrets. Never place them in source code, screenshots, commits, issue reports, videos, or unredacted logs.

## Features

- Outdoor temperature, feels-like temperature, humidity, and dew point
- Wind direction, speed, gust, and daily maximum gust when reported
- Relative barometric pressure with a rising, steady, or falling trend
- Hourly, daily, monthly, and yearly rainfall
- Indoor temperature and humidity when reported
- UV index and solar radiation when available
- Lightning or PM2.5 information when supported by attached sensors
- Current-inclusive 24-hour temperature graph with high and low readings
- Live/stale connection state and last successful update time
- Imperial or metric display units
- Full-page 5×4 plus 3×2, 2×2, 2×1, and 1×1 layouts
- Manual refresh by tapping the widget

Ambient Weather exposes station observations through this API, not a conventional weather forecast. The widget deliberately does not invent forecast conditions.

## Layout previews

All previews use synthetic sample station data.

| Full page — 5×4 |
| --- |
| ![Full page preview](docs/images/weather-full-5x4.png) |

| Wide dashboard — 3×2 | Dashboard tile — 2×2 |
| --- | --- |
| ![3×2 preview](docs/images/weather-wide-3x2.png) | ![2×2 preview](docs/images/weather-medium-2x2.png) |

| Wide compact tile — 2×1 | Compact tile — 1×1 |
| --- | --- |
| ![2×1 preview](docs/images/weather-wide-2x1.png) | ![1×1 preview](docs/images/weather-tile-1x1.png) |

The WigiDash display is 1024×600. A full 5×4 widget receives 1016×592 pixels because WigiDash Manager reserves the grid margins.

## Compatibility

| Component | Status |
| --- | --- |
| Widget | Version 1.0.1 |
| WigiDash Manager | Developed and hardware-tested with 1.2.9559.29232 |
| Windows | Designed for Windows 10 and Windows 11 |
| Runtime target | .NET Framework 4.7.2 |
| WigiDash layout | 1×1, 2×1, 2×2, 3×2, and 5×4 |
| Ambient Weather API | `/v1/devices` current observations and device history |

Later WigiDash Manager versions may update the widget framework. If the widget stops appearing after a Manager update, open a bug report with the new Manager version.

## Refresh behavior and API limits

- Current observations refresh every **60 seconds** by default.
- The selectable live refresh interval is **30–300 seconds**.
- The 24-hour history refreshes every **15 minutes**.
- Requests are serialized with at least **1.1 seconds** between them.
- Rapid manual refresh requests are coalesced while a refresh is already running. Settings changed during a request are applied together, and a follow-up refresh uses the latest settings.
- The freshest observation is added to the displayed graph and high/low calculation even between history refreshes.

Historical readings may lag the current observation because Ambient Weather stores history at a lower cadence. The widget uses the device-list endpoint for the freshest observation and device history only for trends.

When the widget is removed or Manager shuts down, the widget stops accepting new work and cancels outstanding requests. Disposal does not wait on the host UI thread. Resource cleanup is deferred until admitted background/manual/settings refreshes, credential tests, and render callbacks finish, including requests that take time to acknowledge cancellation. A timeout never frees resources still in use.

## Credentials and privacy

- Each installation uses the owner's own API Key and Application Key.
- Keys and the selected station identifier are encrypted for the current Windows user with Windows DPAPI before WigiDash Manager stores the settings.
- Connection-test messages and background refresh errors/logs use fixed troubleshooting text. They never include raw exception text, request URLs, response bodies, keys, or station identifiers, including newly entered keys that have not been saved.
- Requests use HTTPS. Because Ambient Weather's API places credentials in the query string, trusted local HTTPS-inspection or proxy software may still record them.
- The project contains no analytics, telemetry, advertising, cloud relay, or third-party weather service.

To remove the local widget, close WigiDash Manager and delete only this folder, then restart Manager:

```text
%APPDATA%\G.SKILL\WigiDashManager\Widgets\7A2E4F11-3D92-4F65-9B23-C8B6D451A907
```

## Updating

1. Download the newer release ZIP.
2. Close WigiDash Manager completely.
3. Replace the existing GUID-named widget folder with the one from the new release.
4. Restart WigiDash Manager.

Your saved configuration is managed by WigiDash Manager and should remain available. Review the release notes for version-specific upgrade information.

Updating from 1.0.0 to 1.0.1 requires no settings migration. The widget identifier, installation path, and encrypted credential-storage format are unchanged.

## Troubleshooting

### The widget does not appear

1. Confirm WigiDash Manager was fully closed during installation.
2. Confirm the DLL is directly inside the GUID-named folder below `%APPDATA%\G.SKILL\WigiDashManager\Widgets`.
3. Confirm you did not place it under `DefaultWidgets`.
4. Restart Windows if Manager remained open in the background.
5. If Manager was recently updated, report its exact version in a GitHub issue.

### “Ambient Weather rejected the API credentials”

Re-copy both keys from the AmbientWeather.net account page. The API Key and Application Key are different values, and both are required.

### The dashboard says “STALE”

The last valid station data remains visible instead of blanking the page. Check internet connectivity and the station's AmbientWeather.net status. The widget retries automatically at the selected interval.

### A field shows an em dash

Ambient Weather stations and add-on sensors report different field sets. An em dash means the selected station did not provide that reading; it is not treated as zero.

### WigiDash Manager logs

Manager logs are normally under `%USERPROFILE%\AppData\Roaming\G.SKILL\WigiDashManager\Logs`. Redact credentials, exact station identifiers, MAC addresses, and location information before sharing anything from that directory.

## Verify a downloaded release

Each release includes a `.sha256` file. In PowerShell, calculate the ZIP's checksum:

```powershell
Get-FileHash -Algorithm SHA256 .\AmbientWeather-WigiDash-v1.0.1.zip
```

Compare the result with `AmbientWeather-WigiDash-v1.0.1.sha256` from the same release.

## Build from source

Source builds require:

- G.SKILL WigiDash Manager installed locally
- Visual Studio 2022 with **.NET desktop development**, or the .NET Framework compiler included with Windows

Open PowerShell in the project directory and run:

```powershell
.\scripts\Build.ps1 -Configuration Release
```

The ready-to-install development build is created at:

```text
artifacts\AmbientWeatherStation\7A2E4F11-3D92-4F65-9B23-C8B6D451A907.dll
```

Close WigiDash Manager completely, including its notification-area icon, then build and install from the current source with:

```powershell
.\scripts\Install-Widget.ps1
```

The installer **always runs a fresh Release build**, even when a package DLL already exists or the source appears unchanged. You can run it directly without running `Build.ps1` first. This avoids relying on stale artifacts or file timestamps. A failed build stops installation before any destination files are changed. Manager is checked before the build and again before installation, and the installed DLL must match the freshly built DLL's SHA-256 checksum.

The installation directory remains `%APPDATA%\G.SKILL\WigiDashManager\Widgets\7A2E4F11-3D92-4F65-9B23-C8B6D451A907`. Use `-WidgetsPath` to select another Widgets directory for testing. Preview the operation without building or copying files with:

```powershell
.\scripts\Install-Widget.ps1 -WhatIf
```

The build references `WigiDashWidgetFramework.dll` supplied by the installed WigiDash Manager and never packages that framework. If Manager is installed elsewhere, provide its directory explicitly:

```powershell
.\scripts\Build.ps1 -WigiDashManagerPath 'D:\Apps\G.SKILL\WigiDash Manager'
.\scripts\Install-Widget.ps1 -WigiDashManagerPath 'D:\Apps\G.SKILL\WigiDash Manager'
```

## Test and package

Run the local verification suite:

```powershell
.\scripts\Test.ps1
```

It runs the isolated installer, connection-security, and lifecycle regression tests, validates Ambient Weather JSON parsing, renders all five supported layouts, and verifies the Windows DPAPI round trip when a Windows user profile is available. Rendered previews are written to `artifacts\previews`. The suite uses synthetic data and requires no weather-station credentials or API calls.

Run only the installer regression tests with either Windows PowerShell 5.1 or PowerShell 7:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-InstallWidget.ps1
pwsh.exe -NoProfile -File .\scripts\Test-InstallWidget.ps1
```

These tests copy the real installer into temporary synthetic projects and substitute the build and Manager process checks. They cover initial and unchanged installations, source changes with preserved timestamps, added/deleted source files, stale artifacts, build failures, Manager shutdown checks, dry runs, and checksum verification. They do not need WigiDash Manager, a compiler, or a real widget installation. The full `Test.ps1` suite and a real `Build.ps1` need Windows; the real build also requires the installed WigiDash framework. Confirming widget loading and display on the device requires WigiDash Manager and hardware.

Run only the connection-security checks on Windows with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-ConnectionErrors.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-SettingsErrors.ps1
```

These checks compile against Windows .NET Framework and use simulated HTTP responses and the actual settings control with a synthetic parent. They cover sensitive error payloads, newly entered credentials, password edits during an in-flight test, and useful failure messages. They require neither WigiDash Manager nor live weather credentials and never make API calls. Test output contains scenario names and results rather than exception payloads. Both scripts also work with `pwsh.exe`.

Connection errors retain useful categories such as rejected credentials, rate limits, HTTP status, network failures, timeouts, and unreadable responses. Unexpected errors use a generic message so that untrusted exception details cannot reach the settings interface or refresh logs.

Run only the lifecycle regression checks on Windows with either PowerShell host:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Lifecycle.ps1
pwsh.exe -NoProfile -File .\scripts\Test-Lifecycle.ps1
```

These checks compile the actual widget instance and HTTP client with the Windows .NET Framework compiler. Minimal original framework, rendering, and storage test doubles plus a synthetic HTTP handler keep the tests independent of WigiDash Manager, its framework DLL, real station credentials, saved settings, and API access. Controlled request/callback completion exercises rapid refreshes, settings changes, cancellation, disposal, and callback failures. Test output contains scenario names and results without credentials or exception payloads. Live shutdown and restart behavior in WigiDash Manager still requires a manual device check.

Maintainers can build a release ZIP and checksum with:

```powershell
.\scripts\Package-Release.ps1 -Version 1.0.1
```

The packaging script verifies the tests and version, builds against the locally installed WigiDash framework, and writes release assets under `artifacts\release`.

## Project layout

```text
.github\                         Contribution forms and verification workflow
docs\                            Release instructions and publishable images
scripts\Build.ps1               Builds the native widget DLL
scripts\Install-Widget.ps1      Rebuilds and installs from current source
scripts\Package-Release.ps1     Creates the release ZIP and SHA-256 file
scripts\Test.ps1                Runs installer, security, lifecycle, parser, and render checks
scripts\Test-InstallWidget.ps1  Runs isolated synthetic installer regression checks
scripts\Test-ConnectionErrors.ps1 Runs offline HTTP/error-message security checks
scripts\Test-SettingsErrors.ps1 Runs settings UI error-message security checks
scripts\Test-Lifecycle.ps1      Runs offline refresh and shutdown regression checks
src\AmbientWeatherWidget\       Widget, settings UI, API client, and renderer
tools\Preview\                  Deterministic preview/self-test utility
tools\ConnectionTests\          Synthetic HTTP and error-message regression tests
tools\SettingsTests\            Settings UI tests with a synthetic parent
tools\LifecycleTests\           Refresh and shutdown tests with controlled test doubles
```

## Contributing and support

- Read [CONTRIBUTING.md](CONTRIBUTING.md) before proposing a change.
- Read [SUPPORT.md](SUPPORT.md) to choose between Discussions, Issues, and private security reporting.
- Use the structured [bug report](https://github.com/MorangeR18/AmbientWeather-WigiDash/issues/new?template=bug_report.yml) or [feature request](https://github.com/MorangeR18/AmbientWeather-WigiDash/issues/new?template=feature_request.yml).
- Review [SECURITY.md](SECURITY.md) before reporting a vulnerability or sharing diagnostic information.
- See [CHANGELOG.md](CHANGELOG.md) for release history.

## License

Original project code and documentation are available under the [MIT License](LICENSE). You may use, copy, modify, publish, distribute, sublicense, and sell copies subject to the license's notice requirement.

The repository does not redistribute WigiDash Manager binaries, copied WigiDash SDK source, or Ambient Weather API documentation. Product names and trademarks belong to their respective owners. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Upstream references

- [G.SKILL WigiDash product and specifications](https://www.gskill.com/product/412/415/1702982997/WigiDash)
- [Official WigiDash widget SDK repositories](https://github.com/orgs/ElmorLabs-WigiDash/repositories)
- [Ambient Weather API documentation](https://github.com/ambient-weather/api-docs)
- [Ambient Weather device field reference](https://github.com/ambient-weather/api-docs/wiki/Device-Data-Specs)
