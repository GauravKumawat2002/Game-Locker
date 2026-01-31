using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using GameLocker.Common.Services;
using GameLocker.Common.Security;
using GameLocker.Common.Encryption;

namespace GameLocker.ForceDecrypt
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🚨 FORCE DECRYPT TOOL - EMERGENCY FILE RECOVERY");
            Console.WriteLine("================================================");
            
            string folderPath = args.Length > 0 ? args[0] : @"G:\Hogwarts Legacy";
            
            Console.WriteLine($"Target: {folderPath}");
            Console.WriteLine("");
            
            try
            {
                // Initialize encryption system
                string keyStorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GameLocker");
                Console.WriteLine("Loading encryption keys...");
                
                // Load keys directly
                var keyPath = Path.Combine(keyStorePath, "folder_key.dat");
                var ivPath = Path.Combine(keyStorePath, "folder_iv.dat");
                
                if (!File.Exists(keyPath) || !File.Exists(ivPath))
                {
                    Console.WriteLine("❌ Encryption keys not found. Trying alternative approach...");
                    
                    // Try original key files
                    keyPath = Path.Combine(keyStorePath, "keys.dat");
                    ivPath = Path.Combine(keyStorePath, "iv.dat");
                }
                
                byte[] masterKey = null;
                byte[] masterIV = null;
                
                if (File.Exists(keyPath) && File.Exists(ivPath))
                {
                    masterKey = await DpapiHelper.UnprotectFromFileAsync(keyPath);
                    masterIV = await DpapiHelper.UnprotectFromFileAsync(ivPath);
                    Console.WriteLine("✅ Encryption keys loaded successfully!");
                }
                else
                {
                    Console.WriteLine("❌ No valid encryption keys found!");
                    return;
                }
                
                // Find all encrypted files
                var encryptedFiles = Directory.GetFiles(folderPath, "*.enc", SearchOption.AllDirectories);
                Console.WriteLine($"Found {encryptedFiles.Length} encrypted files to process");
                Console.WriteLine("");
                
                int successCount = 0;
                int failCount = 0;
                
                foreach (var encFile in encryptedFiles)
                {
                    try
                    {
                        Console.WriteLine($"Decrypting: {Path.GetFileName(encFile)}");
                        
                        // Determine original file name
                        string originalFile = encFile.Substring(0, encFile.Length - 4); // Remove .enc
                        
                        // Read encrypted content
                        byte[] encryptedData = await File.ReadAllBytesAsync(encFile);
                        
                        // Try to decrypt
                        byte[] decryptedData = null;
                        
                        try 
                        {
                            decryptedData = AesEncryptionHelper.Decrypt(encryptedData, masterKey, masterIV);
                        }
                        catch (Exception decryptEx)
                        {
                            Console.WriteLine($"  ❌ Decrypt failed: {decryptEx.Message}");
                            
                            // Try alternative method - maybe file was corrupted, just remove .enc extension
                            Console.WriteLine($"  🔧 Attempting raw file recovery...");
                            
                            // If the original file doesn't exist, try to recover what we can
                            if (!File.Exists(originalFile))
                            {
                                // Copy the encrypted file without .enc extension as last resort
                                File.Copy(encFile, originalFile, true);
                                Console.WriteLine($"  ⚠️ Raw recovery attempted (file may be corrupted)");
                            }
                            
                            // Delete the .enc file
                            File.Delete(encFile);
                            failCount++;
                            continue;
                        }
                        
                        // Write decrypted content
                        await File.WriteAllBytesAsync(originalFile, decryptedData);
                        
                        // Delete encrypted file
                        File.Delete(encFile);
                        
                        Console.WriteLine($"  ✅ Successfully decrypted!");
                        successCount++;
                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ❌ Error processing {Path.GetFileName(encFile)}: {ex.Message}");
                        failCount++;
                    }
                }
                
                Console.WriteLine("");
                Console.WriteLine("🎯 DECRYPTION SUMMARY:");
                Console.WriteLine($"  ✅ Successfully decrypted: {successCount} files");
                Console.WriteLine($"  ❌ Failed to decrypt: {failCount} files");
                
                if (failCount == 0)
                {
                    Console.WriteLine("");
                    Console.WriteLine("🎉 ALL FILES SUCCESSFULLY DECRYPTED!");
                    Console.WriteLine("Your Hogwarts Legacy game should now work properly!");
                }
                else
                {
                    Console.WriteLine("");
                    Console.WriteLine("⚠️ Some files failed to decrypt but have been recovered where possible.");
                    Console.WriteLine("Try launching the game - it may still work with these files.");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Critical Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}