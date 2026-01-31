# GameLocker

A Windows system service + configuration UI application that enforces a **game access schedule** by locking game folders outside allowed times. Perfect for maintaining discipline and controlling gaming habits.

## 🚀 Latest Update (v1.1.0)

### ✨ **Dynamic File Extension Selection** (NEW!)

- **🔍 Smart Extension Discovery**: Scan your game folders to see ALL file types that exist
- **☑️ Checkbox Selection**: Manually select which file types to encrypt with checkboxes
- **🎯 Per-Folder Settings**: Each game folder can have its own unique encryption configuration
- **⚠️ Risk Indicators**: Clear safety labels (Safe/Moderate/High/Dangerous) to prevent game corruption
- **📊 Real-Time Stats**: See file counts and sizes for each extension type

### 🛡️ **Enhanced Security**

- **🔐 AES-256 File Encryption**: Streaming encryption for large files (2GB+)
- **🛡️ NTFS ACL Protection**: Multi-layer access denial for Users, Everyone, and Authenticated Users
- **⚡ Dual-Layer Security**: Combined encryption + ACL for robust protection
- **🔑 DPAPI Key Protection**: Machine-bound encryption keys

### 📦 **Complete Installer Package**

- **🚀 One-Click Installation**: Professional PowerShell installer
- **📋 Self-Contained**: Includes .NET 8.0 runtime (~161 MB)
- **🔧 Service Auto-Setup**: Automatic Windows Service configuration
- **📱 Start Menu Integration**: Easy access shortcuts

### 🎯 **Quick Start**

1. Download `GameLocker-Installer-v1.1.0.zip`
2. Extract to temporary folder
3. Right-click `Setup.bat` → "Run as administrator"
4. Open "GameLocker Configuration" from Start Menu
5. Add folder → Click "Scan Extensions" → Select safe file types → Save!

---

## 🎮 Features

### **Security Features**

- **AES-256 Encryption**: Game files encrypted during lock periods using streaming encryption for large files
- **NTFS ACL Enforcement**: Multi-layered access denial blocking all user groups
- **DPAPI Key Protection**: Encryption keys secured using Windows Data Protection API
- **Streaming Encryption**: Memory-efficient encryption handles files larger than 2GB
- **Selective Encryption**: Choose which file types to encrypt per folder (v1.1.0)

### **Scheduling & Automation**

- **Scheduled Access Control**: Define specific days and time windows for gaming
- **Windows Service**: Runs automatically at startup, enforces schedule continuously
- **Smart Warnings**: Notifications at 15 and 5 minutes before lock time
- **Background Operation**: Service operates silently without user intervention

### **User Experience**

- **Modern Configuration UI**: User-friendly WinForms interface with real-time status
- **Desktop Notifications**: Visual notifications for lock/unlock events and warnings
- **Start Menu Integration**: Easy access to configuration and uninstaller
- **Complete Installer**: Professional setup experience with automated service registration

## 📋 System Requirements

- **Operating System**: Windows 10/11 (64-bit)
- **Runtime**: .NET 8.0 (included in installer)
- **Privileges**: Administrator rights for installation and ACL management
- **File System**: NTFS (required for ACL functionality)
- **Disk Space**: ~200MB for installation + temporary space equal to largest game folder

## 🏗️ Project Structure

```
GameLocker/
├── src/
│   ├── GameLocker.Common/          # Shared libraries
│   │   ├── Configuration/          # Config management
│   │   ├── Encryption/             # AES & DPAPI helpers
│   │   ├── Models/                 # Data models (config, settings)
│   │   ├── Notifications/          # Toast notifications
│   │   ├── Security/               # NTFS ACL helpers
│   │   └── Services/               # FolderLocker, FileExtensionScanner
│   ├── GameLocker.ConfigUI/        # WinForms configuration app
│   └── GameLocker.Service/         # Windows Service
├── installer/
│   ├── PowerShell/                 # PowerShell installer
│   ├── NSIS/                       # NSIS installer (alternative)
│   └── WiX/                        # WiX installer (future)
├── docs/                           # Developer documentation
│   ├── 01-architecture.md          # System architecture
│   ├── 02-getting-started.md       # Developer setup guide
│   ├── 03-api-reference.md         # API documentation
│   ├── 04-contributing.md          # Contribution guidelines
│   └── 05-troubleshooting.md       # Problem solving guide
├── ForceDecrypt/                   # Emergency recovery tool
├── UnlockTool/                     # Development unlock utility
├── CHANGELOG.md                    # Version history
├── GameLocker.sln                  # Solution file
└── README.md
```

## 🚀 Quick Start

### **Option 1: Use the Complete Installer (Recommended)**

1. **Download** the installer package:

    ```
    GameLocker-Installer-v1.0.zip (~161 MB)
    ```

2. **Extract** to any temporary folder

3. **Install** as Administrator:

    ```batch
    Right-click Setup.bat → "Run as administrator"
    ```

4. **Configure** your games:
    - Launch "GameLocker Configuration" from Start Menu
    - Add your game folder paths
    - Set your gaming schedule
    - Service automatically enforces your rules!

