using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GameLocker.Common.Models;

/// <summary>
/// Per-folder encryption settings with user-selected file extensions.
/// Each game folder can have its own specific extension selection.
/// </summary>
public class FolderEncryptionSettings
{
    /// <summary>
    /// Path to the game folder these settings apply to.
    /// </summary>
    [JsonPropertyName("folderPath")]
    public string FolderPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether to use custom extension selection for this folder.
    /// </summary>
    [JsonPropertyName("useCustomSelection")]
    public bool UseCustomSelection { get; set; } = true;
    
    /// <summary>
    /// List of file extensions the user selected to encrypt for this specific folder.
    /// </summary>
    [JsonPropertyName("selectedExtensions")]
    public List<string> SelectedExtensions { get; set; } = new();
    
    /// <summary>
    /// Timestamp when this folder was last scanned for file extensions.
    /// </summary>
    [JsonPropertyName("lastScanned")]
    public DateTime? LastScanned { get; set; }
    
    /// <summary>
    /// Total number of files found during last scan.
    /// </summary>
    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; }
    
    /// <summary>
    /// Number of unique file extensions found during last scan.
    /// </summary>
    [JsonPropertyName("uniqueExtensions")]
    public int UniqueExtensions { get; set; }
    
    /// <summary>
    /// Fallback to preset settings if no custom selection is made.
    /// </summary>
    [JsonPropertyName("fallbackToPreset")]
    public SelectiveEncryptionSettings? FallbackPreset { get; set; }
    
    /// <summary>
    /// User notes about why specific extensions were selected/deselected.
    /// </summary>
    [JsonPropertyName("userNotes")]
    public string UserNotes { get; set; } = string.Empty;
    
    /// <summary>
    /// Check if a specific file should be encrypted based on user's extension selection.
    /// </summary>
    public bool ShouldEncryptFile(string fileName)
    {
        if (!UseCustomSelection || SelectedExtensions.Count == 0)
        {
            // Fall back to preset if no custom selection
            return FallbackPreset?.ShouldEncryptFile(fileName) ?? false;
        }
        
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return SelectedExtensions.Contains(extension);
    }
    
    /// <summary>
    /// Gets a summary of what will be encrypted.
    /// </summary>
    public string GetEncryptionSummary()
    {
        if (!UseCustomSelection || SelectedExtensions.Count == 0)
        {
            var fallback = FallbackPreset?.GetEncryptionSummary() ?? "No encryption configured";
            return $"Using preset: {fallback}";
        }
        
        if (SelectedExtensions.Count <= 5)
        {
            return $"Custom selection: {string.Join(", ", SelectedExtensions)}";
        }
        else
        {
            var first3 = string.Join(", ", SelectedExtensions.Take(3));
            return $"Custom selection: {first3} and {SelectedExtensions.Count - 3} more";
        }
    }
    
    /// <summary>
    /// Gets statistics about the current selection.
    /// </summary>
    public EncryptionStats GetStats()
    {
        return new EncryptionStats
        {
            TotalExtensions = UniqueExtensions,
            SelectedExtensions = SelectedExtensions.Count,
            SelectionPercentage = UniqueExtensions > 0 ? (SelectedExtensions.Count * 100.0 / UniqueExtensions) : 0,
            LastUpdated = LastScanned ?? DateTime.MinValue
        };
    }
}

/// <summary>
/// Statistics about encryption selection for a folder.
/// </summary>
public class EncryptionStats
{
    public int TotalExtensions { get; set; }
    public int SelectedExtensions { get; set; }
    public double SelectionPercentage { get; set; }
    public DateTime LastUpdated { get; set; }
    
    public string Summary => $"{SelectedExtensions}/{TotalExtensions} extensions ({SelectionPercentage:F1}%)";
}