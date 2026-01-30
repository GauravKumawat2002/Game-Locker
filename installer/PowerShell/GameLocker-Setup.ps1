#Requires -RunAsAdministrator

<#
.SYNOPSIS
    GameLocker Complete Setup Script

.DESCRIPTION
    This PowerShell script sets up the complete GameLocker system including:
    - Windows Service installation and configuration
    - Configuration UI deployment
    - Registry settings
    - Firewall rules (if needed)
    - Service startup configuration

.PARAMETER InstallPath
    Installation directory (default: C:\Program Files\GameLocker)

.PARAMETER ServicePath
    Service installation path (derived from InstallPath)

.PARAMETER ConfigPath
    Configuration UI path (derived from InstallPath)

.EXAMPLE
    .\GameLocker-Setup.ps1
    Installs GameLocker with default settings

.EXAMPLE
    .\GameLocker-Setup.ps1 -InstallPath "D:\GameLocker"
    Installs GameLocker to a custom directory
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Program Files\GameLocker",
    
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "GameLockerService",
    
    [Parameter(Mandatory=$false)]
    [switch]$Silent = $false
)

# Script configuration
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Global variables
$ServicePath = Join-Path $InstallPath "Service"
$ConfigPath = Join-Path $InstallPath "ConfigUI"
$LogPath = Join-Path $InstallPath "Logs"
$DataPath = Join-Path $env:ProgramData "GameLocker"

Write-Host "======================================" -ForegroundColor Green
Write-Host "    GameLocker Complete Setup v1.0    " -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""

function Write-Status {
    param([string]$Message, [string]$Color = "White")
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor $Color
}

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Stop-ExistingService {
    try {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($service) {
            Write-Status "Stopping existing GameLocker service..." "Yellow"
            if ($service.Status -eq 'Running') {
                Stop-Service -Name $ServiceName -Force -NoWait
                Start-Sleep -Seconds 3
            }
            
            # Uninstall existing service
            Write-Status "Uninstalling existing service..." "Yellow"
            sc.exe delete $ServiceName | Out-Null
            Start-Sleep -Seconds 2
        }
    }
    catch {
        Write-Status "Warning: Could not stop existing service: $($_.Exception.Message)" "Yellow"
    }
}

