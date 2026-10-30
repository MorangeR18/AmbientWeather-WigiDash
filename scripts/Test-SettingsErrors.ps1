[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tempRoot = Join-Path $projectRoot 'build\temp'
$testExe = Join-Path $tempRoot 'SettingsTests.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Settings error checks require Windows and the .NET Framework C# compiler.'
}

function Get-TestAssembly([string]$Name) {
    foreach ($kind in @('GAC_MSIL', 'GAC_64', 'GAC_32')) {
        $root = Join-Path 'C:\Windows\Microsoft.NET\assembly' "$kind\$Name"
        $match = Get-ChildItem -LiteralPath $root -Filter "$Name.dll" -Recurse -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName -First 1
        if ($match) { return $match }
    }
    throw "Required .NET Framework test assembly was not found: $Name"
}

New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
$references = @('PresentationCore', 'PresentationFramework', 'WindowsBase', 'System.Xaml', 'System.Net.Http') |
    ForEach-Object { '/reference:' + (Get-TestAssembly $_) }
$sources = @(
    (Join-Path $projectRoot 'src\AmbientWeatherWidget\SettingsControl.cs'),
    (Join-Path $projectRoot 'src\AmbientWeatherWidget\Models.cs'),
    (Join-Path $projectRoot 'src\AmbientWeatherWidget\ConnectionErrors.cs'),
    (Join-Path $projectRoot 'tools\SettingsTests\Program.cs')
)
& $compiler /nologo /target:exe /optimize+ /nowarn:0649 "/out:$testExe" /reference:System.dll /reference:System.Core.dll $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Settings error test utility failed to compile.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Settings error checks reported a failure.' }
