<#
.SYNOPSIS
    Cross-platform Native AOT build script for Nalix Backend.
    Works on Windows (PowerShell 5.1+/7+) and Linux (pwsh 7+).

.DESCRIPTION
    Publishes the Backend project as a Native AOT single-file executable.
    No .NET runtime required on the target machine.

.PARAMETER Runtime
    Target runtime identifier: win-x64 | linux-x64 | linux-arm64
    Default: auto-detects the current OS.

.PARAMETER Output
    Custom output directory. Default: ./publish/<RID>

.PARAMETER Clean
    Remove obj/bin/publish folders before building.

.PARAMETER SkipTrim
    Disable IL trimming (useful for debugging AOT warnings).

.EXAMPLE
    # Windows — build for current OS
    .\build-aot.ps1

    # Linux — cross-compile for ARM64 (e.g. Raspberry Pi)
    pwsh ./build-aot.ps1 -Runtime linux-arm64

    # Clean build for linux-x64
    pwsh ./build-aot.ps1 -Runtime linux-x64 -Clean
#>

[CmdletBinding()]
param(
    [ValidateSet("win-x64", "linux-x64", "linux-arm64")]
    [string]$Runtime,

    [string]$Output,

    [switch]$Clean,

    [switch]$SkipTrim
)

# ── Strict mode ──────────────────────────────────────────────────────
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ── Auto-detect RID if not specified ─────────────────────────────────
if (-not $Runtime) {
    $Runtime = switch ([System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier) {
        { $_ -like "win*" }   { "win-x64" }
        { $_ -like "linux*" } { "linux-x64" }
        default {
            Write-Warning "Could not auto-detect runtime. Defaulting to linux-x64."
            "linux-x64"
        }
    }
}

# ── Paths ────────────────────────────────────────────────────────────
$ProjectFile = Join-Path $PSScriptRoot "Backend.csproj"
$PublishDir  = if ($Output) { $Output } else { Join-Path $PSScriptRoot "publish" $Runtime }

# ── Banner ───────────────────────────────────────────────────────────
$banner = @"
===========================================================
  Nalix Backend — Native AOT Build
  Runtime  : $Runtime
  Output   : $PublishDir
  Config   : Release
  Trim     : $(if ($SkipTrim) { 'OFF' } else { 'ON' })
===========================================================
"@
Write-Host $banner -ForegroundColor Cyan

# ── Clean ────────────────────────────────────────────────────────────
if ($Clean) {
    Write-Host "[clean] Removing obj / bin / publish ..." -ForegroundColor Yellow
    @("obj", "bin", "publish") | ForEach-Object {
        $p = Join-Path $PSScriptRoot $_
        if (Test-Path $p) { Remove-Item $p -Recurse -Force }
    }
}

# ── Build args ───────────────────────────────────────────────────────
# We use /p:AotBuild=true — a LOCAL property consumed only by Backend.csproj.
# All AOT/trim/ILC knobs are declared in the csproj to avoid leaking global
# properties to netstandard2.0 analyzer projects (which causes NETSDK1124).
$publishArgs = @(
    "publish", $ProjectFile,
    "-c", "Release",
    "-r", $Runtime,
    "--self-contained", "true",
    "-o", $PublishDir,
    "/p:AotBuild=true",
    "/p:PublishSingleFile=true",
    "/p:EnableCompressionInSingleFile=true"
)

# Optionally disable trimming (for AOT warning diagnostics)
if ($SkipTrim) {
    $publishArgs += "/p:NoTrim=true"
}

Write-Host "`n[build] dotnet $($publishArgs -join ' ')" -ForegroundColor DarkGray

# ── Publish ──────────────────────────────────────────────────────────
$sw = [System.Diagnostics.Stopwatch]::StartNew()
& dotnet @publishArgs
$exitCode = $LASTEXITCODE
$sw.Stop()

if ($exitCode -ne 0) {
    Write-Host "`n[FAILED] Build failed with exit code $exitCode" -ForegroundColor Red
    exit $exitCode
}

# ── Summary ──────────────────────────────────────────────────────────
$binaryName = if ($Runtime -like "win*") { "Backend.exe" } else { "Backend" }
$binaryPath = Join-Path $PublishDir $binaryName
$sizeMB     = if (Test-Path $binaryPath) {
    [math]::Round((Get-Item $binaryPath).Length / 1MB, 2)
} else { "N/A" }

Write-Host @"

===========================================================
  BUILD SUCCEEDED  ($([math]::Round($sw.Elapsed.TotalSeconds, 1))s)
  Binary : $binaryPath
  Size   : $sizeMB MB
===========================================================
"@ -ForegroundColor Green

Write-Host "`nDone. The binary is fully self-contained — no .NET runtime needed on the target." -ForegroundColor Cyan