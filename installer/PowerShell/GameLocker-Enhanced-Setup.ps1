#Requires -RunAsAdministrator

<#
.SYNOPSIS
    GameLocker Enhanced Setup Script with Selective Encryption Configuration

.DESCRIPTION
    Enhanced GameLocker installer that includes:
    - Interactive selective encryption configuration
    - Safer default encryption settings
    - Checkbox-style file type selection
    - Prevention of game corruption issues

.PARAMETER InstallPath
    Installation directory (default: C:\Program Files\GameLocker)

.PARAMETER ConfigureEncryption
    Enable interactive encryption configuration (default: true)

.PARAMETER Silent
    Run in silent mode with safe defaults

.EXAMPLE
    .\GameLocker-Enhanced-Setup.ps1
    Full installation with encryption configuration

.EXAMPLE
    .\GameLocker-Enhanced-Setup.ps1 -Silent
    Silent installation with safe encryption defaults
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Program Files\GameLocker",
    
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "GameLockerService",
    
    [Parameter(Mandatory=$false)]
    [switch]$ConfigureEncryption = $true,
    
    [Parameter(Mandatory=$false)]
    [switch]$Silent = $false
)

# Import the original installer functions
$originalInstaller = Join-Path $PSScriptRoot "GameLocker-Setup.ps1"
if (-not (Test-Path $originalInstaller)) {
    throw "Original GameLocker-Setup.ps1 not found. Please ensure both files are in the same directory."
}

# Dot source the original script functions (but don't run it)
$originalContent = Get-Content $originalInstaller -Raw
# Remove the execution part and just keep the functions
$functionsOnly = $originalContent -replace '(?s)# Main installation process.*$', ''
Invoke-Expression $functionsOnly

# Enhanced configuration functions for selective encryption
function Show-EncryptionWelcome {
    Write-Host ""
    Write-Host "🎮 GameLocker Selective Encryption Configuration" -ForegroundColor Green
    Write-Host "=================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "⚠️  IMPORTANT: GameLocker v1.0.0 now supports selective encryption!" -ForegroundColor Yellow
    Write-Host "   This prevents the game corruption issues that could occur when" -ForegroundColor Yellow
    Write-Host "   encrypting ALL files (like executables, DLLs, and large assets)." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "💡 Recommendation: Only encrypt save files, configs, and user data" -ForegroundColor Cyan
    Write-Host "   to maintain game stability while protecting your progress." -ForegroundColor Cyan
    Write-Host ""
}

function Get-UserEncryptionPreferences {
    $preferences = @{
        UseSelectiveEncryption = $true
        EncryptSaveFiles = $true
        EncryptConfigFiles = $true
        EncryptUserFiles = $true
        EncryptCacheFiles = $false
        EncryptModFiles = $false
        EncryptExecutables = $false
        EncryptAssets = $false
        CustomExtensions = ""
    }
    
    if ($Silent) {
        Write-Host "Using safe encryption defaults (Silent mode)" -ForegroundColor Cyan
        return $preferences
    }
    
    Show-EncryptionWelcome
    
    Write-Host "Choose which file types to encrypt:" -ForegroundColor Cyan
    Write-Host ""
    
    # Safe options (enabled by default)
    Write-Host "✅ SAFE OPTIONS (Recommended - enabled by default):" -ForegroundColor Green
    
    $saveFiles = Get-UserChoice "   📁 Save game files (.sav, .savegame, .dat, etc.)" $preferences.EncryptSaveFiles
    $preferences.EncryptSaveFiles = $saveFiles
    
    $configFiles = Get-UserChoice "   ⚙️  Configuration files (.ini, .cfg, .json, etc.)" $preferences.EncryptConfigFiles
    $preferences.EncryptConfigFiles = $configFiles
    
    $userFiles = Get-UserChoice "   👤 User profiles (.profile, .player, .account, etc.)" $preferences.EncryptUserFiles
    $preferences.EncryptUserFiles = $userFiles
    
    Write-Host ""
    Write-Host "⚡ MODERATE RISK OPTIONS (usually safe):" -ForegroundColor Yellow
    
    $cacheFiles = Get-UserChoice "   🗃️  Cache files (.cache, .tmp, .log, etc.)" $preferences.EncryptCacheFiles
    $preferences.EncryptCacheFiles = $cacheFiles
    
    $modFiles = Get-UserChoice "   🔧 Mod files (.mod, .pak, .plugin, etc.)" $preferences.EncryptModFiles  
    $preferences.EncryptModFiles = $modFiles
    
    Write-Host ""
    Write-Host "⛔ DANGEROUS OPTIONS (NOT recommended - can cause crashes):" -ForegroundColor Red
    
    Write-Host "   ❌ Executable files (.exe, .dll) - NOT RECOMMENDED" -ForegroundColor Red
    $execChoice = Read-Host "     Enable anyway? (y/N)"
    $preferences.EncryptExecutables = ($execChoice -match '^[Yy]')
    
    Write-Host "   ❌ Asset files (.pak, .wad, .vpk) - NOT RECOMMENDED" -ForegroundColor Red  
    $assetChoice = Read-Host "     Enable anyway? (y/N)"
    $preferences.EncryptAssets = ($assetChoice -match '^[Yy]')
    
    # Custom extensions
    Write-Host ""
    Write-Host "📝 Custom file extensions to encrypt (comma-separated, e.g., .custom,.special):" -ForegroundColor Cyan
    $customExts = Read-Host "   Enter custom extensions (or press Enter to skip)"
    $preferences.CustomExtensions = $customExts.Trim()
    
    return $preferences
}

