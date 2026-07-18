# install.ps1
# Installs Blocky:
#   1. Adds Blocky.exe to Windows startup (HKCU Run key)
#   2. Registers Blocky.Host.exe as a Chrome native-messaging host so the
#      Blocky extension can talk to it (no TCP port involved)
# Run from the folder that contains Blocky.exe and Blocky.Host.exe. No elevation needed.

$ErrorActionPreference = "Stop"

$AppName = "Blocky"
$HostName = "com.blocky.host"
# Stable ID of the Blocky extension, pinned by the "key" field in the extension's manifest.json.
$ExtensionId = "dnmkjkbjklkbjakdjhfjpcjknglifnmb"

$RunKeyPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$NativeHostKeyPath = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\$HostName"

$ScriptDir = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$ExePath = Join-Path $ScriptDir "Blocky.exe"
$HostExePath = Join-Path $ScriptDir "Blocky.Host.exe"

if (!(Test-Path $ExePath)) {
    Write-Error "Could not find Blocky.exe in the script directory: $ScriptDir"
    exit 1
}
if (!(Test-Path $HostExePath)) {
    Write-Error "Could not find Blocky.Host.exe in the script directory: $ScriptDir"
    exit 1
}

# 1. Startup entry
Set-ItemProperty -Path $RunKeyPath -Name $AppName -Value "`"$ExePath`"" -Type String -Force
Write-Host "Added $AppName to Windows startup: $ExePath"

# 2. Native-messaging host manifest + registry key
$ManifestPath = Join-Path $ScriptDir "$HostName.json"
$Manifest = [ordered]@{
    name            = $HostName
    description     = "Blocky native messaging host"
    path            = $HostExePath
    type            = "stdio"
    allowed_origins = @("chrome-extension://$ExtensionId/")
}
$Manifest | ConvertTo-Json | Set-Content -Path $ManifestPath -Encoding UTF8
Write-Host "Wrote native-messaging host manifest: $ManifestPath"

New-Item -Path $NativeHostKeyPath -Force | Out-Null
Set-ItemProperty -Path $NativeHostKeyPath -Name "(Default)" -Value $ManifestPath
Write-Host "Registered native-messaging host for Chrome: $NativeHostKeyPath"

Write-Host ""
Write-Host "Done. Load the extension from extensions/chrome (chrome://extensions -> Load unpacked)."
Write-Host "Its ID must be $ExtensionId (pinned by the manifest 'key')."
