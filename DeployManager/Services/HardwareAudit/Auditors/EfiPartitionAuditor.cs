using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace DeployManager.Services.HardwareAudit.Auditors
{
    public static class EfiPartitionAuditor
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindFirstVolume([Out] StringBuilder lpszVolumeName, uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool FindNextVolume(IntPtr hFindVolume, [Out] StringBuilder lpszVolumeName, uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindVolumeClose(IntPtr hFindVolume);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetFirmwareEnvironmentVariableW(string lpName, string lpGuid, IntPtr pBuffer, uint nSize);

        public static EfiPrebootEnvironmentInfo Audit(bool isUefi)
        {
            var efi = new EfiPrebootEnvironmentInfo();
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                
                // Read FirmwareBootDevice and SystemBootDevice from Control
                using var controlKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control");
                if (controlKey != null)
                {
                    efi.FirmwareBootDevice = controlKey.GetValue("FirmwareBootDevice")?.ToString() ?? "Unknown";
                    efi.SystemBootDevice = controlKey.GetValue("SystemBootDevice")?.ToString() ?? "Unknown";
                }

                if (!isUefi)
                {
                    efi.ReadinessSummary = "Legacy BIOS active. Pre-Boot EFI stage is bypassed.";
                    return efi;
                }

                // Enumerate Volume GUIDs to locate ESP (EFI System Partition)
                StringBuilder volumeName = new StringBuilder(260);
                IntPtr handle = FindFirstVolume(volumeName, (uint)volumeName.Capacity);
                if (handle != IntPtr.Zero && handle != (IntPtr)(-1))
                {
                    try
                    {
                        do
                        {
                            string vol = volumeName.ToString();
                            string efiMsBoot = Path.Combine(vol, @"EFI\Microsoft\Boot");
                            try
                            {
                                if (Directory.Exists(efiMsBoot))
                                {
                                    efi.EspVolumeGuid = vol;

                                    string stdBoot = Path.Combine(vol, @"EFI\Microsoft\Boot\bootmgfw.efi");
                                    string hdnBoot = Path.Combine(vol, @"EFI\Microsoft\Boot\bootmgfw_hidden.efi");
                                    string flkBoot = Path.Combine(vol, @"EFI\Boot\bootx64.efi");
                                    string pcLockEfi = Path.Combine(vol, @"EFI\PCLock\pc_lock_preboot.efi");

                                    efi.HasStandardBootloader = File.Exists(stdBoot);
                                    efi.HasHiddenBootloader = File.Exists(hdnBoot);
                                    efi.HasFallbackBootx64 = File.Exists(flkBoot);
                                    efi.HasPrebootEfi = File.Exists(pcLockEfi);
                                    break;
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

                // Check NVRAM variable accessibility
                try
                {
                    IntPtr dummyBuf = Marshal.AllocHGlobal(4);
                    GetFirmwareEnvironmentVariableW("SetupMode", "{8be4df61-93ca-11d2-aa0d-00e098032b8c}", dummyBuf, 4);
                    int err = Marshal.GetLastWin32Error();
                    Marshal.FreeHGlobal(dummyBuf);
                    // 1314: ERROR_PRIVILEGE_NOT_HELD (means NVRAM API exists, just needs admin privileges)
                    // 0: SUCCESS
                    efi.NvramVariablesSupported = (err == 0 || err == 1314 || err == 203);
                }
                catch
                {
                    efi.NvramVariablesSupported = true;
                }

                efi.PrebootReady = efi.EspVolumeGuid != "Undetected" && (efi.HasStandardBootloader || efi.HasHiddenBootloader);
                efi.ReadinessSummary = efi.PrebootReady 
                    ? "ESP Partition & Windows Bootloader Verified for Pre-Boot Chainloading" 
                    : "ESP Partition Access Restricted (Run as Administrator to audit volume)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareAudit] EFI Preboot audit exception: {ex.Message}");
            }
            return efi;
        }
    }
}
