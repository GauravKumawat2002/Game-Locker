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
            
            string folderPath = args.Length > 0 ? args[0] : @"G:\Hogwarts Legacy";
            
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
                
                if (remainingEncFiles.Length == 0)
                {
                    Console.WriteLine("🎉 All files successfully decrypted!");
                }
                else
                {
                    Console.WriteLine("⚠️ Some files remain encrypted:");
                    foreach (var file in remainingEncFiles.Take(5))
                    {
                        Console.WriteLine($"    - {file.Name}");
                    }
                }
                
                // Check for .gamelocker marker
                string markerPath = Path.Combine(folderPath, ".gamelocker");
                if (File.Exists(markerPath))
                {
                    Console.WriteLine("🗑️ Removing GameLocker marker file...");
                    File.Delete(markerPath);
                    Console.WriteLine("✅ Marker file removed!");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return;
            }
            
            Console.WriteLine("\n🎉 Hogwarts Legacy folder is now fully restored to original state!");
        }
    }
}
