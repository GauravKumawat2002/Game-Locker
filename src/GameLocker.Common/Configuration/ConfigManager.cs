using System.Text;
using System.Text.Json;
using GameLocker.Common.Encryption;
using GameLocker.Common.Models;
using GameLocker.Common.Security;

namespace GameLocker.Common.Configuration;

/// <summary>
/// Manages loading and saving of encrypted GameLocker configuration.
/// </summary>
public class ConfigManager
{
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly string _keyFilePath;
    private readonly string _ivFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Creates a new ConfigManager with the default configuration directory.
    /// </summary>
    public ConfigManager() : this(GetDefaultConfigDirectory())
    {
    }

    /// <summary>
    /// Creates a new ConfigManager with a custom configuration directory.
    /// </summary>
    /// <param name="configDirectory">The directory to store configuration files.</param>
    public ConfigManager(string configDirectory)
    {
        _configDirectory = configDirectory;
        _configFilePath = Path.Combine(_configDirectory, "config.dat");
        _keyFilePath = Path.Combine(_configDirectory, "keys.dat");
        _ivFilePath = Path.Combine(_configDirectory, "iv.dat");
    }

    /// <summary>
    /// Gets the default configuration directory in ProgramData.
    /// </summary>
    public static string GetDefaultConfigDirectory()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "GameLocker");
    }

    /// <summary>
    /// Ensures the configuration directory exists.
    /// </summary>
    public void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }
    }

    /// <summary>
    /// Checks if a configuration exists.
    /// </summary>
    public bool ConfigExists()
    {
        return File.Exists(_configFilePath) && 
               File.Exists(_keyFilePath) && 
               File.Exists(_ivFilePath);
    }

    /// <summary>
    /// Saves the configuration encrypted with AES-256 and keys protected by DPAPI.
    /// </summary>
    /// <param name="config">The configuration to save.</param>
    public async Task SaveConfigAsync(GameLockerConfig config)
    {
        ValidateConfig(config);
        EnsureDirectoryExists();
        PathGuard.EnsureNoReparsePoints(_configDirectory);
        var writeLockPath = Path.Combine(_configDirectory, "config.write.lock");
        PathGuard.EnsureNoReparsePoints(writeLockPath);
        using var writeLock = new FileStream(writeLockPath, FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);

        var existing = await LoadConfigResultAsync();
        if (existing.IsDamaged)
            throw new InvalidDataException($"The saved configuration must be recovered before it can be replaced. {existing.Error}");

        // Serialize config to JSON
        var configJson = JsonSerializer.Serialize(config, JsonOptions);
        var configBytes = Encoding.UTF8.GetBytes(configJson);

        // Generate new key and IV if they don't exist
        byte[] key;
        byte[] iv;

        PathGuard.EnsureNoReparsePoints(_keyFilePath);
        PathGuard.EnsureNoReparsePoints(_ivFilePath);
        if (File.Exists(_keyFilePath) && File.Exists(_ivFilePath))
        {
            // Load existing keys
            key = await DpapiHelper.UnprotectFromFileAsync(_keyFilePath);
            iv = await DpapiHelper.UnprotectFromFileAsync(_ivFilePath);
        }
        else
        {
            // Generate new keys
            key = AesEncryptionHelper.GenerateKey();
            iv = AesEncryptionHelper.GenerateIV();

            // Save keys protected by DPAPI
            await DpapiHelper.ProtectToFileAsync(key, _keyFilePath);
            await DpapiHelper.ProtectToFileAsync(iv, _ivFilePath);
        }

        // Encrypt the config
        var encryptedConfig = AesEncryptionHelper.Encrypt(configBytes, key, iv);

        // Write to a temporary file, force it to stable storage, then swap it into place, keeping
        // the previous version as a backup. Writing directly over config.dat meant a power loss
        // during save truncated it, and a truncated config was indistinguishable from a first run -
        // at which point saving again destroyed the folder list.
        // See ARCHITECTURE-REVIEW.md findings [F-2] and [G-4].
        var tempPath = _configFilePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(encryptedConfig);
                stream.Flush(flushToDisk: true);
            }

            PathGuard.EnsureNoReparsePoints(BackupFilePath);
            if (existing.IsLoaded)
                File.Replace(tempPath, _configFilePath, BackupFilePath, ignoreMetadataErrors: true);
            else
                File.Move(tempPath, _configFilePath);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Gets the path of the backup written on each successful save.
    /// </summary>
    public string BackupFilePath => _configFilePath + ".bak";

    /// <summary>
    /// Loads and decrypts the configuration.
    /// </summary>
    /// <returns>The decrypted configuration, or <c>null</c> if it is missing or unreadable.</returns>
    /// <remarks>
    /// Prefer <see cref="LoadConfigResultAsync"/>. This overload cannot distinguish "no
    /// configuration yet" from "the configuration is damaged", and treating the second as the first
    /// is how a folder list gets silently overwritten.
    /// </remarks>
    public async Task<GameLockerConfig?> LoadConfigAsync() =>
        (await LoadConfigResultAsync()).Config;

    /// <summary>
    /// Loads and decrypts the configuration, reporting exactly what happened.
    /// </summary>
    /// <returns>The outcome, including the configuration when one was read.</returns>
    public async Task<ConfigLoadResult> LoadConfigResultAsync()
    {
        try
        {
            byte[] encryptedConfig;
            try
            {
                PathGuard.EnsureNoReparsePoints(_configFilePath);
                encryptedConfig = await File.ReadAllBytesAsync(_configFilePath);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return File.Exists(BackupFilePath)
                    ? ConfigLoadResult.Unreadable($"{_configFilePath} is missing. A backup exists at {BackupFilePath}.")
                    : ConfigLoadResult.NotFound();
            }

            // Load and unprotect keys
            PathGuard.EnsureNoReparsePoints(_keyFilePath);
            PathGuard.EnsureNoReparsePoints(_ivFilePath);
            var key = await DpapiHelper.UnprotectFromFileAsync(_keyFilePath);
            var iv = await DpapiHelper.UnprotectFromFileAsync(_ivFilePath);

            // Load and decrypt config
            var decryptedBytes = AesEncryptionHelper.Decrypt(encryptedConfig, key, iv);

            // Deserialize
            var configJson = Encoding.UTF8.GetString(decryptedBytes);
            var config = JsonSerializer.Deserialize<GameLockerConfig>(configJson, JsonOptions);

            if (config is null)
                return ConfigLoadResult.Unreadable("The configuration decrypted but contained no data.");

            ValidateConfig(config);
            return ConfigLoadResult.Loaded(config);
        }
        catch (Exception ex)
        {
            // A configuration file exists, so this is damage - never a first run.
            return ConfigLoadResult.Unreadable(ex.Message);
        }
    }

    private static void ValidateConfig(GameLockerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.AllowedDays is null || config.GameFolderPaths is null || config.FoldersPendingRestore is null ||
            config.FolderEncryptionSettings is null || config.EncryptionSettings is null ||
            config.DurationHours is < 1 or > 24 || !Enum.IsDefined(config.LockMode) ||
            config.AllowedDays.Any(day => !Enum.IsDefined(day)) ||
            config.GameFolderPaths.Concat(config.FoldersPendingRestore).Any(string.IsNullOrWhiteSpace) ||
            config.FolderEncryptionSettings.Any(settings => settings is null ||
                string.IsNullOrWhiteSpace(settings.FolderPath) || settings.SelectedExtensions is null))
        {
            throw new InvalidDataException("The saved schedule or folder settings contain invalid values.");
        }
    }

    /// <summary>
    /// Deletes all configuration files.
    /// </summary>
    public void DeleteConfig()
    {
        if (File.Exists(_configFilePath))
            File.Delete(_configFilePath);
        if (File.Exists(_keyFilePath))
            File.Delete(_keyFilePath);
        if (File.Exists(_ivFilePath))
            File.Delete(_ivFilePath);
    }

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    public string ConfigFilePath => _configFilePath;

    /// <summary>
    /// Gets the configuration directory path.
    /// </summary>
    public string ConfigDirectory => _configDirectory;
}
