<#
  Writes a toolkit from extensions/template, the same way "Toolkits > Create" does in Revit.
  Used by CI to check that the template builds for every Revit version; handy for testing too.
    pwsh extensions/tools/new-toolkit.ps1 -Name "Test Kit" -Id TestKit -Root C:\temp\kits
#>
param(
    [Parameter(Mandatory)] [string]$Name,
    [Parameter(Mandatory)] [string]$Id,
    [Parameter(Mandatory)] [string]$Root
)
$ErrorActionPreference = 'Stop'
$template = (Resolve-Path (Join-Path $PSScriptRoot '..\template')).Path
$target = Join-Path $Root $Id
if (Test-Path $target) { throw "$target already exists" }
$values = @{
    '__TOOLKIT_ID__'   = $Id
    '__TOOLKIT_NAME__' = $Name
    '__AUTHOR__'       = [Environment]::UserName
    '__DATE__'         = (Get-Date -Format 'yyyy-MM-dd')
}
Get-ChildItem $template -Recurse -File -Force | ForEach-Object {
    $relative = $_.FullName.Substring($template.Length + 1)
    $text = [IO.File]::ReadAllText($_.FullName)
    foreach ($k in $values.Keys) { $relative = $relative.Replace($k, $values[$k]); $text = $text.Replace($k, $values[$k]) }
    $text = $text.Replace("`r`n", "`n").Replace("`n", "`r`n")
    $path = Join-Path $target $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    $bom = @('.cs', '.xaml', '.ps1') -contains [IO.Path]::GetExtension($path).ToLowerInvariant()
    [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($bom)))
}
Write-Host "Toolkit written to $target"
