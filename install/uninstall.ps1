# uninstall.ps1
# Removes the Blocky startup entry and the Chrome native-messaging host registration.

$AppName = "Blocky"
$HostName = "com.blocky.host"

$RunKeyPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$NativeHostKeyPath = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\$HostName"

$ScriptDir = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$ManifestPath = Join-Path $ScriptDir "$HostName.json"

try {
    if (Get-ItemProperty -Path $RunKeyPath -Name $AppName -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $RunKeyPath -Name $AppName -Force
        Write-Host "Removed $AppName from Windows startup."
    } else {
        Write-Host "$AppName is not in Windows startup."
    }

    if (Test-Path $NativeHostKeyPath) {
        Remove-Item -Path $NativeHostKeyPath -Recurse -Force
        Write-Host "Removed Chrome native-messaging host registration."
    } else {
        Write-Host "Native-messaging host was not registered."
    }

    if (Test-Path $ManifestPath) {
        Remove-Item -Path $ManifestPath -Force
        Write-Host "Removed host manifest: $ManifestPath"
    }
} catch {
    Write-Error "Uninstall failed: $_"
    exit 1
}
