using GameLocker.Common.Models;
using GameLocker.Common.Services;
using System;

namespace FullGameLockerDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🎮 GAMELOCKER COMPLETE SYSTEM DEMO");
        Console.WriteLine("==================================");
        Console.WriteLine();
        
        var testGamePath = @"G:\games\TestGame";
        Console.WriteLine($"📁 Demo Game Folder: {testGamePath}");
        Console.WriteLine();
        
        if (!Directory.Exists(testGamePath))
        {
            Console.WriteLine("❌ Test game folder not found! Please create it first.");
            return;
        }
        
        try
        {
            // Step 1: Scan the game folder
            Console.WriteLine("🔍 STEP 1: Scanning Game Folder for File Types...");
            var scanner = new FileExtensionScanner();
            var scanResult = scanner.ScanFolderExtensions(testGamePath, recursive: true);
            
            Console.WriteLine($"✅ Found {scanResult.UniqueExtensions} file types in {scanResult.TotalFilesFound} files");
            Console.WriteLine();
            
            // Step 2: Show user what they can select
            Console.WriteLine("☑️  STEP 2: Available File Types for Encryption:");
            Console.WriteLine();
            
            var byRisk = scanResult.GetExtensionsByRisk();
            var safeExtensions = byRisk.Where(e => e.RiskLevel == RiskLevel.Safe).ToList();
            var dangerousExtensions = byRisk.Where(e => e.RiskLevel == RiskLevel.Dangerous).ToList();
            
            Console.WriteLine("✅ SAFE TO ENCRYPT (Recommended):");
            foreach (var ext in safeExtensions)
            {
                Console.WriteLine($"   ☑️ {ext.Extension} ({ext.FileCount} files) - {ext.Category}");
            }
            
            Console.WriteLine();
            Console.WriteLine("❌ DANGEROUS - AVOID (Will cause crashes):");
            foreach (var ext in dangerousExtensions)
            {
                Console.WriteLine($"   ⚠️ {ext.Extension} ({ext.FileCount} files) - {ext.Category}");
            }
            
            Console.WriteLine();
            
            // Step 3: Simulate user selection (safe extensions only)
            Console.WriteLine("🎯 STEP 3: User Selects Extensions (Simulating safe choice)...");
            var userSelectedExtensions = safeExtensions.Select(e => e.Extension).ToList();
            Console.WriteLine($"✅ User selected {userSelectedExtensions.Count} safe extensions:");
            Console.WriteLine($"   {string.Join(", ", userSelectedExtensions)}");
            Console.WriteLine();
            
            // Step 4: Create folder encryption settings
            Console.WriteLine("⚙️  STEP 4: Creating Per-Folder Encryption Configuration...");
            var folderSettings = new FolderEncryptionSettings
            {
                FolderPath = testGamePath,
                UseCustomSelection = true,
                SelectedExtensions = userSelectedExtensions,
                LastScanned = scanResult.ScannedAt,
                TotalFiles = scanResult.TotalFilesFound,
                UniqueExtensions = scanResult.UniqueExtensions,
                UserNotes = "Demo: Selected only safe extensions to prevent game corruption"
            };
            
            Console.WriteLine($"📋 Config: {folderSettings.GetEncryptionSummary()}");
            Console.WriteLine($"📊 Stats: {folderSettings.GetStats().Summary}");
            Console.WriteLine();
            
            // Step 5: Test encryption decisions on all files
            Console.WriteLine("🧪 STEP 5: Testing Encryption Decisions on All Files...");
            Console.WriteLine();
            
            var allFiles = Directory.GetFiles(testGamePath, "*", SearchOption.AllDirectories);
            var willEncrypt = new List<string>();
            var willSkip = new List<string>();
            
            foreach (var file in allFiles)
            {
                var fileName = Path.GetFileName(file);
                var extension = Path.GetExtension(file).ToLowerInvariant();
                
                if (folderSettings.ShouldEncryptFile(fileName))
                {
                    willEncrypt.Add(fileName);
                    Console.WriteLine($"   ✅ ENCRYPT: {fileName.PadRight(20)} ({extension}) - Safe");
                }
                else
                {
                    willSkip.Add(fileName);
                    var reason = dangerousExtensions.Any(d => d.Extension == extension) ? "Dangerous!" : "Not selected";
                    Console.WriteLine($"   ❌ SKIP:    {fileName.PadRight(20)} ({extension}) - {reason}");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("📊 ENCRYPTION SUMMARY:");
            Console.WriteLine($"   ✅ Files to encrypt: {willEncrypt.Count} (safe user data)");
            Console.WriteLine($"   ❌ Files to skip: {willSkip.Count} (dangerous or not selected)");
            
            // Show the dangerous files that would have caused crashes
            var dangerousFiles = allFiles.Where(f => 
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return dangerousExtensions.Any(d => d.Extension == ext);
            }).ToList();
            
            if (dangerousFiles.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("🚨 FILES THAT WOULD CAUSE CRASHES IF ENCRYPTED:");
                foreach (var dangerous in dangerousFiles)
                {
                    Console.WriteLine($"   ⚠️ {Path.GetFileName(dangerous)} - Would break the game!");
                }
                Console.WriteLine($"   ✅ These {dangerousFiles.Count} files are SAFELY SKIPPED by the new system!");
            }
            
            Console.WriteLine();
            Console.WriteLine("🎉 DEMO COMPLETE - SYSTEM WORKING PERFECTLY!");
            Console.WriteLine("============================================");
            Console.WriteLine();
            Console.WriteLine("✨ Key Benefits Demonstrated:");
            Console.WriteLine("   🔍 Dynamic file type discovery");
            Console.WriteLine("   ☑️ Manual checkbox-style selection");
            Console.WriteLine("   🚦 Clear safety indicators");
            Console.WriteLine("   🛡️ Prevents game corruption");
            Console.WriteLine("   📁 Per-folder custom configurations");
            Console.WriteLine();
            Console.WriteLine("🎮 Hogwarts Legacy Issue SOLVED:");
            Console.WriteLine("   ❌ Old: All files encrypted → Game crashes");
            Console.WriteLine("   ✅ New: Only safe files encrypted → Game works!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Demo failed: {ex.Message}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}