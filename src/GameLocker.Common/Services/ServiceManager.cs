using System.ServiceProcess;

namespace GameLocker.Common.Services;

/// <summary>
/// Provides functionality to manage the GameLocker Windows Service
/// </summary>
public static class ServiceManager
{
    private const string ServiceName = "GameLocker Service";
    
    /// <summary>
    /// Checks if the GameLocker service is installed
    /// </summary>
    public static bool IsServiceInstalled()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            var status = service.Status; // This will throw if service doesn't exist
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Gets the current status of the GameLocker service
    /// </summary>
    public static ServiceControllerStatus? GetServiceStatus()
    {
        try
        {
            if (!IsServiceInstalled()) return null;
            
            using var service = new ServiceController(ServiceName);
            return service.Status;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Checks if the GameLocker service is running
    /// </summary>
    public static bool IsServiceRunning()
    {
        var status = GetServiceStatus();
        return status == ServiceControllerStatus.Running;
    }
    
    /// <summary>
    /// Starts the GameLocker service if it's not running
    /// </summary>
    public static async Task<bool> StartServiceAsync()
    {
        try
        {
            if (!IsServiceInstalled()) return false;
            
            using var service = new ServiceController(ServiceName);
            if (service.Status == ServiceControllerStatus.Running)
                return true;
                
            service.Start();
            
            // Wait for service to start (max 30 seconds)
            await Task.Run(() =>
            {
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            });
            
            return service.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Stops the GameLocker service if it's running
    /// </summary>
    public static async Task<bool> StopServiceAsync()
    {
        try
        {
            if (!IsServiceInstalled()) return false;
            
            using var service = new ServiceController(ServiceName);
            if (service.Status == ServiceControllerStatus.Stopped)
                return true;
                
            service.Stop();
            
            // Wait for service to stop (max 30 seconds)
            await Task.Run(() =>
            {
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            });
            
            return service.Status == ServiceControllerStatus.Stopped;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Restarts the GameLocker service
    /// </summary>
    public static async Task<bool> RestartServiceAsync()
    {
        var stopped = await StopServiceAsync();
        if (!stopped) return false;
        
        // Wait a moment before starting
        await Task.Delay(2000);
        
        return await StartServiceAsync();
    }
    
    /// <summary>
    /// Sends a command to the service to reload configuration immediately
    /// </summary>
    public static async Task<bool> SendConfigReloadCommandAsync()
    {
        // Since we can't directly send commands to a background service,
        // we'll use a file-based signal mechanism
        try
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GameLocker");
            Directory.CreateDirectory(configDir);
            
            var signalFile = Path.Combine(configDir, "reload_signal");
            await File.WriteAllTextAsync(signalFile, DateTime.Now.ToString("O"));
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Sends a command to immediately apply encryption to a new folder
    /// </summary>
    public static async Task<bool> SendImmediateActionCommandAsync(string action, string folderPath)
    {
        try
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GameLocker");
            Directory.CreateDirectory(configDir);
            
            var commandFile = Path.Combine(configDir, "immediate_action");
            var command = $"{action}|{folderPath}|{DateTime.Now:O}";
            await File.WriteAllTextAsync(commandFile, command);
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}