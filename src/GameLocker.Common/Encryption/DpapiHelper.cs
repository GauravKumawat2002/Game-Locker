using System.Security.Cryptography;

namespace GameLocker.Common.Encryption;

/// <summary>
/// Provides Windows DPAPI (Data Protection API) functionality for secure key storage.
/// Uses machine scope so only processes on the same machine can decrypt.
/// </summary>
public static class DpapiHelper
{
    // Optional entropy for additional security
    private static readonly byte[] AdditionalEntropy = 
        "GameLocker.Security.Entropy.v1"u8.ToArray();

    /// <summary>
    /// Protects (encrypts) data using Windows DPAPI with machine scope.
    /// </summary>
    /// <param name="data">The data to protect.</param>
    /// <returns>The protected (encrypted) data.</returns>
    public static byte[] Protect(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return ProtectedData.Protect(
            data, 
            AdditionalEntropy, 
            DataProtectionScope.LocalMachine);
    }

    /// <summary>
    /// Unprotects (decrypts) data using Windows DPAPI with machine scope.
    /// </summary>
    /// <param name="protectedData">The protected data to decrypt.</param>
    /// <returns>The unprotected (decrypted) data.</returns>
    public static byte[] Unprotect(byte[] protectedData)
    {
        ArgumentNullException.ThrowIfNull(protectedData);

        return ProtectedData.Unprotect(
            protectedData, 
            AdditionalEntropy, 
            DataProtectionScope.LocalMachine);
    }

    /// <summary>
    /// Protects data and saves to a file.
    /// </summary>
    /// <param name="data">The data to protect.</param>
    /// <param name="filePath">Path to save the protected data.</param>
    public static async Task ProtectToFileAsync(byte[] data, string filePath)
    {
        var protectedData = Protect(data);
        await File.WriteAllBytesAsync(filePath, protectedData);
    }

    /// <summary>
    /// Reads protected data from a file and unprotects it.
    /// </summary>
    /// <param name="filePath">Path to the protected data file.</param>
    /// <returns>The unprotected data.</returns>
    public static async Task<byte[]> UnprotectFromFileAsync(string filePath)
    {
        var protectedData = await File.ReadAllBytesAsync(filePath);
        return Unprotect(protectedData);
    }
}