### **Option 2: Build from Source**

1. **Clone** the repository:

    ```bash
    git clone https://github.com/GauravKumawat2002/Game-Locker.git
    cd Game-Locker/code
    ```

2. **Build** the solution:

    ```powershell
    dotnet build
    ```

3. **Run Configuration UI**:
    ```powershell
    # Run as Administrator for full functionality
    dotnet run --project src/GameLocker.ConfigUI
    ```

### **Manual Service Installation (Advanced)**

If you prefer to install manually from source:

1. **Publish the service**:

    ```powershell
    dotnet publish src/GameLocker.Service -c Release -r win-x64 --self-contained -o publish/service
    dotnet publish src/GameLocker.ConfigUI -c Release -r win-x64 --self-contained -o publish/configui
    ```

2. **Install the service** (Run as Administrator):

    ```powershell
    sc.exe create "GameLocker Service" binpath= "C:\path\to\publish\service\GameLocker.Service.exe" start= auto
    sc.exe description "GameLocker Service" "Automatically locks and unlocks game folders based on configured schedules."
    ```

3. **Start the service**:
    ```powershell
    sc.exe start "GameLocker Service"
    ```

### **Uninstalling GameLocker**

**Using Start Menu:**

- Launch "Uninstall GameLocker" from Start Menu

**Manual Uninstall:**

```powershell
# Stop and remove service
sc.exe stop "GameLocker Service"
sc.exe delete "GameLocker Service"

# Run uninstaller
"C:\Program Files\GameLocker\GameLocker-Uninstall.ps1"
```

## ⚙️ Configuration

Use the ConfigUI application to set:

1. **Allowed Gaming Days**: Select which days of the week gaming is allowed
2. **Start Time**: When the gaming window opens
3. **Duration**: How many hours the gaming window lasts
4. **Game Folders**: Add paths to your game folders
5. **Notifications**: Enable/disable desktop notifications
6. **Polling Interval**: How often the service checks the schedule

Configuration is stored encrypted in:

```
C:\ProgramData\GameLocker\
├── config.dat     # Encrypted configuration
├── keys.dat       # DPAPI-protected AES key
└── iv.dat         # DPAPI-protected initialization vector
```

## 🔐 Security & How It Works

### **Dual-Layer Protection System**

**1. File Encryption (AES-256)**

- All game files are encrypted using AES-256-CBC with PKCS7 padding
- Streaming encryption handles files larger than 2GB efficiently (64KB buffer)
- Each installation uses unique 256-bit encryption keys
- Keys are protected using Windows DPAPI with LocalMachine scope

**2. Access Control Lists (ACL)**

- Comprehensive NTFS permission denial for multiple user groups:
    - `BUILTIN\Users` - Blocks regular user access
    - `NT AUTHORITY\Everyone` - Blocks all users
    - `NT AUTHORITY\Authenticated Users` - Blocks all logged-in users
    - Current user SID - Blocks even the folder owner
- Uses `FullControl` deny rules with inheritance for complete protection

**3. Service-Level Enforcement**

- Windows Service runs with LocalService privileges
- Continuous monitoring of gaming schedules
- Automatic lock/unlock based on configured time windows
- Resistant to user manipulation (requires admin to stop service)

### **File Structure During Lock**

```
Game Folder (Locked)/
├── .gamelocker                    # Hidden encrypted marker file
├── GameExecutable.exe.enc         # Encrypted game executable
├── GameData/
│   ├── config.xml.enc             # Encrypted config files
│   └── saves/
│       └── save1.dat.enc          # Encrypted save files
└── Assets/                        # Folders remain accessible for browsing
    ├── textures.pak.enc           # But content files are encrypted
    └── audio/
        └── music.mp3.enc          # All files encrypted recursively
```

### **Security Evaluation**

| Attack Vector                | Protection Level | Notes                                              |
| ---------------------------- | ---------------- | -------------------------------------------------- |
| **Regular User Access**      | 🔒 **Complete**  | ACL + Encryption blocks all access                 |
| **Administrative Bypass**    | ⚠️ **Deterrent** | Requires manual ACL modification + key extraction  |
| **Service Termination**      | ⚠️ **Partial**   | Admin can stop service, but files remain encrypted |
| **Boot from External Media** | 🔒 **Protected** | DPAPI keys tied to machine, encryption remains     |
| **Key Extraction**           | ⚠️ **Advanced**  | Requires admin + forensics knowledge               |

### **Bypass Difficulty Assessment**

- **Casual Users**: Effectively impossible without admin rights
- **Determined Admins**: Requires significant technical effort and time
- **Physical Access**: Keys are machine-bound, but extractable by experts

## 📱 Notifications

The service sends Windows toast notifications for:

- 🔒 **Folder Locked**: When gaming time ends
- 🎮 **Folder Unlocked**: When gaming time starts
- ⚠️ **Warning**: 15 minutes and 5 minutes before lock time
- ✅ **Service Started**: When the service begins monitoring
- ⏹️ **Service Stopped**: When the service stops

