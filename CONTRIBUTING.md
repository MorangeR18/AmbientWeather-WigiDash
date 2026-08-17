# Contributing

Thanks for helping improve Ambient Weather Station for G.SKILL WigiDash.

## Before opening an issue

- Search existing issues to avoid duplicates.
- Confirm that WigiDash Manager is fully closed and restarted after installing an updated DLL.
- Remove API keys, application keys, exact station identifiers, MAC addresses, and other personal information from screenshots and logs.
- Include the WigiDash Manager version, Windows version, widget version, layout size, and concise reproduction steps.

## Proposing a change

1. Fork the repository and create a focused branch from `main`.
2. Make the smallest change that solves the problem.
3. Run `.\scripts\Test.ps1`.
4. When WigiDash Manager is installed locally, also run `.\scripts\Build.ps1 -Configuration Release` and test the widget on the device.
5. Update `CHANGELOG.md` under **Unreleased** for user-visible changes.
6. Open a pull request explaining the problem, solution, testing performed, and any remaining limitations.

## Development requirements

- Windows 10 or 11
- G.SKILL WigiDash Manager
- Visual Studio 2022 with **.NET desktop development**, or the .NET Framework compiler included with Windows
- An AmbientWeather.net station and personal API/application keys for live integration testing

The build references `WigiDashWidgetFramework.dll` from the locally installed WigiDash Manager. Do not commit or redistribute that DLL, copied SDK source, API credentials, station identifiers, build output, or logs.

## Coding guidelines

- Preserve compatibility with the .NET Framework compiler used by WigiDash Manager.
- Keep network requests serialized and below Ambient Weather's documented request limit.
- Treat missing sensor fields as unavailable, not zero.
- Never log credentials or exact station identifiers.
- Keep the dashboard readable at every supported WigiDash grid size.

By contributing, you agree that your contribution is licensed under the repository's MIT License.