function Get-UserChoice {
    param(
        [string]$Description,
        [bool]$DefaultValue
    )
    
    $defaultText = if ($DefaultValue) { "Y/n" } else { "y/N" }
    $choice = Read-Host "$Description [$defaultText]"
    
    if ([string]::IsNullOrWhiteSpace($choice)) {
        return $DefaultValue
    }
    
    return $choice -match '^[Yy]'
}

function Save-EncryptionConfiguration {
    param([hashtable]$Preferences)
    
    Write-Status "Saving encryption configuration..." "Cyan"
    
    # Create the configuration object matching our SelectiveEncryptionSettings class
    $encryptionConfig = @{
        UseSelectiveEncryption = $Preferences.UseSelectiveEncryption
        EncryptSaveFiles = $Preferences.EncryptSaveFiles
        EncryptConfigFiles = $Preferences.EncryptConfigFiles
        EncryptUserFiles = $Preferences.EncryptUserFiles
        EncryptCacheFiles = $Preferences.EncryptCacheFiles
        EncryptModFiles = $Preferences.EncryptModFiles
        EncryptExecutables = $Preferences.EncryptExecutables
        EncryptAssets = $Preferences.EncryptAssets
        CustomExtensions = $Preferences.CustomExtensions
    }
    
    # Create a default GameLocker configuration with selective encryption
    $gameLockerConfig = @{
        AllowedDays = @()  # Empty by default, user sets up in Config UI
        StartTime = "18:00:00"  # 6 PM default
        DurationHours = 2
        GameFolderPaths = @()  # Empty by default, user sets up in Config UI
        PollingIntervalMinutes = 1
        NotificationsEnabled = $true
        EncryptionSettings = $encryptionConfig
    }
    
    # Save to the data directory
    $DataPath = Join-Path $env:ProgramData "GameLocker"
    $configFile = Join-Path $DataPath "gamelocker-config.json"
    
    # Ensure data directory exists
    if (!(Test-Path $DataPath)) {
        New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
    }
    
    $configJson = $gameLockerConfig | ConvertTo-Json -Depth 5
    $configJson | Out-File -FilePath $configFile -Encoding UTF8
    
    Write-Status "Configuration saved to: $configFile" "Gray"
}

