using GameLocker.Common.Configuration;
using System.Text.Json;

var configManager = new ConfigManager();
var config = await configManager.LoadConfigAsync();

if (config == null)
{
    Console.WriteLine(""No config found"");
}
else
{
    Console.WriteLine($""Config Loaded Successfully!"");
    Console.WriteLine($""Allowed Days: {string.Join("", "", config.AllowedDays)}"");
    Console.WriteLine($""Start Time: {config.StartTime}"");
    Console.WriteLine($""Duration: {config.DurationHours} hours"");
    Console.WriteLine($""Folder Count: {config.GameFolderPaths.Count}"");
    Console.WriteLine(""Folders:"");
    foreach (var path in config.GameFolderPaths)
    {
        Console.WriteLine($""  - {path}"");
    }
    
    var now = DateTime.Now;
    Console.WriteLine($""\nCurrent Time: {now}"");
    Console.WriteLine($""Current Day: {now.DayOfWeek}"");
    Console.WriteLine($""Is Within Allowed Time: {config.IsWithinAllowedTime(now)}"");
}
