[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$installer = Join-Path $PSScriptRoot 'Install-Widget.ps1'
$widgetId = '7A2E4F11-3D92-4F65-9B23-C8B6D451A907'
$powershell = (Get-Process -Id $PID).Path
$temporaryParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
# Keep paths short enough for Windows PowerShell's .NET Framework path limit.
$testRoot = Join-Path $temporaryParent ('AWIT-' + [guid]::NewGuid().ToString('N').Substring(0, 12))
$passed = 0

# Build.ps1 and external process state are simulated; one test corrupts copied
# bytes to exercise checksum verification. Every scenario executes an unmodified
# copy of the production installer in its own PowerShell process.
$syntheticBuild = @'
[CmdletBinding()]
param([string]$Configuration, [string]$WigiDashManagerPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$countPath = Join-Path $root 'build-count.txt'
$count = if (Test-Path -LiteralPath $countPath) { [int](Get-Content -LiteralPath $countPath -Raw) } else { 0 }
[System.IO.File]::WriteAllText($countPath, [string]($count + 1))
$PSBoundParameters | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'build-parameters.json')
$mode = Get-Content -LiteralPath (Join-Path $root 'build-mode.txt') -Raw
if ($mode -eq 'throw') { throw 'Synthetic compiler failure.' }
if ($mode -eq 'exit') { exit 23 }
$package = Join-Path $root 'artifacts\AmbientWeatherStation'
$dll = Join-Path $package '7A2E4F11-3D92-4F65-9B23-C8B6D451A907.dll'
if ($mode -eq 'missing') {
    if (Test-Path -LiteralPath $dll) { Remove-Item -LiteralPath $dll -Force }
    return
}
New-Item -ItemType Directory -Path $package -Force | Out-Null
$parts = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -File -Recurse |
    Sort-Object FullName | ForEach-Object { $_.Name + ':' + [System.IO.File]::ReadAllText($_.FullName) })
[System.IO.File]::WriteAllText($dll, 'synthetic-dll|' + ($parts -join '|'))
[System.IO.File]::WriteAllText([System.IO.Path]::ChangeExtension($dll, '.pdb'), 'synthetic-pdb')
if ($mode -eq 'manager-after') { [System.IO.File]::WriteAllText((Join-Path $root 'manager-running'), 'running') }
'@

$runner = @'
[CmdletBinding()]
param([string]$FixtureRoot, [switch]$WhatIfInstall, [switch]$CorruptCopy, [string]$CustomManagerPath, [int]$PreviousExitCode = 0)
$ErrorActionPreference = 'Stop'
$global:InstallerTestFixture = $FixtureRoot
$global:InstallerTestCorruptCopy = $CorruptCopy

# The simulated process state cannot interact with the real WigiDash Manager.
function Get-Process {
    [CmdletBinding()]
    param([string]$Name)
    if ($Name -ne 'WigiDashManager') { throw "Unexpected process lookup: $Name" }
    if (Test-Path -LiteralPath (Join-Path $global:InstallerTestFixture 'manager-running')) {
        [pscustomobject]@{ ProcessName = 'WigiDashManager' }
    }
}

function Copy-Item {
    [CmdletBinding(SupportsShouldProcess)]
    param([string[]]$Path, [string[]]$LiteralPath, [string]$Destination, [switch]$Force)
    Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
    if ($global:InstallerTestCorruptCopy) {
        $installed = Join-Path $Destination '7A2E4F11-3D92-4F65-9B23-C8B6D451A907.dll'
        if (Test-Path -LiteralPath $installed) { [System.IO.File]::WriteAllText($installed, 'corrupted-copy') }
    }
}

$parameters = @{ WidgetsPath = (Join-Path $FixtureRoot 'widgets'); Confirm = $false }
if ($WhatIfInstall) { $parameters.WhatIf = $true }
if ($CustomManagerPath) { $parameters.WigiDashManagerPath = $CustomManagerPath }
$global:LASTEXITCODE = $PreviousExitCode
try {
    & (Join-Path $FixtureRoot 'scripts\Install-Widget.ps1') @parameters
    $result = @{ Succeeded = $true; Message = '' }
} catch {
    $result = @{ Succeeded = $false; Message = $_.Exception.Message + [Environment]::NewLine + $_.ScriptStackTrace }
}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $FixtureRoot 'result.json')
'@

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -cne $Actual) { throw "$Message Expected '$Expected'; received '$Actual'." }
}

