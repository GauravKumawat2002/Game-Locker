using GameLocker.Common.Models;
using GameLocker.Common.Services;
using System;

namespace TestGameFolderDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🎮 GameLocker Live Demo - Test Game Folder Analysis");
        Console.WriteLine("==================================================");
        Console.WriteLine();
        
        var testGamePath = @"G:\games\TestGame";
        Console.WriteLine($"🔍 Analyzing Test Game Folder: {testGamePath}");
        Console.WriteLine();
        
        if (!Directory.Exists(testGamePath))
        {
            Console.WriteLine("❌ Test game folder not found!");
            Console.WriteLine("Please run the setup script first to create the test files.");
            return;
        }
        
        // Scan the test game folder
        var scanner = new FileExtensionScanner();
        var result = scanner.ScanFolderExtensions(testGamePath, recursive: true);
        
        Console.WriteLine($"📊 Scan Results:");
        Console.WriteLine($"   Total Files: {result.TotalFilesFound}");
        Console.WriteLine($"   Unique Extensions: {result.UniqueExtensions}");
        Console.WriteLine();
        
        // Show all extensions by risk level
        Console.WriteLine("🚦 File Extensions by Safety Level:");
        Console.WriteLine();
        
        var byRisk = result.GetExtensionsByRisk();
        var currentRisk = RiskLevel.Safe;
        
        foreach (var ext in byRisk)
        {
            if (ext.RiskLevel != currentRisk)
            {
                currentRisk = ext.RiskLevel;
                var header = currentRisk switch
                {
                    RiskLevel.Safe => "✅ SAFE TO ENCRYPT (Recommended)",
                    RiskLevel.Moderate => "⚡ MODERATE RISK (Usually OK)",
                    RiskLevel.High => "⚠️ HIGH RISK (Be Careful)",
                    RiskLevel.Dangerous => "❌ DANGEROUS (Will Cause Crashes!)",
                    _ => "❓ UNKNOWN RISK"
                };
                Console.WriteLine($"{header}:");
            }
            
            var icon = ext.RiskLevel switch
            {
                RiskLevel.Safe => "✅",
                RiskLevel.Moderate => "⚡",
                RiskLevel.High => "⚠️",
                RiskLevel.Dangerous => "❌",
                _ => "❓"
            };
            
            Console.WriteLine($"  {icon} {ext.Extension.PadRight(8)} ({ext.FileCount} files) - {ext.Category}");
            Console.WriteLine($"     Examples: {ext.ExampleFilesList}");
        }
        
        Console.WriteLine();
        Console.WriteLine("💡 Recommended Selection for Safe Encryption:");
        
        var safeExtensions = byRisk.Where(e => e.RiskLevel == RiskLevel.Safe).ToList();
        if (safeExtensions.Count > 0)
        {
            Console.WriteLine("   Select these extensions for encryption:");
            foreach (var ext in safeExtensions)
            {
                Console.WriteLine($"   ☑️ {ext.Extension} - {ext.Category} ({ext.FileCount} files)");
            }
        }
        
        var dangerousExtensions = byRisk.Where(e => e.RiskLevel == RiskLevel.Dangerous).ToList();
        if (dangerousExtensions.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("⚠️ AVOID These Extensions (Will Break Your Game):");
            foreach (var ext in dangerousExtensions)
            {
                Console.WriteLine($"   ❌ {ext.Extension} - {ext.FileCount} files (Executable files)");
            }
        }
        
        Console.WriteLine();
        Console.WriteLine("🎯 Demo Summary:");
        Console.WriteLine("This shows exactly how the new dynamic extension system works:");
        Console.WriteLine("1. 🔍 Scans your actual game folder");
        Console.WriteLine("2. 📋 Lists all file types found with examples");
        Console.WriteLine("3. 🚦 Shows safety level for each extension");
        Console.WriteLine("4. ☑️ You manually select which types to encrypt");
        Console.WriteLine("5. 🛡️ Dangerous files (.exe, .dll) are clearly marked to avoid");
        Console.WriteLine();
        Console.WriteLine("✨ This prevents the Hogwarts Legacy corruption issue by giving");
        Console.WriteLine("   you complete control over which file types get encrypted!");
        
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}