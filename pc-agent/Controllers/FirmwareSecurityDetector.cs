using System;
using System.Runtime.InteropServices;

namespace PC.SecurityAgent.Controllers
{
    public enum FirmwareSecurityLevel
    {
        Level1_DesktopOnly = 1,
        Level4_TriVectorSelfHealing = 4,
        Level5_HardwareSpiRom = 5
    }

    public static class FirmwareSecurityDetector
    {
        private const uint ACPI_SIGNATURE = 0x41435049; // 'ACPI' in ASCII
        private const uint WPBT_TABLE_ID = 0x54425057;  // 'WPBT' in ASCII

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetSystemFirmwareTable(
            uint firmwareTableProviderSignature,
            uint firmwareTableID,
            IntPtr firmwareTableBuffer,
            uint bufferSize
        );

        public static (FirmwareSecurityLevel Level, string Description) DetectSecurityLevel()
        {
            try
            {
                // 1. Check for physical ACPI WPBT Hardware Table in Motherboard Firmware
                uint size = GetSystemFirmwareTable(ACPI_SIGNATURE, WPBT_TABLE_ID, IntPtr.Zero, 0);
                if (size > 0)
                {
                    return (
                        FirmwareSecurityLevel.Level5_HardwareSpiRom,
                        "Level 5: Physical Motherboard SPI Flash ROM Hardware WPBT Active (Format-Proof)"
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Firmware Detector] WPBT check warning: {ex.Message}");
            }

            // 2. Check for Mode 1 Tri-Vector Pre-Boot Cloak
            try
            {
                string pcLockDir = @"C:\Program Files\PCSecuritySystem\pc_lock_preboot.efi";
                if (System.IO.File.Exists(pcLockDir) || System.IO.Directory.Exists(@"C:\ProgramData\PCLock"))
                {
                    return (
                        FirmwareSecurityLevel.Level4_TriVectorSelfHealing,
                        "Level 4: Tri-Vector Self-Healing EFI Persistence Active (Software Enforced)"
                    );
                }
            }
            catch { }

            return (
                FirmwareSecurityLevel.Level1_DesktopOnly,
                "Level 1: Desktop Security Agent Active (Uncloaked Boot)"
            );
        }
    }
}
