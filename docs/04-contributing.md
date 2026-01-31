# Contributing to GameLocker

Thank you for your interest in contributing to GameLocker! This guide will help you get started.

## Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [Development Workflow](#development-workflow)
4. [Code Standards](#code-standards)
5. [Testing Guidelines](#testing-guidelines)
6. [Pull Request Process](#pull-request-process)
7. [Issue Guidelines](#issue-guidelines)

---

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow
- Keep discussions on-topic

---

## Getting Started

### Prerequisites

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```powershell
   git clone https://github.com/YOUR_USERNAME/Game-Locker.git
   cd Game-Locker/code
   ```
3. Add upstream remote:
   ```powershell
   git remote add upstream https://github.com/GauravKumawat2002/Game-Locker.git
   ```
4. Install dependencies:
   ```powershell
   dotnet restore
   dotnet build
   ```

### Development Environment

See [02-getting-started.md](02-getting-started.md) for detailed setup instructions.

---

## Development Workflow

### 1. Create a Feature Branch

```powershell
# Sync with upstream
git fetch upstream
git checkout master
git merge upstream/master

# Create feature branch
git checkout -b feature/your-feature-name
```

### 2. Make Your Changes

- Write clean, readable code
- Follow existing code patterns
- Add XML documentation for public APIs
- Test your changes thoroughly

### 3. Commit Your Changes

Use conventional commit messages:

```powershell
# Feature
git commit -m "feat: add extension scanning to ConfigUI"

# Bug fix
git commit -m "fix: resolve ACL permissions on Windows 11"

# Documentation
git commit -m "docs: update API reference for FolderLocker"

# Refactoring
git commit -m "refactor: simplify encryption helper methods"
```

**Commit Message Format:**
```
<type>: <short description>

[optional body with more details]
```

**Types:**
- `feat` - New feature
- `fix` - Bug fix
- `docs` - Documentation only
- `style` - Formatting, no code change
- `refactor` - Code restructuring
- `test` - Adding tests
- `chore` - Maintenance tasks

### 4. Push and Create PR

```powershell
git push origin feature/your-feature-name
```

Then create a Pull Request on GitHub.

---

## Code Standards

### C# Coding Conventions

Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).

**Key Points:**

```csharp
// Use PascalCase for public members
public class FolderLocker
{
    public string FolderPath { get; set; }
    public async Task LockFolderAsync(string path) { }
}

// Use camelCase for private fields with underscore prefix
private readonly string _configPath;
private byte[]? _masterKey;

// Use meaningful names
public bool IsWithinAllowedTime(DateTime currentTime) // Good
public bool Check(DateTime t) // Bad

// XML documentation for public APIs
/// <summary>
/// Locks a folder by encrypting files and applying ACL restrictions.
/// </summary>
/// <param name="folderPath">Full path to the folder to lock.</param>
/// <returns>Information about the locked folder.</returns>
public async Task<GameFolderInfo> LockFolderAsync(string folderPath)
```

### File Organization

```csharp
// File: FolderLocker.cs

using System;                           // System namespaces first
using System.IO;
using GameLocker.Common.Encryption;     // Project namespaces after

namespace GameLocker.Common.Services;   // File-scoped namespace

/// <summary>
/// Class documentation here.
/// </summary>
public class FolderLocker
{
    // Fields
    private readonly string _keyStorePath;
    
    // Constructor
    public FolderLocker(string keyStorePath)
    {
        _keyStorePath = keyStorePath;
    }
    
    // Public methods
    public async Task InitializeAsync() { }
    
    // Private methods
    private void EncryptFile(string path) { }
}
```

### Error Handling

```csharp
// Catch specific exceptions
try
{
    await EncryptFileAsync(filePath);
}
catch (IOException ex)
{
    // Log and handle gracefully
    Console.WriteLine($"Failed to encrypt {filePath}: {ex.Message}");
}

// Use meaningful error messages
if (!Directory.Exists(folderPath))
{
    info.LastError = $"Folder does not exist: {folderPath}";
    return info;
}
```

### Async/Await

```csharp
// Always use async suffix for async methods
public async Task<GameFolderInfo> LockFolderAsync(string path)

// Prefer Task over void for async methods
public async Task SaveConfigAsync()  // Good
public async void SaveConfig()       // Bad (only for event handlers)

// Use ConfigureAwait(false) in library code
await File.WriteAllBytesAsync(path, data).ConfigureAwait(false);
```

---

## Testing Guidelines

### Manual Testing Checklist

Before submitting a PR, test these scenarios:

- [ ] Build succeeds with no warnings
- [ ] ConfigUI launches and displays correctly
- [ ] Can add/remove game folders
- [ ] Extension scanning works correctly
- [ ] Lock/unlock cycle completes successfully
- [ ] Service starts and runs without errors
- [ ] Notifications appear correctly
- [ ] Files encrypt/decrypt correctly
- [ ] Large files (>1GB) work properly

### Creating Test Folders

```powershell
# Create test folder structure
$testPath = "G:\games\TestGame"
New-Item -ItemType Directory -Path $testPath -Force

# Create test files
"Test save data" | Out-File "$testPath\save.sav"
"[Settings]`nvolume=100" | Out-File "$testPath\config.ini"
"Test file" | Out-File "$testPath\readme.txt"

# Create a large test file (100MB)
$bytes = New-Object byte[] (100MB)
[System.IO.File]::WriteAllBytes("$testPath\largefile.dat", $bytes)
```

### Testing Encryption

```powershell
# Verify encryption
Get-ChildItem "G:\games\TestGame" -Recurse | ForEach-Object {
    if ($_.Extension -eq ".enc") {
        Write-Host "Encrypted: $($_.FullName)"
    }
}

# Verify decryption
Get-ChildItem "G:\games\TestGame" -Recurse | ForEach-Object {
    if ($_.Extension -ne ".enc" -and $_.Extension -ne ".gamelocker") {
        Write-Host "Decrypted: $($_.FullName)"
    }
}
```

### Using Test Projects

```powershell
# Run extension scanning test
dotnet run --project src/TestExtensionScanning

# Run selective encryption test
dotnet run --project src/TestSelectiveEncryption

# Run complete workflow test
dotnet run --project src/TestCompleteWorkflow
```

---

## Pull Request Process

### Before Submitting

1. **Sync with upstream**
   ```powershell
   git fetch upstream
   git rebase upstream/master
   ```

2. **Build and test**
   ```powershell
   dotnet build -c Release
   # Test manually with ConfigUI
   ```

3. **Check for warnings**
   ```powershell
   dotnet build -c Release 2>&1 | Select-String -Pattern "warning"
   ```

### PR Description Template

```markdown
## Description
Brief description of changes.

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing Done
- Tested lock/unlock cycle
- Tested with large files
- Tested extension scanning

## Screenshots (if applicable)
[Add screenshots for UI changes]

## Checklist
- [ ] Code follows project style guidelines
- [ ] Documentation updated
- [ ] All tests pass
- [ ] No new warnings
```

### Review Process

1. Maintainer reviews code
2. CI checks pass (if configured)
3. Address any requested changes
4. PR is merged

---

## Issue Guidelines

### Bug Reports

Use this template:

```markdown
## Bug Description
Clear description of the bug.

## Steps to Reproduce
1. Step one
2. Step two
3. ...

## Expected Behavior
What should happen.

## Actual Behavior
What actually happens.

## Environment
- Windows Version: [e.g., Windows 11 23H2]
- .NET Version: [e.g., 8.0.1]
- GameLocker Version: [e.g., 1.1.0]

## Logs/Screenshots
[Add any relevant information]
```

### Feature Requests

```markdown
## Feature Description
What feature would you like?

## Use Case
Why is this feature needed?

## Proposed Solution
How should it work?

## Alternatives Considered
Other solutions you've thought about.
```

---

## Project Structure for Contributors

When adding new features, follow this structure:

```
src/
├── GameLocker.Common/          # Add shared logic here
│   ├── Models/                 # Data models
│   ├── Services/               # Business logic services
│   └── Helpers/                # Utility classes
│
├── GameLocker.ConfigUI/        # Add UI changes here
│   ├── Form1.cs                # Main form logic
│   └── Form1.Designer.cs       # UI layout (use VS designer)
│
└── GameLocker.Service/         # Add service logic here
    └── GameLockerService.cs    # Background service
```

### Adding a New Feature

1. **Create model classes** in `GameLocker.Common/Models/`
2. **Create service classes** in `GameLocker.Common/Services/`
3. **Update UI** in `GameLocker.ConfigUI/Form1.cs`
4. **Update service** in `GameLocker.Service/GameLockerService.cs` (if needed)
5. **Update documentation** in `docs/`
6. **Update README.md** if major feature

---

## Questions?

- Open an issue for questions
- Check existing issues first
- Be patient with responses

Thank you for contributing! 🎮
