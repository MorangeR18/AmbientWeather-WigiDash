# Ambient Weather Station for G.SKILL WigiDash v1.0.0

The first public release turns an Ambient Weather personal weather station into a live, full-page WigiDash dashboard with compact layout options.

## Download

Download both files under **Assets**:

- `AmbientWeather-WigiDash-v1.0.0.zip` — ready-to-install widget package
- `AmbientWeather-WigiDash-v1.0.0.sha256` — checksum for verifying the ZIP

Do not use GitHub's automatically generated “Source code” archives unless you intend to build the project yourself.

## Highlights

- Full-page 5×4 dashboard plus 3×2, 2×2, 2×1, and 1×1 layouts
- Live temperature, humidity, dew point, wind, barometer, rainfall, UV, solar, and optional sensor readings
- Current-inclusive 24-hour temperature graph and high/low values
- Configurable 30–300 second live refresh; 60 seconds by default
- 15-minute historical-data refresh
- Imperial and metric units
- Windows DPAPI protection for credentials and the selected station identifier
- Manual refresh by tapping the widget

## Compatibility

- Windows 10 or Windows 11
- G.SKILL WigiDash and WigiDash Manager
- Developed and hardware-tested with WigiDash Manager `1.2.9559.29232`
- Each user must provide their own Ambient Weather API Key and Application Key

## Install

1. Close WigiDash Manager completely, including its notification-area icon.
2. Open the release ZIP.
3. Press **Win+R**, enter `%APPDATA%\G.SKILL\WigiDashManager\Widgets`, and select **OK**.
4. Copy the entire `7A2E4F11-3D92-4F65-9B23-C8B6D451A907` folder from the ZIP into that `Widgets` folder.
5. Start WigiDash Manager and add **Ambient Weather Station** to a page.
6. Open the widget settings, enter your own two Ambient Weather keys, load and select your station, then save.

Detailed update, uninstall, privacy, and troubleshooting instructions are in the repository [README](https://github.com/MorangeR18/AmbientWeather-WigiDash#readme) and the ZIP's `INSTALL.txt`.

## Known limitations

- This release displays observations; Ambient Weather's endpoint used here does not provide a conventional weather forecast.
- Available fields depend on the station and attached sensors. Missing values display as an em dash.
- Installation is manual until or unless the widget is accepted into the WigiDash Manager Market.
- The GitHub verification workflow checks parsing and layout rendering. The release DLL is built locally against the official framework installed with WigiDash Manager and is hardware-tested separately.

## Integrity

SHA-256 of `AmbientWeather-WigiDash-v1.0.0.zip`:

```text
7f34246e8de2e613c39a0646fd13b8ddd7a6989ed4159473dd047011a3ef2523
```

## Support and privacy

Use the repository's structured bug-report form for reproducible problems. Never post API keys, Application Keys, exact station identifiers, MAC addresses, precise locations, or unredacted logs.

This is an unofficial community project and is not affiliated with or endorsed by G.SKILL or Ambient Weather.
