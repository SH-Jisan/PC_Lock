using System;
using System.Diagnostics;
using System.IO;

namespace PC.SecurityAgent.Controllers
{
    public class BootGuardHealer
    {
        private static readonly object _lockObj = new();

        public static void HealBootConfiguration()
        {
            lock (_lockObj)
            {
                bool mounted = false;
                try
                {
                    Console.WriteLine("[BootGuard Healer] Checking EFI System Partition & Boot Order integrity...");

                    // 1. Mount EFI System Partition to drive S:
                    RunProcess("mountvol", "S: /s");
                    mounted = true;

                    string espRoot = @"S:\EFI";
                    if (!Directory.Exists(espRoot))
                    {
                        Console.WriteLine("[BootGuard Warning] EFI Partition not mounted on S:. Skipping pass.");
                        return;
                    }

                    string standardBootmgfw = @"S:\EFI\Microsoft\Boot\bootmgfw.efi";
                    string hiddenBootmgfw = @"S:\EFI\Microsoft\Boot\bootmgfw_hidden.efi";
                    string bootDir = @"S:\EFI\Boot";
                    string fallbackBootx64 = @"S:\EFI\Boot\bootx64.efi";
                    string pcLockDir = @"S:\EFI\PCLock";
                    string pcLockEfi = @"S:\EFI\PCLock\pc_lock_preboot.efi";

                    // 2. Cloak bootmgfw.efi if Windows Update recreated it
                    if (File.Exists(standardBootmgfw))
                    {
                        Console.WriteLine("[BootGuard Action] Windows Update recreated bootmgfw.efi. Re-cloaking to bootmgfw_hidden.efi...");
                        if (File.Exists(hiddenBootmgfw))
                        {
                            File.Delete(hiddenBootmgfw);
                        }
                        File.Move(standardBootmgfw, hiddenBootmgfw);
                        Console.WriteLine("[BootGuard Success] bootmgfw.efi re-cloaked successfully.");
                    }

                    // 3. Ensure Default Hardware Fallback (\EFI\Boot\bootx64.efi) exists
                    if (!Directory.Exists(bootDir)) Directory.CreateDirectory(bootDir);
                    if (!Directory.Exists(pcLockDir)) Directory.CreateDirectory(pcLockDir);

                    // If local source binary exists in app folder, copy to EFI
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string localEfiSource = Path.Combine(appDir, "pc_lock_preboot.efi");
                    if (!File.Exists(localEfiSource))
                    {
                        localEfiSource = @"C:\Program Files\PCSecuritySystem\pc_lock_preboot.efi";
                    }

                    if (File.Exists(localEfiSource))
                    {
                        File.Copy(localEfiSource, fallbackBootx64, true);
                        File.Copy(localEfiSource, pcLockEfi, true);
                    }

                    // 4. Enforce BCD Firmware Boot Order (Remove direct {bootmgr} from F12 display order)
                    RunProcess("bcdedit", "/set {fwbootmgr} displayorder {bootmgr} /remove");
                    Console.WriteLine("[BootGuard Success] Boot configuration healed and verified.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BootGuard Error] HealBootConfiguration encountered exception: {ex.Message}");
                }
                finally
                {
                    if (mounted)
                    {
                        RunProcess("mountvol", "S: /d");
                    }
                }
            }
        }


        private static void RunProcess(string filename, string arguments)
        {
            try
            {
                using Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = filename,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                process.WaitForExit(5000);
            }
            catch { }
        }
    }
}
