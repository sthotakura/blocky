# Install-BlockyStartup.ps1
# Adds the Blocky application to Windows startup

$AppName = "Blocky"
$RegistryPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$ScriptDir = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$ExePath = Join-Path -Path $ScriptDir -ChildPath "Blocky.exe"

# Validate the executable path
if (!(Test-Path $ExePath)) {
    Write-Error "Could not find Blocky.exe in the script directory: $ScriptDir"
    exit 1
}

# Add the application to the registry
try {
    Set-ItemProperty -Path $RegistryPath -Name $AppName -Value "`"$ExePath`"" -Type String -Force
    Write-Host "Successfully added $AppName to Windows startup."
    Write-Host "Path: $ExePath"
} catch {
    Write-Error "Failed to add $AppName to startup: $_"
    exit 1
}