function Create-Directories {
    Write-Status "Creating installation directories..." "Cyan"
    
    $directories = @($InstallPath, $ServicePath, $ConfigPath, $LogPath, $DataPath)
    foreach ($dir in $directories) {
        if (!(Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-Status "Created: $dir" "Gray"
        }
    }
}

function Copy-Files {
    Write-Status "Copying GameLocker files..." "Cyan"
    
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $publishDir = Split-Path -Parent (Split-Path -Parent $scriptDir)
    
    # Copy service files
    $serviceSource = Join-Path $publishDir "publish\service"
    if (Test-Path $serviceSource) {
        Copy-Item -Path "$serviceSource\*" -Destination $ServicePath -Recurse -Force
        Write-Status "Service files copied to $ServicePath" "Gray"
    } else {
        throw "Service files not found at $serviceSource. Please run 'dotnet publish' first."
    }
    
    # Copy config UI files
    $configSource = Join-Path $publishDir "publish\configui"
    if (Test-Path $configSource) {
        Copy-Item -Path "$configSource\*" -Destination $ConfigPath -Recurse -Force
        Write-Status "Config UI files copied to $ConfigPath" "Gray"
    } else {
        throw "Config UI files not found at $configSource. Please run 'dotnet publish' first."
    }
    
    # Copy configuration files
    $configFiles = @("appsettings.json", "appsettings.Development.json")
    foreach ($configFile in $configFiles) {
        $sourceConfig = Join-Path $publishDir "publish\$configFile"
        if (Test-Path $sourceConfig) {
            Copy-Item -Path $sourceConfig -Destination $ServicePath -Force
            Write-Status "Configuration file $configFile copied" "Gray"
        }
    }
}

function Install-Service {
    Write-Status "Installing GameLocker Windows Service..." "Cyan"
    
    $serviceExePath = Join-Path $ServicePath "GameLocker.Service.exe"
    
    if (!(Test-Path $serviceExePath)) {
        throw "Service executable not found at: $serviceExePath"
    }
    
    # Install the service
    $createResult = sc.exe create $ServiceName binpath= "`"$serviceExePath`"" start= auto DisplayName= "GameLocker Service" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create service: $createResult"
    }
    
    # Set service description
    sc.exe description $ServiceName "Automatically locks and unlocks game folders based on configured schedules." | Out-Null
    
    # Configure service recovery options
    sc.exe failure $ServiceName reset= 30 actions= restart/5000/restart/10000/restart/15000 | Out-Null
    
    Write-Status "Service installed successfully" "Green"
}

function Configure-Service {
    Write-Status "Configuring service settings..." "Cyan"
    
    # Create service configuration file
    $serviceConfig = @{
        ServiceName = $ServiceName
        InstallPath = $InstallPath
        DataPath = $DataPath
        LogPath = $LogPath
        ConfigUIPath = Join-Path $ConfigPath "GameLocker.ConfigUI.exe"
        Version = "1.0.0"
        InstallDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    }
    
    $configJson = $serviceConfig | ConvertTo-Json -Depth 3
    $configFile = Join-Path $DataPath "service-config.json"
    $configJson | Out-File -FilePath $configFile -Encoding UTF8
    
    Write-Status "Service configuration saved to $configFile" "Gray"
}

function Set-Permissions {
    Write-Status "Setting up permissions..." "Cyan"
    
    try {
        # Grant service account permissions to installation directory
        $acl = Get-Acl $InstallPath
        $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule("NT AUTHORITY\LOCAL SERVICE","FullControl","ContainerInherit,ObjectInherit","None","Allow")
        $acl.SetAccessRule($accessRule)
        Set-Acl -Path $InstallPath -AclObject $acl
        
        # Grant permissions to data directory
        $acl = Get-Acl $DataPath
        $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule("NT AUTHORITY\LOCAL SERVICE","FullControl","ContainerInherit,ObjectInherit","None","Allow")
        $acl.SetAccessRule($accessRule)
        Set-Acl -Path $DataPath -AclObject $acl
        
        Write-Status "Permissions configured" "Gray"
    }
    catch {
        Write-Status "Warning: Could not set some permissions: $($_.Exception.Message)" "Yellow"
    }
}

function Create-StartMenuShortcuts {
    Write-Status "Creating Start Menu shortcuts..." "Cyan"
    
    try {
        $startMenuPath = [Environment]::GetFolderPath('CommonPrograms')
        $gameLockFolder = Join-Path $startMenuPath "GameLocker"
        
        if (!(Test-Path $gameLockFolder)) {
            New-Item -ItemType Directory -Path $gameLockFolder -Force | Out-Null
        }
        
        $shell = New-Object -ComObject WScript.Shell
        
        # Config UI shortcut
        $configShortcut = $shell.CreateShortcut((Join-Path $gameLockFolder "GameLocker Configuration.lnk"))
        $configShortcut.TargetPath = Join-Path $ConfigPath "GameLocker.ConfigUI.exe"
        $configShortcut.WorkingDirectory = $ConfigPath
        $configShortcut.Description = "Configure GameLocker settings"
        $configShortcut.Save()
        
        # Uninstall shortcut
        $uninstallShortcut = $shell.CreateShortcut((Join-Path $gameLockFolder "Uninstall GameLocker.lnk"))
        $uninstallShortcut.TargetPath = "powershell.exe"
        $uninstallShortcut.Arguments = "-ExecutionPolicy Bypass -File `"$(Join-Path $InstallPath 'GameLocker-Uninstall.ps1')`""
        $uninstallShortcut.Description = "Uninstall GameLocker"
        $uninstallShortcut.Save()
        
        Write-Status "Start Menu shortcuts created" "Gray"
    }
    catch {
        Write-Status "Warning: Could not create shortcuts: $($_.Exception.Message)" "Yellow"
    }
}

