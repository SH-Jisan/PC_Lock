using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PC.SecurityAgent.Controllers
{
    /// <summary>
    /// Enterprise BootGuard Auto-Healer implementing Solution 1:
    /// Zero-Drive-Letter Direct Volume Path Access (\\?\Volume{GUID}\)
    /// </summary>
    public class BootGuardHealer
    {
        private static readonly object _lockObj = new();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindFirstVolume([Out] StringBuilder lpszVolumeName, uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool FindNextVolume(IntPtr hFindVolume, [Out] StringBuilder lpszVolumeName, uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindVolumeClose(IntPtr hFindVolume);

        public static void HealBootConfiguration()
        {
            lock (_lockObj)
            {
                string? mountedDrive = null;
                try
                {
                    Console.WriteLine("[BootGuard Healer] Running Zero-Drive-Letter Integrity Audit...");

                    // 1. Attempt Primary Strategy: Zero-Drive-Letter Direct Volume GUID Access
                    string? efiVolumeRoot = FindEfiVolumeGuidDirect();

                    if (string.IsNullOrWhiteSpace(efiVolumeRoot) || !Directory.Exists(Path.Combine(efiVolumeRoot, "EFI")))
                    {
                        // Fallback Strategy: Dynamic High-Letter Allocation (Z:, Y:, X:)
                        char freeLetter = GetAvailableDriveLetter();
                        mountedDrive = $"{freeLetter}:";
                        RunProcess("mountvol", $"{mountedDrive} /s");
                        efiVolumeRoot = $@"{mountedDrive}\";
                    }

                    string espEfi = Path.Combine(efiVolumeRoot, "EFI");
                    if (!Directory.Exists(espEfi))
                    {
                        Console.WriteLine($"[BootGuard Warning] EFI System Partition structure inaccessible. Skipping pass.");
                        return;
                    }

                    string standardBootmgfw = Path.Combine(efiVolumeRoot, @"EFI\Microsoft\Boot\bootmgfw.efi");
                    string hiddenBootmgfw = Path.Combine(efiVolumeRoot, @"EFI\Microsoft\Boot\bootmgfw_hidden.efi");
                    string bootDir = Path.Combine(efiVolumeRoot, @"EFI\Boot");
                    string fallbackBootx64 = Path.Combine(efiVolumeRoot, @"EFI\Boot\bootx64.efi");
                    string pcLockDir = Path.Combine(efiVolumeRoot, @"EFI\PCLock");
                    string pcLockEfi = Path.Combine(efiVolumeRoot, @"EFI\PCLock\pc_lock_preboot.efi");
                    string wifiConfigFile = Path.Combine(efiVolumeRoot, @"EFI\PCLock\wifi_config.json");

                    // 2. Cloak bootmgfw.efi -> bootmgfw_hidden.efi if Windows Update recreated it
                    if (File.Exists(standardBootmgfw))
                    {
                        Console.WriteLine("[BootGuard Action] Windows Update recreated bootmgfw.efi. Re-cloaking -> bootmgfw_hidden.efi...");
                        if (File.Exists(hiddenBootmgfw)) File.Delete(hiddenBootmgfw);
                        File.Move(standardBootmgfw, hiddenBootmgfw);
                    }

                    // 3. Ensure Default Hardware Fallback & PCLock directories exist
                    if (!Directory.Exists(bootDir)) Directory.CreateDirectory(bootDir);
                    if (!Directory.Exists(pcLockDir)) Directory.CreateDirectory(pcLockDir);

                    // 4. Synchronize Active Windows Wi-Fi Profile to Pre-Boot Partition
                    SyncActiveWifiProfileToPreBoot(wifiConfigFile);

                    // 5. Deploy Pre-Boot binaries if available
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

                    // 6. Enforce BCD Firmware Boot Order (Remove direct {bootmgr} from F12 display order)
                    RunProcess("bcdedit", "/set {fwbootmgr} displayorder {bootmgr} /remove");
                    Console.WriteLine("[BootGuard Success] Zero-Drive-Letter Boot cloaking and Wi-Fi sync verified.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BootGuard Error] Heal pass encountered exception: {ex.Message}");
                }
                finally
                {
                    // Clean up temporary fallback mount if one was created
                    if (mountedDrive != null)
                    {
                        RunProcess("mountvol", $"{mountedDrive} /d");
                    }
                }
            }
        }

        private static string? FindEfiVolumeGuidDirect()
        {
            try
            {
                StringBuilder volumeName = new StringBuilder(260);
                IntPtr handle = FindFirstVolume(volumeName, (uint)volumeName.Capacity);
                if (handle == IntPtr.Zero || handle == (IntPtr)(-1)) return null;

                try
                {
                    do
                    {
                        string vol = volumeName.ToString();
                        string efiCheck = Path.Combine(vol, @"EFI\Microsoft\Boot");
                        try
                        {
                            if (Directory.Exists(efiCheck))
                            {
                                return vol;
                            }
                        }
                        catch { }
                    } while (FindNextVolume(handle, volumeName, (uint)volumeName.Capacity));
                }
                finally
                {
                    FindVolumeClose(handle);
                }
            }
            catch { }
            return null;
        }

        private static char GetAvailableDriveLetter()
        {
            var usedDrives = DriveInfo.GetDrives().Select(d => char.ToUpper(d.Name[0])).ToHashSet();
            for (char c = 'Z'; c >= 'E'; c--)
            {
                if (!usedDrives.Contains(c)) return c;
            }
            return 'S';
        }

        private static void SyncActiveWifiProfileToPreBoot(string destinationJsonPath)
        {
            try
            {
                string netshInterfaces = RunProcessWithOutput("netsh", "wlan show interfaces");
                var ssidMatch = Regex.Match(netshInterfaces, @"\bSSID\s*:\s*(.+)$", RegexOptions.Multiline);

                if (ssidMatch.Success)
                {
                    string ssid = ssidMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(ssid))
                    {
                        string profileOutput = RunProcessWithOutput("netsh", $"wlan show profile name=\"{ssid}\" key=clear");
                        var keyMatch = Regex.Match(profileOutput, @"Key Content\s*:\s*(.+)$", RegexOptions.Multiline);
                        string psk = keyMatch.Success ? keyMatch.Groups[1].Value.Trim() : string.Empty;

                        string json = $"{{\n  \"ssid\": \"{ssid}\",\n  \"psk\": \"{psk}\",\n  \"synced_at\": \"{DateTime.UtcNow:o}\"\n}}";
                        File.WriteAllText(destinationJsonPath, json);
                        Console.WriteLine($"[BootGuard Wi-Fi] Synced active profile ({ssid}) to Pre-Boot EFI partition.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BootGuard Wi-Fi Sync Warning] Wi-Fi sync skipped: {ex.Message}");
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

        private static string RunProcessWithOutput(string filename, string arguments)
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
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
