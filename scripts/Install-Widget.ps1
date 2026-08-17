[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$WidgetsPath = (Join-Path $env:APPDATA 'G.SKILL\WigiDashManager\Widgets')
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $projectRoot 'artifacts\AmbientWeatherStation'
$widgetId = '7A2E4F11-3D92-4F65-9B23-C8B6D451A907'

if (-not (Test-Path -LiteralPath (Join-Path $packageRoot "$widgetId.dll"))) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release
}

if (Get-Process -Name 'WigiDashManager' -ErrorAction SilentlyContinue) {
    throw 'WigiDash Manager is running. Close it completely, including its notification-area icon, and run the installer again.'
}

if ([string]::IsNullOrWhiteSpace($WidgetsPath)) {
    throw 'The WigiDash custom-widget path is empty.'
}

$destination = Join-Path $WidgetsPath $widgetId
if ($PSCmdlet.ShouldProcess($destination, 'Install Ambient Weather WigiDash widget')) {
    New-Item -ItemType Directory -Force -Path $WidgetsPath | Out-Null
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -Path (Join-Path $packageRoot '*') -Destination $destination -Force
    $sourceDll = Join-Path $packageRoot "$widgetId.dll"
    $installedDll = Join-Path $destination "$widgetId.dll"
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $sourceDll).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $installedDll).Hash) {
        throw 'The installed DLL did not match the package checksum.'
    }
    Write-Host "Installed custom widget to: $destination" -ForegroundColor Green
    Write-Host 'Start WigiDash Manager, then find Ambient Weather Station in the widget list.' -ForegroundColor Cyan
}
