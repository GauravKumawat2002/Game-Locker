# Getting Started - Developer Guide

This guide will help you set up your development environment and start contributing to GameLocker.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Environment Setup](#environment-setup)
3. [Building the Project](#building-the-project)
4. [Running in Development](#running-in-development)
5. [Development Tools](#development-tools)
6. [Common Tasks](#common-tasks)

---

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| **.NET SDK** | 8.0+ | Build and run the application |
| **Windows** | 10/11 (64-bit) | Required for NTFS ACL and DPAPI |
| **Git** | Latest | Version control |
| **PowerShell** | 5.1+ | Installer scripts |

### Recommended IDE

- **Visual Studio 2022** (Community or higher)
  - Workload: ".NET Desktop Development"
  
- **Visual Studio Code** with extensions:
  - C# Dev Kit
  - C#
  - .NET Install Tool
  - PowerShell

### Verify Installation

```powershell
# Check .NET SDK
dotnet --version
# Expected: 8.0.x or higher

# Check PowerShell
$PSVersionTable.PSVersion
# Expected: 5.1 or higher (or PowerShell 7.x)
```

---

## Environment Setup

### 1. Clone the Repository

```powershell
git clone https://github.com/GauravKumawat2002/Game-Locker.git
cd Game-Locker/code
```

### 2. Restore Dependencies

```powershell
dotnet restore
```

### 3. Build the Solution

```powershell
dotnet build
```

### 4. Verify Build

You should see output similar to:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Building the Project

### Debug Build

```powershell
# Build all projects
dotnet build

# Build specific project
dotnet build src/GameLocker.ConfigUI
dotnet build src/GameLocker.Service
```

### Release Build

```powershell
dotnet build -c Release
```

### Create Self-Contained Executables

```powershell
# EASIEST: Use the build script
.\\Build-Installer.ps1 -BuildType Both

# OR manually:
# Publish Service (with .NET Runtime)
dotnet publish src/GameLocker.Service -c Release -r win-x64 --self-contained true -o installer-package/publish/service

# Publish ConfigUI (with .NET Runtime)
dotnet publish src/GameLocker.ConfigUI -c Release -r win-x64 --self-contained true -o installer-package/publish/configui
```

### Create Installer Package

```powershell
# Use the automated build script (recommended)
.\\Build-Installer.ps1 -BuildType ZIP    # PowerShell-based installer
.\\Build-Installer.ps1 -BuildType EXE    # GUI installer (requires Inno Setup)
.\\Build-Installer.ps1 -BuildType Both   # Create both installers

# See installer/README.md for detailed setup instructions
```

---

## Running in Development

### Running the ConfigUI

```powershell
# Run with dotnet (Debug mode)
dotnet run --project src/GameLocker.ConfigUI

# Or run the compiled executable
./src/GameLocker.ConfigUI/bin/Debug/net8.0-windows10.0.17763.0/GameLocker.ConfigUI.exe
```

> **Note**: Run as Administrator for full ACL functionality.

### Running the Service (Debug Mode)

The service can run as a console application for debugging:

```powershell
# Run service in console mode
dotnet run --project src/GameLocker.Service

# The service will output logs to console
# Press Ctrl+C to stop
```

### Installing the Service (for testing)

```powershell
# Create service (Admin required)
sc.exe create "GameLocker Service" binpath= "C:\full\path\to\GameLocker.Service.exe" start= auto

# Start service
sc.exe start "GameLocker Service"

# Check status
sc.exe query "GameLocker Service"

# Stop and remove when done
sc.exe stop "GameLocker Service"
sc.exe delete "GameLocker Service"
```

---

## Development Tools

### UnlockTool

For development testing, use the unlock tool to manually unlock folders:

```powershell
cd UnlockTool
dotnet run "C:\Path\To\Test\Game\Folder"
```

### ForceDecrypt

Emergency recovery tool for encrypted files:

```powershell
cd ForceDecrypt
dotnet run "C:\Path\To\Folder\With\Encrypted\Files"
```

### Test Projects

The solution includes several test projects:

```powershell
# Test extension scanning
dotnet run --project src/TestExtensionScanning

# Test selective encryption
dotnet run --project src/TestSelectiveEncryption

# Test complete workflow
dotnet run --project src/TestCompleteWorkflow
```

### ServiceManager Script

Manage the Windows Service easily:

```powershell
# View available commands
.\ServiceManager.ps1 -Help

# Install service
.\ServiceManager.ps1 -Install

# Start/Stop
.\ServiceManager.ps1 -Start
.\ServiceManager.ps1 -Stop

# View status
.\ServiceManager.ps1 -Status

# Uninstall
.\ServiceManager.ps1 -Uninstall
```

---

## Common Tasks

### Adding a New Feature

1. **Create a branch**
   ```powershell
   git checkout -b feature/your-feature-name
   ```

2. **Implement in GameLocker.Common** (if shared logic)
   - Add models to `Models/`
   - Add services to `Services/`
   - Add helpers to appropriate folders

3. **Update the UI** (if user-facing)
   - Modify `Form1.cs` for logic
   - Modify `Form1.Designer.cs` for UI elements

4. **Update the Service** (if automation needed)
   - Modify `GameLockerService.cs`

5. **Test thoroughly**
   - Create a test folder with sample files
   - Test lock/unlock cycles
   - Test schedule-based automation

6. **Commit and push**
   ```powershell
   git add .
   git commit -m "feat: add your feature description"
   git push origin feature/your-feature-name
   ```

### Testing Encryption

1. Create a test folder with various files
2. Use ConfigUI to add the folder
3. Click "Scan Extensions" to see file types
4. Select safe extensions only
5. Lock the folder
6. Verify files are encrypted (.enc)
7. Unlock and verify decryption

### Debugging ACL Issues

```powershell
# Check current ACL
icacls "C:\Path\To\Game\Folder"

# Force reset permissions
icacls "C:\Path\To\Game\Folder" /reset /T

# Grant full control to current user
icacls "C:\Path\To\Game\Folder" /grant "%USERNAME%:F" /T
```

### Viewing Service Logs

```powershell
# View Windows Event Log
Get-EventLog -LogName Application -Source "GameLocker*" -Newest 50

# Or check service console output in debug mode
```

---

## Configuration Files

### appsettings.json (Service)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### Config Data Location

```
C:\ProgramData\GameLocker\
├── config.dat         # Encrypted user configuration
├── folder_key.dat     # DPAPI-protected encryption key
└── folder_iv.dat      # DPAPI-protected IV
```

---

## Troubleshooting

### Build Errors

**Error: Target framework not found**
```powershell
# Install .NET 8.0 SDK
winget install Microsoft.DotNet.SDK.8
```

**Error: Windows SDK not found**
```powershell
# Install Windows 10 SDK
# Visual Studio Installer → Modify → Individual Components → Windows 10 SDK
```

### Runtime Errors

**Error: Access denied when locking folder**
- Run as Administrator
- Ensure folder is on NTFS partition
- Check no files are in use

**Error: Encryption keys not found**
- Keys are stored per-machine
- Run `dotnet run --project src/GameLocker.ConfigUI` to regenerate

### Service Issues

**Service won't start**
```powershell
# Check event log
Get-EventLog -LogName Application -Source "GameLocker*" -Newest 10

# Verify executable exists
Test-Path "C:\Program Files\GameLocker\service\GameLocker.Service.exe"
```

---

## Next Steps

- Read [01-architecture.md](01-architecture.md) for system overview
- Read [03-api-reference.md](03-api-reference.md) for API documentation
- Read [04-contributing.md](04-contributing.md) for contribution guidelines
