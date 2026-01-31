using GameLocker.Common.Models;
using GameLocker.Common.Services;
using System;

namespace TestExtensionScanning;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("🎮 GameLocker Dynamic Extension Scanner Demo");
        Console.WriteLine("=============================================");
        Console.WriteLine();
        
        // Test with a Windows folder that should have diverse file types
        var testPath = @"C:\Windows\System32";
        
        Console.WriteLine($"🔍 Scanning Test Folder: {testPath}");
        Console.WriteLine("(Using Windows System32 as example - has diverse file types)");
        Console.WriteLine();
        
        var scanner = new FileExtensionScanner();
        
        // First, get a quick preview
        Console.WriteLine("⚡ Quick Preview (top-level files):");
        var preview = scanner.GetExtensionPreview(testPath, 50);
        if (preview.Count > 0)
        {
            foreach (var ext in preview.Take(10))
            {
                Console.WriteLine($"   {ext}");
            }
            if (preview.Count > 10)
            {
                Console.WriteLine($"   ... and {preview.Count - 10} more extensions");
            }
        }
        else
        {
            Console.WriteLine("   No files found or folder doesn't exist");
        }
        Console.WriteLine();
        
        // Full scan if folder exists
        if (Directory.Exists(testPath))
        {
            Console.WriteLine("🔬 Scanning top-level only (System32 has many subfolders)...");
            var result = scanner.ScanFolderExtensions(testPath, recursive: false); // Don't recurse System32!
            
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                Console.WriteLine($"❌ Error: {result.ErrorMessage}");
                return;
            }
            
            Console.WriteLine($"✅ Scan Complete!");
            Console.WriteLine($"   📁 Total Files: {result.TotalFilesFound:N0}");
            Console.WriteLine($"   📝 Unique Extensions: {result.UniqueExtensions}");
            Console.WriteLine($"   ⏱️ Scanned at: {result.ScannedAt:HH:mm:ss}");
            Console.WriteLine();
            
            // Show extensions by risk level
            Console.WriteLine("🚦 Extensions by Risk Level:");
            Console.WriteLine();
            
            var byRisk = result.GetExtensionsByRisk();
            var currentRisk = RiskLevel.Safe;
            
            foreach (var ext in byRisk)
            {
                if (ext.RiskLevel != currentRisk)
                {
                    currentRisk = ext.RiskLevel;
                    var riskColor = currentRisk switch
                    {
                        RiskLevel.Safe => "✅ SAFE TO ENCRYPT",
                        RiskLevel.Moderate => "⚡ MODERATE RISK",
                        RiskLevel.High => "⚠️ HIGH RISK", 
                        RiskLevel.Dangerous => "❌ DANGEROUS - AVOID",
                        _ => "❓ UNKNOWN RISK"
                    };
                    Console.WriteLine($"{riskColor}:");
                }
                
                var riskSymbol = ext.RiskLevel switch
                {
                    RiskLevel.Safe => "✅",
                    RiskLevel.Moderate => "⚡",
                    RiskLevel.High => "⚠️",
                    RiskLevel.Dangerous => "❌",
                    _ => "❓"
                };
                
                Console.WriteLine($"   {riskSymbol} {ext.Extension.PadRight(12)} - {ext.Description.PadRight(20)} ({ext.Category})");
                if (ext.ExampleFiles.Count > 0)
                {
                    Console.WriteLine($"      Examples: {ext.ExampleFilesList}");
                }
            }
            Console.WriteLine();
            
            // Show what would be selected with different approaches
            Console.WriteLine("💡 Encryption Selection Examples:");
            Console.WriteLine();
            
            // Safe approach
            var safeExtensions = byRisk.Where(e => e.RiskLevel == RiskLevel.Safe).Select(e => e.Extension).ToList();
            Console.WriteLine($"🛡️ Safe Approach ({safeExtensions.Count} extensions):");
            Console.WriteLine($"   {string.Join(", ", safeExtensions.Take(8))}");
            if (safeExtensions.Count > 8) Console.WriteLine($"   ... and {safeExtensions.Count - 8} more");
            Console.WriteLine();
            
            // Aggressive approach  
            var aggressiveExtensions = byRisk.Where(e => e.RiskLevel <= RiskLevel.High).Select(e => e.Extension).ToList();
            Console.WriteLine($"⚡ Aggressive Approach ({aggressiveExtensions.Count} extensions):");
            Console.WriteLine($"   Includes all safe + moderate + high risk extensions");
            Console.WriteLine();
            
            // Show dangerous extensions to avoid
            var dangerousExtensions = byRisk.Where(e => e.RiskLevel == RiskLevel.Dangerous).ToList();
            if (dangerousExtensions.Count > 0)
            {
                Console.WriteLine($"🚨 AVOID These Extensions (will cause crashes):");
                foreach (var dangerous in dangerousExtensions)
                {
                    Console.WriteLine($"   ❌ {dangerous.Extension} - {dangerous.FileCount} files ({dangerous.Category})");
                }
                Console.WriteLine();
            }
            
            // Create sample folder encryption settings
            var folderSettings = new FolderEncryptionSettings
            {
                FolderPath = testPath,
                UseCustomSelection = true,
                SelectedExtensions = safeExtensions,
                LastScanned = result.ScannedAt,
                TotalFiles = result.TotalFilesFound,
                UniqueExtensions = result.UniqueExtensions,
                UserNotes = "Selected only safe extensions to prevent system issues"
            };
            
            Console.WriteLine("📋 Sample Folder Configuration:");
            Console.WriteLine($"   Path: {folderSettings.FolderPath}");
            Console.WriteLine($"   Selection: {folderSettings.GetEncryptionSummary()}");
            Console.WriteLine($"   Stats: {folderSettings.GetStats().Summary}");
            Console.WriteLine($"   Notes: {folderSettings.UserNotes}");
            Console.WriteLine();
            
            // Test file encryption decisions
            Console.WriteLine("🔍 Test File Encryption Decisions:");
            var testFiles = new[] { "save.dat", "config.ini", "player.profile", "game.exe", "texture.dll", "cache.tmp" };
            foreach (var testFile in testFiles)
            {
                var shouldEncrypt = folderSettings.ShouldEncryptFile(testFile);
                var symbol = shouldEncrypt ? "✅" : "❌";
                Console.WriteLine($"   {symbol} {testFile.PadRight(15)} - {(shouldEncrypt ? "ENCRYPT" : "SKIP")}");
            }
        }
        else
        {
            Console.WriteLine("ℹ️ Test folder not accessible");
            Console.WriteLine("   Try running this with a game folder to see real results!");
        }
        
        Console.WriteLine();
        Console.WriteLine("🎯 This solves the original problem:");
        Console.WriteLine("   ✅ Users can see EXACTLY what file types exist in their game");
        Console.WriteLine("   ✅ Manual checkbox selection for complete control");
        Console.WriteLine("   ✅ Clear risk indicators prevent dangerous selections");
        Console.WriteLine("   ✅ No more encrypted .exe/.dll files causing crashes!");
    }
}