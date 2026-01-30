using System.Text;
using System.Text.Json;
using GameLocker.Common.Encryption;
using GameLocker.Common.Models;

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
        EnsureDirectoryExists();

        // Serialize config to JSON
        var configJson = JsonSerializer.Serialize(config, JsonOptions);
        var configBytes = Encoding.UTF8.GetBytes(configJson);

        // Generate new key and IV if they don't exist
        byte[] key;
        byte[] iv;

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

        // Save the encrypted config
        await File.WriteAllBytesAsync(_configFilePath, encryptedConfig);
    }

    /// <summary>
    /// Loads and decrypts the configuration.
    /// </summary>
    /// <returns>The decrypted configuration, or null if not found.</returns>
    public async Task<GameLockerConfig?> LoadConfigAsync()
    {
        if (!ConfigExists())
        {
            return null;
        }

        try
        {
            // Load and unprotect keys
            var key = await DpapiHelper.UnprotectFromFileAsync(_keyFilePath);
            var iv = await DpapiHelper.UnprotectFromFileAsync(_ivFilePath);

            // Load and decrypt config
            var encryptedConfig = await File.ReadAllBytesAsync(_configFilePath);
            var decryptedBytes = AesEncryptionHelper.Decrypt(encryptedConfig, key, iv);

            // Deserialize
            var configJson = Encoding.UTF8.GetString(decryptedBytes);
            return JsonSerializer.Deserialize<GameLockerConfig>(configJson, JsonOptions);
        }
        catch (Exception)
        {
            // If decryption fails, return null
            return null;
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