function New-Fixture([string]$Name) {
    $root = Join-Path $testRoot $Name
    New-Item -ItemType Directory -Path (Join-Path $root 'scripts'), (Join-Path $root 'src') -Force | Out-Null
    Copy-Item -LiteralPath $installer -Destination (Join-Path $root 'scripts\Install-Widget.ps1')
    [System.IO.File]::WriteAllText((Join-Path $root 'scripts\Build.ps1'), $syntheticBuild)
    [System.IO.File]::WriteAllText((Join-Path $root 'src\Widget.cs'), 'source-v1')
    [System.IO.File]::WriteAllText((Join-Path $root 'build-mode.txt'), 'success')
    return $root
}

function Invoke-Installer([string]$Root, [switch]$WhatIfInstall, [switch]$CorruptCopy, [string]$CustomManagerPath, [int]$PreviousExitCode = 0) {
    $arguments = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $testRoot 'Run-Installer.ps1'), '-FixtureRoot', $Root)
    if ($WhatIfInstall) { $arguments += '-WhatIfInstall' }
    if ($CorruptCopy) { $arguments += '-CorruptCopy' }
    if ($CustomManagerPath) { $arguments += @('-CustomManagerPath', $CustomManagerPath) }
    if ($PreviousExitCode) { $arguments += @('-PreviousExitCode', [string]$PreviousExitCode) }
    & $powershell @arguments *> (Join-Path $Root 'run.log')
    if ($LASTEXITCODE -ne 0) {
        throw "Installer test host failed with exit code $LASTEXITCODE. $(Get-Content -LiteralPath (Join-Path $Root 'run.log') -Raw)"
    }
    return (Get-Content -LiteralPath (Join-Path $Root 'result.json') -Raw | ConvertFrom-Json)
}

