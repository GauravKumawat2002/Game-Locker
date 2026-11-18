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
    [string]$InstallPath = (Join-Path $env:ProgramFiles 'GameLocker'),
    
    [Parameter(Mandatory=$false)]
    [ValidateSet('GameLockerService')]
    [string]$ServiceName = 'GameLockerService',
    
    [Parameter(Mandatory=$false)]
    [switch]$Silent = $false
)

# Script configuration
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Capture script directory at script level (not inside functions)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrEmpty($ScriptDir)) {
    $ScriptDir = $PSScriptRoot
}
if ([string]::IsNullOrEmpty($ScriptDir)) {
    $ScriptDir = (Get-Location).Path
}

# Global variables
$InstallPath = [IO.Path]::GetFullPath($InstallPath)
$ServicePath = Join-Path $InstallPath "Service"
$ConfigPath = Join-Path $InstallPath "ConfigUI"
$RecoveryPath = Join-Path $InstallPath "Recovery"
$LogPath = Join-Path $InstallPath "Logs"
$DataPath = Join-Path $env:ProgramData "GameLocker"
$PublishPath = Join-Path $ScriptDir 'publish'
$Payload = [ordered]@{
    service = @{ Directory = $ServicePath; Executable = 'GameLocker.Service.exe' }
    configui = @{ Directory = $ConfigPath; Executable = 'GameLocker.ConfigUI.exe' }
    recovery = @{ Directory = $RecoveryPath; Executable = 'GameLocker.Recovery.exe' }
}

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
    Write-Status "Checking for existing GameLocker service..." "Cyan"

    foreach ($name in @($ServiceName, 'GameLocker Service')) {
        $service = Get-Service -Name $name -ErrorAction SilentlyContinue
        if (!$service) { continue }

        try {
            if ($service.Status -ne 'Stopped') {
                if ($service.Status -ne 'StopPending') {
                    Stop-Service -InputObject $service
                }
                $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
            }
        } finally {
            $service.Dispose()
        }

        if ($name -ne $ServiceName) {
            $deleteResult = sc.exe delete $name
            if ($LASTEXITCODE -ne 0) {
                throw "Could not remove the legacy service '$name'. $deleteResult"
            }
        }
    }
}

