# StreamShield Installer
# Usage: powershell -ExecutionPolicy Bypass -File install.ps1
# Add -Startup to also launch StreamShield on Windows startup.
# Add -FrameworkDependent for a smaller exe (~5MB) that requires .NET 10 runtime.

#Requires -Version 5.1
param(
    [switch]$Startup,
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$project = Join-Path $repoRoot 'src\StreamShield\StreamShield.csproj'
$publishDir = Join-Path $repoRoot 'publish'
$installDir = Join-Path $env:LOCALAPPDATA 'StreamShield'
$exeName = 'StreamShield.exe'

# Publish
Write-Host "Publishing StreamShield..." -ForegroundColor Cyan
if ($FrameworkDependent) {
    Write-Host "(Framework-dependent mode - requires .NET 10 runtime)" -ForegroundColor Yellow
    dotnet publish $project -c Release -r win-x64 `
        -p:PublishSingleFile=true -p:SelfContained=false -o $publishDir
} else {
    dotnet publish $project -c Release -r win-x64 --self-contained `
        -p:PublishSingleFile=true -o $publishDir
}
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

# Install (exe + native DLLs required by WPF/UIAutomation)
if (!(Test-Path $installDir)) { New-Item -ItemType Directory -Path $installDir | Out-Null }
Copy-Item (Join-Path $publishDir '*.exe') $installDir -Force
Copy-Item (Join-Path $publishDir '*.dll') $installDir -Force
Write-Host "Installed to: $installDir" -ForegroundColor Green

# Desktop shortcut
$shell = New-Object -ComObject WScript.Shell
$desktopPath = [Environment]::GetFolderPath('Desktop')
$lnk = $shell.CreateShortcut((Join-Path $desktopPath 'StreamShield.lnk'))
$lnk.TargetPath = Join-Path $installDir $exeName
$lnk.WorkingDirectory = $installDir
$lnk.Description = 'StreamShield - Sensitive field protection'
$lnk.IconLocation = (Join-Path $installDir $exeName) + ',0'
$lnk.Save()
Write-Host "Desktop shortcut created." -ForegroundColor Green

# Start Menu shortcut (makes app findable via Windows Search)
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$lnk = $shell.CreateShortcut((Join-Path $startMenuDir 'StreamShield.lnk'))
$lnk.TargetPath = Join-Path $installDir $exeName
$lnk.WorkingDirectory = $installDir
$lnk.Description = 'StreamShield - Sensitive field protection'
$lnk.IconLocation = (Join-Path $installDir $exeName) + ',0'
$lnk.Save()
Write-Host "Start Menu shortcut created (findable via Windows Search)." -ForegroundColor Green

# Startup shortcut (optional)
if ($Startup) {
    $startupPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'
    $lnk = $shell.CreateShortcut((Join-Path $startupPath 'StreamShield.lnk'))
    $lnk.TargetPath = Join-Path $installDir $exeName
    $lnk.WorkingDirectory = $installDir
    $lnk.Description = 'StreamShield - Sensitive field protection'
    $lnk.IconLocation = (Join-Path $installDir $exeName) + ',0'
    $lnk.Save()
    Write-Host "Startup shortcut created (launches on login)." -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Double-click 'StreamShield' on your desktop to launch." -ForegroundColor Cyan
