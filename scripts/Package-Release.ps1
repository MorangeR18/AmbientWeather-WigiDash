[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',
    [string]$WigiDashManagerPath = 'C:\Program Files (x86)\G.SKILL\WigiDash Manager'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$widgetId = '7A2E4F11-3D92-4F65-9B23-C8B6D451A907'
$artifactRoot = Join-Path $projectRoot 'artifacts'
$packageRoot = Join-Path $artifactRoot 'AmbientWeatherStation'
$releaseRoot = Join-Path $artifactRoot 'release'
$stagingRoot = Join-Path $releaseRoot "AmbientWeather-WigiDash-$Version"
$verificationRoot = Join-Path $releaseRoot "verify-$Version"
$widgetRoot = Join-Path $stagingRoot $widgetId
$zipPath = Join-Path $releaseRoot "AmbientWeather-WigiDash-v$Version.zip"
$checksumPath = Join-Path $releaseRoot "AmbientWeather-WigiDash-v$Version.sha256"
$assemblyInfoPath = Join-Path $projectRoot 'src\AmbientWeatherWidget\Properties\AssemblyInfo.cs'
$installGuidePath = Join-Path $projectRoot 'docs\RELEASE_INSTALL.txt'

$resolvedProjectRoot = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd('\')
$resolvedStagingRoot = [System.IO.Path]::GetFullPath($stagingRoot)
$resolvedZipPath = [System.IO.Path]::GetFullPath($zipPath)
$resolvedVerificationRoot = [System.IO.Path]::GetFullPath($verificationRoot)
if (-not $resolvedStagingRoot.StartsWith("$resolvedProjectRoot\artifacts\release\", [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedZipPath.StartsWith("$resolvedProjectRoot\artifacts\release\", [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedVerificationRoot.StartsWith("$resolvedProjectRoot\artifacts\release\", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release paths resolved outside the project artifacts directory.'
}

$assemblyInfo = Get-Content -LiteralPath $assemblyInfoPath -Raw
$versionMatch = [regex]::Match($assemblyInfo, 'AssemblyFileVersion\("(?<version>\d+\.\d+\.\d+)\.\d+"\)')
if (-not $versionMatch.Success) {
    throw 'Could not read the widget version from AssemblyInfo.cs.'
}
if ($versionMatch.Groups['version'].Value -ne $Version) {
    throw "Release version $Version does not match widget version $($versionMatch.Groups['version'].Value)."
}

& (Join-Path $PSScriptRoot 'Test.ps1')
& (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release -WigiDashManagerPath $WigiDashManagerPath

$sourceDll = Join-Path $packageRoot "$widgetId.dll"
if (-not (Test-Path -LiteralPath $sourceDll)) {
    throw "Built widget DLL was not found: $sourceDll"
}

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $verificationRoot) {
    Remove-Item -LiteralPath $verificationRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $widgetRoot | Out-Null
Copy-Item -LiteralPath $sourceDll -Destination (Join-Path $widgetRoot "$widgetId.dll") -Force
Copy-Item -LiteralPath $installGuidePath -Destination (Join-Path $stagingRoot 'INSTALL.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $stagingRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination $stagingRoot -Force
Compress-Archive -Path (Join-Path $stagingRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

Expand-Archive -LiteralPath $zipPath -DestinationPath $verificationRoot
$verifiedDll = Join-Path $verificationRoot "$widgetId\$widgetId.dll"
foreach ($requiredFile in @($verifiedDll, (Join-Path $verificationRoot 'INSTALL.txt'), (Join-Path $verificationRoot 'LICENSE'), (Join-Path $verificationRoot 'THIRD_PARTY_NOTICES.md'))) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "Release verification failed; missing archive entry: $requiredFile"
    }
}
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $sourceDll).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $verifiedDll).Hash) {
    throw 'Release verification failed; the archived widget DLL does not match the build output.'
}
Remove-Item -LiteralPath $verificationRoot -Recurse -Force

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$hash  $(Split-Path -Leaf $zipPath)" -Encoding ASCII

Write-Host "Release package: $zipPath" -ForegroundColor Green
Write-Host "SHA-256: $hash" -ForegroundColor Cyan
Write-Host "Checksum file: $checksumPath" -ForegroundColor DarkGray