function Create-UninstallScript {
    Write-Status "Creating uninstall script..." "Cyan"
    
    $uninstallScript = @'
#Requires -RunAsAdministrator

Write-Host "GameLocker Uninstaller" -ForegroundColor Red
Write-Host "======================" -ForegroundColor Red

$ServiceName = "GameLockerService"
$InstallPath = "C:\Program Files\GameLocker"
$DataPath = "$env:ProgramData\GameLocker"

# Stop and remove service
try {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Host "Stopping GameLocker service..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force -NoWait
        Start-Sleep -Seconds 3
        
        Write-Host "Removing GameLocker service..." -ForegroundColor Yellow
        sc.exe delete $ServiceName | Out-Null
    }
}
catch {
    Write-Host "Warning: Could not remove service: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Remove installation directory
if (Test-Path $InstallPath) {
    Write-Host "Removing installation directory..." -ForegroundColor Yellow
    Remove-Item -Path $InstallPath -Recurse -Force -ErrorAction SilentlyContinue
}

# Remove Start Menu shortcuts
$startMenuPath = [Environment]::GetFolderPath('CommonPrograms')
$gameLockFolder = Join-Path $startMenuPath "GameLocker"
if (Test-Path $gameLockFolder) {
    Write-Host "Removing Start Menu shortcuts..." -ForegroundColor Yellow
    Remove-Item -Path $gameLockFolder -Recurse -Force -ErrorAction SilentlyContinue
}

# Ask about data directory
$keepData = Read-Host "Keep configuration and data files? (Y/n)"
if ($keepData -notmatch '^[Yy]?$') {
    if (Test-Path $DataPath) {
        Write-Host "Removing data directory..." -ForegroundColor Yellow
        Remove-Item -Path $DataPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "GameLocker has been uninstalled." -ForegroundColor Green
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
'@
    
    $uninstallPath = Join-Path $InstallPath "GameLocker-Uninstall.ps1"
    $uninstallScript | Out-File -FilePath $uninstallPath -Encoding UTF8
    
    Write-Status "Uninstall script created at $uninstallPath" "Gray"
}

function Start-Service {
    Write-Status "Starting GameLocker service..." "Cyan"
    
    try {
        Start-Service -Name $ServiceName
        Start-Sleep -Seconds 2
        
        $service = Get-Service -Name $ServiceName
        if ($service.Status -eq 'Running') {
            Write-Status "Service started successfully" "Green"
        } else {
            Write-Status "Warning: Service may not have started correctly" "Yellow"
        }
    }
    catch {
        Write-Status "Warning: Could not start service: $($_.Exception.Message)" "Yellow"
    }
}

function Show-CompletionMessage {
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Green
    Write-Host "     GameLocker Installation Complete  " -ForegroundColor Green
    Write-Host "======================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Installation Details:" -ForegroundColor Cyan
    Write-Host "  Service Name: $ServiceName" -ForegroundColor Gray
    Write-Host "  Install Path: $InstallPath" -ForegroundColor Gray
    Write-Host "  Data Path: $DataPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Launch 'GameLocker Configuration' from Start Menu" -ForegroundColor Gray
    Write-Host "  2. Configure your game folders and schedules" -ForegroundColor Gray
    Write-Host "  3. The service will automatically manage your folders" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Service Status:" -ForegroundColor Cyan
    
    try {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($service) {
            Write-Host "  Status: $($service.Status)" -ForegroundColor $(if($service.Status -eq 'Running') { 'Green' } else { 'Yellow' })
            Write-Host "  Startup Type: $($service.StartType)" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  Status: Unknown" -ForegroundColor Yellow
    }
    
    Write-Host ""
}

# Main installation process
try {
    # Check prerequisites
    if (!(Test-Administrator)) {
        throw "This script requires Administrator privileges. Please run as Administrator."
    }
    
    Write-Status "Starting GameLocker installation..." "Green"
    Write-Status "Install Path: $InstallPath" "Gray"
    
    # Installation steps
    Stop-ExistingService
    Create-Directories
    Copy-Files
    Install-Service
    Configure-Service
    Set-Permissions
    Create-StartMenuShortcuts
    Create-UninstallScript
    Start-Service
    
    Show-CompletionMessage
    
    if (!$Silent) {
        Write-Host "Press any key to exit..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
}
catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Installation failed." -ForegroundColor Red
    
    if (!$Silent) {
        Write-Host "Press any key to exit..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
    exit 1
}