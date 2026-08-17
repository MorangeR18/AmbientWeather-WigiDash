[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tempRoot = Join-Path $projectRoot 'build\temp'
$previewOutput = Join-Path $projectRoot 'artifacts\previews'
New-Item -ItemType Directory -Force -Path $tempRoot, $previewOutput | Out-Null

$sources = @(
    (Join-Path $projectRoot 'src\AmbientWeatherWidget\Models.cs'),
    (Join-Path $projectRoot 'src\AmbientWeatherWidget\AmbientWeatherClient.cs'),
    (Join-Path $projectRoot 'src\AmbientWeatherWidget\SecretProtector.cs'),
    (Join-Path $projectRoot 'src\AmbientWeatherWidget\WeatherRenderer.cs'),
    (Join-Path $projectRoot 'tools\Preview\Program.cs')
)
$testExe = Join-Path $tempRoot 'Preview.exe'
$systemNetHttp = Get-ChildItem -LiteralPath 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Net.Http' -Filter 'System.Net.Http.dll' -Recurse -ErrorAction Stop |
    Select-Object -ExpandProperty FullName -First 1
& $compiler /nologo /target:exe /optimize+ /nowarn:0649 "/out:$testExe" /reference:System.dll /reference:System.Core.dll `
    /reference:System.Drawing.dll "/reference:$systemNetHttp" $sources
if ($LASTEXITCODE -ne 0) { throw 'Preview and self-test utility failed to compile.' }
& $testExe $previewOutput
if ($LASTEXITCODE -ne 0) { throw 'Preview and self-test utility reported a failure.' }

$expected = @(
    'weather-full-5x4.png',
    'weather-wide-3x2.png',
    'weather-medium-2x2.png',
    'weather-wide-2x1.png',
    'weather-tile-1x1.png'
)
foreach ($file in $expected) {
    $path = Join-Path $previewOutput $file
    if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -lt 1000) {
        throw "Preview validation failed: $path"
    }
}
Write-Host "Verification passed. Previews: $previewOutput" -ForegroundColor Green
