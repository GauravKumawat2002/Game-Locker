# GameLocker - Complete Build & Package Script
# This script builds everything and creates a proper installer

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('ZIP', 'EXE', 'Both')]
    [string]$BuildType = 'Both'
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  GameLocker Complete Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$projectRoot = "h:\Work\10xProductivity\game_locker\code"
$outputRoot = "h:\Work\10xProductivity\game_locker"

# Step 1: Clean
Write-Host "[1/6] Cleaning previous builds..." -ForegroundColor Yellow
Set-Location $projectRoot
dotnet clean GameLocker.sln -c Release | Out-Null

# Step 2: Build Service
Write-Host "[2/6] Building GameLocker Service (self-contained)..." -ForegroundColor Yellow
dotnet publish src/GameLocker.Service/GameLocker.Service.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o "installer-package/publish/service" `
    | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Service build failed!" -ForegroundColor Red
    exit 1
}

# Step 3: Build ConfigUI
Write-Host "[3/6] Building GameLocker ConfigUI (self-contained)..." -ForegroundColor Yellow
dotnet publish src/GameLocker.ConfigUI/GameLocker.ConfigUI.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o "installer-package/publish/configui" `
    | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: ConfigUI build failed!" -ForegroundColor Red
    exit 1
}

# Step 4: Copy installer scripts
Write-Host "[4/6] Copying installer scripts..." -ForegroundColor Yellow
Copy-Item "installer/PowerShell/GameLocker-Setup.ps1" -Destination "installer-package/GameLocker-Setup.ps1" -Force
Copy-Item "installer/PowerShell/Package-Installer.ps1" -Destination "installer-package/Package-Installer.ps1" -Force

# Step 5: Create ZIP installer
if ($BuildType -eq 'ZIP' -or $BuildType -eq 'Both') {
    Write-Host "[5/6] Creating ZIP installer..." -ForegroundColor Yellow
    $zipPath = Join-Path $outputRoot "GameLocker-Installer-v1.1.0.zip"
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path "installer-package\*" -DestinationPath $zipPath -Force
    
    $zipFile = Get-Item $zipPath
    Write-Host "  ✓ ZIP created: $($zipFile.Name) ($([math]::Round($zipFile.Length/1MB,2)) MB)" -ForegroundColor Green
}

# Step 6: Create EXE installer (if Inno Setup is installed)
if ($BuildType -eq 'EXE' -or $BuildType -eq 'Both') {
    Write-Host "[6/6] Creating EXE installer..." -ForegroundColor Yellow
    
    $innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    
    if (Test-Path $innoSetupPath) {
        & $innoSetupPath "installer\InnoSetup\GameLocker-Setup.iss" | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            $exeFile = Get-Item (Join-Path $outputRoot "GameLocker-Installer-v1.1.0.exe")
            Write-Host "  ✓ EXE created: $($exeFile.Name) ($([math]::Round($exeFile.Length/1MB,2)) MB)" -ForegroundColor Green
        } else {
            Write-Host "  ✗ EXE build failed!" -ForegroundColor Red
        }
    } else {
        Write-Host "  ⚠ Inno Setup not found. Skipping EXE installer." -ForegroundColor Yellow
        Write-Host "    Download from: https://jrsoftware.org/isdl.php" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output files in: $outputRoot" -ForegroundColor Cyan
Write-Host ""
