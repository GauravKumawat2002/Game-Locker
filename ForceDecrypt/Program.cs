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
            Console.WriteLine("FORCE DECRYPT TOOL - file recovery");
            Console.WriteLine("==================================");
            Console.WriteLine("This tool never deletes a file it could not decrypt and verify.");
            Console.WriteLine("");

            // No default target. This previously defaulted to a hardcoded personal game folder,
            // so running the tool with no arguments pointed its destructive logic at whatever
            // happened to live there. See ARCHITECTURE-REVIEW.md finding [T-7].
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: ForceDecrypt <folder-path>");
                Console.Error.WriteLine("       The folder path is required and is never inferred.");
                Environment.ExitCode = 2;
                return;
            }

            string folderPath = args[0];

            if (!Directory.Exists(folderPath))
            {
                Console.Error.WriteLine($"Folder does not exist: {folderPath}");
                Environment.ExitCode = 2;
                return;
            }

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
                
                // There is deliberately no fallback to keys.dat / iv.dat here. Those protect the
                // configuration file, not game files; using them guarantees every decryption fails,
                // which used to drive the whole folder down the destructive "raw recovery" path.
                // See ARCHITECTURE-REVIEW.md finding [D-5].
                if (!File.Exists(keyPath) || !File.Exists(ivPath))
                {
                    Console.Error.WriteLine($"Folder encryption keys not found in {keyStorePath}.");
                    Console.Error.WriteLine("Expected folder_key.dat and folder_iv.dat.");
                    Console.Error.WriteLine("Without them these files cannot be decrypted. Nothing has been modified.");
                    Environment.ExitCode = 3;
                    return;
                }

                byte[] masterKey = await DpapiHelper.UnprotectFromFileAsync(keyPath);
                byte[] masterIV = await DpapiHelper.UnprotectFromFileAsync(ivPath);
                Console.WriteLine("Encryption keys loaded.");
                
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
                        byte[] decryptedData;

                        try
                        {
                            decryptedData = AesEncryptionHelper.Decrypt(encryptedData, masterKey, masterIV);
                        }
                        catch (Exception decryptEx)
                        {
                            // The previous implementation copied the CIPHERTEXT over the plaintext
                            // filename and then deleted the .enc - destroying the only recoverable
                            // copy at exactly the moment the data was still recoverable, while
                            // reporting it as a successful recovery.
                            // See ARCHITECTURE-REVIEW.md finding [D-5]. Leave both files alone.
                            Console.Error.WriteLine($"  FAILED: {decryptEx.Message}");
                            Console.Error.WriteLine($"          '{Path.GetFileName(encFile)}' left untouched for retry.");
                            failCount++;
                            continue;
                        }

                        if (File.Exists(originalFile))
                        {
                            Console.Error.WriteLine($"  SKIPPED: '{Path.GetFileName(originalFile)}' already exists.");
                            Console.Error.WriteLine( "           Refusing to overwrite it. Resolve manually.");
                            failCount++;
                            continue;
                        }

                        // Write plaintext to a temporary file, verify it round-trips, and only then
                        // put it in place and remove the ciphertext.
                        var tempFile = originalFile + ".gltmp";
                        await File.WriteAllBytesAsync(tempFile, decryptedData);

                        var written = await File.ReadAllBytesAsync(tempFile);
                        if (written.Length != decryptedData.Length ||
                            !written.AsSpan().SequenceEqual(decryptedData))
                        {
                            File.Delete(tempFile);
                            Console.Error.WriteLine($"  FAILED: verification mismatch writing '{Path.GetFileName(originalFile)}'.");
                            Console.Error.WriteLine( "          Ciphertext left untouched for retry.");
                            failCount++;
                            continue;
                        }

                        File.Move(tempFile, originalFile);
                        File.Delete(encFile);

                        Console.WriteLine($"  OK");
                        successCount++;

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ❌ Error processing {Path.GetFileName(encFile)}: {ex.Message}");
                        failCount++;
                    }
                }
                
                Console.WriteLine("");
                Console.WriteLine("SUMMARY");
                Console.WriteLine($"  Decrypted and verified: {successCount}");
                Console.WriteLine($"  Failed:                 {failCount}");

                if (failCount == 0)
                {
                    Console.WriteLine("");
                    Console.WriteLine("All files decrypted and verified.");
                }
                else
                {
                    Console.WriteLine("");
                    Console.WriteLine($"{failCount} file(s) could not be decrypted. Their .enc files were NOT deleted,");
                    Console.WriteLine("so they can be retried once the cause is resolved. The folder is incomplete");
                    Console.WriteLine("and the game will not run correctly until every file is recovered.");
                    Environment.ExitCode = 1;
                }

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Critical error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Environment.ExitCode = 4;
            }

            if (!Console.IsInputRedirected)
            {
                Console.WriteLine("");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}