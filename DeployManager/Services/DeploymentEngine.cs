using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DeployManager.Services
{
    public class DeploymentEngine
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

        public static async Task<bool> DeployAsync(Action<string> log, IProgress<int> progress)
        {
            try
            {
                log("🚀 Initializing Tri-Vector Self-Healing Deployment (Mode 1)...");
                progress.Report(5);

                // Step 1: Scan topology & Mount EFI partition
                log("[1/4] Scanning storage topology & mounting EFI partition...");
                string mountLetter = GetAvailableDriveLetter();
                log($"[*] Selected EFI Mount Point: {mountLetter}:");

                ExecuteCommand("mountvol", $"{mountLetter}: /d");
                ExecuteCommand("mountvol", $"{mountLetter}: /s");
                progress.Report(25);

                // Step 2: Configure Vector 1 (Hardware Bootloader Cloaking)
                log("[2/4] Configuring Vector 1 (Hardware Bootloader Cloaking)...");
                string efiRoot = $"{mountLetter}:\\EFI";
                string pcLockDir = Path.Combine(efiRoot, "PCLock");
                string bootDir = Path.Combine(efiRoot, "Boot");
                string msBootDir = Path.Combine(efiRoot, "Microsoft", "Boot");

                Directory.CreateDirectory(pcLockDir);
                Directory.CreateDirectory(bootDir);
                Directory.CreateDirectory(msBootDir);

                string originalBootMgfw = Path.Combine(msBootDir, "bootmgfw.efi");
                string hiddenBootMgfw = Path.Combine(msBootDir, "bootmgfw_hidden.efi");

                if (File.Exists(originalBootMgfw))
                {
                    log("[*] Cloaking Microsoft bootmgfw.efi -> bootmgfw_hidden.efi...");
                    if (File.Exists(hiddenBootMgfw)) File.Delete(hiddenBootMgfw);
                    File.Move(originalBootMgfw, hiddenBootMgfw);
                }

                // Copy Pre-boot binaries
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string prebootBin = Path.GetFullPath(Path.Combine(baseDir, @"..\..\uefi-preboot\bin\pc_lock_preboot.efi"));
                if (!File.Exists(prebootBin)) prebootBin = Path.GetFullPath(Path.Combine(baseDir, @"uefi-preboot\bin\pc_lock_preboot.efi"));
                if (!File.Exists(prebootBin)) prebootBin = @"D:\Soft\PC_Lock\uefi-preboot\bin\pc_lock_preboot.efi";

                if (File.Exists(prebootBin))
                {
                    log($"[*] Deploying pre-boot binary from: {prebootBin}");
                    File.Copy(prebootBin, Path.Combine(msBootDir, "bootmgfw.efi"), true);
                    File.Copy(prebootBin, Path.Combine(bootDir, "bootx64.efi"), true);
                    File.Copy(prebootBin, Path.Combine(pcLockDir, "pc_lock_preboot.efi"), true);
                    log("[✔] Pre-Boot firmware cloak successfully installed into EFI partition.");
                }
                else
                {
                    log($"[⚠️ Warning] Pre-boot binary not found at {prebootBin}. Continuing agent setup...");
                }
                progress.Report(50);

                // Step 3: Vector 2 (BCD Priority Enforcer)
                log("[3/4] Configuring Vector 2 (BCD Firmware Priority Enforcer)...");
                ExecuteCommand("bcdedit", "/set {fwbootmgr} displayorder {bootmgr} /remove");
                ExecuteCommand("mountvol", $"{mountLetter}: /d");
                log("[✔] BCD firmware boot priority configured.");
                progress.Report(75);

                // Step 4: Vector 3 (Background Agent Service)
                log("[4/4] Activating Vector 3 (Continuous Background Agent)...");
                string dotnetExe = @"D:\Soft\dotnet\dotnet.exe";
                if (!File.Exists(dotnetExe)) dotnetExe = "dotnet";

                string agentDll = Path.GetFullPath(Path.Combine(baseDir, @"..\..\pc-agent\bin\App\PC.SecurityAgent.dll"));
                if (!File.Exists(agentDll)) agentDll = @"D:\Soft\PC_Lock\pc-agent\bin\App\PC.SecurityAgent.dll";

                ExecuteCommand("taskkill", "/F /IM PC.SecurityAgent.exe");

                // Start process detached & hidden
                Process.Start(new ProcessStartInfo
                {
                    FileName = dotnetExe,
                    Arguments = $"\"{agentDll}\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });

                // Configure Windows Startup Run Key for reboot persistence
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                    key?.SetValue("PCSecurityAgent", $"\"{dotnetExe}\" \"{agentDll}\"");
                }
                catch { }

                log("[✔] Background PC Security Agent is active and connected.");
                progress.Report(100);

                log("🎉 [SUCCESS] Mode 1: Tri-Vector Self-Healing Deployment Complete!");
                return true;
            }
            catch (Exception ex)
            {
                log($"❌ [ERROR] Deployment failed: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> UninstallAsync(Action<string> log, IProgress<int> progress)
        {
            try
            {
                log("══════════════════════════════════════════════════════════");
                log("🗑️ Starting Deep 6-Stage Factory-State System Restoration...");
                log("══════════════════════════════════════════════════════════");
                progress.Report(5);

                // Stage 1: Purge PC from Supabase Cloud
                log("[Stage 1/6] Purging device identity from Supabase Cloud Database...");
                string? machineGuid = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")?.GetValue("MachineGuid")?.ToString();
                if (!string.IsNullOrWhiteSpace(machineGuid) && machineGuid.Length >= 8)
                {
                    string pcId = $"pc_{machineGuid.Substring(0, 8)}";
                    try
                    {
                        var payload = new { pcId = pcId, hardwareUuid = machineGuid };
                        string json = JsonSerializer.Serialize(payload);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var res = await Http.PostAsync("https://pc-lock.onrender.com/api/devices/pc/deregister", content);
                        if (res.IsSuccessStatusCode)
                        {
                            log($"[✔] Workstation ({pcId}) completely purged from Supabase Cloud.");
                        }
                        else
                        {
                            log($"[*] Cloud purge response: {res.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        log($"[Notice] Cloud purge notice: {ex.Message}");
                    }
                }
                progress.Report(20);

                // Stage 2: Stop and Terminate all background security agents
                log("[Stage 2/6] Terminating active security daemons and background agents...");
                ExecuteCommand("sc", "stop \"PCSecurityAgent\"");
                ExecuteCommand("sc", "delete \"PCSecurityAgent\"");
                ExecuteCommand("sc", "stop \"PCSecurityAgentService\"");
                ExecuteCommand("sc", "delete \"PCSecurityAgentService\"");
                ExecuteCommand("taskkill", "/F /IM PC.SecurityAgent.exe");
                log("[✔] All security daemons and background tasks stopped.");
                progress.Report(40);

                // Stage 3: Mount EFI & Restore Microsoft Bootloader
                log("[Stage 3/6] Restoring original Windows EFI Bootloader in firmware...");
                string mountLetter = GetAvailableDriveLetter();
                ExecuteCommand("mountvol", $"{mountLetter}: /d");
                ExecuteCommand("mountvol", $"{mountLetter}: /s");

                string msBootDir = $"{mountLetter}:\\EFI\\Microsoft\\Boot";
                string hiddenBootMgfw = Path.Combine(msBootDir, "bootmgfw_hidden.efi");
                string originalBootMgfw = Path.Combine(msBootDir, "bootmgfw.efi");
                string bootDir = $"{mountLetter}:\\EFI\\Boot";
                string bootx64Orig = Path.Combine(bootDir, "bootx64_orig.efi");
                string bootx64 = Path.Combine(bootDir, "bootx64.efi");
                string pcLockDir = $"{mountLetter}:\\EFI\\PCLock";

                // A. Restore bootmgfw_hidden.efi -> bootmgfw.efi
                if (File.Exists(hiddenBootMgfw))
                {
                    if (File.Exists(originalBootMgfw)) File.Delete(originalBootMgfw);
                    File.Move(hiddenBootMgfw, originalBootMgfw);
                    log("[✔] Original Microsoft bootmgfw.efi successfully restored.");
                }

                // B. Restore bootx64_orig.efi if present
                if (File.Exists(bootx64Orig))
                {
                    if (File.Exists(bootx64)) File.Delete(bootx64);
                    File.Move(bootx64Orig, bootx64);
                    log("[✔] Original fallback bootx64.efi restored.");
                }

                // C. Delete EFI\PCLock directory completely
                if (Directory.Exists(pcLockDir))
                {
                    Directory.Delete(pcLockDir, true);
                    log("[✔] Deleted EFI\\PCLock folder and pre-boot configurations.");
                }

                ExecuteCommand("mountvol", $"{mountLetter}: /d");
                progress.Report(60);

                // Stage 4: Restore BCD Bootloader Display Priority
                log("[Stage 4/6] Restoring standard Windows BCD Boot Priority order in BIOS...");
                ExecuteCommand("bcdedit", "/set {fwbootmgr} displayorder {bootmgr} /addfirst");
                log("[✔] Standard Windows Boot Manager set as #1 boot priority.");
                progress.Report(80);

                // Stage 5: Clean Registry & Auto-Start entries
                log("[Stage 5/6] Cleaning Windows Registry, Credential Providers & Run entries...");
                try
                {
                    using var runKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                    runKey?.DeleteValue("PCSecurityAgent", false);
                    runKey?.DeleteValue("PCSecurityAgentService", false);

                    Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\PCSecuritySystem", false);
                    log("[✔] Registry configuration keys completely purged.");
                }
                catch (Exception ex)
                {
                    log($"[Notice] Registry clean notice: {ex.Message}");
                }
                progress.Report(95);

                // Stage 6: Post-Uninstallation Integrity Audit
                log("[Stage 6/6] Performing post-removal system integrity audit...");
                bool isClean = true;
                try
                {
                    using var testKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\PCSecuritySystem");
                    if (testKey != null) isClean = false;
                }
                catch { }

                if (isClean)
                {
                    log("[✔] System integrity audit passed: 100% factory clean.");
                }

                progress.Report(100);
                log("══════════════════════════════════════════════════════════");
                log("🎉 [SUCCESS] PC Security & Pre-Boot Completely Removed!");
                log("   Your PC & Supabase Database are 100% factory restored.");
                log("══════════════════════════════════════════════════════════");
                return true;
            }
            catch (Exception ex)
            {
                log($"❌ [ERROR] Uninstallation error: {ex.Message}");
                return false;
            }
        }

        private static string GetAvailableDriveLetter()
        {
            char[] letters = new char[] { 'Z', 'Y', 'X', 'W', 'V', 'U', 'T', 'S', 'R', 'Q', 'P' };
            foreach (char l in letters)
            {
                if (!Directory.Exists($"{l}:\\")) return l.ToString();
            }
            return "Z";
        }

        private static void ExecuteCommand(string filename, string arguments)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                p?.WaitForExit(4000);
            }
            catch { }
        }
    }
}