function Create-Directories {
    Write-Status "Creating installation directories..." "Cyan"
    
    $directories = @($InstallPath, $ServicePath, $ConfigPath, $RecoveryPath, $LogPath, $DataPath)
    foreach ($dir in $directories) {
        if (!(Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-Status "Created: $dir" "Gray"
        }
    }
}

function Copy-Files {
    Write-Status "Copying GameLocker files..." "Cyan"

    foreach ($component in $Payload.GetEnumerator()) {
        $source = Join-Path $PublishPath $component.Key
        Copy-Item -Path (Join-Path $source '*') -Destination $component.Value.Directory -Recurse -Force
    }
}

function Install-Service {
    Write-Status "Installing GameLocker Windows Service..." "Cyan"
    
    $serviceExePath = Join-Path $ServicePath "GameLocker.Service.exe"
    
    if (!(Test-Path $serviceExePath)) {
        throw "Service executable not found at: $serviceExePath"
    }
    
    $operation = if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) { 'config' } else { 'create' }
    $createResult = sc.exe $operation $ServiceName binpath= "`"$serviceExePath`"" start= auto DisplayName= "GameLocker Service" 2>&1
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
    Write-Status 'Securing GameLocker data...' 'Cyan'
    $directory = Get-Item -LiteralPath $DataPath -Force
    if (!$directory.PSIsContainer -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Data path is not an ordinary directory: $DataPath"
    }

    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $inheritance = [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'
    foreach ($type in @('LocalSystemSid', 'BuiltinAdministratorsSid')) {
        $sid = [Security.Principal.SecurityIdentifier]::new([Security.Principal.WellKnownSidType]$type, $null)
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', $inheritance, 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    [IO.Directory]::SetAccessControl($DataPath, $acl)
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

        $recoveryShortcut = $shell.CreateShortcut((Join-Path $gameLockFolder 'GameLocker Recovery.lnk'))
        $recoveryShortcut.TargetPath = Join-Path $RecoveryPath 'GameLocker.Recovery.exe'
        $recoveryShortcut.WorkingDirectory = $RecoveryPath
        $recoveryShortcut.Description = 'List folders that need repair'
        $recoveryShortcut.Save()
        
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

$ErrorActionPreference = 'Stop'
Write-Host "GameLocker Uninstaller" -ForegroundColor Red
Write-Host "======================" -ForegroundColor Red

$ServiceName = "GameLockerService"
$InstallPath = [IO.Path]::GetFullPath($PSScriptRoot)
$DataPath = "$env:ProgramData\GameLocker"
$recoveryExe = Join-Path $InstallPath 'Recovery/GameLocker.Recovery.exe'
if (!(Test-Path -LiteralPath $recoveryExe -PathType Leaf)) {
    throw "Uninstall cancelled because the recovery tool is missing at '$recoveryExe'."
}

Write-Host 'Stopping GameLocker and restoring managed folders...'
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    try {
        if ($service.Status -ne 'Stopped') {
            if ($service.Status -ne 'StopPending') {
                Stop-Service -InputObject $service
            }
            $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }
    } finally {
        $service.Dispose()
    }
}

& $recoveryExe restore-all
if ($LASTEXITCODE -ne 0) {
    throw "Uninstall cancelled. Repair the folders reported above and run the uninstaller again."
}

if ($service) {
    sc.exe delete $ServiceName | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'GameLocker folders were restored. The service could not be removed, so the installed files were kept.'
    }
}

# Remove the payload directories without deleting other contents of a custom installation root.
$installPrefix = $InstallPath.TrimEnd([char[]]@('\', '/')) + [IO.Path]::DirectorySeparatorChar
foreach ($directory in @('Service', 'ConfigUI', 'Recovery')) {
    $component = Join-Path $InstallPath $directory
    if (!(Test-Path -LiteralPath $component)) { continue }
    $target = (Resolve-Path -LiteralPath $component).ProviderPath
    $item = Get-Item -LiteralPath $target -Force
    if (!$target.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        !$item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Uninstall could not safely remove '$component'."
    }
    Remove-Item -LiteralPath $target -Recurse -Force
}

$startMenuPath = [Environment]::GetFolderPath('CommonPrograms')
$gameLockFolder = Join-Path $startMenuPath 'GameLocker'
foreach ($name in @('GameLocker Configuration.lnk', 'GameLocker Recovery.lnk', 'Uninstall GameLocker.lnk')) {
    $shortcut = Join-Path $gameLockFolder $name
    if (Test-Path -LiteralPath $shortcut -PathType Leaf) {
        Remove-Item -LiteralPath $shortcut -Force
    }
}

if (Test-Path $DataPath) {
    Write-Host "Configuration, state and keys were kept at $DataPath."
}

Remove-Item -LiteralPath $PSCommandPath -Force
Write-Host "GameLocker has been uninstalled." -ForegroundColor Green
'@
    
    $uninstallPath = Join-Path $InstallPath "GameLocker-Uninstall.ps1"
    $uninstallScript | Out-File -FilePath $uninstallPath -Encoding UTF8
    
    Write-Status "Uninstall script created at $uninstallPath" "Gray"
}

function Start-GameLockerService {
    Write-Status "Starting GameLocker service..." "Cyan"

    $service = Get-Service -Name $ServiceName
    try {
        Start-Service -InputObject $service
        $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
    } finally {
        $service.Dispose()
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

    foreach ($component in $Payload.GetEnumerator()) {
        $binary = Join-Path (Join-Path $PublishPath $component.Key) $component.Value.Executable
        if (!(Test-Path -LiteralPath $binary -PathType Leaf)) {
            throw "Installer package is missing '$binary'."
        }
        if (Test-Path -LiteralPath $component.Value.Directory) {
            $destination = Get-Item -LiteralPath $component.Value.Directory -Force
            if (!$destination.PSIsContainer -or ($destination.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "Installation directory is not an ordinary directory: $($component.Value.Directory)"
            }
            $installedBinary = Join-Path $component.Value.Directory $component.Value.Executable
            if (!(Test-Path -LiteralPath $installedBinary -PathType Leaf) -and
                (Get-ChildItem -LiteralPath $component.Value.Directory -Force | Select-Object -First 1)) {
                throw "Installation would overwrite an unrelated folder: $($component.Value.Directory)"
            }
        }
    }

    # Installation steps
    Stop-ExistingService
    Create-Directories
    Set-Permissions
    Copy-Files
    Install-Service
    Configure-Service
    Create-StartMenuShortcuts
    Create-UninstallScript
    Start-GameLockerService
    
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
