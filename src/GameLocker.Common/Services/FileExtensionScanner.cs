using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameLocker.Common.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameLocker.Common.Services;

/// <summary>
/// Scans game folders to discover actual file extensions and provides statistics.
/// This allows users to see exactly what file types exist and make informed encryption decisions.
/// </summary>
public class FileExtensionScanner
{
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a scanner.
    /// </summary>
    /// <param name="logger">Where to report per-file problems. Defaults to discarding them.</param>
    public FileExtensionScanner(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Extensions that must never be encrypted, whatever the user selects.
    /// </summary>
    /// <remarks>
    /// Encrypting any of these corrupts a game install. This is enforced in
    /// <c>FolderEncryptionSettings.ShouldEncryptFile</c> and
    /// <c>SelectiveEncryptionSettings.ShouldEncryptFile</c>, so no UI action, saved configuration,
    /// or custom-extension string can override it.
    /// <para>
    /// <c>.dat</c> is on this list. It was previously classified as a "Safe" save-file extension and
    /// auto-selected by default, but <c>.dat</c> is one of the most common primary asset-archive
    /// extensions in commercial games - Insomniac's engine ships Spider-Man's asset data as
    /// <c>.toc</c> + <c>.dat</c> pairs. See ARCHITECTURE-REVIEW.md finding [G-7].
    /// </para>
    /// </remarks>
    public static readonly IReadOnlySet<string> NeverEncryptExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Executables and libraries
        ".exe", ".dll", ".so", ".dylib", ".com", ".sys", ".drv", ".ocx", ".msi", ".bat", ".cmd", ".ps1",
        // Asset archives and engine containers
        ".dat", ".bin", ".pak", ".toc", ".arc", ".cpk", ".forge", ".rpf", ".bank", ".sarc",
        ".assets", ".bundle", ".resource", ".res", ".big", ".wad", ".vpk", ".bsa", ".ba2",
        ".uasset", ".umap", ".upk", ".pck", ".rvdata", ".vfs", ".psarc", ".xnb",
        // Disk and package images
        ".iso", ".img", ".cab", ".msix", ".appx",
    };

    /// <summary>
    /// Determines whether a file may never be encrypted, based on its extension.
    /// </summary>
    /// <param name="fileName">A file name or path.</param>
    /// <returns><c>true</c> when the file must be left untouched.</returns>
    public static bool IsNeverEncryptable(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return true;

        var extension = Path.GetExtension(fileName);
        return string.IsNullOrEmpty(extension) || NeverEncryptExtensions.Contains(extension);
    }

    /// <summary>
    /// Scans a folder and returns all unique file extensions with file counts.
    /// </summary>
    /// <param name="folderPath">Path to scan</param>
    /// <param name="recursive">Whether to scan subdirectories</param>
    /// <returns>Dictionary of extensions with file counts and example files</returns>
    public ExtensionScanResult ScanFolderExtensions(string folderPath, bool recursive = true)
    {
        var result = new ExtensionScanResult
        {
            FolderPath = folderPath,
            ScannedAt = DateTime.Now,
            Extensions = new Dictionary<string, ExtensionInfo>()
        };
        
        if (!Directory.Exists(folderPath))
        {
            result.ErrorMessage = "Folder does not exist";
            return result;
        }
        
        try
        {
            PathGuard.EnsureNoReparsePoints(folderPath);
            var files = Directory.GetFiles(folderPath, "*", PathGuard.FileEnumerationOptions(recursive));
            
            result.TotalFilesFound = files.Length;
            
            foreach (var file in files)
            {
                try
                {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    var fileName = Path.GetFileName(file);
                    PathGuard.EnsureNoReparsePoints(file);
                    var fileInfo = new FileInfo(file);
                    
                    // Skip files without extensions or just dots
                    if (string.IsNullOrEmpty(extension) || extension == ".")
                    {
                        extension = "[no extension]";
                    }
                    
                    if (!result.Extensions.ContainsKey(extension))
                    {
                        result.Extensions[extension] = new ExtensionInfo
                        {
                            Extension = extension,
                            FileCount = 0,
                            TotalSize = 0,
                            ExampleFiles = new List<string>(),
                            Category = CategorizeExtension(extension),
                            RiskLevel = AssessRiskLevel(extension)
                        };
                    }
                    
                    var extInfo = result.Extensions[extension];
                    extInfo.FileCount++;
                    extInfo.TotalSize += fileInfo.Length;
                    
                    // Keep up to 3 example files
                    if (extInfo.ExampleFiles.Count < 3)
                    {
                        extInfo.ExampleFiles.Add(fileName);
                    }
                }
                catch (Exception ex)
                {
                    // Skip individual file errors but continue scanning
                    _logger.LogDebug(ex, "Could not inspect {File} while scanning: {Message}", file, ex.Message);
                }
            }
            
            result.UniqueExtensions = result.Extensions.Count;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }
        
        return result;
    }
    
