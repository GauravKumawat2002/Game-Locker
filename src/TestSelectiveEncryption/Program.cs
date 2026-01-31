using GameLocker.Common.Models;
using GameLocker.Common.Services;
using System;

namespace TestSelectiveEncryption;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🎮 GameLocker Selective Encryption Test");
        Console.WriteLine("=====================================");
        
        // Create different selective encryption configurations
        var safeConfig = new SelectiveEncryptionSettings
        {
            UseSelectiveEncryption = true,
            EncryptSaveFiles = true,
            EncryptConfigFiles = true,
            EncryptUserFiles = true,
            EncryptCacheFiles = false,
            EncryptModFiles = false,
            EncryptExecutables = false,
            EncryptAssets = false,
            CustomExtensions = ""
        };
        
        var aggressiveConfig = new SelectiveEncryptionSettings
        {
            UseSelectiveEncryption = true,
            EncryptSaveFiles = true,
            EncryptConfigFiles = true,
            EncryptUserFiles = true,
            EncryptCacheFiles = true,
            EncryptModFiles = true,
            EncryptExecutables = false,  // Still dangerous
            EncryptAssets = false,       // Still dangerous
            CustomExtensions = ".custom,.special"
        };
        
        var dangerousConfig = new SelectiveEncryptionSettings
        {
            UseSelectiveEncryption = false  // Encrypt everything - NOT RECOMMENDED
        };
        
        Console.WriteLine("📋 Configuration Profiles:");
        Console.WriteLine($"1. Safe Mode: {safeConfig.GetEncryptionSummary()}");
        Console.WriteLine($"2. Aggressive Mode: {aggressiveConfig.GetEncryptionSummary()}");
        Console.WriteLine($"3. Dangerous Mode: {dangerousConfig.GetEncryptionSummary()}");
        Console.WriteLine();
        
        // Test file type checking
        var testFiles = new[]
        {
            "savegame.sav",
            "config.ini", 
            "player.profile",
            "cache.tmp",
            "mod.pak",
            "game.exe",
            "texture.pak",
            "data.dat",
            "settings.json",
            "achievements.dat",
            "graphics.dll",
            "music.ogg",
            "readme.txt",
            "special.custom"
        };
        
        Console.WriteLine("🔍 File Type Analysis (Safe Mode):");
        Console.WriteLine("File Name".PadRight(20) + "Extension".PadRight(12) + "Encrypt?");
        Console.WriteLine(new string('-', 40));
        
        foreach (var file in testFiles)
        {
            var extension = Path.GetExtension(file);
            var willEncrypt = safeConfig.ShouldEncryptFile(file);
            var status = willEncrypt ? "✅ YES" : "❌ NO (safer)";
            
            Console.WriteLine($"{file.PadRight(20)}{extension.PadRight(12)}{status}");
        }
        
        Console.WriteLine();
        Console.WriteLine("💡 Recommendation: Use Safe Mode to prevent game corruption!");
        Console.WriteLine("   Only encrypt save files, configs, and user data.");
        Console.WriteLine("   Avoid encrypting executables, DLLs, and large assets.");
        
        // Show extensions that would be encrypted
        Console.WriteLine("\n📝 Extensions that will be encrypted in Safe Mode:");
        var extensions = safeConfig.GetExtensionsToEncrypt();
        foreach (var ext in extensions.Take(15))
        {
            Console.Write($"{ext}, ");
        }
        if (extensions.Length > 15)
        {
            Console.Write($"... and {extensions.Length - 15} more");
        }
        Console.WriteLine();
        
        Console.WriteLine("\n✨ Selective encryption prevents the corruption issues we saw with Hogwarts Legacy!");
        Console.WriteLine("   Those 18 .enc files that caused crashes would have been avoided with Safe Mode.");
    }
}