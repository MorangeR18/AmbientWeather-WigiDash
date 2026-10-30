# Ambient Weather Station for G.SKILL WigiDash v1.0.1

Released October 30, 2026. This maintenance release fixes source-build installation reliability, protects connection-error messages, and makes widget shutdown safe while requests or callbacks are still running.

## Download and integrity

Download both assets from the [v1.0.1 release](https://github.com/MorangeR18/AmbientWeather-WigiDash/releases/tag/v1.0.1):

- `AmbientWeather-WigiDash-v1.0.1.zip` — ready-to-install widget package.
- `AmbientWeather-WigiDash-v1.0.1.sha256` — SHA-256 checksum of that ZIP.

Verify the downloaded ZIP in PowerShell and compare the value with the checksum file from the same release:

```powershell
Get-FileHash -Algorithm SHA256 .\AmbientWeather-WigiDash-v1.0.1.zip
```

Published ZIP SHA-256:

```text
49f4044f177956b37a4592187d3d98c4d04e92dbd79b69f6eec84872d4ecda7c
```

GitHub's automatically generated source archives require a local build. The prepared package contains the widget DLL, installation instructions, license, and third-party notices; WigiDash Manager supplies its own framework DLL.

## Fixed

### Source installation

`Install-Widget.ps1` now runs a fresh Release build on every installation, including when an artifact already exists or source timestamps appear unchanged. Build failures stop installation before destination files change. The installer still requires WigiDash Manager to be closed and verifies the copied DLL with SHA-256. `-WhatIf` performs no build or installation, and `-WigiDashManagerPath` supports a custom Manager location.

### Connection-error privacy

Connection tests and background refreshes use shared, fixed troubleshooting messages. API keys, application keys, station identifiers, request URLs, response bodies, and nested exception details are excluded from these error messages and refresh logs. Newly entered, unsaved credentials are covered, including edits made while a connection test is in progress. Useful categories remain available for rejected credentials, rate limits, HTTP status, network failures, timeouts, and invalid responses.

### Shutdown and concurrent refreshes

Shutdown stops new work, cancels requests, and defers resource cleanup until active refreshes, credential tests, and callbacks finish. Disposal does not wait for refresh tasks on the host UI thread. Repeated or reentrant disposal is safe, and rendering or host-callback failures cannot strand the refresh semaphore. Refreshes use coherent settings snapshots, discard obsolete results, and coalesce a follow-up refresh for the latest settings.

These changes address source-level risks. No credential-disclosure incident or widget crash was established during this review.

## Validation record

The fix implementation passed 61 deterministic regression scenarios:

| Suite | Scenarios |
| --- | ---: |
| Source installer | 17 |
| HTTP client and connection-error privacy | 21 |
| Settings interface | 5 |
| Widget lifecycle and concurrency | 18 |

Additional checks passed for Windows DPAPI credential protection, weather-data parsing, current-inclusive temperature ranges, and all five rendered layouts. The regression suites were exercised under Windows PowerShell 5.1 and PowerShell 7.6.9. All fixtures use synthetic credentials, station identifiers, requests, and failures; no live credentials, user settings, raw personal logs, or vendor binaries are included in the tests.

The maintainer confirmed normal operation on Windows 11 with WigiDash hardware after testing manual refreshes, settings refreshes, and Manager shutdown/restart. That confirmation applies to the fix build before its version metadata was changed to 1.0.1. Final v1.0.1 packaging passed the automated suite, built DLL version 1.0.1.0, and verified the archived DLL against the build output. GitHub Actions records remote verification separately from device testing.

## Compatibility and upgrade

- Windows 10 and Windows 11 remain the supported platform targets; live validation for these fixes was performed on Windows 11.
- The runtime target remains .NET Framework 4.7.2, using the widget framework installed with WigiDash Manager.
- The widget identifier, five layout sizes, default refresh interval, request-rate controls, and installation directory are unchanged.
- No settings migration is required from 1.0.0. Saved settings remain managed by WigiDash Manager, with the existing Windows DPAPI credential format.

To update:

1. Close WigiDash Manager completely, including its notification-area icon.
2. Verify and open `AmbientWeather-WigiDash-v1.0.1.zip`.
3. Open `%APPDATA%\G.SKILL\WigiDashManager\Widgets`.
4. Replace the existing `7A2E4F11-3D92-4F65-9B23-C8B6D451A907` folder with the folder from the ZIP.
5. Restart WigiDash Manager and confirm the existing widget loads and refreshes.

Source-build users can run `scripts\Install-Widget.ps1` after closing Manager. This rebuilds and verifies the installation automatically. Full instructions are in the [README](https://github.com/MorangeR18/AmbientWeather-WigiDash/blob/v1.0.1/README.md) and the ZIP's `INSTALL.txt`.

## History and support

See the [changes since v1.0.0](https://github.com/MorangeR18/AmbientWeather-WigiDash/compare/v1.0.0...v1.0.1) and [changelog](https://github.com/MorangeR18/AmbientWeather-WigiDash/blob/v1.0.1/CHANGELOG.md). The original [v1.0.0 release notes](https://github.com/MorangeR18/AmbientWeather-WigiDash/blob/v1.0.0/docs/RELEASE_NOTES_v1.0.0.md) remain available as the historical record.

Report reproducible bugs through the repository's issue form and security concerns through its [security policy](https://github.com/MorangeR18/AmbientWeather-WigiDash/blob/main/SECURITY.md). Never include real keys, station identifiers, MAC addresses, precise locations, or unredacted logs in reports.

This is an unofficial community project, unaffiliated with G.SKILL or Ambient Weather, distributed under the MIT License.