function Show-EncryptionSummary {
    param([hashtable]$Preferences)
    
    Write-Host ""
    Write-Host "📋 Your Encryption Configuration Summary:" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
    
    $enabledCategories = @()
    $riskyCategories = @()
    
    if ($Preferences.EncryptSaveFiles) { $enabledCategories += "Save files" }
    if ($Preferences.EncryptConfigFiles) { $enabledCategories += "Configuration files" }
    if ($Preferences.EncryptUserFiles) { $enabledCategories += "User profiles" }
    if ($Preferences.EncryptCacheFiles) { $enabledCategories += "Cache files" }
    if ($Preferences.EncryptModFiles) { $enabledCategories += "Mod files" }
    if ($Preferences.EncryptExecutables) { $riskyCategories += "⚠️ Executables (RISKY)" }
    if ($Preferences.EncryptAssets) { $riskyCategories += "⚠️ Asset files (RISKY)" }
    
    if ($enabledCategories.Count -gt 0) {
        Write-Host "✅ Will encrypt: $($enabledCategories -join ', ')" -ForegroundColor Green
    }
    
    if ($riskyCategories.Count -gt 0) {
        Write-Host "⚠️  Also encrypting: $($riskyCategories -join ', ')" -ForegroundColor Yellow
        Write-Host "   Monitor for game stability issues!" -ForegroundColor Yellow
    }
    
    if (-not [string]::IsNullOrWhiteSpace($Preferences.CustomExtensions)) {
        Write-Host "📝 Custom extensions: $($Preferences.CustomExtensions)" -ForegroundColor Cyan
    }
    
    if ($enabledCategories.Count -eq 0 -and $riskyCategories.Count -eq 0) {
        Write-Host "❌ No file types will be encrypted" -ForegroundColor Red
    }
    
    Write-Host ""
}

function Confirm-Configuration {
    param([hashtable]$Preferences)
    
    Show-EncryptionSummary $Preferences
    
    if (-not $Silent) {
        $confirm = Read-Host "Proceed with this configuration? (Y/n)"
        if ($confirm -match '^[Nn]') {
            Write-Host "Installation cancelled by user." -ForegroundColor Yellow
            exit 0
        }
    }
}

# Enhanced main installation process
try {
    # Check prerequisites
    if (!(Test-Administrator)) {
        throw "This script requires Administrator privileges. Please run as Administrator."
    }
    
    Write-Status "Starting Enhanced GameLocker installation..." "Green"
    Write-Status "Install Path: $InstallPath" "Gray"
    
    # Configure encryption settings if not silent
    $encryptionPrefs = @{}
    if ($ConfigureEncryption -and -not $Silent) {
        $encryptionPrefs = Get-UserEncryptionPreferences
        Confirm-Configuration $encryptionPrefs
    } elseif ($Silent) {
        # Use safe defaults in silent mode
        $encryptionPrefs = @{
            UseSelectiveEncryption = $true
            EncryptSaveFiles = $true
            EncryptConfigFiles = $true
            EncryptUserFiles = $true
            EncryptCacheFiles = $false
            EncryptModFiles = $false
            EncryptExecutables = $false
            EncryptAssets = $false
            CustomExtensions = ""
        }
        Write-Status "Using safe encryption defaults" "Cyan"
    }
    
    # Standard installation steps
    Stop-ExistingService
    Create-Directories
    Copy-Files
    Install-Service
    Configure-Service
    Set-Permissions
    Create-StartMenuShortcuts
    Create-UninstallScript
    
    # Save encryption configuration
    if ($encryptionPrefs.Count -gt 0) {
        Save-EncryptionConfiguration $encryptionPrefs
    }
    
    Start-Service
    
    # Enhanced completion message
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Green
    Write-Host "  GameLocker Enhanced Installation Complete" -ForegroundColor Green
    Write-Host "======================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "🎮 NEW FEATURES:" -ForegroundColor Cyan
    Write-Host "  ✅ Selective encryption prevents game corruption" -ForegroundColor Green
    Write-Host "  ✅ Smart file type filtering" -ForegroundColor Green
    Write-Host "  ✅ Emergency decryption tools available" -ForegroundColor Green
    Write-Host ""
    Write-Host "📁 Installation Details:" -ForegroundColor Cyan
    Write-Host "  Service: $ServiceName" -ForegroundColor Gray
    Write-Host "  Path: $InstallPath" -ForegroundColor Gray
    Write-Host "  Config: $env:ProgramData\GameLocker" -ForegroundColor Gray
    
    if ($encryptionPrefs.Count -gt 0) {
        Show-EncryptionSummary $encryptionPrefs
    }
    
    Write-Host "🚀 Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Launch 'GameLocker Configuration' from Start Menu" -ForegroundColor Gray
    Write-Host "  2. Add your game folders" -ForegroundColor Gray
    Write-Host "  3. Set your gaming schedule" -ForegroundColor Gray
    Write-Host "  4. Enjoy safer, smarter game folder protection!" -ForegroundColor Gray
    Write-Host ""
    
    if (!$Silent) {
        Write-Host "Press any key to exit..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
}
catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Enhanced installation failed." -ForegroundColor Red
    
    if (!$Silent) {
        Write-Host "Press any key to exit..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
    exit 1
}