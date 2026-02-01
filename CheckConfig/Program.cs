using GameLocker.Common.Configuration;
using GameLocker.Common.Services;

var configManager = new ConfigManager();
var config = await configManager.LoadConfigAsync();

if (config == null)
{
    Console.WriteLine("❌ No config found");
    return;
}

Console.WriteLine("✅ Config Loaded Successfully!");
Console.WriteLine($"📅 Allowed Days: {string.Join(", ", config.AllowedDays)}");
Console.WriteLine($"⏰ Start Time: {config.StartTime}");
Console.WriteLine($"⏱️ Duration: {config.DurationHours} hours");
Console.WriteLine($"📁 Folder Count: {config.GameFolderPaths.Count}");
Console.WriteLine("\n📂 Folders:");
foreach (var path in config.GameFolderPaths)
{
    var exists = Directory.Exists(path);
    Console.WriteLine($"  [{(exists ? "✓" : "✗")}] {path}");
}

var now = DateTime.Now;
Console.WriteLine($"\n🕐 Current Time: {now}");
Console.WriteLine($"📆 Current Day: {now.DayOfWeek}");
Console.WriteLine($"🎮 Is Within Allowed Time: {config.IsWithinAllowedTime(now)}");

// Test FolderLocker
Console.WriteLine("\n🔒 Testing FolderLocker:");
var folderLocker = new FolderLocker(configManager.ConfigDirectory);
await folderLocker.InitializeAsync();

foreach (var path in config.GameFolderPaths)
{
    if (!Directory.Exists(path))
    {
        Console.WriteLine($"  ⚠️ {path} - Folder does not exist");
        continue;
    }
    
    var state = folderLocker.GetFolderState(path);
    Console.WriteLine($"  [{state}] {path}");
    
    if (state == GameLocker.Common.Models.FolderState.Unlocked)
    {
        Console.WriteLine($"     📝 Attempting to lock...");
        try
        {
            var result = await folderLocker.LockFolderAsync(path);
            Console.WriteLine($"     Result: {result.State} - Error: {result.LastError ?? "None"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"     ❌ Exception: {ex.Message}");
        }
    }
}
