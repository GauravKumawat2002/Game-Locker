# GameLocker

A Windows system service + configuration UI application that enforces a **game access schedule** by locking game folders outside allowed times. Perfect for maintaining discipline and controlling gaming habits.

## 🎮 Features

- **Scheduled Access Control**: Define specific days and time windows for gaming
- **NTFS ACL Enforcement**: Blocks access to game folders at the OS level
- **AES-256 Encryption**: Lock markers are encrypted for added security
- **DPAPI Key Protection**: Encryption keys are protected using Windows DPAPI (machine scope)
- **Windows Service**: Runs automatically at startup, enforces schedule continuously
- **Desktop Notifications**: Visual notifications for lock/unlock events
- **Warning System**: Warnings at 15 and 5 minutes before lock time
- **Easy Configuration**: User-friendly WinForms configuration interface

## 📋 Requirements

- Windows 10/11 (64-bit)
- .NET 8.0 SDK or Runtime
- Administrator privileges (for ACL modifications)
- NTFS filesystem

## 🏗️ Project Structure

```
GameLocker/
├── src/
│   ├── GameLocker.Common/          # Shared libraries
│   │   ├── Configuration/          # Config management
│   │   ├── Encryption/             # AES & DPAPI helpers
│   │   ├── Models/                 # Data models
│   │   ├── Notifications/          # Toast notifications
│   │   ├── Security/               # NTFS ACL helpers
│   │   └── Services/               # Folder locking service
│   ├── GameLocker.ConfigUI/        # WinForms configuration app
│   └── GameLocker.Service/         # Windows Service
├── installer/                      # Installer project (future)
├── docs/                           # Documentation
├── GameLocker.sln                  # Solution file
└── README.md
```

## 🚀 Getting Started

### Building from Source

1. **Clone or download** the repository
2. **Open terminal** in the GameLocker directory
3. **Build the solution**:
    ```powershell
    dotnet build
    ```

### Running the Configuration UI

```powershell
# Run as Administrator for full functionality
dotnet run --project src/GameLocker.ConfigUI
```

### Installing the Windows Service

1. **Publish the service**:

    ```powershell
    dotnet publish src/GameLocker.Service -c Release -o publish
    ```

2. **Install the service** (Run as Administrator):

    ```powershell
    sc.exe create "GameLocker Service" binpath= "C:\path\to\publish\GameLocker.Service.exe"
    ```

3. **Start the service**:
    ```powershell
    sc.exe start "GameLocker Service"
    ```

### Uninstalling the Service

```powershell
# Stop the service
sc.exe stop "GameLocker Service"

# Delete the service
sc.exe delete "GameLocker Service"
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

## 🔐 Security

### How It Works

1. **ACL Enforcement**: Game folders have their NTFS permissions modified to deny access to regular users
2. **Lock Markers**: Hidden `.gamelocker` files are created with encrypted timestamps
3. **DPAPI Protection**: All encryption keys are protected using Windows DPAPI with LocalMachine scope
4. **Service Privileges**: The Windows Service runs with system privileges for reliable enforcement

### Bypass Difficulty

- **Normal Users**: Cannot access locked folders or modify ACLs
- **Administrators**: Can technically bypass by modifying ACLs manually, but this requires effort
- **Physical Access**: A determined admin with physical access can extract keys (out of scope)

## 📱 Notifications

The service sends Windows toast notifications for:

- 🔒 **Folder Locked**: When gaming time ends
- 🎮 **Folder Unlocked**: When gaming time starts
- ⚠️ **Warning**: 15 minutes and 5 minutes before lock time
- ✅ **Service Started**: When the service begins monitoring
- ⏹️ **Service Stopped**: When the service stops

## 🛠️ Development

### Prerequisites

- .NET 8.0 SDK
- Visual Studio Code or Visual Studio 2022+
- Windows 10/11

### Recommended VS Code Extensions

- C# Dev Kit
- C#
- .NET Install Tool
- PowerShell

### Testing Lock Functionality

The ConfigUI includes a "Test Lock" button that:

1. Temporarily locks the selected folder
2. Allows you to verify the lock is working
3. Immediately unlocks the folder after testing

### Running in Debug Mode

```powershell
# Run the service directly (not as Windows Service)
dotnet run --project src/GameLocker.Service
```

## 📦 Future Enhancements

- [ ] WiX Installer for easy deployment
- [ ] System tray icon for quick status
- [ ] Emergency unlock with time-delayed PIN
- [ ] Statistics and usage logging
- [ ] Multiple schedule profiles

## ⚠️ Important Notes

1. **Administrator Required**: The service needs admin privileges to modify NTFS ACLs
2. **NTFS Only**: Game folders must be on NTFS partitions
3. **Service Must Run**: The enforcement only works when the service is running
4. **Backup Your Games**: While the system doesn't modify game files, always maintain backups

## 📄 License

This project is created for personal use. Use at your own risk.

## 🤝 Contributing

Feel free to submit issues and enhancement requests!

---

**Made with ❤️ for better productivity**
