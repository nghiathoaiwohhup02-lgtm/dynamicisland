<#
  Builds "WCP Extensions Setup <version>.exe" (one file with the add-in for Revit 2024-2027 inside).
  Needs the .NET 10 SDK. Run from anywhere:
    pwsh extensions/tools/build-setup.ps1
  Output: extensions/artifacts/
#>
param(
    [string]$Out = (Join-Path $PSScriptRoot '..\artifacts')
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$payload = Join-Path $Out 'payload'
Remove-Item $payload, (Join-Path $Out 'payload.zip'), (Join-Path $Out 'setup') -Recurse -Force -ErrorAction SilentlyContinue

$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup[0].Version
Write-Host "WCP Extensions $version"

$addin = Join-Path $root 'src\WcpExtensions\WcpExtensions.csproj'
foreach ($year in '2024', '2025', '2026', '2027') {
    Write-Host "== Add-in for Revit $year"
    $outDir = Join-Path $Out "build\$year"
    dotnet build $addin -c Release "-p:RevitVersion=$year" -o $outDir --nologo -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Add-in build failed for Revit $year" }
    New-Item -ItemType Directory -Force -Path (Join-Path $payload $year) | Out-Null
    Copy-Item (Join-Path $outDir 'WcpExtensions.dll'), (Join-Path $outDir 'WcpExtensions.pdb') (Join-Path $payload $year)
}
Set-Content -Path (Join-Path $payload 'version.txt') -Value $version -Encoding ascii

# Forward slashes in entry names on every PowerShell version.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zipPath = Join-Path $Out 'payload.zip'
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
try {
    Get-ChildItem $payload -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($payload.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entry, 'Optimal') | Out-Null
    }
} finally { $zip.Dispose() }

Write-Host "== Setup.exe"
$setupOut = Join-Path $Out 'setup'
dotnet build (Join-Path $root 'src\Setup\Setup.csproj') -c Release "-p:PayloadZip=$zipPath" -o $setupOut --nologo -v:minimal
if ($LASTEXITCODE -ne 0) { throw "Setup build failed" }
$final = Join-Path $Out "WCP Extensions Setup $version.exe"
Copy-Item (Join-Path $setupOut 'WcpExtensionsSetup.exe') $final -Force
Write-Host "OK -> $final"
