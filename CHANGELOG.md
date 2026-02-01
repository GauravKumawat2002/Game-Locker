# Changelog

All notable changes to GameLocker will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.2.0] - 2026-02-01

### Fixed

- **Critical: ACL Permission Removal** - Fixed issue where unlock operations weren't properly removing DENY ACL rules
    - Unlock now removes ALL deny rules, not just specific ones
    - Added `RemoveAccessRuleAll` to ensure complete cleanup
    - Restores Full Control permissions to Users and Authenticated Users groups

- **Critical: Unlock Without Lock Marker** - Fixed unlock failing silently when lock marker was missing
    - Unlock now ALWAYS removes ACL restrictions regardless of lock marker presence
    - Ensures folders can be recovered even if the `.gamelocker` marker file is deleted

- **Critical: Folder Decryption on Removal** - Fixed major bug where removing a folder from config left files encrypted
    - Files are now decrypted DIRECTLY by the ConfigUI before removal from config
    - Added live progress dialog showing decryption status
    - Handles hidden encrypted files properly
    - No longer relies on service-based decryption (which had timing issues)

- **Decryption Progress UI** - Fixed progress dialog not appearing during decryption
    - Changed from non-modal to modal dialog (ShowDialog)
    - Runs decryption in background task for responsive UI
    - Uses DirectoryInfo to find hidden .enc files
    - Unhides files before decryption

- **Service-UI Communication** - Fixed disconnect between ConfigUI and running service
    - Added `ServiceManager` class for real-time service communication
    - ConfigUI now sends immediate lock/unlock commands when folders are added/removed
    - Service responds within 5 seconds instead of waiting for polling interval

- **Faster Command Processing** - Reduced polling interval from 1 minute to 5 seconds
    - Service checks for immediate commands every 5 seconds
    - Much faster response to configuration changes

### Changed

- **Improved Service Polling** - Service now uses 5-second intervals for command checks
- **Better Error Handling** - ACL operations now continue even on partial failures
- **Enhanced Logging** - More detailed logs for unlock and ACL operations
- **Folder Removal Workflow** - ConfigUI now handles decryption directly with progress feedback

### Added

- **ServiceManager** - New class for ConfigUI to communicate with the Windows Service
    - `SendImmediateActionCommandAsync()` - Send lock/unlock commands immediately
    - `SendConfigReloadCommandAsync()` - Force service to reload configuration
    - `GetServiceStatus()`, `StartServiceAsync()`, `StopServiceAsync()` - Service management

- **DecryptSingleFileAsync** - New public API in FolderLocker for UI-based decryption

- **Progress Dialog** - Visual feedback during folder decryption
    - Shows current file being decrypted
    - Progress bar with file count
    - Success/failure summary

- **UnlockTool** - Emergency decryption utility for recovery scenarios
    - Standalone tool to decrypt locked folders
    - Useful for data recovery when service is unavailable

---

## [1.1.0] - 2026-01-31

### Added

- **Dynamic File Extension Selection** - Scan game folders to discover actual file types
    - Per-folder custom extension configuration
    - Checkbox-based manual selection in ConfigUI
    - Risk level indicators (Safe/Moderate/High/Dangerous)
    - Real-time file count and size statistics

- **FileExtensionScanner Service** - Intelligent file type discovery
    - Categorizes extensions by type (Save Files, Configuration, Executables, etc.)
    - Risk assessment to prevent game corruption
    - Example file previews

- **Enhanced FolderLocker** - Selective encryption support
    - `LockFolderWithCustomExtensionsAsync()` for per-folder settings
    - Skips dangerous file types (.exe, .dll, .bin)
    - Detailed encryption summary logging

- **ForceDecrypt Tool** - Emergency file recovery
    - Recovers encrypted files when normal decryption fails
    - Raw recovery fallback for padding errors
    - Handles corrupted encryption scenarios

- **Complete Developer Documentation**
    - Architecture overview
    - Getting started guide
    - API reference
    - Contributing guidelines
    - Troubleshooting guide

### Changed

- ConfigUI completely redesigned with extension selection panel
- Improved safety indicators in UI
- Enhanced logging for encryption operations

### Fixed

- Resolved Hogwarts Legacy corruption issue (encrypted .exe files)
- Fixed padding errors during decryption of interrupted files

---

## [1.0.0] - 2026-01-30

### Added

- **AES-256 File Encryption**
    - Streaming encryption for large files (2GB+)
    - 64KB buffer for memory efficiency
    - Encrypted files stored with `.enc` extension

- **DPAPI Key Protection**
    - Encryption keys secured using Windows DPAPI
    - Machine-scope protection (keys tied to computer)
    - Automatic key generation on first run

- **NTFS ACL Protection**
    - Multi-layered access denial
    - Blocks Users, Everyone, and Authenticated Users groups
    - Prevents access even with Explorer

- **Windows Service**
    - Automatic schedule enforcement
    - Background operation
    - Auto-start on system boot

- **Configuration UI (WinForms)**
    - User-friendly folder management
    - Schedule configuration
    - Real-time status display

- **Desktop Notifications**
    - Lock/unlock notifications
    - 15-minute and 5-minute warnings
    - Service status alerts

- **PowerShell Installer**
    - One-click installation
    - Self-contained deployment (~161 MB)
    - Automatic service registration
    - Start Menu integration

- **Development Tools**
    - UnlockTool for development testing
    - ServiceManager.ps1 for service control
    - TestSuite.ps1 for validation

### Security

- Dual-layer protection (encryption + ACL)
- Lock marker files for state tracking
- Hidden encrypted files

---

## [Unreleased]

### Planned

- System tray icon for quick access
- Emergency unlock PIN with time delay
- Usage statistics and gaming time tracking
- Multiple schedule profiles
- WiX MSI installer option
- Configuration backup/restore
- Game launch detection

---

## Version History Summary

| Version | Date       | Highlights                                     |
| ------- | ---------- | ---------------------------------------------- |
| 1.1.0   | 2026-01-31 | Dynamic extension selection, ForceDecrypt tool |
| 1.0.0   | 2026-01-30 | Initial release with full encryption + ACL     |

---

## Upgrade Notes

### From 1.0.0 to 1.1.0

1. **Backup existing configuration**

    ```powershell
    Copy-Item "C:\ProgramData\GameLocker" "C:\ProgramData\GameLocker.bak" -Recurse
    ```

2. **Install new version** using the installer

3. **Configure per-folder extensions** (optional)
    - Open ConfigUI
    - Select each folder
    - Click "Scan Extensions"
    - Select only safe file types

4. **Migration is automatic** - existing settings are preserved

---

## Reporting Issues

Found a bug? Have a feature request?

- GitHub Issues: https://github.com/GauravKumawat2002/Game-Locker/issues
- Include version number, Windows version, and steps to reproduce
