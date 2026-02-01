# Troubleshooting Guide

Common issues and solutions for GameLocker.

## Table of Contents

1. [Installation Issues](#installation-issues)
2. [Service Issues](#service-issues)
3. [Encryption Issues](#encryption-issues)
4. [UI Issues](#ui-issues)
5. [Recovery Tools](#recovery-tools)
6. [Advanced Diagnostics](#advanced-diagnostics)

---

## Installation Issues

### "Access Denied" During Installation

**Symptom:** Installer fails with access denied error.

**Solution:**

1. Right-click `Setup.bat` and select "Run as administrator"
2. If still failing, check antivirus isn't blocking the installer
3. Try running from an elevated PowerShell:
    ```powershell
    Start-Process powershell -Verb RunAs
    cd "C:\path\to\installer"
    .\GameLocker-Setup.ps1
    ```

### Service Fails to Install

**Symptom:** Service creation fails during installation.

**Solution:**

1. Check if service already exists:
    ```powershell
    Get-Service "GameLocker Service" -ErrorAction SilentlyContinue
    ```
2. Remove existing service:
    ```powershell
    sc.exe delete "GameLocker Service"
    ```
3. Retry installation

### Missing .NET Runtime

**Symptom:** Application won't start, mentions .NET.

**Solution:**
The installer includes .NET runtime. If using debug builds:

```powershell
# Install .NET 8.0 Runtime
winget install Microsoft.DotNet.Runtime.8
```

---

## Service Issues

### Service Won't Start

**Symptom:** Service fails to start or crashes immediately.

**Diagnostics:**

```powershell
# Check service status
Get-Service "GameLocker Service"

# View event log
Get-EventLog -LogName Application -Source "GameLocker*" -Newest 20

# Check if executable exists
Test-Path "C:\Program Files\GameLocker\service\GameLocker.Service.exe"
```

**Solutions:**

1. **Missing config directory:**

    ```powershell
    New-Item -ItemType Directory -Path "C:\ProgramData\GameLocker" -Force
    ```

2. **Permission issues:**

    ```powershell
    # Grant LocalService access to config folder
    icacls "C:\ProgramData\GameLocker" /grant "NT AUTHORITY\LOCAL SERVICE:F" /T
    ```

3. **Corrupted installation:**
    - Uninstall and reinstall GameLocker

### Service Running But Not Locking

**Symptom:** Service is running but folders don't lock/unlock.

**Diagnostics:**

```powershell
# Check if config exists
Test-Path "C:\ProgramData\GameLocker\config.dat"

# Run ConfigUI to verify settings
```

**Solutions:**

1. **No configuration:** Open ConfigUI and save settings
2. **Invalid folder paths:** Verify folders exist
3. **Schedule not set:** Configure allowed days and times
4. **Service not responding:** The service checks for commands every 5 seconds. Wait at least 10 seconds after making changes.

### ConfigUI Changes Not Applied

**Symptom:** You change settings in ConfigUI but folders don't lock/unlock.

**Cause:** In v1.1.0 and earlier, the ConfigUI didn't immediately notify the service.

**Solution (v1.2.0+):**

- This is fixed in v1.2.0. The ConfigUI now sends immediate commands to the service.
- Changes should apply within 5-10 seconds.

**Solution (older versions):**

1. Restart the service after saving configuration:
    ```powershell
    Restart-Service GameLockerService
    ```

### Folder Removal Doesn't Decrypt Files

**Symptom:** You remove a folder from ConfigUI but files remain encrypted/locked.

**Cause:** In v1.1.0 and earlier, folder removal didn't trigger decryption.

**Solution (v1.2.0+):**

- This is fixed in v1.2.0. Removing a folder now triggers immediate decryption.

**Solution (older versions/manual recovery):**

```powershell
# Send manual unlock command
$command = "unlock|G:\games\YourFolder|$(Get-Date -Format 'o')"
$command | Out-File "C:\ProgramData\GameLocker\immediate_action" -Encoding utf8 -NoNewline

# Or restart service to trigger full unlock cycle
Restart-Service GameLockerService
```

### Folder Locked But No .gamelocker Marker

**Symptom:** Folder is inaccessible but there's no `.gamelocker` file inside.

**Cause:** The unlock process may have been interrupted or the marker was deleted.

**Solution (v1.2.0+):**

- Send an unlock command even without the marker - it will remove ACL restrictions:
    ```powershell
    $command = "unlock|G:\games\LockedFolder|$(Get-Date -Format 'o')"
    $command | Out-File "C:\ProgramData\GameLocker\immediate_action" -Encoding utf8 -NoNewline
    ```

**Manual Solution:**

```powershell
# Reset ACL permissions manually
icacls "G:\games\LockedFolder" /reset /T /C
```

### Service Keeps Stopping

**Symptom:** Service starts then stops after a while.

**Solutions:**

1. Check for unhandled exceptions in Event Log
2. Verify disk space is available
3. Check for file system issues on game folders

---

## Encryption Issues

### Game Crashes After Locking

**Symptom:** Game won't run after folder was locked/unlocked.

**Cause:** Executable files (.exe, .dll) were encrypted.

**Solution:**

1. Use ForceDecrypt tool to recover files:

    ```powershell
    cd ForceDecrypt
    dotnet run "G:\games\CrashedGame"
    ```

2. **Prevention:** Use selective encryption - only encrypt safe file types:
    - ✅ Safe: `.sav`, `.ini`, `.cfg`, `.json`, `.txt`
    - ❌ Avoid: `.exe`, `.dll`, `.bin`

### "Padding is Invalid" Error

**Symptom:** Decryption fails with padding error.

**Cause:** Interrupted encryption, file corruption, or key mismatch.

**Solution:**

1. Use ForceDecrypt with raw recovery:

    ```powershell
    cd ForceDecrypt
    dotnet run "G:\games\CorruptedFolder"
    ```

2. ForceDecrypt will attempt:
    - Standard decryption first
    - Raw recovery (strip headers) if that fails

### Files Still Encrypted After Unlock

**Symptom:** `.enc` files remain after unlocking.

**Diagnostics:**

```powershell
# Check for .enc files
Get-ChildItem "G:\games\TestGame" -Recurse -Filter "*.enc"

# Check lock marker
Test-Path "G:\games\TestGame\.gamelocker"
```

**Solutions:**

1. **Manually trigger unlock:**

    ```powershell
    cd UnlockTool
    dotnet run "G:\games\TestGame"
    ```

2. **Check encryption keys exist:**
    ```powershell
    Test-Path "C:\ProgramData\GameLocker\folder_key.dat"
    Test-Path "C:\ProgramData\GameLocker\folder_iv.dat"
    ```

### Missing Encryption Keys

**Symptom:** Cannot decrypt files, keys missing.

**Cause:** Keys were deleted or machine was reinstalled.

**Impact:** Encrypted files cannot be recovered without original keys.

**Prevention:**

- Never delete `C:\ProgramData\GameLocker\folder_*.dat`
- Backup encryption keys if needed
- Use selective encryption for critical folders

---

## UI Issues

### ConfigUI Won't Launch

**Symptom:** ConfigUI crashes or shows error on startup.

**Solutions:**

1. **Run as Administrator:**

    ```powershell
    Start-Process "C:\Program Files\GameLocker\configui\GameLocker.ConfigUI.exe" -Verb RunAs
    ```

2. **Check dependencies:**

    ```powershell
    # Verify all files exist
    Get-ChildItem "C:\Program Files\GameLocker\configui" -Filter "*.dll" | Measure-Object
    ```

3. **Reset configuration:**
    ```powershell
    Remove-Item "C:\ProgramData\GameLocker\config.dat" -Force
    # Restart ConfigUI to create fresh config
    ```

### Extension Scanner Not Working

**Symptom:** "Scan Extensions" button does nothing or shows error.

**Solutions:**

1. **Folder not selected:** Select a folder first in the list
2. **Folder doesn't exist:** Verify path is valid
3. **Permission denied:** Run as Administrator

### Settings Not Saving

**Symptom:** Changes don't persist after closing ConfigUI.

**Diagnostics:**

```powershell
# Check config file timestamp
Get-Item "C:\ProgramData\GameLocker\config.dat" | Select-Object LastWriteTime
```

**Solutions:**

1. **Run as Administrator** - required for writing to ProgramData
2. **Check disk space** - ensure drive isn't full
3. **Check permissions:**
    ```powershell
    icacls "C:\ProgramData\GameLocker"
    ```

---

## Recovery Tools

### UnlockTool

Development tool to manually unlock folders.

```powershell
cd UnlockTool
dotnet run "G:\games\LockedFolder"
```

**What it does:**

1. Removes ACL restrictions
2. Decrypts all `.enc` files
3. Removes `.gamelocker` marker

### ForceDecrypt

Emergency recovery for corrupted encrypted files.

```powershell
cd ForceDecrypt
dotnet run "G:\games\CorruptedFolder"
```

**What it does:**

1. Scans for `.enc` files
2. Attempts standard decryption
3. Falls back to raw recovery if decryption fails
4. Preserves original files with `.recovered` suffix

### Manual ACL Reset

If ACL restrictions are stuck:

```powershell
# Reset all permissions on folder
icacls "G:\games\StuckFolder" /reset /T

# Grant full control to current user
$user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
icacls "G:\games\StuckFolder" /grant "${user}:F" /T

# Take ownership (if needed)
takeown /f "G:\games\StuckFolder" /r /d y
```

---

## Advanced Diagnostics

### Checking Folder State

```powershell
# Check if folder is locked
function Test-FolderLocked {
    param([string]$Path)

    $marker = Join-Path $Path ".gamelocker"
    $encFiles = Get-ChildItem $Path -Recurse -Filter "*.enc" -ErrorAction SilentlyContinue

    [PSCustomObject]@{
        Path = $Path
        HasMarker = Test-Path $marker
        EncryptedFiles = $encFiles.Count
        IsLocked = (Test-Path $marker) -or ($encFiles.Count -gt 0)
    }
}

Test-FolderLocked "G:\games\TestGame"
```

### Viewing Encryption Keys

```powershell
# Check key files exist
Get-ChildItem "C:\ProgramData\GameLocker" -Filter "*.dat" |
    Select-Object Name, Length, LastWriteTime
```

### Service Detailed Logging

For debugging service issues:

1. Edit `appsettings.json`:

    ```json
    {
    	"Logging": {
    		"LogLevel": {
    			"Default": "Debug",
    			"Microsoft.Hosting.Lifetime": "Information"
    		}
    	}
    }
    ```

2. Restart service and check logs

### Testing Encryption Manually

```powershell
# Create test file
"Test content" | Out-File "C:\temp\test.txt"

# Run encryption test project
dotnet run --project src/TestSelectiveEncryption

# Check results
Get-ChildItem "C:\temp" | Where-Object { $_.Name -like "test.*" }
```

---

## Getting Help

If issues persist:

1. **Check existing issues:** https://github.com/GauravKumawat2002/Game-Locker/issues
2. **Create new issue** with:
    - Windows version
    - GameLocker version
    - Steps to reproduce
    - Error messages/screenshots
    - Event log entries

---

## Quick Reference

| Problem               | Quick Fix                               |
| --------------------- | --------------------------------------- |
| Service won't start   | Check Event Log, verify paths           |
| Game crashes          | Use ForceDecrypt, avoid .exe encryption |
| Files still encrypted | Run UnlockTool manually                 |
| ACL stuck             | Use `icacls /reset`                     |
| ConfigUI crashes      | Run as Administrator                    |
| Keys missing          | Reinstall (files may be unrecoverable)  |
