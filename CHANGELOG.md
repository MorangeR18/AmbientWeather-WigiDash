# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and releases use [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.1] - 2026-10-30

### Fixed

- Widget shutdown now stops new work and cancels requests without waiting on the host UI thread. HTTP, synchronization, cancellation, and bitmap resources are released only after admitted refreshes, credential tests, and render callbacks finish.
- Concurrent refresh and settings changes use coherent credential/station snapshots, discard obsolete results, and coalesce a follow-up refresh for the latest settings. Rendering and host callback failures no longer prevent refresh-gate release; error messages remain sanitized.
- Connection-test errors and background refresh errors now use shared, fixed troubleshooting messages instead of arbitrary exception text. Unsaved keys, request URLs, station identifiers, response bodies, and nested exception details are excluded from these messages and refresh logs.
- HTTP client failures are converted to safe exceptions without retaining sensitive inner exceptions; caller cancellation still behaves as cancellation.
- Source installation now rebuilds the Release DLL on every run, including when an artifact already exists, and stops before installation if the build fails. WigiDash Manager must remain closed and the installed DLL is still verified with SHA-256.
- Source installation accepts `-WigiDashManagerPath` for a non-default Manager location; `-WhatIf` previews the operation without building or installing.

### Added

- Deterministic offline lifecycle regression tests for rapid refresh requests, settings changes, cancellation, concurrent/reentrant disposal, and callback failures, included in local verification and Windows CI.
- Offline connection-security regression tests with synthetic credentials, simulated HTTP failures, and the actual settings control, including credentials edited while a test is in progress.
- Deterministic PowerShell installer regression tests using synthetic source and package files, included in the local verification suite and Windows CI.

## [1.0.0] - 2026-08-17

### Added

- Full-page 5×4 Ambient Weather station dashboard for G.SKILL WigiDash.
- Compact 3×2, 2×2, 2×1, and 1×1 layouts from the same widget.
- Live outdoor, indoor, wind, barometer, rainfall, UV, solar, lightning, and PM2.5 readings when reported by the station.
- A 24-hour outdoor-temperature trend with high and low readings.
- Configurable 30–300 second live-data refresh interval and 15-minute history refresh.
- Windows DPAPI protection for API credentials and the selected station identifier.
- Manual refresh by tapping the widget.
- PowerShell build, verification, installation, and release-packaging scripts.

[Unreleased]: https://github.com/MorangeR18/AmbientWeather-WigiDash/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/MorangeR18/AmbientWeather-WigiDash/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/MorangeR18/AmbientWeather-WigiDash/releases/tag/v1.0.0
