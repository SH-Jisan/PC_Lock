using System;
using DeployManager.Services.HardwareAudit.Models;

namespace DeployManager.Services.HardwareAudit.Auditors
{
    public static class AdaptiveProfileCalculator
    {
        public static AdaptiveProfile Calculate(HardwareAuditReport report)
        {
            var profile = new AdaptiveProfile();

            string mobo = $"{report.Motherboard.Manufacturer} {report.Motherboard.Product}".Trim();
            string gpu = report.Graphics.GpuName;
            profile.HardwareSummary = $"{mobo} | GPU: {gpu} | BIOS: {report.Bios.Vendor} ({report.Bios.BootMode})";

            // Vector 1: Check Legacy BIOS vs UEFI
            if (!report.Bios.IsUefi)
            {
                profile.ProfileType = AdaptiveProfileType.LegacySafeShield;
                profile.ProfileName = "Legacy Safe Shield (0% Boot Risk)";
                profile.ShortBadge = "LEGACY SAFE";
                profile.Rationale = "Legacy BIOS / MBR partition detected. Pre-boot EFI modification is bypassed to eliminate 100% of motherboard boot-freeze risk. Enforcing full-strength Windows Kernel & Desktop Cyber Shield.";
                profile.EnablePrebootFirmware = false;
                profile.PreserveFactoryBootloader = true;
                profile.EnableDesktopSessionShield = true;
                profile.EnableWifiPrebootSync = false;

                profile.SetupHighlights.Add("[✓] Standard MBR Boot Sector completely untouched");
                profile.SetupHighlights.Add("[✓] Zero Motherboard or BIOS freeze risk");
                profile.SetupHighlights.Add("[✓] Fullscreen Isolated Desktop Cyber Shield enabled");
                profile.SetupHighlights.Add("[✓] Low-level keyboard shortcut blocking active (WinKey, Alt+Tab, TaskMgr)");
                return profile;
            }

            // Vector 2: Check Secure Boot Enforcement
            if (report.Bios.SecureBootEnabled)
            {
                profile.ProfileType = AdaptiveProfileType.EnterpriseSecureBoot;
                profile.ProfileName = "Enterprise SecureBoot Shield (0% Violation Risk)";
                profile.ShortBadge = "SECUREBOOT SAFE";
                profile.Rationale = "Active UEFI Secure Boot policy detected. To eliminate third-party UEFI signature violation red-screens, standard factory Microsoft bootloader is 100% preserved. Enforcing pre-logon Windows service & isolated desktop shield.";
                profile.EnablePrebootFirmware = false;
                profile.PreserveFactoryBootloader = true;
                profile.EnableDesktopSessionShield = true;
                profile.EnableWifiPrebootSync = false;

                profile.SetupHighlights.Add("[✓] Microsoft Factory bootmgfw.efi 100% preserved");
                profile.SetupHighlights.Add("[✓] Zero Secure Boot violation red-screen risk");
                profile.SetupHighlights.Add("[✓] Pre-logon Windows Security Agent daemon configured");
                profile.SetupHighlights.Add("[✓] Fullscreen Cyber UI with PIN keypad and dynamic QR unlock");
                return profile;
            }

            // Vector 3: Permissive / Unlocked UEFI with GOP Graphics & Ready ESP
            if (report.Graphics.UefiGopSupported && report.EfiPreboot.PrebootReady)
            {
                profile.ProfileType = AdaptiveProfileType.MaxSecDualPlane;
                profile.ProfileName = "MaxSec Dual-Plane Hardware Engine";
                profile.ShortBadge = "MAX-SEC DUAL PLANE";
                profile.Rationale = "Modern UEFI firmware, GPT partition, and UEFI GOP Graphics Output Protocol verified. Motherboard firmware supports pre-boot console and watchdog. Enforcing Dual-Plane: Pre-Boot Firmware Watchdog + Windows Desktop Cyber Shield.";
                profile.EnablePrebootFirmware = true;
                profile.PreserveFactoryBootloader = false;
                profile.EnableDesktopSessionShield = true;

                bool hasWifi = false;
                if (report.Network.AllAdapters != null)
                {
                    foreach (var adapter in report.Network.AllAdapters)
                    {
                        if (adapter.InterfaceType.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                            adapter.Name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                            adapter.Description.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                            adapter.Description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase))
                        {
                            hasWifi = true;
                            break;
                        }
                    }
                }
                if (!hasWifi && (report.Network.PrimaryAdapterName.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) || 
                                 report.Network.PrimaryAdapterName.Contains("Wireless", StringComparison.OrdinalIgnoreCase)))
                {
                    hasWifi = true;
                }

                profile.EnableWifiPrebootSync = hasWifi;

                profile.SetupHighlights.Add("[✓] Dual-Plane Epoch 1: Pre-Boot Firmware Watchdog configured");
                profile.SetupHighlights.Add("[✓] Dual-Plane Epoch 2: Windows Isolated Desktop Shield configured");
                if (hasWifi)
                {
                    profile.SetupHighlights.Add("[✓] Wi-Fi Profile auto-sync to Pre-Boot ESP partition enabled");
                }
                else
                {
                    profile.SetupHighlights.Add("[✓] Motherboard UNDI/SNP Ethernet driver binding enabled");
                }
                profile.SetupHighlights.Add("[✓] Low-level keyboard shortcut blocking active");
                return profile;
            }

            // Fallback: Default to Enterprise Safe
            profile.ProfileType = AdaptiveProfileType.EnterpriseSecureBoot;
            profile.ProfileName = "Enterprise Safe Profile (Hardware Tailored)";
            profile.ShortBadge = "ENTERPRISE SAFE";
            profile.Rationale = "UEFI detected with non-standard GOP display protocols. Standard factory bootloader preserved to ensure reliable visual boot. Windows Desktop Cyber Shield active.";
            profile.EnablePrebootFirmware = false;
            profile.PreserveFactoryBootloader = true;
            profile.EnableDesktopSessionShield = true;
            profile.SetupHighlights.Add("[✓] Standard Microsoft Bootloader preserved");
            profile.SetupHighlights.Add("[✓] Windows Desktop Cyber Shield active");
            return profile;
        }
    }
}
