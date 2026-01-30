@echo off
echo ========================================
echo     GameLocker Setup (Administrator)
echo ========================================
echo.

:: Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo This installer requires Administrator privileges.
    echo Please right-click and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

:: Change to installer directory
cd /d "%~dp0"

:: Run PowerShell installer
echo Starting GameLocker installation...
echo.
powershell.exe -ExecutionPolicy Bypass -File "GameLocker-Setup.ps1"

if %errorLevel% equ 0 (
    echo.
    echo Installation completed successfully!
) else (
    echo.
    echo Installation failed. Check the error messages above.
)

pause