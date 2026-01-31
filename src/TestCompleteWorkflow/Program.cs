using GameLocker.Common.Models;
using GameLocker.Common.Services;
using System;
using System.Linq;

namespace TestCompleteWorkflow;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🎮 GameLocker Complete Dynamic Extension Workflow");
        Console.WriteLine("=================================================");
        Console.WriteLine();
        
        // Simulate adding a game folder to GameLocker
        var gamePath = @"C:\Windows\System32"; // Using System32 as test folder
        Console.WriteLine($"📁 Adding Game Folder: {gamePath}");
        Console.WriteLine();
        
        // Step 1: User adds game folder, system scans for file types
        Console.WriteLine("🔍 Step 1: Scanning folder for file extensions...");
        var scanner = new FileExtensionScanner();
        var scanResult = scanner.ScanFolderExtensions(gamePath, recursive: false);
        
        if (!string.IsNullOrEmpty(scanResult.ErrorMessage))
        {
            Console.WriteLine($"❌ Scan failed: {scanResult.ErrorMessage}");
            return;
        }
        
        Console.WriteLine($"✅ Found {scanResult.UniqueExtensions} different file types in {scanResult.TotalFilesFound:N0} files");
        Console.WriteLine();
        
        // Step 2: Show user the extensions categorized by risk
        Console.WriteLine("🚦 Step 2: Showing file types by safety level...");
        Console.WriteLine();
        
        var byRisk = scanResult.GetExtensionsByRisk();
        var safeExtensions = byRisk.Where(e => e.RiskLevel == RiskLevel.Safe).ToList();
        var dangerousExtensions = byRisk.Where(e => e.RiskLevel == RiskLevel.Dangerous).ToList();
        
        if (safeExtensions.Count > 0)
        {
            Console.WriteLine("✅ SAFE to encrypt (recommended):");
            foreach (var ext in safeExtensions.Take(5))
            {
                Console.WriteLine($"   ☑️ {ext.Extension} ({ext.FileCount} files) - {ext.Category}");
            }
            if (safeExtensions.Count > 5) Console.WriteLine($"   ... and {safeExtensions.Count - 5} more safe options");
        }
        
        Console.WriteLine();
        if (dangerousExtensions.Count > 0)
        {
            Console.WriteLine("❌ DANGEROUS to encrypt (avoid):");
            foreach (var ext in dangerousExtensions.Take(3))
            {
                Console.WriteLine($"   ⚠️ {ext.Extension} ({ext.FileCount} files) - Will cause crashes!");
            }
        }
        
        Console.WriteLine();
        Console.WriteLine("🎯 Step 3: User selects which extensions to encrypt...");
        Console.WriteLine("(In real UI, user would see checkboxes for each extension)");
        
        // Simulate user selecting only safe extensions
        var userSelectedExtensions = safeExtensions.Select(e => e.Extension).ToList();
        Console.WriteLine($"✅ User selected {userSelectedExtensions.Count} safe extensions: {string.Join(", ", userSelectedExtensions.Take(4))}...");
        Console.WriteLine();
        
        // Step 4: Create per-folder encryption settings
        Console.WriteLine("⚙️ Step 4: Creating per-folder encryption configuration...");
        var folderSettings = new FolderEncryptionSettings
        {
            FolderPath = gamePath,
            UseCustomSelection = true,
            SelectedExtensions = userSelectedExtensions,
            LastScanned = scanResult.ScannedAt,
            TotalFiles = scanResult.TotalFilesFound,
            UniqueExtensions = scanResult.UniqueExtensions,
            UserNotes = "Selected only safe file types to prevent system crashes"
        };
        
        Console.WriteLine($"📋 Configuration Summary: {folderSettings.GetEncryptionSummary()}");
        Console.WriteLine($"📊 Selection Stats: {folderSettings.GetStats().Summary}");
        Console.WriteLine();
        
        // Step 5: Save to main config
        Console.WriteLine("💾 Step 5: Saving configuration...");
        var mainConfig = new GameLockerConfig();
        mainConfig.GameFolderPaths.Add(gamePath);
        mainConfig.SetFolderEncryptionSettings(folderSettings);
        
        Console.WriteLine($"✅ Saved encryption settings for folder: {gamePath}");
        Console.WriteLine();
        
        // Step 6: Test encryption decisions
        Console.WriteLine("🧪 Step 6: Testing encryption decisions for sample files...");
        var testFiles = new[]
        {
            "savegame.dat",    // Safe extension - should encrypt
            "config.ini",      // Safe extension - should encrypt  
            "game.exe",        // Dangerous extension - should skip
            "graphics.dll",    // Dangerous extension - should skip
            "readme.txt",      // Safe extension - should encrypt
            "shader.bin",      // Dangerous extension - should skip
            "settings.json",   // Safe extension - should encrypt
            "music.mp3"        // Not selected - should skip
        };
        
        foreach (var testFile in testFiles)
        {
            var shouldEncrypt = folderSettings.ShouldEncryptFile(testFile);
            var extension = Path.GetExtension(testFile);
            var action = shouldEncrypt ? "ENCRYPT ✅" : "SKIP ❌";
            var reason = shouldEncrypt ? "User selected this extension" : "Not in user's selection";
            
            Console.WriteLine($"   📄 {testFile.PadRight(15)} ({extension}) → {action} - {reason}");
        }
        
        Console.WriteLine();
        Console.WriteLine("🎉 Workflow Complete!");
        Console.WriteLine("======================================");
        Console.WriteLine();
        Console.WriteLine("✨ What we achieved:");
        Console.WriteLine("   🔍 Dynamic scanning found actual file types in the game folder");
        Console.WriteLine("   🚦 Clear safety indicators prevent dangerous selections");
        Console.WriteLine("   ☑️ User has complete control with checkbox-style selection");
        Console.WriteLine("   📁 Per-folder settings allow different configs for each game");
        Console.WriteLine("   🛡️ Prevents corruption by skipping system-critical files");
        Console.WriteLine("   ⚡ No more encrypted .exe/.dll files causing crashes!");
        Console.WriteLine();
        Console.WriteLine("🎮 This solves the Hogwarts Legacy issue:");
        Console.WriteLine("   The 18 .enc files that caused crashes would never have been");
        Console.WriteLine("   encrypted because .exe/.dll are marked DANGEROUS and users");
        Console.WriteLine("   can see exactly what they're selecting!");
        Console.WriteLine();
        
        // Show comparison
        Console.WriteLine("📊 Old vs New Approach:");
        Console.WriteLine("   ❌ Old: Encrypt ALL files → 4,751 files encrypted → Game crashes");
        Console.WriteLine($"   ✅ New: User selected {userSelectedExtensions.Count} safe extensions → {GetFileCountForExtensions(scanResult, userSelectedExtensions)} files encrypted → Game works!");
    }
    
    static int GetFileCountForExtensions(ExtensionScanResult scanResult, List<string> extensions)
    {
        return scanResult.Extensions
            .Where(kvp => extensions.Contains(kvp.Key))
            .Sum(kvp => kvp.Value.FileCount);
    }
}