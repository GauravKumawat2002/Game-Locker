using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GameLocker.Common.Models;

/// <summary>
/// Configuration for selective file type encryption.
/// This is much safer than encrypting all files and prevents game corruption.
/// </summary>
public class SelectiveEncryptionSettings
{
    /// <summary>
    /// Enable selective encryption (safer than encrypting everything)
    /// </summary>
    [JsonPropertyName("useSelectiveEncryption")]
    public bool UseSelectiveEncryption { get; set; } = true;
    
    /// <summary>
    /// Encrypt save game files - SAFE: These are player data files
    /// </summary>
    [JsonPropertyName("encryptSaveFiles")]
    public bool EncryptSaveFiles { get; set; } = true;
    
    /// <summary>
    /// Encrypt configuration files - SAFE: Settings and preferences
    /// </summary>
    [JsonPropertyName("encryptConfigFiles")]
    public bool EncryptConfigFiles { get; set; } = true;
    
    /// <summary>
    /// Encrypt user profile and settings - SAFE: Player progress files
    /// </summary>
    [JsonPropertyName("encryptUserFiles")]
    public bool EncryptUserFiles { get; set; } = true;
    
    /// <summary>
    /// Encrypt cache and temporary files - MOSTLY SAFE: Can be regenerated
    /// </summary>
    [JsonPropertyName("encryptCacheFiles")]
    public bool EncryptCacheFiles { get; set; } = false;
    
    /// <summary>
    /// Encrypt mod files and user content - RISKY: May affect mod functionality
    /// </summary>
    [JsonPropertyName("encryptModFiles")]
    public bool EncryptModFiles { get; set; } = false;
    
    /// <summary>
    /// Encrypt executable files - DANGEROUS: Can cause game corruption and crashes
    /// </summary>
    [JsonPropertyName("encryptExecutables")]
    public bool EncryptExecutables { get; set; } = false;
    
    /// <summary>
    /// Encrypt large asset files - DANGEROUS: Very slow and can cause corruption
    /// </summary>
    [JsonPropertyName("encryptAssets")]
    public bool EncryptAssets { get; set; } = false;
    
    /// <summary>
    /// Custom file extensions to encrypt (comma-separated)
    /// </summary>
    [JsonPropertyName("customExtensions")]
    public string CustomExtensions { get; set; } = string.Empty;
    
    /// <summary>
    /// Get all file extensions that should be encrypted based on current settings
    /// </summary>
    /// <returns>Array of file extensions to encrypt (lowercase)</returns>
    public string[] GetExtensionsToEncrypt()
    {
        var extensions = new List<string>();
        
        if (EncryptSaveFiles)
        {
            // Common save file extensions across different games
            extensions.AddRange(new[] { 
                ".save", ".sav", ".dat", ".progress", ".profile", ".slot", 
                ".savegame", ".gamesave", ".checkpoint", ".autosave" 
            });
        }
        
        if (EncryptConfigFiles)
        {
            // Configuration and settings files
            extensions.AddRange(new[] { 
                ".cfg", ".ini", ".conf", ".config", ".settings", ".pref", ".prefs",
                ".options", ".setup", ".properties", ".xml", ".json" 
            });
        }
        
        if (EncryptUserFiles)
        {
            // User-specific files
            extensions.AddRange(new[] { 
                ".user", ".usr", ".player", ".account", ".character", ".stats",
                ".achievements", ".scores", ".leaderboard", ".playerdata" 
            });
        }
        
        if (EncryptCacheFiles)
        {
            // Cache and temporary files (usually safe to encrypt)
            extensions.AddRange(new[] { 
                ".cache", ".tmp", ".temp", ".log", ".bak", ".backup",
                ".old", ".orig", ".~", ".swp", ".lock" 
            });
        }
        
        if (EncryptModFiles)
        {
            // Mod and user-generated content (be careful with these)
            extensions.AddRange(new[] { 
                ".mod", ".plugin", ".addon", ".pak", ".zip", ".rar", ".7z",
                ".workshop", ".steam", ".nexus", ".custom" 
            });
        }
        
        if (EncryptExecutables)
        {
            // DANGEROUS: Executables and libraries (NOT RECOMMENDED)
            extensions.AddRange(new[] { 
                ".exe", ".dll", ".so", ".dylib", ".bin", ".com", ".bat", ".cmd",
                ".msi", ".app", ".deb", ".rpm" 
            });
        }
        
        if (EncryptAssets)
        {
            // DANGEROUS: Large game assets (NOT RECOMMENDED - causes performance issues)
            extensions.AddRange(new[] { 
                ".pak", ".wad", ".vpk", ".bsa", ".ba2", ".big", ".data", ".assets",
                ".bundle", ".resource", ".res", ".arc", ".img", ".iso" 
            });
        }
        
        // Add custom extensions
        if (!string.IsNullOrWhiteSpace(CustomExtensions))
        {
            var customExts = CustomExtensions.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var ext in customExts)
            {
                var cleanExt = ext.Trim().ToLowerInvariant();
                if (!cleanExt.StartsWith("."))
                    cleanExt = "." + cleanExt;
                
                if (!string.IsNullOrWhiteSpace(cleanExt) && cleanExt.Length > 1)
                    extensions.Add(cleanExt);
            }
        }
        
        // Return distinct, lowercase extensions
        return extensions.Select(ext => ext.ToLowerInvariant()).Distinct().ToArray();
    }
    
    /// <summary>
    /// Check if a specific file should be encrypted based on its extension
    /// </summary>
    /// <param name="fileName">Name of the file to check</param>
    /// <returns>True if the file should be encrypted</returns>
    public bool ShouldEncryptFile(string fileName)
    {
        if (!UseSelectiveEncryption)
            return true; // Encrypt everything if selective mode is disabled
            
        if (string.IsNullOrWhiteSpace(fileName))
            return false;
        
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return GetExtensionsToEncrypt().Contains(extension);
    }
    
    /// <summary>
    /// Get a summary of what will be encrypted with current settings
    /// </summary>
    /// <returns>Human-readable description of encryption scope</returns>
    public string GetEncryptionSummary()
    {
        if (!UseSelectiveEncryption)
            return "All files will be encrypted (NOT RECOMMENDED)";
        
        var categories = new List<string>();
        
        if (EncryptSaveFiles) categories.Add("Save files");
        if (EncryptConfigFiles) categories.Add("Configuration files");
        if (EncryptUserFiles) categories.Add("User profiles");
        if (EncryptCacheFiles) categories.Add("Cache files");
        if (EncryptModFiles) categories.Add("Mod files");
        if (EncryptExecutables) categories.Add("⚠️ Executables (RISKY)");
        if (EncryptAssets) categories.Add("⚠️ Asset files (RISKY)");
        
        if (!string.IsNullOrWhiteSpace(CustomExtensions))
        {
            categories.Add($"Custom: {CustomExtensions}");
        }
        
        if (categories.Count == 0)
            return "No files will be encrypted";
        
        return "Will encrypt: " + string.Join(", ", categories);
    }
}