# GameLocker Architecture Overview

This document provides a comprehensive overview of the GameLocker system architecture for developers who want to understand, contribute to, or extend the project.

## Table of Contents

1. [System Overview](#system-overview)
2. [Project Structure](#project-structure)
3. [Component Diagram](#component-diagram)
4. [Key Design Decisions](#key-design-decisions)
5. [Data Flow](#data-flow)
6. [Security Model](#security-model)

---

## System Overview

GameLocker is a Windows-based application that enforces gaming schedules by locking game folders outside of allowed time windows. It consists of three main components:

| Component | Type | Purpose |
|-----------|------|---------|
| **GameLocker.Service** | Windows Service | Runs in background, enforces schedule, locks/unlocks folders automatically |
| **GameLocker.ConfigUI** | WinForms App | User interface for configuration and manual control |
| **GameLocker.Common** | Class Library | Shared code for encryption, configuration, and folder management |

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         USER LAYER                              │
│  ┌─────────────────┐                    ┌─────────────────┐    │
│  │  ConfigUI App   │◄──── Config ─────►│  Windows UI     │    │
│  │  (WinForms)     │                    │  (Desktop)      │    │
│  └────────┬────────┘                    └─────────────────┘    │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                  CONFIGURATION LAYER                      │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐  │   │
│  │  │ ConfigMgr   │  │ JSON/DPAPI   │  │ FolderSettings │  │   │
│  │  └─────────────┘  └──────────────┘  └────────────────┘  │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                        SERVICE LAYER                            │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │               GameLocker.Service                         │   │
│  │  ┌──────────┐  ┌──────────────┐  ┌─────────────────┐   │   │
│  │  │ Scheduler│  │ FolderLocker │  │ Notifications   │   │   │
│  │  └──────────┘  └──────────────┘  └─────────────────┘   │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                       SECURITY LAYER                            │
│  ┌─────────────────┐  ┌─────────────────┐  ┌───────────────┐   │
│  │  AES-256        │  │  NTFS ACL       │  │  DPAPI Keys   │   │
│  │  Encryption     │  │  Restrictions   │  │  Protection   │   │
│  └─────────────────┘  └─────────────────┘  └───────────────┘   │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      FILE SYSTEM LAYER                          │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                   NTFS Game Folders                       │   │
│  │  • Encrypted files (.enc)                                 │   │
│  │  • Lock markers (.gamelocker)                             │   │
│  │  • Original files (when unlocked)                         │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
code/
├── GameLocker.sln                 # Visual Studio Solution
├── src/
│   ├── GameLocker.Common/         # Shared class library
│   │   ├── Configuration/
│   │   │   └── ConfigManager.cs   # Config load/save with encryption
│   │   ├── Encryption/
│   │   │   ├── AesEncryptionHelper.cs  # AES-256 streaming encryption
│   │   │   └── DpapiHelper.cs          # Windows DPAPI key protection
│   │   ├── Models/
│   │   │   ├── GameLockerConfig.cs     # Main config model
│   │   │   ├── FolderState.cs          # Folder lock states
│   │   │   ├── FolderEncryptionSettings.cs  # Per-folder settings
│   │   │   └── SelectiveEncryptionSettings.cs  # Extension presets
│   │   ├── Notifications/
│   │   │   └── NotificationHelper.cs   # Windows toast notifications
│   │   ├── Security/
│   │   │   └── AclHelper.cs            # NTFS ACL management
│   │   └── Services/
│   │       ├── FolderLocker.cs         # Main locking service
│   │       └── FileExtensionScanner.cs # Dynamic file type discovery
│   │
│   ├── GameLocker.ConfigUI/       # WinForms configuration app
│   │   ├── Form1.cs               # Main form logic
│   │   ├── Form1.Designer.cs      # UI layout
│   │   └── Program.cs             # Entry point
│   │
│   └── GameLocker.Service/        # Windows Service
│       ├── GameLockerService.cs   # Service implementation
│       ├── Program.cs             # Host builder
│       └── appsettings.json       # Service configuration
│
├── installer/                     # Installation scripts
│   ├── PowerShell/                # PowerShell installer
│   ├── NSIS/                      # NSIS installer (alternative)
│   └── WiX/                       # WiX installer (future)
│
├── docs/                          # Documentation
├── ForceDecrypt/                  # Emergency recovery tool
└── UnlockTool/                    # Development unlock utility
```

---

## Component Diagram

### GameLocker.Common

The shared library contains all business logic:

```
GameLocker.Common
├── Configuration/
│   └── ConfigManager
│       ├── LoadAsync() → Load encrypted config
│       ├── SaveAsync() → Save encrypted config
│       └── InitializeAsync() → Setup encryption keys
│
├── Encryption/
│   ├── AesEncryptionHelper
│   │   ├── EncryptStream() → Stream-based encryption (large files)
│   │   ├── DecryptStream() → Stream-based decryption
│   │   ├── Encrypt() → In-memory encryption (small data)
│   │   ├── Decrypt() → In-memory decryption
│   │   ├── GenerateKey() → Create 256-bit AES key
│   │   └── GenerateIV() → Create initialization vector
│   │
│   └── DpapiHelper
│       ├── ProtectToFileAsync() → Save DPAPI-protected data
│       └── UnprotectFromFileAsync() → Load DPAPI-protected data
│
├── Models/
│   ├── GameLockerConfig → Schedule, folders, notifications
│   ├── FolderEncryptionSettings → Per-folder extension selection
│   ├── SelectiveEncryptionSettings → Preset-based encryption
│   └── FolderState (enum) → Locked/Unlocked/Unknown
│
├── Security/
│   └── AclHelper
│       ├── DenyAccess() → Apply deny ACL rules
│       └── AllowAccess() → Remove deny ACL rules
│
├── Services/
│   ├── FolderLocker
│   │   ├── InitializeAsync() → Load/generate encryption keys
│   │   ├── LockFolderAsync() → Encrypt + ACL restrict
│   │   ├── UnlockFolderAsync() → Decrypt + ACL restore
│   │   ├── LockFolderWithCustomExtensionsAsync() → Selective encryption
│   │   └── GetFolderState() → Check current lock state
│   │
│   └── FileExtensionScanner
│       ├── ScanFolderExtensions() → Discover file types
│       ├── CategorizeExtension() → Group by type
│       └── AssessRiskLevel() → Safety categorization
│
└── Notifications/
    └── NotificationHelper
        └── ShowNotification() → Windows toast
```

---

## Key Design Decisions

### 1. Dual-Layer Security

We use **both** encryption and ACL restrictions for maximum protection:

```csharp
// Step 1: Encrypt all files
await EncryptFolderContentsAsync(folderPath);

// Step 2: Apply ACL deny rules
AclHelper.DenyAccess(folderPath);
```

**Why both?**
- Encryption alone: Files remain accessible (just garbled)
- ACL alone: Admin can easily remove restrictions
- Both together: Files are inaccessible AND encrypted

### 2. Streaming Encryption

For games with large files (2GB+), we use streaming encryption:

```csharp
public static void EncryptStream(Stream source, Stream destination, byte[] key, byte[] iv)
{
    using var aes = Aes.Create();
    aes.Key = key;
    aes.IV = iv;
    aes.Mode = CipherMode.CBC;
    aes.Padding = PaddingMode.PKCS7;

    using var cryptoStream = new CryptoStream(destination, aes.CreateEncryptor(), CryptoStreamMode.Write);
    source.CopyTo(cryptoStream);  // 64KB buffer, memory efficient
}
```

### 3. Per-Folder Extension Selection

Instead of a one-size-fits-all approach, each folder can have custom settings:

```csharp
public class FolderEncryptionSettings
{
    public string FolderPath { get; set; }
    public bool UseCustomSelection { get; set; }
    public List<string> SelectedExtensions { get; set; }  // User picks these
}
```

### 4. DPAPI Key Protection

Encryption keys are protected using Windows DPAPI with machine scope:

```csharp
var protectedData = ProtectedData.Protect(
    plainBytes, 
    null, 
    DataProtectionScope.LocalMachine  // Tied to this machine
);
```

---

## Data Flow

### Locking a Folder

```
User clicks "Lock" → ConfigUI
         │
         ▼
┌─────────────────────────────────────────┐
│ 1. Load encryption keys from DPAPI      │
│ 2. Scan folder for files matching       │
│    user-selected extensions             │
│ 3. Create .gamelocker marker file       │
│ 4. For each file:                       │
│    - Stream encrypt to .enc file        │
│    - Delete original                    │
│    - Set .enc file as hidden            │
│ 5. Apply ACL deny rules to folder       │
│ 6. Show notification                    │
└─────────────────────────────────────────┘
         │
         ▼
Folder is now locked (encrypted + ACL protected)
```

### Service Schedule Check

```
┌─────────────────────────────────────────┐
│         GameLocker.Service              │
│                                         │
│  Every 1 minute (configurable):         │
│  ┌───────────────────────────────────┐  │
│  │ 1. Load configuration             │  │
│  │ 2. Check IsWithinAllowedTime()    │  │
│  │ 3. For each game folder:          │  │
│  │    - If allowed && locked:        │  │
│  │        → Unlock folder            │  │
│  │    - If not allowed && unlocked:  │  │
│  │        → Lock folder              │  │
│  │ 4. Check for warning times        │  │
│  │    - 15 min before: notify        │  │
│  │    - 5 min before: notify         │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

---

## Security Model

### Attack Surface Analysis

| Attack Vector | Protection | Bypass Difficulty |
|---------------|------------|-------------------|
| User opens folder | ACL Deny rules | Impossible without admin |
| User reads .enc file | AES-256 encryption | Cryptographically secure |
| User deletes .gamelocker | Files stay encrypted | Useless without keys |
| Admin stops service | Files stay encrypted | Files remain protected |
| Extract keys from memory | DPAPI machine-bound | Requires forensics |
| Boot from USB | DPAPI tied to machine | Keys inaccessible |

### File System During Lock

```
Game Folder/
├── .gamelocker                    ← Encrypted lock marker (hidden)
├── game.exe.enc                   ← Encrypted executable (hidden)
├── saves/
│   └── player.sav.enc            ← Encrypted save file (hidden)
└── config/
    └── settings.ini.enc          ← Encrypted config (hidden)

Folder permissions: DENY access for Users, Everyone, Authenticated Users
```

### Key Storage

```
C:\ProgramData\GameLocker\
├── config.dat     ← Encrypted configuration (AES)
├── folder_key.dat ← DPAPI-protected AES key
└── folder_iv.dat  ← DPAPI-protected IV
```

---

## Next Steps

- See [02-getting-started.md](02-getting-started.md) for development environment setup
- See [03-api-reference.md](03-api-reference.md) for detailed API documentation
- See [04-contributing.md](04-contributing.md) for contribution guidelines