## 🛠️ Development

### **Prerequisites**

- .NET 8.0 SDK
- Visual Studio Code or Visual Studio 2022+
- Windows 10/11 with NTFS filesystem
- PowerShell 5.1+ (for installer scripts)

### **Recommended VS Code Extensions**

- C# Dev Kit
- C#
- .NET Install Tool
- PowerShell

### **Development Workflow**

**1. Clone and Build:**

```powershell
git clone https://github.com/GauravKumawat2002/Game-Locker.git
cd Game-Locker/code
dotnet build
```

**2. Test Configuration UI:**

```powershell
# Run as Administrator for full ACL functionality
dotnet run --project src/GameLocker.ConfigUI
```

**3. Test Service (Debug Mode):**

```powershell
# Run service directly (not as Windows Service)
dotnet run --project src/GameLocker.Service
```

**4. Build Complete Installer:**

```powershell
# Build release versions
dotnet publish src/GameLocker.Service -c Release -r win-x64 --self-contained -o publish/service
dotnet publish src/GameLocker.ConfigUI -c Release -r win-x64 --self-contained -o publish/configui

# Create installer package
cd installer/PowerShell
powershell -ExecutionPolicy Bypass -File "Package-Installer.ps1"
```

### **Development Tools**

**Unlock Tool** (`UnlockTool/`) - For development testing:

```powershell
# Unlock a locked folder during development
cd UnlockTool
dotnet run "C:\Path\To\Locked\Game\Folder"
```

**Installer Validation:**

```powershell
# Validate installer package structure
cd installer-package
powershell -ExecutionPolicy Bypass -File "Quick-Test.ps1"
```

### **Testing Security Features**

**Test File Encryption:**

1. Configure a test folder in ConfigUI
2. Enable lock → Verify files get `.enc` extensions
3. Try to access files → Should be denied + encrypted
4. Disable lock → Files should be decrypted and accessible

**Test ACL Protection:**

1. Lock a folder using the service
2. Try to access as regular user → Should get "Access Denied"
3. Check folder properties → Should see restrictive permissions
4. Unlock folder → Permissions should be restored

**Performance Testing:**

- Test with large game folders (>10GB)
- Verify streaming encryption handles large files efficiently
- Monitor memory usage during encryption/decryption operations

## 🎯 Recent Completions (v1.1.0)

- ✅ **Dynamic Extension Discovery**: Scan folders to see actual file types
- ✅ **Per-Folder Settings**: Each folder can have unique encryption config
- ✅ **Checkbox Selection UI**: Easy extension selection with risk indicators
- ✅ **ForceDecrypt Tool**: Emergency recovery for encrypted files
- ✅ **Complete Documentation**: Developer handbook in docs/ folder
- ✅ **Enhanced FolderLocker**: Selective encryption support

## 📦 Previous Release (v1.0.0)

- ✅ **Complete File Encryption**: AES-256 streaming encryption
- ✅ **Enhanced ACL Security**: Multi-layered access control
- ✅ **Professional Installer**: PowerShell-based with UAC handling
- ✅ **Self-Contained Deployment**: Includes .NET runtime
- ✅ **Windows Service**: Automatic schedule enforcement
- ✅ **Desktop Notifications**: Lock/unlock alerts

## 📦 Future Enhancements

### **High Priority**

- [ ] **System Tray Icon**: Quick status and manual override controls
- [ ] **Emergency Unlock PIN**: Time-delayed unlock with configurable PIN
- [ ] **Usage Statistics**: Gaming time tracking and reporting

### **Medium Priority**

- [ ] **Multiple Profiles**: Different schedules for weekdays/weekends
- [ ] **WiX MSI Installer**: Alternative to PowerShell installer
- [ ] **Configuration Backup/Restore**: Export/import settings

### **Low Priority**

- [ ] **Game Launch Detection**: Automatic folder detection when games launch
- [ ] **Web-based Configuration**: Browser-based admin panel
- [ ] **Cloud Sync**: Configuration synchronization across devices

## ⚠️ Important Notes

1. **Administrator Required**: The service needs admin privileges to modify NTFS ACLs
2. **NTFS Only**: Game folders must be on NTFS partitions
3. **Service Must Run**: The enforcement only works when the service is running
4. **Selective Encryption**: Only encrypt safe file types (.sav, .ini, .cfg) - avoid .exe, .dll
5. **Backup Keys**: Never delete `C:\ProgramData\GameLocker\folder_*.dat` encryption keys

## 📚 Documentation

Full developer documentation is available in the `docs/` folder:

- [Architecture Overview](docs/01-architecture.md) - System design and components
- [Getting Started](docs/02-getting-started.md) - Development environment setup
- [API Reference](docs/03-api-reference.md) - Detailed API documentation
- [Contributing](docs/04-contributing.md) - How to contribute
- [Troubleshooting](docs/05-troubleshooting.md) - Common issues and solutions

## 📄 License

This project is created for personal use. Use at your own risk.

## 🤝 Contributing

Feel free to submit issues and enhancement requests!

---

**Made with ❤️ for better productivity**
