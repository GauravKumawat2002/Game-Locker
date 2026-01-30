using System.Security.Cryptography;

namespace GameLocker.Common.Encryption;

/// <summary>
/// Provides AES-256 encryption and decryption functionality.
/// </summary>
public static class AesEncryptionHelper
{
    private const int KeySize = 256; // AES-256
    private const int BlockSize = 128;
    private const int IvSize = 16; // 128 bits

    /// <summary>
    /// Generates a new random AES-256 key.
    /// </summary>
    /// <returns>A 32-byte array containing the AES key.</returns>
    public static byte[] GenerateKey()
    {
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.GenerateKey();
        return aes.Key;
    }

    /// <summary>
    /// Generates a new random initialization vector (IV).
    /// </summary>
    /// <returns>A 16-byte array containing the IV.</returns>
    public static byte[] GenerateIV()
    {
        using var aes = Aes.Create();
        aes.GenerateIV();
        return aes.IV;
    }

    /// <summary>
    /// Encrypts data using AES-256 CBC mode.
    /// </summary>
    /// <param name="plainData">The data to encrypt.</param>
    /// <param name="key">The AES-256 key (32 bytes).</param>
    /// <param name="iv">The initialization vector (16 bytes).</param>
    /// <returns>The encrypted data.</returns>
    public static byte[] Encrypt(byte[] plainData, byte[] key, byte[] iv)
    {
        ArgumentNullException.ThrowIfNull(plainData);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(iv);

        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes for AES-256.", nameof(key));
        if (iv.Length != IvSize)
            throw new ArgumentException($"IV must be {IvSize} bytes.", nameof(iv));

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(plainData, 0, plainData.Length);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Decrypts data using AES-256 CBC mode.
    /// </summary>
    /// <param name="encryptedData">The data to decrypt.</param>
    /// <param name="key">The AES-256 key (32 bytes).</param>
    /// <param name="iv">The initialization vector (16 bytes).</param>
    /// <returns>The decrypted data.</returns>
    public static byte[] Decrypt(byte[] encryptedData, byte[] key, byte[] iv)
    {
        ArgumentNullException.ThrowIfNull(encryptedData);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(iv);

        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes for AES-256.", nameof(key));
        if (iv.Length != IvSize)
            throw new ArgumentException($"IV must be {IvSize} bytes.", nameof(iv));

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
        {
            cs.Write(encryptedData, 0, encryptedData.Length);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Encrypts a file and writes to a new file with .enc extension.
    /// </summary>
    /// <param name="sourcePath">Path to the source file.</param>
    /// <param name="key">The AES-256 key.</param>
    /// <param name="iv">The initialization vector.</param>
    /// <returns>Path to the encrypted file.</returns>
    public static async Task<string> EncryptFileAsync(string sourcePath, byte[] key, byte[] iv)
    {
        var destPath = sourcePath + ".enc";
        var plainData = await File.ReadAllBytesAsync(sourcePath);
        var encryptedData = Encrypt(plainData, key, iv);
        await File.WriteAllBytesAsync(destPath, encryptedData);
        return destPath;
    }

    /// <summary>
    /// Decrypts a .enc file back to its original form.
    /// </summary>
    /// <param name="encryptedPath">Path to the encrypted file (should end with .enc).</param>
    /// <param name="key">The AES-256 key.</param>
    /// <param name="iv">The initialization vector.</param>
    /// <returns>Path to the decrypted file.</returns>
    public static async Task<string> DecryptFileAsync(string encryptedPath, byte[] key, byte[] iv)
    {
        if (!encryptedPath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Encrypted file should have .enc extension.", nameof(encryptedPath));

        var destPath = encryptedPath[..^4]; // Remove .enc extension
        var encryptedData = await File.ReadAllBytesAsync(encryptedPath);
        var decryptedData = Decrypt(encryptedData, key, iv);
        await File.WriteAllBytesAsync(destPath, decryptedData);
        return destPath;
    }
}
