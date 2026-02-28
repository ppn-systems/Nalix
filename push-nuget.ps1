<#
.SYNOPSIS
Pushes NuGet packages (.nupkg) to a NuGet feed using nuget.exe.

.DESCRIPTION
- Reads API key from a local file (api.txt)
- Pushes all .nupkg in the artifacts folder
- Skips symbol packages by default (.snupkg)
- Can skip duplicates (recommended for CI)

.PREREQUISITES
- nuget.exe is present next to this script (or specify -NuGetExe)
- api.txt is present next to this script (or specify -ApiKeyFile)
- Packages exist in the artifacts folder (default: .\artifacts\nuget)

.USAGE
pwsh .\push-nuget.ps1
pwsh .\push-nuget.ps1 -Source "https://api.nuget.org/v3/index.json"
pwsh .\push-nuget.ps1 -WhatIf
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $false)]
    [string]$PackagesDir = ".\artifacts\nuget",

    [Parameter(Mandatory = $false)]
    [string]$Source = "https://api.nuget.org/v3/index.json",

    [Parameter(Mandatory = $false)]
    [string]$NuGetExe = ".\tools\nuget.exe",

    [Parameter(Mandatory = $false)]
    [string]$ApiKeyFile = ".\tools\nuget.key",

    [Parameter(Mandatory = $false)]
    [switch]$SkipDuplicates,

    [Parameter(Mandatory = $false)]
    [switch]$IncludeSymbols
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Section {
    param([Parameter(Mandatory = $true)][string]$Title)
    Write-Host ""
    Write-Host ("=" * 72) -ForegroundColor DarkGray
    Write-Host ("  " + $Title) -ForegroundColor Cyan
    Write-Host ("=" * 72) -ForegroundColor DarkGray
}

function Assert-FileExists {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string]$Name)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Name not found: $Path"
    }
}

try {
    Write-Section "Nalix NuGet Push"

    Assert-FileExists -Path $NuGetExe -Name "nuget.exe"
    Assert-FileExists -Path $ApiKeyFile -Name "API key file (api.txt)"

    if (-not (Test-Path -LiteralPath $PackagesDir)) {
        throw "Packages directory not found: $PackagesDir"
    }

    $apiKey = (Get-Content -LiteralPath $ApiKeyFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        throw "API key file is empty: $ApiKeyFile"
    }

    $pkgDirFull = (Resolve-Path -LiteralPath $PackagesDir).Path
    $nugetFull = (Resolve-Path -LiteralPath $NuGetExe).Path

    Write-Host "PackagesDir : $pkgDirFull"
    Write-Host "Source      : $Source"
    Write-Host "nuget.exe    : $nugetFull"
    Write-Host "SkipDuplicates: $($SkipDuplicates.IsPresent)"
    Write-Host "IncludeSymbols: $($IncludeSymbols.IsPresent)"

    Write-Section "Discover Packages"

    $packages = @(Get-ChildItem -LiteralPath $pkgDirFull -File |
    	Where-Object {
    	    $_.Extension -ieq ".nupkg" -and
     	   ($IncludeSymbols.IsPresent -or $_.Name -notmatch '\.symbols\.nupkg$') -and
    	    $_.Name -notmatch '\.snupkg$'
   	 } |
    	Sort-Object Name)

    if ($packages.Count -eq 0) {
        Write-Host "No .nupkg packages found in: $pkgDirFull" -ForegroundColor Yellow
        exit 0
    }

    foreach ($p in $packages) {
        Write-Host ("- " + $p.Name) -ForegroundColor Green
    }

    Write-Section "Push"

    foreach ($p in $packages) {
        $args = @(
            "push",
            $p.FullName,
            "-ApiKey", $apiKey,
            "-Source", $Source,
            "-NonInteractive"
        )

        if ($SkipDuplicates.IsPresent) {
            # nuget.exe supports -SkipDuplicate (singular)
            $args += "-SkipDuplicate"
        }

        $cmdLine = "nuget.exe " + ($args | ForEach-Object {
                if ($_ -match '\s') { '"' + $_ + '"' } else { $_ }
            } | Join-String -Separator " ")

        if ($PSCmdlet.ShouldProcess($p.FullName, "Push package to $Source")) {
            Write-Host ">> $cmdLine" -ForegroundColor DarkGray
            & $nugetFull @args
            if ($LASTEXITCODE -ne 0) {
                throw "Push failed (exit $LASTEXITCODE): $($p.Name)"
            }
        }
    }

    Write-Host ""
    Write-Host "Done." -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}