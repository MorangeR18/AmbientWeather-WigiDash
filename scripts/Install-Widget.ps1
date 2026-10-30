[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$WidgetsPath = (Join-Path $env:APPDATA 'G.SKILL\WigiDashManager\Widgets'),
    [string]$WigiDashManagerPath = 'C:\Program Files (x86)\G.SKILL\WigiDash Manager'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $projectRoot 'artifacts\AmbientWeatherStation'
$widgetId = '7A2E4F11-3D92-4F65-9B23-C8B6D451A907'

if (Get-Process -Name 'WigiDashManager' -ErrorAction SilentlyContinue) {
    throw 'WigiDash Manager is running. Close it completely, including its notification-area icon, and run the installer again.'
}

if ([string]::IsNullOrWhiteSpace($WidgetsPath)) {
    throw 'The WigiDash custom-widget path is empty.'
}

$destination = Join-Path $WidgetsPath $widgetId
if ($PSCmdlet.ShouldProcess($destination, 'Build Release and install Ambient Weather WigiDash widget')) {
    # Always compile the current source. Existing artifacts and timestamps do not
    # prove that a DLL contains this checkout's code, references, or build options.
    $LASTEXITCODE = 0
    & (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release -WigiDashManagerPath $WigiDashManagerPath
    if (-not $? -or $LASTEXITCODE -ne 0) {
        throw 'The widget build failed. No widget files were installed.'
    }

    $sourceDll = Join-Path $packageRoot "$widgetId.dll"
    if (-not (Test-Path -LiteralPath $sourceDll -PathType Leaf)) {
        throw 'The widget build did not produce the expected package DLL. No widget files were installed.'
    }

    # Building can take time; Manager must still be closed before copying files.
    if (Get-Process -Name 'WigiDashManager' -ErrorAction SilentlyContinue) {
        throw 'WigiDash Manager is running. Close it completely, including its notification-area icon, and run the installer again.'
    }

    $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceDll).Hash
    New-Item -ItemType Directory -Force -Path $WidgetsPath | Out-Null
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -Path (Join-Path $packageRoot '*') -Destination $destination -Force
    $installedDll = Join-Path $destination "$widgetId.dll"
    if ($sourceHash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $installedDll).Hash) {
        throw 'The installed DLL did not match the package checksum.'
    }
    Write-Host "Installed custom widget to: $destination" -ForegroundColor Green
    Write-Host 'Start WigiDash Manager, then find Ambient Weather Station in the widget list.' -ForegroundColor Cyan
}
