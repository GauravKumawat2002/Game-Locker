# GameLocker Installer Setup Guide

## Quick Start

### Option 1: Build from the repository root

```powershell
.\Build-Installer.ps1 -BuildType Both
```

The ZIP and optional Inno Setup EXE contain self-contained Windows x64 builds
of the service, the configuration UI, and the recovery tool. The output defaults
to the repository root. Use `-BuildType ZIP` if Inno Setup is unavailable.

Use `-OutputDirectory` and `-PackageDirectory` to build into separate
directories outside the checkout.

---

## Option 2: Create EXE Installer with GUI

### Prerequisites

1. **Install Inno Setup** (Free)
    - Download: https://jrsoftware.org/isdl.php
    - Or via Chocolatey: `choco install innosetup -y`

### Build Steps

```powershell
.\Build-Installer.ps1 -BuildType EXE
```

---

## Option 3: Manual Build (for debugging)

```powershell
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

# Build Recovery
dotnet publish src/GameLocker.Recovery/GameLocker.Recovery.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o "installer-package/publish/recovery"

# Copy scripts
Copy-Item "installer/PowerShell/GameLocker-Setup.ps1", "installer/PowerShell/Setup.bat" -Destination "installer-package/" -Force

# Create ZIP
Compress-Archive -Path "installer-package\*" -DestinationPath ".\GameLocker-Installer-v1.3.0.zip" -Force
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

### Uninstall reports that recovery failed

The uninstaller stops the service and restores managed folders before removing
the installation. Repair any reported folders with the installed Recovery tool
before retrying. Run the tool from an administrator terminal to see recovery
details.

```powershell
& "$env:ProgramFiles\GameLocker\Recovery\GameLocker.Recovery.exe" restore-all
```

The recovery state and encryption keys are kept in `%ProgramData%\GameLocker`.

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

Set the release version in `../Directory.Build.props` and `InnoSetup/GameLocker-Setup.iss`, update `../CHANGELOG.md`, and commit the tested changes locally. From the repository root, validate the packages and push an annotated tag for the release commit.

```powershell
.\Build-Installer.ps1 -BuildType Both
git push origin master
git tag -a v1.3.0 -F path/to/release-notes.md
git push origin refs/tags/v1.3.0
```

Windows QA tests the tagged EXE and ZIP installers, then publishes the same files and `SHA256SUMS.txt` as the latest stable release. The tag annotation supplies the release notes.

---

## Next Steps for Professional Look

GameLocker Configuration, GameLocker Recovery, and the EXE installer share the
multi-size icon at `installer/Icons/gamelocker.ico`. Both installers use it for
their Start menu shortcuts.

1. **Code Signing** (Optional, for production)
    - Get a code signing certificate
    - Sign the EXE: `signtool sign /f cert.pfx /p password GameLocker-Installer.exe`

---

## Questions?

Check the documentation:

- GitHub: https://github.com/GauravKumawat2002/Game-Locker
- Issues: https://github.com/GauravKumawat2002/Game-Locker/issues
