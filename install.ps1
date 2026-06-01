# Nalix.Shield Agent — One-line Installer for Windows
# Usage: iwr -useb https://shield.ppn.io.vn/scripts/install.ps1 | iex
# Or:    .\install.ps1 -ApiKey "YOUR_KEY" -Domain "myserver.ppn.io.vn"

param(
    [string]$ApiKey = "",
    [string]$Domain = "",
    [string]$BackendAddress = "127.0.0.1:25565"
)

$ErrorActionPreference = "Stop"

$InstallDir = "$env:ProgramFiles\NalixShield"
$GithubRepo = "ppn-systems/shield"
$ServiceName = "NalixShieldAgent"

# Parse args from iex invocation
if ($args.Count -ge 2) {
    $ApiKey = $args[0]
    $Domain = $args[1]
}

if ([string]::IsNullOrEmpty($ApiKey) -or [string]::IsNullOrEmpty($Domain)) {
    Write-Host "============================================"
    Write-Host "  Nalix.Shield Agent Installer (Windows)"
    Write-Host "============================================"
    Write-Host ""
    Write-Host "Usage (interactive):"
    Write-Host '  .\install.ps1 -ApiKey "YOUR_KEY" -Domain "myserver.ppn.io.vn"'
    Write-Host ""
    Write-Host "Usage (one-liner):"
    Write-Host '  iwr -useb https://shield.ppn.io.vn/scripts/install.ps1 | iex'
    Write-Host ""
    $ApiKey = Read-Host "Enter your API Key"
    $Domain = Read-Host "Enter your domain (e.g. myserver.ppn.io.vn)"
}

Write-Host ""
Write-Host "============================================"
Write-Host "  Nalix.Shield Agent Installer"
Write-Host "============================================"
Write-Host "  Domain:  $Domain"
Write-Host "  Backend: $BackendAddress"
Write-Host ""

# Create install directory
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Download agent binary from GitHub Releases
Write-Host "[1/4] Downloading agent binary..."
$DownloadUrl = "https://github.com/$GithubRepo/releases/latest/download/agent.exe"
$AgentPath = Join-Path $InstallDir "nalix-agent.exe"
try {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $AgentPath -UseBasicParsing
} catch {
    Write-Host "Download failed: $_"
    Write-Host "Please download manually from: https://github.com/$GithubRepo/releases"
    exit 1
}

# Write configuration
Write-Host "[2/4] Writing configuration..."
$ConfigContent = @"
[Agent]
node_address = shield.ppn.io.vn:57200
api_key = $ApiKey
hostname = $Domain
backend_address = $BackendAddress
max_tunnels = 100
eula = true
"@
$ConfigPath = Join-Path $InstallDir "server.ini"
Set-Content -Path $ConfigPath -Value $ConfigContent -Encoding UTF8

# Register as Windows Service using NSSM or sc.exe
Write-Host "[3/4] Registering Windows Service..."

# Try NSSM first (better process management), fall back to sc.exe
$nssmPath = Get-Command nssm -ErrorAction SilentlyContinue
if ($nssmPath) {
    & nssm install $ServiceName $AgentPath
    & nssm set $ServiceName AppDirectory $InstallDir
    & nssm set $ServiceName DisplayName "Nalix.Shield DDoS Protection Agent"
    & nssm set $ServiceName Description "Protects game server traffic via Nalix.Shield proxy tunnel"
    & nssm set $ServiceName Start SERVICE_AUTO_START
    & nssm set $ServiceName AppStdout (Join-Path $InstallDir "stdout.log")
    & nssm set $ServiceName AppStderr (Join-Path $InstallDir "stderr.log")
} else {
    # Fallback: use sc.exe (runs as raw Windows Service)
    sc.exe create $ServiceName binPath= "`"$AgentPath`"" start= auto DisplayName= "Nalix.Shield Agent" 2>$null
}

# Start service
Write-Host "[4/4] Starting service..."
Start-Service -Name $ServiceName -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "============================================"
Write-Host "  Installation Complete!"
Write-Host "============================================"
Write-Host "  Config:  $ConfigPath"
Write-Host "  Binary:  $AgentPath"
Write-Host "  Status:  Get-Service $ServiceName"
Write-Host ""
Write-Host "  Agent is now running and protecting your server."
Write-Host "  Manage at: https://shield.ppn.io.vn/#/dashboard"
Write-Host ""