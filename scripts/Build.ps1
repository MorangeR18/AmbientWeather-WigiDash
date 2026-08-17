[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$WigiDashManagerPath = 'C:\Program Files (x86)\G.SKILL\WigiDash Manager'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$assemblyName = '7A2E4F11-3D92-4F65-9B23-C8B6D451A907.dll'
$buildRoot = Join-Path $projectRoot 'build'
$tempRoot = Join-Path $buildRoot 'temp'
$packageRoot = Join-Path $projectRoot 'artifacts\AmbientWeatherStation'

function Get-GacAssembly([string]$name) {
    $roots = @(
        "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\$name",
        "C:\Windows\Microsoft.NET\assembly\GAC_64\$name",
        "C:\Windows\Microsoft.NET\assembly\GAC_32\$name"
    )
    foreach ($root in $roots) {
        $match = Get-ChildItem -LiteralPath $root -Filter "$name.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName -First 1
        if ($match) { return $match }
    }
    throw "Required .NET Framework assembly was not found: $name"
}

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework C# compiler was not found. Install Visual Studio 2022 with .NET desktop development.'
}

$frameworkAssembly = Join-Path $WigiDashManagerPath 'WigiDashWidgetFramework.dll'
if (-not (Test-Path -LiteralPath $frameworkAssembly)) {
    throw "WigiDashWidgetFramework.dll was not found at '$frameworkAssembly'. Install WigiDash Manager or pass -WigiDashManagerPath."
}

New-Item -ItemType Directory -Force -Path $tempRoot, $packageRoot | Out-Null

$presentationCore = Get-GacAssembly 'PresentationCore'
$presentationFramework = Get-GacAssembly 'PresentationFramework'
$windowsBase = Get-GacAssembly 'WindowsBase'
$systemXaml = Get-GacAssembly 'System.Xaml'
$systemNetHttp = Get-GacAssembly 'System.Net.Http'
$sourceRoot = Join-Path $projectRoot 'src\AmbientWeatherWidget'
$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -Recurse | ForEach-Object { $_.FullName }
$pluginOutput = Join-Path $tempRoot $assemblyName
$debugSwitch = if ($Configuration -eq 'Debug') { '/debug:full' } else { '/debug:pdbonly' }
& $compiler /nologo /target:library /optimize+ $debugSwitch "/out:$pluginOutput" `
    "/reference:$frameworkAssembly" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    "/reference:$systemNetHttp" `
    "/reference:$presentationCore" "/reference:$presentationFramework" "/reference:$systemXaml" "/reference:$windowsBase" `
    $sources
if ($LASTEXITCODE -ne 0) { throw 'Ambient Weather widget failed to compile.' }

Copy-Item -LiteralPath $pluginOutput -Destination (Join-Path $packageRoot $assemblyName) -Force
$pdb = [System.IO.Path]::ChangeExtension($pluginOutput, '.pdb')
if (Test-Path -LiteralPath $pdb) { Copy-Item -LiteralPath $pdb -Destination $packageRoot -Force }

Write-Host "Built widget package: $packageRoot" -ForegroundColor Green
Write-Host 'WigiDashWidgetFramework.dll is intentionally excluded; WigiDash Manager supplies it.' -ForegroundColor DarkGray
