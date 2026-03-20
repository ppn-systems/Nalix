# Requires: PowerShell 7+
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DotnetArgs
)

$ErrorActionPreference = "Stop"

function Set-IfMissing([string]$name, [string]$value) {
    $current = [Environment]::GetEnvironmentVariable($name, "Process")
    if ([string]::IsNullOrWhiteSpace($current)) {
        Set-Item -Path ("Env:{0}" -f $name) -Value $value
    }
}

if ([string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
    throw "USERPROFILE is missing. Cannot infer NuGet/MSBuild default paths."
}

Set-IfMissing "APPDATA" (Join-Path $env:USERPROFILE "AppData\Roaming")
Set-IfMissing "LOCALAPPDATA" (Join-Path $env:USERPROFILE "AppData\Local")
Set-IfMissing "ProgramData" "C:\ProgramData"
Set-IfMissing "ALLUSERSPROFILE" "C:\ProgramData"
Set-IfMissing "PUBLIC" "C:\Users\Public"

if ([string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramW6432)) {
        $env:ProgramFiles = $env:ProgramW6432
    }
    else {
        $env:ProgramFiles = "C:\Program Files"
    }
}

Set-IfMissing "ProgramW6432" "C:\Program Files"
Set-IfMissing "ProgramFiles(x86)" "C:\Program Files (x86)"
Set-IfMissing "CommonProgramFiles" "C:\Program Files\Common Files"
Set-IfMissing "CommonProgramFiles(x86)" "C:\Program Files (x86)\Common Files"
Set-IfMissing "NUGET_COMMON_APPLICATION_DATA" $env:ProgramData
Set-IfMissing "NUGET_MACHINE_WIDE_CONFIG_BASE_DIRECTORY" $env:ProgramData

if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    $env:NUGET_PACKAGES = Join-Path $repoRoot ".nuget\packages"
}

if (-not $DotnetArgs -or $DotnetArgs.Count -eq 0) {
    $DotnetArgs = @("restore", "-m:1", "-p:RestoreDisableParallel=true")
}
elseif ($DotnetArgs.Count -gt 0 -and $DotnetArgs[0] -eq "restore") {
    if (-not ($DotnetArgs -contains "-m:1")) {
        $DotnetArgs += "-m:1"
    }

    if (-not ($DotnetArgs -contains "-p:RestoreDisableParallel=true")) {
        $DotnetArgs += "-p:RestoreDisableParallel=true"
    }
}

Write-Host "Using APPDATA=$env:APPDATA"
Write-Host "Using LOCALAPPDATA=$env:LOCALAPPDATA"
Write-Host "Using ProgramData=$env:ProgramData"
Write-Host "Using ALLUSERSPROFILE=$env:ALLUSERSPROFILE"
Write-Host "Using PUBLIC=$env:PUBLIC"
Write-Host "Using ProgramFiles=$env:ProgramFiles"
Write-Host "Using ProgramW6432=$env:ProgramW6432"
Write-Host "Using ProgramFiles(x86)=${env:ProgramFiles(x86)}"
Write-Host "Using CommonProgramFiles=$env:CommonProgramFiles"
Write-Host "Using CommonProgramFiles(x86)=${env:CommonProgramFiles(x86)}"
Write-Host "Using NUGET_PACKAGES=$env:NUGET_PACKAGES"
Write-Host "Using NUGET_COMMON_APPLICATION_DATA=$env:NUGET_COMMON_APPLICATION_DATA"
Write-Host "Using NUGET_MACHINE_WIDE_CONFIG_BASE_DIRECTORY=$env:NUGET_MACHINE_WIDE_CONFIG_BASE_DIRECTORY"

& dotnet @DotnetArgs
exit $LASTEXITCODE
