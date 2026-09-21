using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeployManager.Services.HardwareAudit.Auditors
{
    public static class BiosAuditor
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFirmwareType(ref uint firmwareType);

        public static BiosInfo Audit()
        {
            var bios = new BiosInfo();
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var biosKey = baseKey.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                if (biosKey != null)
                {
                    bios.Vendor = biosKey.GetValue("BIOSVendor")?.ToString()?.Trim() ?? "Unknown";
                    bios.Version = biosKey.GetValue("BIOSVersion")?.ToString()?.Trim() ?? "Unknown";
                    bios.ReleaseDate = biosKey.GetValue("BIOSReleaseDate")?.ToString()?.Trim() ?? "Unknown";
                    bios.MajorRelease = biosKey.GetValue("BiosMajorRelease")?.ToString()?.Trim() ?? "Unknown";
                    bios.MinorRelease = biosKey.GetValue("BiosMinorRelease")?.ToString()?.Trim() ?? "Unknown";
                }

                // Detect UEFI vs Legacy via kernel32 GetFirmwareType
                uint firmwareType = 0;
                if (GetFirmwareType(ref firmwareType))
                {
                    // 1: Unknown, 2: Bios (Legacy), 3: UEFI
                    switch (firmwareType)
                    {
                        case 2:
                            bios.BootMode = "Legacy BIOS";
                            bios.IsUefi = false;
                            break;
                        case 3:
                            bios.BootMode = "UEFI Native";
                            bios.IsUefi = true;
                            break;
                        default:
                            bios.BootMode = "Unknown / Emulated";
                            bios.IsUefi = false;
                            break;
                    }
                }
                else
                {
                    bios.BootMode = "Undetermined";
                }

                // Detect Secure Boot status from Registry
                using var secureBootKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                if (secureBootKey != null)
                {
                    object? val = secureBootKey.GetValue("UEFISecureBootEnabled");
                    if (val is int intVal)
                    {
                        bios.SecureBootEnabled = (intVal == 1);
                        bios.SecureBootStatus = bios.SecureBootEnabled ? "Active (Enabled)" : "Disabled";
                    }
                    else
                    {
                        bios.SecureBootStatus = "Disabled / Not Present";
                    }
                }
                else
                {
                    bios.SecureBootStatus = "Disabled (Legacy / Unsupported)";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareAudit] BIOS audit exception: {ex.Message}");
            }
            return bios;
        }
    }
}
