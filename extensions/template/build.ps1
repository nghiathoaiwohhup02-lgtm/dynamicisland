<#
  WCP Extensions kit - builds this toolkit so Revit can load it. Do not edit.

  Usage (from this folder):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1              every Revit version WCP Extensions is installed for
    powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Revit 2025  only Revit 2025
    powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Revit 2024,2027

  Needs a .NET SDK (8 or newer; 10 when building for Revit 2027). If none is found, the
  .NET 10 SDK is installed once into %LOCALAPPDATA%\WCP-Extensions\dotnet (no admin rights).

  After "BUILD OK": press Reload in Revit.
#>
param(
    [string[]]$Revit
)

# 'Continue', not 'Stop': Windows PowerShell 5.1 turns any stderr line of dotnet into a
# terminating error when the output is redirected (as it is when Revit runs this script).
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }

$root = $PSScriptRoot
$project = Get-ChildItem -Path $root -Filter '*.csproj' -File | Select-Object -First 1
if (-not $project) {
    Write-Host "ERROR: no .csproj file in $root"
    exit 1
}

# ---- Which Revit versions -------------------------------------------------------------
$supported = @('2024', '2025', '2026', '2027')
$years = @()
foreach ($item in @($Revit)) {
    if ($item) { $years += ($item -split '[,; ]') | Where-Object { $_ } }
}
if ($years.Count -eq 0) {
    $years = @($supported | Where-Object { Test-Path (Join-Path $env:APPDATA "Autodesk\Revit\Addins\$_\WcpExtensions.addin") })
}
if ($years.Count -eq 0) {
    $years = @($supported | Where-Object { Test-Path (Join-Path $env:ProgramFiles "Autodesk\Revit $_\Revit.exe") })
}
if ($years.Count -eq 0) { $years = @('2025') }
foreach ($y in $years) {
    if ($supported -notcontains $y) {
        Write-Host "ERROR: Revit $y is not supported. Use 2024, 2025, 2026 or 2027."
        exit 1
    }
}

# ---- Find a .NET SDK ------------------------------------------------------------------
$needMajor = 8
if ($years -contains '2027') { $needMajor = 10 }

function Get-SdkMajor([string]$exe) {
    if (-not $exe -or -not (Test-Path $exe)) { return 0 }
    try {
        $best = 0
        foreach ($line in (& $exe --list-sdks 2>$null)) {
            if ($line -match '^(\d+)\.') {
                $major = [int]$Matches[1]
                if ($major -gt $best) { $best = $major }
            }
        }
        return $best
    } catch { return 0 }
}

$privateDir = Join-Path $env:LOCALAPPDATA 'WCP-Extensions\dotnet'
$candidates = @(
    (Join-Path $privateDir 'dotnet.exe'),
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe')
)
$onPath = Get-Command dotnet -ErrorAction SilentlyContinue
if ($onPath) { $candidates += $onPath.Path }

$dotnet = $null
foreach ($c in $candidates) {
    if ((Get-SdkMajor $c) -ge $needMajor) { $dotnet = $c; break }
}

if (-not $dotnet) {
    Write-Host "No .NET SDK $needMajor+ found. Installing .NET SDK 10 (one time, about 250 MB)"
    Write-Host "into $privateDir ..."
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        [Net.WebRequest]::DefaultWebProxy.Credentials = [Net.CredentialCache]::DefaultNetworkCredentials
        $installer = Join-Path $env:TEMP 'wcp-dotnet-install.ps1'
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing -ErrorAction Stop
        & $installer -Channel 10.0 -InstallDir $privateDir -NoPath -ErrorAction Stop
    } catch {
        Write-Host "ERROR: could not install the .NET SDK: $($_.Exception.Message)"
        Write-Host "Install the .NET 10 SDK yourself from https://dotnet.microsoft.com/download and build again."
        exit 1
    }
    $dotnet = Join-Path $privateDir 'dotnet.exe'
    if ((Get-SdkMajor $dotnet) -lt $needMajor) {
        Write-Host "ERROR: .NET SDK install did not finish. Build again, or install the .NET 10 SDK yourself."
        exit 1
    }
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:MSBUILDDISABLENODEREUSE = '1'

# ---- Build ----------------------------------------------------------------------------
# One stamp per run: every build gets a new assembly name (Toolkit_b<stamp>.dll), so
# Revit can load it next to the old one when you press Reload.
$stamp = Get-Date -Format 'yyMMddHHmmss'
$failed = @()
Write-Host "Toolkit $($project.BaseName) - Revit $($years -join ', ')"
foreach ($y in $years) {
    Write-Host ""
    Write-Host "== Revit $y"
    & $dotnet build $project.FullName -c Release "-p:RevitVersion=$y" "-p:BuildStamp=$stamp" -nologo -v:minimal -nodeReuse:false "-clp:NoSummary"
    if ($LASTEXITCODE -ne 0) { $failed += $y }
}

Write-Host ""
if ($failed.Count -gt 0) {
    Write-Host "BUILD FAILED for Revit $($failed -join ', '). Fix the errors above and build again."
    exit 1
}
Write-Host "BUILD OK (Revit $($years -join ', ')). Press Reload in Revit to load it."
exit 0
