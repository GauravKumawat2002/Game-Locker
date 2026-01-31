# API Reference

Complete API documentation for GameLocker components. This reference is intended for developers extending or integrating with GameLocker.

## Table of Contents

1. [GameLocker.Common](#gamelockercommon)
   - [Configuration](#configuration)
   - [Encryption](#encryption)
   - [Models](#models)
   - [Security](#security)
   - [Services](#services)
   - [Notifications](#notifications)

---

## GameLocker.Common

The shared class library containing all core functionality.

---

### Configuration

#### ConfigManager

Manages loading and saving encrypted configuration.

**Namespace:** `GameLocker.Common.Configuration`

```csharp
public class ConfigManager
{
    public ConfigManager(string configPath);
    
    public async Task InitializeAsync();
    public async Task<GameLockerConfig> LoadAsync();
    public async Task SaveAsync(GameLockerConfig config);
}
```

**Constructor Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `configPath` | `string` | Directory path for configuration files |

**Methods:**

| Method | Returns | Description |
|--------|---------|-------------|
| `InitializeAsync()` | `Task` | Initializes encryption keys (creates if not exist) |
| `LoadAsync()` | `Task<GameLockerConfig>` | Loads and decrypts configuration |
| `SaveAsync(config)` | `Task` | Encrypts and saves configuration |

**Usage Example:**

```csharp
var configPath = @"C:\ProgramData\GameLocker";
var configManager = new ConfigManager(configPath);

// Initialize (creates keys if needed)
await configManager.InitializeAsync();

// Load configuration
var config = await configManager.LoadAsync();

// Modify and save
config.AllowedDays.Add(DayOfWeek.Saturday);
await configManager.SaveAsync(config);
```

---

### Encryption

#### AesEncryptionHelper

Static helper for AES-256 encryption operations.

**Namespace:** `GameLocker.Common.Encryption`

```csharp
public static class AesEncryptionHelper
{
    public static byte[] GenerateKey();
    public static byte[] GenerateIV();
    
    public static byte[] Encrypt(byte[] data, byte[] key, byte[] iv);
    public static byte[] Decrypt(byte[] encryptedData, byte[] key, byte[] iv);
    
    public static void EncryptStream(Stream source, Stream destination, byte[] key, byte[] iv);
    public static void DecryptStream(Stream source, Stream destination, byte[] key, byte[] iv);
}
```

**Methods:**

| Method | Returns | Description |
|--------|---------|-------------|
| `GenerateKey()` | `byte[]` | Generates 256-bit AES key |
| `GenerateIV()` | `byte[]` | Generates 128-bit initialization vector |
| `Encrypt()` | `byte[]` | Encrypts byte array in memory |
| `Decrypt()` | `byte[]` | Decrypts byte array in memory |
| `EncryptStream()` | `void` | Stream-based encryption for large files |
| `DecryptStream()` | `void` | Stream-based decryption for large files |

**Usage Example:**

```csharp
// Generate keys
var key = AesEncryptionHelper.GenerateKey();
var iv = AesEncryptionHelper.GenerateIV();

// In-memory encryption (small data)
var data = Encoding.UTF8.GetBytes("Secret data");
var encrypted = AesEncryptionHelper.Encrypt(data, key, iv);
var decrypted = AesEncryptionHelper.Decrypt(encrypted, key, iv);

// Streaming encryption (large files)
using var source = File.OpenRead("largefile.bin");
using var dest = File.Create("largefile.bin.enc");
AesEncryptionHelper.EncryptStream(source, dest, key, iv);
```

---

#### DpapiHelper

Windows DPAPI integration for key protection.

**Namespace:** `GameLocker.Common.Encryption`

```csharp
public static class DpapiHelper
{
    public static async Task ProtectToFileAsync(byte[] data, string filePath);
    public static async Task<byte[]> UnprotectFromFileAsync(string filePath);
}
```

**Methods:**

| Method | Returns | Description |
|--------|---------|-------------|
| `ProtectToFileAsync()` | `Task` | Protects data with DPAPI and saves to file |
| `UnprotectFromFileAsync()` | `Task<byte[]>` | Loads and unprotects DPAPI-encrypted data |

**Security Note:** Uses `DataProtectionScope.LocalMachine` - keys are tied to the machine and cannot be extracted or used elsewhere.

**Usage Example:**

```csharp
var key = AesEncryptionHelper.GenerateKey();

// Protect and save
await DpapiHelper.ProtectToFileAsync(key, @"C:\ProgramData\GameLocker\key.dat");

// Load and unprotect
var loadedKey = await DpapiHelper.UnprotectFromFileAsync(@"C:\ProgramData\GameLocker\key.dat");
```

---

### Models

#### GameLockerConfig

Main configuration model.

**Namespace:** `GameLocker.Common.Models`

```csharp
public class GameLockerConfig
{
    public List<DayOfWeek> AllowedDays { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DurationHours { get; set; }
    public List<string> GameFolderPaths { get; set; }
    public int PollingIntervalMinutes { get; set; }
    public bool NotificationsEnabled { get; set; }
    public SelectiveEncryptionSettings EncryptionSettings { get; set; }
    public List<FolderEncryptionSettings> FolderEncryptionSettings { get; set; }
    
    public bool IsWithinAllowedTime(DateTime currentTime);
    public DateTime? GetNextUnlockTime(DateTime currentTime);
    public DateTime? GetNextLockTime(DateTime currentTime);
    public FolderEncryptionSettings? GetFolderEncryptionSettings(string folderPath);
    public void SetFolderEncryptionSettings(FolderEncryptionSettings settings);
    public FolderEncryptionSettings GetEffectiveFolderSettings(string folderPath);
}
```

**Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AllowedDays` | `List<DayOfWeek>` | Empty | Days when gaming is allowed |
| `StartTime` | `TimeOnly` | 18:00 | Start of gaming window |
| `DurationHours` | `int` | 2 | Length of gaming window |
| `GameFolderPaths` | `List<string>` | Empty | Paths to game folders |
| `PollingIntervalMinutes` | `int` | 1 | Service check interval |
| `NotificationsEnabled` | `bool` | true | Show toast notifications |
| `EncryptionSettings` | `SelectiveEncryptionSettings` | Default | Global encryption presets |
| `FolderEncryptionSettings` | `List<FolderEncryptionSettings>` | Empty | Per-folder settings |

**Methods:**

| Method | Returns | Description |
|--------|---------|-------------|
| `IsWithinAllowedTime(time)` | `bool` | Check if gaming allowed at given time |
| `GetNextUnlockTime(time)` | `DateTime?` | Next scheduled unlock time |
| `GetNextLockTime(time)` | `DateTime?` | Next scheduled lock time |
| `GetFolderEncryptionSettings(path)` | `FolderEncryptionSettings?` | Get settings for folder |
| `SetFolderEncryptionSettings(settings)` | `void` | Save settings for folder |
| `GetEffectiveFolderSettings(path)` | `FolderEncryptionSettings` | Get or create settings |

---

#### FolderEncryptionSettings

Per-folder encryption configuration.

**Namespace:** `GameLocker.Common.Models`

```csharp
public class FolderEncryptionSettings
{
    public string FolderPath { get; set; }
    public bool UseCustomSelection { get; set; }
    public List<string> SelectedExtensions { get; set; }
    public DateTime? LastScanned { get; set; }
    public SelectiveEncryptionSettings? FallbackPreset { get; set; }
    
    public bool ShouldEncryptFile(string fileName);
    public string GetEncryptionSummary();
    public (int total, int selected) GetStats();
}
```

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `FolderPath` | `string` | Full path to game folder |
| `UseCustomSelection` | `bool` | Use custom extensions vs preset |
| `SelectedExtensions` | `List<string>` | Extensions user selected |
| `LastScanned` | `DateTime?` | When folder was last scanned |
| `FallbackPreset` | `SelectiveEncryptionSettings?` | Fallback if no custom selection |

**Usage Example:**

```csharp
var settings = new FolderEncryptionSettings
{
    FolderPath = @"G:\games\TestGame",
    UseCustomSelection = true,
    SelectedExtensions = new List<string> { ".sav", ".ini", ".cfg" },
    LastScanned = DateTime.Now
};

// Check if file should be encrypted
if (settings.ShouldEncryptFile("savegame.sav"))
{
    // Encrypt this file
}
```

---

#### FolderState

Enum for folder lock states.

**Namespace:** `GameLocker.Common.Models`

```csharp
public enum FolderState
{
    Unknown = 0,
    Locked = 1,
    Unlocked = 2
}
```

---

### Security

#### AclHelper

NTFS ACL management for folder protection.

**Namespace:** `GameLocker.Common.Security`

```csharp
public static class AclHelper
{
    public static void DenyAccess(string folderPath);
    public static void AllowAccess(string folderPath);
}
```

**Methods:**

| Method | Description |
|--------|-------------|
| `DenyAccess(path)` | Applies deny rules for Users, Everyone, Authenticated Users |
| `AllowAccess(path)` | Removes deny rules, restores default access |

**Applied ACL Rules:**
- `BUILTIN\Users` - Deny FullControl
- `NT AUTHORITY\Everyone` - Deny FullControl
- `NT AUTHORITY\Authenticated Users` - Deny FullControl
- Current User SID - Deny FullControl

**Usage Example:**

```csharp
// Lock folder
AclHelper.DenyAccess(@"G:\games\TestGame");

// Unlock folder
AclHelper.AllowAccess(@"G:\games\TestGame");
```

---

### Services

#### FolderLocker

Main service for locking and unlocking game folders.

**Namespace:** `GameLocker.Common.Services`

```csharp
public class FolderLocker
{
    public FolderLocker(string keyStorePath);
    
    public async Task InitializeAsync();
    public async Task<GameFolderInfo> LockFolderAsync(string folderPath);
    public async Task<GameFolderInfo> UnlockFolderAsync(string folderPath);
    public async Task<GameFolderInfo> LockFolderWithCustomExtensionsAsync(string folderPath, FolderEncryptionSettings settings);
    public FolderState GetFolderState(string folderPath);
    public bool HasLockMarker(string folderPath);
}
```

**Constructor Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `keyStorePath` | `string` | Directory for encryption keys |

**Methods:**

| Method | Returns | Description |
|--------|---------|-------------|
| `InitializeAsync()` | `Task` | Load or generate encryption keys |
| `LockFolderAsync(path)` | `Task<GameFolderInfo>` | Encrypt all files + apply ACL |
| `UnlockFolderAsync(path)` | `Task<GameFolderInfo>` | Decrypt files + remove ACL |
| `LockFolderWithCustomExtensionsAsync(path, settings)` | `Task<GameFolderInfo>` | Lock with custom extension selection |
| `GetFolderState(path)` | `FolderState` | Check current lock state |
| `HasLockMarker(path)` | `bool` | Check if .gamelocker exists |

**Usage Example:**

```csharp
var locker = new FolderLocker(@"C:\ProgramData\GameLocker");
await locker.InitializeAsync();

// Lock folder (encrypts all files)
var result = await locker.LockFolderAsync(@"G:\games\TestGame");
Console.WriteLine($"State: {result.State}");

// Lock with custom extensions
var settings = new FolderEncryptionSettings
{
    FolderPath = @"G:\games\TestGame",
    UseCustomSelection = true,
    SelectedExtensions = new List<string> { ".sav", ".ini" }
};
var result2 = await locker.LockFolderWithCustomExtensionsAsync(@"G:\games\TestGame", settings);

// Unlock folder
var unlockResult = await locker.UnlockFolderAsync(@"G:\games\TestGame");
```

---

#### FileExtensionScanner

Scans folders to discover file types.

**Namespace:** `GameLocker.Common.Services`

```csharp
public class FileExtensionScanner
{
    public ExtensionScanResult ScanFolderExtensions(string folderPath, bool recursive = true);
    public List<string> GetExtensionPreview(string folderPath, int maxFiles = 100);
}
```

**Methods:**

| Method | Returns | Description |
|--------|---------|-------------|
| `ScanFolderExtensions(path, recursive)` | `ExtensionScanResult` | Full scan with statistics |
| `GetExtensionPreview(path, maxFiles)` | `List<string>` | Quick preview of extensions |

**Usage Example:**

```csharp
var scanner = new FileExtensionScanner();
var result = scanner.ScanFolderExtensions(@"G:\games\TestGame");

Console.WriteLine($"Total files: {result.TotalFilesFound}");
Console.WriteLine($"Unique extensions: {result.UniqueExtensions}");

foreach (var ext in result.Extensions.Values.OrderByDescending(e => e.FileCount))
{
    Console.WriteLine($"{ext.Extension}: {ext.FileCount} files ({ext.FormattedSize}) - {ext.RiskLevel}");
}
```

---

#### ExtensionScanResult

Result from folder scanning.

```csharp
public class ExtensionScanResult
{
    public string FolderPath { get; set; }
    public DateTime ScannedAt { get; set; }
    public int TotalFilesFound { get; set; }
    public int UniqueExtensions { get; set; }
    public Dictionary<string, ExtensionInfo> Extensions { get; set; }
    public string? ErrorMessage { get; set; }
    
    public Dictionary<string, List<ExtensionInfo>> GetExtensionsByCategory();
    public List<ExtensionInfo> GetExtensionsByRisk();
}
```

---

#### ExtensionInfo

Information about a file extension.

```csharp
public class ExtensionInfo
{
    public string Extension { get; set; }
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    public List<string> ExampleFiles { get; set; }
    public string Category { get; set; }
    public RiskLevel RiskLevel { get; set; }
    
    public string FormattedSize { get; }
    public string Description { get; }
    public string ExampleFilesList { get; }
}
```

---

#### RiskLevel

Enum for encryption risk categorization.

```csharp
public enum RiskLevel
{
    Safe = 0,       // Save files, configs - safe to encrypt
    Moderate = 1,   // Cache, images - usually safe
    High = 2,       // Mods, game assets - may cause issues
    Dangerous = 3,  // Executables, DLLs - will cause crashes
    Unknown = 4     // Unknown file type
}
```

---

### Notifications

#### NotificationHelper

Windows toast notification helper.

**Namespace:** `GameLocker.Common.Notifications`

```csharp
public static class NotificationHelper
{
    public static void ShowNotification(string title, string message);
}
```

**Usage Example:**

```csharp
NotificationHelper.ShowNotification(
    "GameLocker", 
    "Gaming time ends in 5 minutes!"
);
```

---

## GameFolderInfo

Result structure returned from lock/unlock operations.

```csharp
public class GameFolderInfo
{
    public string Path { get; set; }
    public FolderState State { get; set; }
    public DateTime? LastStateChange { get; set; }
    public string? LastError { get; set; }
}
```

---

## Complete Integration Example

```csharp
using GameLocker.Common.Configuration;
using GameLocker.Common.Models;
using GameLocker.Common.Services;

// Setup
var configPath = @"C:\ProgramData\GameLocker";
var configManager = new ConfigManager(configPath);
var folderLocker = new FolderLocker(configPath);
var scanner = new FileExtensionScanner();

// Initialize
await configManager.InitializeAsync();
await folderLocker.InitializeAsync();

// Load or create config
var config = await configManager.LoadAsync();

// Add game folder
var gamePath = @"G:\games\TestGame";
if (!config.GameFolderPaths.Contains(gamePath))
{
    config.GameFolderPaths.Add(gamePath);
}

// Scan folder for extensions
var scanResult = scanner.ScanFolderExtensions(gamePath);
Console.WriteLine($"Found {scanResult.UniqueExtensions} file types");

// Configure per-folder settings
var folderSettings = new FolderEncryptionSettings
{
    FolderPath = gamePath,
    UseCustomSelection = true,
    SelectedExtensions = scanResult.Extensions.Values
        .Where(e => e.RiskLevel == RiskLevel.Safe)
        .Select(e => e.Extension)
        .ToList(),
    LastScanned = DateTime.Now
};
config.SetFolderEncryptionSettings(folderSettings);

// Save config
await configManager.SaveAsync(config);

// Lock folder with custom settings
var lockResult = await folderLocker.LockFolderWithCustomExtensionsAsync(gamePath, folderSettings);
Console.WriteLine($"Folder state: {lockResult.State}");

// Later: unlock
var unlockResult = await folderLocker.UnlockFolderAsync(gamePath);
Console.WriteLine($"Folder state: {unlockResult.State}");
```

---

## Next Steps

- See [01-architecture.md](01-architecture.md) for system design
- See [02-getting-started.md](02-getting-started.md) for setup guide
- See [04-contributing.md](04-contributing.md) for contribution guidelines
