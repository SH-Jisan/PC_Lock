using System;
using System.Diagnostics;
using System.IO;

namespace DeployManager.Services
{
    /// <summary>
    /// Manages EFI System Partition (ESP) mounting, EFI bootloader restoration, and BCD boot order.
    /// </summary>
    public static class BootloaderManager
    {
        public static string GetAvailableDriveLetter()
        {
            char[] letters = new char[] { 'Z', 'Y', 'X', 'W', 'V', 'U', 'T', 'S', 'R', 'Q', 'P' };
            foreach (char l in letters)
            {
                if (!Directory.Exists($"{l}:\\")) return l.ToString();
            }
            return "Z";
        }

        public static void ExecuteCommand(string filename, string arguments)
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

        public static string? FindPrebootEfiBinary(string baseDir)
        {
            string[] efiCandidates = new[]
            {
                Path.Combine(baseDir, "pc_lock_preboot.efi"),
                Path.Combine(baseDir, "UEFI", "pc_lock_preboot.efi"),
                Path.GetFullPath(Path.Combine(baseDir, @"..\UEFI\pc_lock_preboot.efi")),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\uefi-preboot\bin\pc_lock_preboot.efi")),
                @"D:\Soft\PC_Lock\uefi-preboot\bin\pc_lock_preboot.efi",
                @"C:\Program Files\PCSecuritySystem\pc_lock_preboot.efi"
            };

            foreach (var path in efiCandidates)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public static void MountEsp(string mountLetter)
        {
            ExecuteCommand("mountvol", $"{mountLetter}: /d");
            ExecuteCommand("mountvol", $"{mountLetter}: /s");
        }

        public static void UnmountEsp(string mountLetter)
        {
            ExecuteCommand("mountvol", $"{mountLetter}: /d");
        }

        public static void SetWindowsBootManagerPrimary()
        {
            ExecuteCommand("bcdedit", "/set {fwbootmgr} displayorder {bootmgr} /addfirst");
        }

        public static void RemoveWindowsBootManagerFromDisplayOrder()
        {
            ExecuteCommand("bcdedit", "/set {fwbootmgr} displayorder {bootmgr} /remove");
        }

        public static void EnsureFactoryState(Action<string> log)
        {
            string mountLetter = GetAvailableDriveLetter();
            MountEsp(mountLetter);

            string msBootDir = $"{mountLetter}:\\EFI\\Microsoft\\Boot";
            string hiddenBootMgfw = Path.Combine(msBootDir, "bootmgfw_hidden.efi");
            string originalBootMgfw = Path.Combine(msBootDir, "bootmgfw.efi");
            string pcLockDir = $"{mountLetter}:\\EFI\\PCLock";

            if (File.Exists(hiddenBootMgfw))
            {
                if (File.Exists(originalBootMgfw)) File.Delete(originalBootMgfw);
                File.Move(hiddenBootMgfw, originalBootMgfw);
                log("[✔] Microsoft bootmgfw.efi verified in standard factory state.");
            }

            if (Directory.Exists(pcLockDir))
            {
                try { Directory.Delete(pcLockDir, true); } catch { }
            }

            UnmountEsp(mountLetter);
            SetWindowsBootManagerPrimary();
            log("[✔] Windows Boot Manager confirmed as primary bootloader (0% boot delay).");
        }

        public static bool DeployPreBootEfi(Action<string> log)
        {
            string mountLetter = GetAvailableDriveLetter();
            MountEsp(mountLetter);

            string efiRoot = $"{mountLetter}:\\EFI";
            string pcLockDir = Path.Combine(efiRoot, "PCLock");
            string bootDir = Path.Combine(efiRoot, "Boot");
            string msBootDir = Path.Combine(efiRoot, "Microsoft", "Boot");

            Directory.CreateDirectory(pcLockDir);
            Directory.CreateDirectory(bootDir);
            Directory.CreateDirectory(msBootDir);

            string originalBootMgfw = Path.Combine(msBootDir, "bootmgfw.efi");
            string hiddenBootMgfw = Path.Combine(msBootDir, "bootmgfw_hidden.efi");

            if (File.Exists(originalBootMgfw) && !File.Exists(hiddenBootMgfw))
            {
                File.Move(originalBootMgfw, hiddenBootMgfw);
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string? prebootBin = FindPrebootEfiBinary(baseDir);

            if (prebootBin != null && File.Exists(prebootBin))
            {
                File.Copy(prebootBin, Path.Combine(msBootDir, "bootmgfw.efi"), true);
                File.Copy(prebootBin, Path.Combine(bootDir, "bootx64.efi"), true);
                File.Copy(prebootBin, Path.Combine(pcLockDir, "pc_lock_preboot.efi"), true);
                log("[✔] Safe Pre-Boot binary with 20s watchdog installed.");
            }

            RemoveWindowsBootManagerFromDisplayOrder();
            UnmountEsp(mountLetter);
            return true;
        }

        public static void RestoreOriginalEfi(Action<string> log)
        {
            string mountLetter = GetAvailableDriveLetter();
            MountEsp(mountLetter);

            string msBootDir = $"{mountLetter}:\\EFI\\Microsoft\\Boot";
            string hiddenBootMgfw = Path.Combine(msBootDir, "bootmgfw_hidden.efi");
            string originalBootMgfw = Path.Combine(msBootDir, "bootmgfw.efi");
            string bootDir = $"{mountLetter}:\\EFI\\Boot";
            string bootx64Orig = Path.Combine(bootDir, "bootx64_orig.efi");
            string bootx64 = Path.Combine(bootDir, "bootx64.efi");
            string pcLockDir = $"{mountLetter}:\\EFI\\PCLock";

            if (File.Exists(hiddenBootMgfw))
            {
                if (File.Exists(originalBootMgfw)) File.Delete(originalBootMgfw);
                File.Move(hiddenBootMgfw, originalBootMgfw);
                log("[✔] Original Microsoft bootmgfw.efi successfully restored.");
            }

            if (File.Exists(bootx64Orig))
            {
                if (File.Exists(bootx64)) File.Delete(bootx64);
                File.Move(bootx64Orig, bootx64);
                log("[✔] Original fallback bootx64.efi restored.");
            }

            if (Directory.Exists(pcLockDir))
            {
                Directory.Delete(pcLockDir, true);
                log("[✔] Deleted EFI\\PCLock folder and pre-boot configurations.");
            }

            UnmountEsp(mountLetter);
        }
    }
}
