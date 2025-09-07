# Builds and packs NuGet packages for the solution.
# Usage: pwsh .\pack.ps1
# Optional: pwsh .\pack.ps1 -Solution ".\src\Nalix.sln" -Configuration Release -OutDir ".\artifacts\nuget" -Pause

[CmdletBinding()]
param(
    [string]$Solution = ".\src\Nalix.sln",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutDir = ".\artifacts\nuget",
    [switch]$Pause
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

try {
    if (-not (Test-Path -LiteralPath $Solution)) {
        throw "Solution not found: $Solution"
    }

    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

    Write-Host "Packing solution: $Solution"
    Write-Host "Configuration: $Configuration"
    Write-Host "Output: $OutDir"
    Write-Host ""

    dotnet restore $Solution
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE" }

    dotnet pack $Solution --configuration $Configuration --output $OutDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE" }

    Write-Host ""
    Write-Host "Done. Packages:"
    Get-ChildItem -LiteralPath $OutDir -Filter *.nupkg -File | ForEach-Object { Write-Host ("- " + $_.FullName) }
    Get-ChildItem -LiteralPath $OutDir -Filter *.snupkg -File -ErrorAction SilentlyContinue | ForEach-Object { Write-Host ("- " + $_.FullName) }
}
finally {
    if ($Pause.IsPresent) {
        Write-Host ""
        Read-Host "Press Enter to exit"
    }
}