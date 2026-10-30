[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tempRoot = Join-Path $projectRoot 'build\temp'
$testExe = Join-Path $tempRoot 'ConnectionTests.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Connection regression tests require the Windows .NET Framework C# compiler.'
}
$systemNetHttp = Get-ChildItem -LiteralPath 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Net.Http' -Filter 'System.Net.Http.dll' -Recurse -ErrorAction Stop |
    Select-Object -ExpandProperty FullName -First 1
if (-not $systemNetHttp) { throw 'The .NET Framework System.Net.Http assembly was not found.' }

New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
$sources = @(
    (Join-Path $projectRoot 'src\AmbientWeatherWidget\Models.cs'),
    (Join-Path $projectRoot 'src\AmbientWeatherWidget\ConnectionErrors.cs'),
    (Join-Path $projectRoot 'src\AmbientWeatherWidget\AmbientWeatherClient.cs'),
    (Join-Path $projectRoot 'tools\ConnectionTests\Program.cs')
)
& $compiler /nologo /target:exe /optimize+ /nowarn:0649 "/out:$testExe" /reference:System.dll /reference:System.Core.dll `
    "/reference:$systemNetHttp" /reference:System.Web.Extensions.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Connection error regression tests failed to compile.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Connection error regression tests reported a failure.' }
Write-Host 'Connection error verification passed using synthetic data and an in-memory HTTP handler.' -ForegroundColor Green
