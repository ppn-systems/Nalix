<#
.SYNOPSIS
Builds the Backend application for Raspberry Pi 5 (Linux ARM64).

.DESCRIPTION
This script uses 'dotnet publish' to create a self-contained, single-file executable
that can run on a Raspberry Pi 5 without requiring the .NET SDK to be installed.
#>

$ErrorActionPreference = "Stop"

# Define paths and architecture
$ProjectFile = "$PSScriptRoot\Backend.csproj"
$OutputFolder = "$PSScriptRoot\bin\Release\net10.0\linux-arm64\publish"
$Runtime = "linux-arm64"

Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host " Building Nalix Backend for Raspberry Pi 5 ($Runtime)" -ForegroundColor Cyan
Write-Host "===========================================================" -ForegroundColor Cyan

# Publish as Self-Contained Single File
dotnet publish $ProjectFile `
    -c Release `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Copy monitoring script to publish folder
Copy-Item "$PSScriptRoot\run-backend-pi.sh" -Destination "$OutputFolder\run-backend-pi.sh" -Force

# Copy server.ini to publish folder so it can be pushed to Pi
$SourceIni = "C:\ProgramData\Nalix\config\server.ini"
if (Test-Path $SourceIni) {
    Copy-Item $SourceIni -Destination "$OutputFolder\server.ini" -Force
}

Write-Host "`n===========================================================" -ForegroundColor Green
Write-Host " Build succeeded! Output is located at:" -ForegroundColor Green
Write-Host " $OutputFolder" -ForegroundColor Yellow
Write-Host "===========================================================" -ForegroundColor Green

Write-Host "`nTo transfer and run on Raspberry Pi, use these commands:" -ForegroundColor Cyan
Write-Host "1. Upload files (replace <USER> and <IP>):"
Write-Host "   scp `"$OutputFolder\Backend`" `"$OutputFolder\run-backend-pi.sh`" `"$OutputFolder\server.ini`" <USER>@<IP>:~/" -ForegroundColor Yellow
Write-Host "2. SSH into your Raspberry Pi:"
Write-Host "   ssh <USER>@<IP>" -ForegroundColor Yellow
Write-Host "3. Make the monitoring script executable and run it:"
Write-Host "   chmod +x ~/run-backend-pi.sh" -ForegroundColor Yellow
Write-Host "   ./run-backend-pi.sh" -ForegroundColor Yellow