function Get-InstalledDll([string]$Root) { Join-Path $Root "widgets\$widgetId\$widgetId.dll" }
function Get-PackageDll([string]$Root) { Join-Path $Root "artifacts\AmbientWeatherStation\$widgetId.dll" }
function Get-BuildCount([string]$Root) {
    $path = Join-Path $Root 'build-count.txt'
    if (Test-Path -LiteralPath $path) { return [int](Get-Content -LiteralPath $path -Raw) }
    return 0
}
function Set-FixtureFile([string]$Path, [string]$Content) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    [System.IO.File]::WriteAllText($Path, $Content)
}
function Assert-Installed([string]$Root, [string]$Expected) {
    Assert-Equal $Expected ([System.IO.File]::ReadAllText((Get-InstalledDll $Root))) 'Installed bytes differ from the current source.'
    Assert-Equal (Get-FileHash -Algorithm SHA256 -LiteralPath (Get-PackageDll $Root)).Hash `
        (Get-FileHash -Algorithm SHA256 -LiteralPath (Get-InstalledDll $Root)).Hash 'Package and installed checksums differ.'
}
function Test-Case([string]$Name, [scriptblock]$Body) {
    & $Body
    $script:passed++
    Write-Host "PASS: $Name" -ForegroundColor Green
}

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $testRoot 'Run-Installer.ps1'), $runner)

    Test-Case 'Initial installation builds Release and verifies copied files' {
        $root = New-Fixture 'initial'
        $result = Invoke-Installer $root
        Assert-True $result.Succeeded $result.Message
        Assert-Equal 1 (Get-BuildCount $root) 'Initial installation must build once.'
        Assert-Installed $root 'synthetic-dll|Widget.cs:source-v1'
        $parameters = Get-Content -LiteralPath (Join-Path $root 'build-parameters.json') -Raw | ConvertFrom-Json
        Assert-Equal 'Release' $parameters.Configuration 'Installer must select Release.'
        Assert-Equal 'synthetic-pdb' ([System.IO.File]::ReadAllText([System.IO.Path]::ChangeExtension((Get-InstalledDll $root), '.pdb'))) 'Package PDB was not copied.'
    }

    Test-Case 'Unchanged source still rebuilds on every installation' {
        $root = New-Fixture 'unchanged'
        Assert-True (Invoke-Installer $root).Succeeded 'First installation failed.'
        Assert-True (Invoke-Installer $root).Succeeded 'Unchanged reinstallation failed.'
        Assert-Equal 2 (Get-BuildCount $root) 'Existing package must never bypass the build.'
        Assert-Installed $root 'synthetic-dll|Widget.cs:source-v1'
    }

    Test-Case 'Source edits with preserved timestamps install current content' {
        $root = New-Fixture 'modified'
        Assert-True (Invoke-Installer $root).Succeeded 'First installation failed.'
        $source = Get-Item -LiteralPath (Join-Path $root 'src\Widget.cs')
        $timestamp = $source.LastWriteTimeUtc
        [System.IO.File]::WriteAllText($source.FullName, 'source-v2')
        $source.LastWriteTimeUtc = $timestamp
        Assert-True (Invoke-Installer $root).Succeeded 'Modified-source installation failed.'
        Assert-Equal 2 (Get-BuildCount $root) 'Modified source must rebuild.'
        Assert-Installed $root 'synthetic-dll|Widget.cs:source-v2'
    }

    Test-Case 'Added and deleted source files are reflected in the installed DLL' {
        $root = New-Fixture 'source-set'
        Assert-True (Invoke-Installer $root).Succeeded 'First installation failed.'
        Set-FixtureFile (Join-Path $root 'src\Added.cs') 'new-source'
        Assert-True (Invoke-Installer $root).Succeeded 'Added-source installation failed.'
        Assert-Installed $root 'synthetic-dll|Added.cs:new-source|Widget.cs:source-v1'
        Remove-Item -LiteralPath (Join-Path $root 'src\Widget.cs')
        Assert-True (Invoke-Installer $root).Succeeded 'Deleted-source installation failed.'
        Assert-Installed $root 'synthetic-dll|Added.cs:new-source'
        Assert-Equal 3 (Get-BuildCount $root) 'Every source-set change must rebuild.'
    }

    Test-Case 'A stale package newer than source cannot be reinstalled' {
        $root = New-Fixture 'stale-artifact'
        Set-FixtureFile (Get-PackageDll $root) 'stale-package'
        (Get-Item -LiteralPath (Get-PackageDll $root)).LastWriteTimeUtc = [datetime]'2099-01-01T00:00:00Z'
        Set-FixtureFile (Get-InstalledDll $root) 'previous-installation'
        Assert-True (Invoke-Installer $root).Succeeded 'Installation with stale artifacts failed.'
        Assert-Equal 1 (Get-BuildCount $root) 'A newer artifact must still rebuild.'
        Assert-Installed $root 'synthetic-dll|Widget.cs:source-v1'
    }

    foreach ($failureMode in @('throw', 'exit', 'missing')) {
        Test-Case "$failureMode build failure preserves existing installation and settings" {
            $root = New-Fixture ('existing-failure-' + $failureMode)
            Set-FixtureFile (Get-PackageDll $root) 'stale-package'
            Set-FixtureFile (Get-InstalledDll $root) 'previous-installation'
            $settings = Join-Path (Split-Path -Parent (Get-InstalledDll $root)) 'settings.json'
            Set-FixtureFile $settings '{"synthetic":true}'
            Set-FixtureFile (Join-Path $root 'build-mode.txt') $failureMode
            $result = Invoke-Installer $root
            Assert-True (-not $result.Succeeded) "$failureMode must stop installation."
            Assert-Equal 1 (Get-BuildCount $root) 'The failing build was not invoked.'
            Assert-Equal 'previous-installation' ([System.IO.File]::ReadAllText((Get-InstalledDll $root))) 'A failed build replaced the installed DLL.'
            Assert-Equal '{"synthetic":true}' ([System.IO.File]::ReadAllText($settings)) 'A failed build modified settings.'
        }
        Test-Case "$failureMode build failure creates no initial installation" {
            $root = New-Fixture ('initial-failure-' + $failureMode)
            Set-FixtureFile (Join-Path $root 'build-mode.txt') $failureMode
            $result = Invoke-Installer $root
            Assert-True (-not $result.Succeeded) "$failureMode must stop initial installation."
            Assert-True (-not (Test-Path -LiteralPath (Join-Path $root 'widgets'))) 'A failed build created the destination.'
        }
    }

    Test-Case 'Running WigiDash Manager blocks both build and installation' {
        $root = New-Fixture 'manager-running'
        Set-FixtureFile (Join-Path $root 'manager-running') 'running'
        $result = Invoke-Installer $root
        Assert-True (-not $result.Succeeded) 'Running Manager must block installation.'
        Assert-True ($result.Message -match 'WigiDash Manager.*running') 'Expected the Manager shutdown instruction.'
        Assert-Equal 0 (Get-BuildCount $root) 'Running Manager should be detected before building.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $root 'widgets'))) 'Running Manager allowed destination writes.'
    }

    Test-Case 'Manager starting during build blocks destination writes' {
        $root = New-Fixture 'manager-after-build'
        Set-FixtureFile (Join-Path $root 'build-mode.txt') 'manager-after'
        Set-FixtureFile (Get-InstalledDll $root) 'previous-installation'
        $result = Invoke-Installer $root
        Assert-True (-not $result.Succeeded) 'Manager starting during build must block installation.'
        Assert-Equal 1 (Get-BuildCount $root) 'Expected the build to run before Manager started.'
        Assert-Equal 'previous-installation' ([System.IO.File]::ReadAllText((Get-InstalledDll $root))) 'Manager start allowed destination writes.'
    }

    Test-Case 'WhatIf creates neither a build nor an installation' {
        $root = New-Fixture 'what-if'
        $result = Invoke-Installer $root -WhatIfInstall
        Assert-True $result.Succeeded $result.Message
        Assert-Equal 0 (Get-BuildCount $root) 'WhatIf unexpectedly invoked Build.ps1.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $root 'artifacts'))) 'WhatIf created build artifacts.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $root 'widgets'))) 'WhatIf created the destination.'
    }

    Test-Case 'Custom WigiDash framework path is forwarded to Build.ps1' {
        $root = New-Fixture 'custom-framework'
        $customPath = Join-Path $root 'custom framework path'
        $result = Invoke-Installer $root -CustomManagerPath $customPath
        Assert-True $result.Succeeded $result.Message
        $parameters = Get-Content -LiteralPath (Join-Path $root 'build-parameters.json') -Raw | ConvertFrom-Json
        Assert-Equal $customPath $parameters.WigiDashManagerPath 'Custom framework path was lost.'
        Assert-Installed $root 'synthetic-dll|Widget.cs:source-v1'
    }

    Test-Case 'Post-install SHA-256 verification rejects corrupted copied bytes' {
        $root = New-Fixture 'checksum-mismatch'
        $result = Invoke-Installer $root -CorruptCopy
        Assert-True (-not $result.Succeeded) 'Corrupted copy was reported as a successful install.'
        Assert-True ($result.Message -match 'checksum') 'Failure must identify the checksum mismatch.'
        Assert-Equal 'synthetic-dll|Widget.cs:source-v1' ([System.IO.File]::ReadAllText((Get-PackageDll $root))) 'Corruption fixture changed the source package.'
        Assert-Equal 'corrupted-copy' ([System.IO.File]::ReadAllText((Get-InstalledDll $root))) 'Corruption fixture did not exercise copied bytes.'
    }

    Test-Case 'An earlier command failure does not poison a successful build' {
        $root = New-Fixture 'previous-exit-code'
        $result = Invoke-Installer $root -PreviousExitCode 47
        Assert-True $result.Succeeded $result.Message
        Assert-Equal 1 (Get-BuildCount $root) 'Expected one successful build after the previous failure.'
        Assert-Installed $root 'synthetic-dll|Widget.cs:source-v1'
    }

    Write-Host "Installer regression checks passed: $passed scenarios (PowerShell $($PSVersionTable.PSVersion))." -ForegroundColor Green
} finally {
    # Resolve and validate the exact harness directory before recursive cleanup.
    if (Test-Path -LiteralPath $testRoot) {
        $resolvedRoot = (Resolve-Path -LiteralPath $testRoot).ProviderPath
        $expectedRoot = [System.IO.Path]::GetFullPath($testRoot)
        $resolvedParent = [System.IO.Path]::GetDirectoryName($resolvedRoot).TrimEnd('\', '/')
        if ($resolvedRoot -ne $expectedRoot -or $resolvedParent -ne $temporaryParent.TrimEnd('\', '/') -or
            [System.IO.Path]::GetFileName($resolvedRoot) -notmatch '^AWIT-[0-9a-f]{12}$') {
            throw "Refusing to remove unexpected test directory: $resolvedRoot"
        }
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
