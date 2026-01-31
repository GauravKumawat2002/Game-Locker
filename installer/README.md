# GameLocker Installer Setup Guide

## Quick Start

### Option 1: Run the Build Script (EASIEST!)

```powershell
cd "h:\Work\10xProductivity\game_locker\code"
.\Build-Installer.ps1 -BuildType Both
```

This creates:

- ✅ **ZIP Installer** (PowerShell-based, works immediately)
- ✅ **EXE Installer** (GUI-based, professional look)

---

## Option 2: Create EXE Installer with GUI

### Prerequisites

1. **Install Inno Setup** (Free)
    - Download: https://jrsoftware.org/isdl.php
    - Or via Chocolatey: `choco install innosetup -y`

### Build Steps

```powershell
# 1. Build the applications
cd "h:\Work\10xProductivity\game_locker\code"
.\Build-Installer.ps1

# 2. The EXE installer will be created at:
# h:\Work\10xProductivity\game_locker\GameLocker-Installer-v1.1.0.exe
```

---

## Option 3: Manual Build (for debugging)

```powershell
cd "h:\Work\10xProductivity\game_locker\code"

# Clean
dotnet clean GameLocker.sln -c Release

# Build Service (SELF-CONTAINED - includes .NET runtime!)
dotnet publish src/GameLocker.Service/GameLocker.Service.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o "installer-package/publish/service"

# Build ConfigUI (SELF-CONTAINED - includes .NET runtime!)
dotnet publish src/GameLocker.ConfigUI/GameLocker.ConfigUI.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o "installer-package/publish/configui"

# Copy scripts
Copy-Item "installer/PowerShell/GameLocker-Setup.ps1" -Destination "installer-package/" -Force

# Create ZIP
Compress-Archive -Path "installer-package\*" -DestinationPath "..\GameLocker-Installer-v1.1.0.zip" -Force
```

---

## What Changed?

### Before (BROKEN):

- Published with `--self-contained false`
- Required users to install .NET 8.0 Runtime separately
- Service wouldn't start without .NET
- ConfigUI showed ".NET required" error

### After (FIXED):

- Publishing with `--self-contained true`
- Includes .NET 8.0 Runtime in the package
- No dependencies needed!
- Works on any Windows 10/11 x64 machine

### File Size Impact:

- **Before**: ~10-20 MB (requires .NET)
- **After**: ~150 MB (everything included)

---

## Installer Types Comparison

| Feature           | ZIP + PowerShell  | EXE (Inno Setup)      |
| ----------------- | ----------------- | --------------------- |
| File Size         | ~150 MB           | ~150 MB               |
| GUI               | ❌ Terminal-based | ✅ Professional GUI   |
| Progress Bar      | ❌                | ✅                    |
| Uninstaller       | ✅ Script         | ✅ Windows-integrated |
| Desktop Icon      | ❌                | ✅ Optional           |
| Start Menu        | ✅                | ✅                    |
| Upgrade Detection | ❌                | ✅                    |
| Admin Check       | ✅                | ✅                    |

**Recommendation**: Use EXE installer for end users!

---

## Testing the Build

After building, test on a clean machine:

1. **Create a VM or use a test machine**
2. **DO NOT install .NET Runtime** (to verify self-contained works)
3. **Run the installer**
4. **Check**:
    - ConfigUI launches without errors
    - Service starts successfully
    - No ".NET required" messages

---

## Troubleshooting

### Service won't start

```powershell
# Check service status
sc.exe query GameLockerService

# Check Event Log
Get-WinEvent -LogName Application -MaxEvents 20 | Where-Object { $_.Message -like "*GameLocker*" }
```

### Build fails

```powershell
# Clean everything
dotnet clean GameLocker.sln -c Release
Remove-Item "installer-package/publish" -Recurse -Force -ErrorAction SilentlyContinue

# Try again
.\Build-Installer.ps1
```

---

## Publishing to GitHub

```powershell
# Build
cd "h:\Work\10xProductivity\game_locker\code"
.\Build-Installer.ps1 -BuildType Both

# Upload to GitHub Release
cd "h:\Work\10xProductivity\game_locker\code"
gh release upload v1.1.0 "..\GameLocker-Installer-v1.1.0.exe" --clobber
gh release upload v1.1.0 "..\GameLocker-Installer-v1.1.0.zip" --clobber
```

---

## Next Steps for Professional Look

1. **Add Application Icon**
    - Create `installer/Icons/gamelocker.ico` (256x256)
    - Tools: GIMP, Photoshop, or online converters

2. **Code Signing** (Optional, for production)
    - Get a code signing certificate
    - Sign the EXE: `signtool sign /f cert.pfx /p password GameLocker-Installer.exe`

3. **Version Info**
    - Update version in `.csproj` files
    - Update Inno Setup script version

---

## Questions?

Check the documentation:

- GitHub: https://github.com/GauravKumawat2002/Game-Locker
- Issues: https://github.com/GauravKumawat2002/Game-Locker/issues
