@echo off
setlocal
echo ========================================
echo           GameLocker Setup
echo ========================================
echo.

set "GAMELOCKER_SETUP_SCRIPT=%~dp0GameLocker-Setup.ps1"
echo Approve the Windows administrator prompt when it appears.
echo.
powershell.exe -NoProfile -Command "$ErrorActionPreference = 'Stop'; try { $script = $env:GAMELOCKER_SETUP_SCRIPT; $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()); $options = @{ FilePath = Join-Path $PSHOME 'powershell.exe'; ArgumentList = '-NoProfile -ExecutionPolicy Bypass -File ' + [char]34 + $script + [char]34 + ' -Silent'; PassThru = $true; Wait = $true }; if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { $options.Verb = 'RunAs' }; $process = Start-Process @options; exit $process.ExitCode } catch { Write-Host $_.Exception.Message; exit 1 }"
set "setupResult=%errorLevel%"

if %setupResult% equ 0 (
    echo.
    echo Installation completed successfully!
    echo Open GameLocker Configuration from the Start menu.
) else (
    echo.
    echo GameLocker setup did not complete.
)

pause
exit /b %setupResult%
