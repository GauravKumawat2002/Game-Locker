# GameLocker Service Management Script
# Run as Administrator

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("install", "uninstall", "start", "stop", "status", "restart")]
    [string]$Action = "status"
)

$ServiceName = "GameLockerService"
$ServicePath = "h:\Work\10xProductivity\game_locker\code\src\GameLocker.Service\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\GameLocker.Service.exe"

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Show-ServiceStatus {
    try {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($service) {
            Write-Host "🎮 GameLocker Service Status:" -ForegroundColor Cyan
            Write-Host "   Name: $($service.Name)" -ForegroundColor White
            Write-Host "   Status: $($service.Status)" -ForegroundColor $(if($service.Status -eq 'Running') { 'Green' } else { 'Yellow' })
            Write-Host "   Start Type: $($service.StartType)" -ForegroundColor White
        } else {
            Write-Host "❌ GameLocker Service is not installed" -ForegroundColor Red
        }
    } catch {
        Write-Host "❌ Error checking service: $($_.Exception.Message)" -ForegroundColor Red
    }
}

switch ($Action.ToLower()) {
    "install" {
        if (-not (Test-Administrator)) {
            Write-Host "❌ Administrator privileges required for installation" -ForegroundColor Red
            exit 1
        }
        
        if (-not (Test-Path $ServicePath)) {
            Write-Host "❌ Service executable not found: $ServicePath" -ForegroundColor Red
            Write-Host "Please build the service first with: dotnet publish" -ForegroundColor Yellow
            exit 1
        }
        
        try {
            # Remove existing service if it exists
            $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
            if ($existingService) {
                Write-Host "Removing existing service..." -ForegroundColor Yellow
                Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
                sc.exe delete $ServiceName | Out-Null
                Start-Sleep 2
            }
            
            Write-Host "Installing GameLocker service..." -ForegroundColor Cyan
            $result = sc.exe create $ServiceName binPath= "`"$ServicePath`"" start= auto DisplayName= "GameLocker Service"
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Service installed successfully" -ForegroundColor Green
                Start-Service -Name $ServiceName
                Show-ServiceStatus
            } else {
                Write-Host "❌ Service installation failed" -ForegroundColor Red
            }
        } catch {
            Write-Host "❌ Installation error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    "uninstall" {
        if (-not (Test-Administrator)) {
            Write-Host "❌ Administrator privileges required for uninstallation" -ForegroundColor Red
            exit 1
        }
        
        try {
            $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
            if ($service) {
                Write-Host "Uninstalling GameLocker service..." -ForegroundColor Yellow
                Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
                sc.exe delete $ServiceName | Out-Null
                Write-Host "✅ Service uninstalled successfully" -ForegroundColor Green
            } else {
                Write-Host "ℹ️ Service is not installed" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "❌ Uninstallation error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    "start" {
        try {
            Start-Service -Name $ServiceName
            Write-Host "✅ Service started" -ForegroundColor Green
            Show-ServiceStatus
        } catch {
            Write-Host "❌ Failed to start service: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    "stop" {
        try {
            Stop-Service -Name $ServiceName -Force
            Write-Host "⏹️ Service stopped" -ForegroundColor Yellow
            Show-ServiceStatus
        } catch {
            Write-Host "❌ Failed to stop service: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    "restart" {
        try {
            Restart-Service -Name $ServiceName -Force
            Write-Host "🔄 Service restarted" -ForegroundColor Green
            Show-ServiceStatus
        } catch {
            Write-Host "❌ Failed to restart service: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    "status" {
        Show-ServiceStatus
    }
}

Write-Host ""
Write-Host "Available commands:" -ForegroundColor Cyan
Write-Host "  .\ServiceManager.ps1 install   - Install the service" -ForegroundColor Gray
Write-Host "  .\ServiceManager.ps1 start     - Start the service" -ForegroundColor Gray  
Write-Host "  .\ServiceManager.ps1 stop      - Stop the service" -ForegroundColor Gray
Write-Host "  .\ServiceManager.ps1 restart   - Restart the service" -ForegroundColor Gray
Write-Host "  .\ServiceManager.ps1 status    - Show service status" -ForegroundColor Gray
Write-Host "  .\ServiceManager.ps1 uninstall - Uninstall the service" -ForegroundColor Gray