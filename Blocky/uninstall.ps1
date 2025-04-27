# Uninstall-BlockyStartup.ps1
# Removes the Blocky application from Windows startup

$AppName = "Blocky"
$RegistryPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"

try {
    if (Get-ItemProperty -Path $RegistryPath -Name $AppName -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $RegistryPath -Name $AppName -Force
        Write-Host "Successfully removed $AppName from Windows startup."
    } else {
        Write-Host "$AppName is not in Windows startup."
    }
} catch {
    Write-Error "Failed to remove $AppName from startup: $_"
    exit 1
}