    /// <summary>
    /// Categorizes file extensions into logical groups for UI display.
    /// </summary>
    private string CategorizeExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".sav" or ".save" or ".savegame" or ".gamesave" or ".autosave"
                or ".progress" or ".slot" or ".checkpoint" => "Save Files",
            ".dat" or ".toc" or ".arc" or ".cpk" or ".forge" or ".rpf" or ".bank" or ".sarc" => "Game Data",
            ".cfg" or ".ini" or ".conf" or ".config" or ".settings" or ".pref" or ".json" or ".xml" => "Configuration",
            ".profile" or ".user" or ".player" or ".account" or ".character" => "User Data",
            ".exe" or ".dll" or ".so" or ".dylib" or ".bin" => "Executables",
            ".pak" or ".wad" or ".vpk" or ".bsa" or ".ba2" or ".big" or ".assets" => "Game Assets",
            ".txt" or ".log" or ".md" or ".readme" => "Text Files",
            ".jpg" or ".png" or ".bmp" or ".tga" or ".dds" => "Images",
            ".wav" or ".mp3" or ".ogg" or ".m4a" => "Audio",
            ".mp4" or ".avi" or ".mkv" or ".mov" => "Video", 
            ".tmp" or ".temp" or ".cache" or ".bak" => "Temporary",
            ".mod" or ".plugin" or ".addon" => "Mods",
            _ => "Other"
        };
    }
    
    /// <summary>
    /// Assesses the risk level of encrypting specific file types.
    /// </summary>
    private RiskLevel AssessRiskLevel(string extension)
    {
        // Anything on the hard denylist is Dangerous regardless of what the table below says.
        if (NeverEncryptExtensions.Contains(extension))
            return RiskLevel.Dangerous;

        return extension.ToLowerInvariant() switch
        {
            // SAFE: unambiguous player-save data only.
            //
            // RiskLevel.Safe drives the "tick this by default" behaviour in the ConfigUI, so this
            // set is deliberately small. Configuration formats (.ini, .cfg, .xml, .json) were moved
            // out of it: they are load-bearing engine configuration in most titles and are read at
            // startup, so encrypting them prevents the game from launching.
            // See ARCHITECTURE-REVIEW.md finding [G-7].
            ".sav" or ".save" or ".savegame" or ".gamesave" or ".autosave"
                or ".progress" or ".slot" or ".checkpoint" => RiskLevel.Safe,

            // MODERATE: usually recoverable or regenerated, but can affect a running game.
            ".cfg" or ".ini" or ".conf" or ".config" or ".settings" or ".pref" or ".json" or ".xml" => RiskLevel.Moderate,
            ".profile" or ".user" or ".player" or ".account" or ".character" => RiskLevel.Moderate,
            ".txt" or ".log" or ".md" or ".readme" => RiskLevel.Moderate,
            ".tmp" or ".temp" or ".cache" or ".bak" => RiskLevel.Moderate,

            // HIGH: large, or loaded directly by the engine.
            ".jpg" or ".png" or ".bmp" or ".tga" or ".dds" => RiskLevel.High,
            ".wav" or ".mp3" or ".ogg" or ".m4a" => RiskLevel.High,
            ".mp4" or ".avi" or ".mkv" or ".mov" => RiskLevel.High,
            ".mod" or ".plugin" or ".addon" => RiskLevel.High,

            _ => RiskLevel.Unknown
        };
    }
    
    /// <summary>
    /// Gets a quick preview of extensions in a folder without full scan.
    /// </summary>
    public List<string> GetExtensionPreview(string folderPath, int maxFiles = 100)
    {
        var extensions = new HashSet<string>();
        
        if (!Directory.Exists(folderPath)) return new List<string>();
        
        try
        {
            PathGuard.EnsureNoReparsePoints(folderPath);
            var files = Directory.EnumerateFiles(folderPath, "*", PathGuard.FileEnumerationOptions(recursive: false))
                .Take(maxFiles);
                
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!string.IsNullOrEmpty(ext) && ext != ".")
                {
                    extensions.Add(ext);
                }
            }
        }
        catch
        {
            // Return empty list on error
        }
        
        return extensions.OrderBy(e => e).ToList();
    }
}

/// <summary>
/// Result of scanning a folder for file extensions.
/// </summary>
public class ExtensionScanResult
{
    public string FolderPath { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public int TotalFilesFound { get; set; }
    public int UniqueExtensions { get; set; }
    public Dictionary<string, ExtensionInfo> Extensions { get; set; } = new();
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Gets extensions grouped by category for UI display.
    /// </summary>
    public Dictionary<string, List<ExtensionInfo>> GetExtensionsByCategory()
    {
        return Extensions.Values
            .GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Extension).ToList());
    }
    
    /// <summary>
    /// Gets extensions sorted by risk level (safest first).
    /// </summary>
    public List<ExtensionInfo> GetExtensionsByRisk()
    {
        return Extensions.Values
            .OrderBy(e => (int)e.RiskLevel)
            .ThenByDescending(e => e.FileCount)
            .ToList();
    }
}

/// <summary>
/// Information about a specific file extension found during scanning.
/// </summary>
public class ExtensionInfo
{
    public string Extension { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    public List<string> ExampleFiles { get; set; } = new();
    public string Category { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    
    /// <summary>
    /// Gets a human-readable size string.
    /// </summary>
    public string FormattedSize => FormatBytes(TotalSize);
    
    /// <summary>
    /// Gets a description for UI display.
    /// </summary>
    public string Description => $"{FileCount} files ({FormattedSize})";
    
    /// <summary>
    /// Gets example files as a comma-separated string.
    /// </summary>
    public string ExampleFilesList => string.Join(", ", ExampleFiles);
    
    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
        return $"{bytes / (1024 * 1024 * 1024):F1} GB";
    }
}

/// <summary>
/// Risk level for encrypting specific file types.
/// </summary>
public enum RiskLevel
{
    Safe = 0,       // No risk - save files, configs
    Moderate = 1,   // Low risk - cache, images  
    High = 2,       // May cause issues - mods, assets
    Dangerous = 3,  // Will likely cause crashes - executables
    Unknown = 4     // Unknown file type
}
