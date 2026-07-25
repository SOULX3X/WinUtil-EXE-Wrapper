<#
.SYNOPSIS
    Builds the WinUtil wrapper as a standalone single-file EXE.

.DESCRIPTION
    Produces a self-contained, single-file WinUtil.exe (~70 MB) that
    bundles everything — no .NET runtime install needed on the target
    machine, no extra DLLs. winutil.ps1 is auto-updated from GitHub
    on launch and run as Administrator with the PowerShell console
    window hidden (only winutil's WPF UI is visible).

    Use -FrameworkDependent for a tiny framework-dependent build.

.PARAMETER Configuration
    Debug or Release. Default: Release.

.PARAMETER Runtime
    Target RID. Default: win-x64.

.PARAMETER FrameworkDependent
    Build a small framework-dependent app that needs .NET 8 installed,
    instead of the default self-contained single-file EXE.

.PARAMETER Output
    Output directory for the publish result. Default: publish.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -FrameworkDependent
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [switch]$FrameworkDependent,

    [string]$Output = 'publish'
)

$ErrorActionPreference = 'Stop'
$wrapperRoot = $PSScriptRoot
$publishDir = Join-Path $wrapperRoot $Output

$mode = if ($FrameworkDependent) { 'framework-dependent' } else { 'self-contained / standalone' }
Write-Host "`n==> Publishing $Configuration/$Runtime ($mode) to $publishDir" -ForegroundColor Cyan

# The project file already defaults to self-contained single-file win-x64;
# we only override it for the framework-dependent build.
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }

Push-Location $wrapperRoot
try {
    dotnet publish `
        -c $Configuration `
        -r $Runtime `
        -o $publishDir `
        --self-contained $selfContained

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
}

$targetExe = Join-Path $publishDir 'WinUtil.exe'
Write-Host "`nDone: $targetExe" -ForegroundColor Green