# GameLocker Package Builder
# This script creates a complete installer package

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "..\..\installer-package"
)

Write-Host "GameLocker Package Builder v1.0" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)

# Create output directory
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

Write-Host "Building release versions..." -ForegroundColor Cyan

# Build and publish service
Set-Location $projectRoot
dotnet publish "src/GameLocker.Service/GameLocker.Service.csproj" -c Release -r win-x64 --self-contained --output "publish/service" | Out-Host

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build service!" -ForegroundColor Red
    exit 1
}

# Build and publish config UI
dotnet publish "src/GameLocker.ConfigUI/GameLocker.ConfigUI.csproj" -c Release -r win-x64 --self-contained --output "publish/configui" | Out-Host

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build config UI!" -ForegroundColor Red
    exit 1
}

Write-Host "Copying files to installer package..." -ForegroundColor Cyan

# Copy installer files
Copy-Item -Path "$scriptDir\*" -Destination $OutputPath -Recurse -Force

# Copy published applications
New-Item -ItemType Directory -Path "$OutputPath\publish" -Force | Out-Null
Copy-Item -Path "$projectRoot\publish\*" -Destination "$OutputPath\publish" -Recurse -Force

# Copy configuration files
Copy-Item -Path "$projectRoot\publish\*.json" -Destination "$OutputPath\publish" -Force -ErrorAction SilentlyContinue

Write-Host "Creating README for installer..." -ForegroundColor Cyan

$readme = @"
# GameLocker Installer Package

## Installation Instructions

1. Extract this package to any temporary location
2. Right-click on 'Setup.bat' and select "Run as administrator"
3. Follow the installation prompts
4. Launch "GameLocker Configuration" from the Start Menu to configure your game folders

## What Gets Installed

- GameLocker Windows Service (auto-starts with Windows)
- GameLocker Configuration UI
- All required .NET runtime dependencies
- Start Menu shortcuts
- Automatic uninstaller

## Installation Locations

- Program Files: `C:\Program Files\GameLocker\`
- Configuration Data: `%ProgramData%\GameLocker\`
- Service Logs: `C:\Program Files\GameLocker\Logs\`

## System Requirements

- Windows 10/11 (x64)
- Administrator privileges for installation
- .NET 8.0 Runtime (included in installer)

## Support

If you encounter any issues:
1. Check the service logs at: `C:\Program Files\GameLocker\Logs\`
2. Restart the GameLocker service from Services.msc
3. Run the Configuration UI as Administrator if needed

## Uninstallation

Use "Uninstall GameLocker" from the Start Menu or run:
`C:\Program Files\GameLocker\GameLocker-Uninstall.ps1` as Administrator
"@

$readme | Out-File -FilePath "$OutputPath\README.txt" -Encoding UTF8

Write-Host "Package created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Package location: $((Resolve-Path $OutputPath).Path)" -ForegroundColor Yellow
Write-Host ""
Write-Host "To install GameLocker:" -ForegroundColor Cyan
Write-Host "  1. Navigate to the package directory" -ForegroundColor Gray
Write-Host "  2. Right-click Setup.bat and 'Run as administrator'" -ForegroundColor Gray
Write-Host "  3. Follow the installation prompts" -ForegroundColor Gray
Write-Host ""