using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using GameLocker.Common.Services;
using GameLocker.Common.Security;

namespace GameLocker.UnlockTool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("GameLocker Decrypt Tool");
            Console.WriteLine("======================");

            // No default target. This previously defaulted to a hardcoded personal game folder.
            // See ARCHITECTURE-REVIEW.md finding [T-7].
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: UnlockTool <folder-path>");
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

            Console.WriteLine($"Decrypting folder: {folderPath}");
            
            try
            {
                // Check if running as admin
                if (!AclHelper.IsRunningAsAdmin())
                {
                    Console.WriteLine("WARNING: Not running as administrator.");
                }
                
                // Initialize FolderLocker
                string keyStorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GameLocker");
                var folderLocker = new FolderLocker(keyStorePath);
                
                Console.WriteLine("Initializing encryption keys...");
                await folderLocker.InitializeAsync();
                
                // Check current state
                var currentState = folderLocker.GetFolderState(folderPath);
                Console.WriteLine($"Current folder state: {currentState}");
                
                if (currentState == GameLocker.Common.Models.FolderState.Locked)
                {
                    Console.WriteLine("🔓 Unlocking and decrypting folder...");
                    
                    var result = await folderLocker.UnlockFolderAsync(folderPath);
                    
                    if (result.State == GameLocker.Common.Models.FolderState.Unlocked)
                    {
                        Console.WriteLine("✅ SUCCESS: Folder fully unlocked and decrypted!");
                    }
                    else
                    {
                        Console.WriteLine($"❌ FAILED: {result.LastError ?? "Unknown error"}");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("ℹ️ Folder is not in locked state, but checking for encrypted files...");
                    
                    // Force decrypt if there are .enc files
                    var encryptedFiles = Directory.GetFiles(folderPath, "*.enc", SearchOption.AllDirectories);
                    if (encryptedFiles.Length > 0)
                    {
                        Console.WriteLine($"Found {encryptedFiles.Length} encrypted files. Decrypting...");
                        var result = await folderLocker.UnlockFolderAsync(folderPath);
                        
                        if (result.State == GameLocker.Common.Models.FolderState.Unlocked)
                        {
                            Console.WriteLine("✅ SUCCESS: Files decrypted!");
                        }
                    }
                }
                
                // Verify final state
                Console.WriteLine("\n📁 Final verification:");
                var di = new DirectoryInfo(folderPath);
                var allFiles = di.GetFiles("*", SearchOption.AllDirectories);
                var remainingEncFiles = allFiles.Where(f => f.Name.EndsWith(".enc")).ToArray();
                var normalFiles = allFiles.Where(f => !f.Name.EndsWith(".enc") && f.Name != ".gamelocker").ToArray();
                
                Console.WriteLine($"  - Total files: {allFiles.Length}");
                Console.WriteLine($"  - Encrypted files remaining: {remainingEncFiles.Length}");
                Console.WriteLine($"  - Normal files: {normalFiles.Length}");
                
                if (remainingEncFiles.Length > 0)
                {
                    // The lock marker is deliberately left in place: it is the only remaining record
                    // that this folder is under GameLocker management, and removing it while files
                    // are still encrypted makes the folder report itself as Unlocked.
                    // See ARCHITECTURE-REVIEW.md finding [F-1].
                    Console.Error.WriteLine($"{remainingEncFiles.Length} file(s) remain encrypted:");
                    foreach (var file in remainingEncFiles.Take(10))
                    {
                        Console.Error.WriteLine($"    - {file.FullName}");
                    }
                    if (remainingEncFiles.Length > 10)
                        Console.Error.WriteLine($"    ... and {remainingEncFiles.Length - 10} more");

                    Console.Error.WriteLine("");
                    Console.Error.WriteLine("The folder is NOT fully restored and the lock marker has been kept.");
                    Console.Error.WriteLine("Resolve the cause and run again before using the folder.");
                    Environment.ExitCode = 1;
                    return;
                }

                Console.WriteLine("All files decrypted.");

                // Only now is it safe to clear the marker.
                string markerPath = Path.Combine(folderPath, ".gamelocker");
                if (File.Exists(markerPath))
                {
                    var markerInfo = new FileInfo(markerPath);
                    markerInfo.Attributes &= ~FileAttributes.Hidden;
                    File.Delete(markerPath);
                    Console.WriteLine("Lock marker removed.");
                }

                Console.WriteLine($"\n'{folderPath}' is fully restored.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Environment.ExitCode = 4;
                return;
            }
        }
    }
}
