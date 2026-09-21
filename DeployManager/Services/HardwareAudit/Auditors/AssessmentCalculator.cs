using System;
using System.IO;
using System.Linq;

namespace DeployManager.Services.HardwareAudit.Auditors
{
    public static class AssessmentCalculator
    {
        public static PrebootAssetsDiagnostics EvaluatePrebootAssets(
            BiosInfo bios,
            GraphicsHardwareInfo gfx,
            NetworkConnectivityInfo net,
            StorageHardwareInfo storage,
            EfiPrebootEnvironmentInfo efi)
        {
            var diag = new PrebootAssetsDiagnostics();

            // 1. Graphics GOP
            diag.GraphicsGopReady = bios.IsUefi && gfx.UefiGopSupported;
            diag.GraphicsGopDetail = diag.GraphicsGopReady
                ? $"✔ GOP Display Driver Ready: {gfx.GpuName} (Driver: {Path.GetFileName(gfx.DriverPath)})"
                : "⚠ GOP Unavailable in Legacy mode.";
            diag.DiagnosticChecklist.Add(diag.GraphicsGopDetail);

            // 2. Network UNDI / SNP
            var primaryAdapter = net.AllAdapters.FirstOrDefault(a => a.IsPhysical && a.InterfaceType.Contains("Ethernet")) 
                              ?? net.AllAdapters.FirstOrDefault(a => a.IsPhysical);
            
            diag.NetworkUndiReady = primaryAdapter != null && primaryAdapter.PrebootUndiSupported;
            diag.NetworkUndiDetail = diag.NetworkUndiReady
                ? $"✔ Pre-Boot UNDI/SNP Stack Ready: {primaryAdapter?.Description ?? "Ethernet"} (Driver: {Path.GetFileName(primaryAdapter?.DriverPath)})"
                : "ℹ Pre-Boot Network: Wi-Fi/Virtual adapter requires active profile sync.";
            diag.DiagnosticChecklist.Add(diag.NetworkUndiDetail);

            // 3. Storage Block I/O
            diag.StorageBlockIoReady = storage.PrebootStorageSupported;
            diag.StorageBlockIoDetail = diag.StorageBlockIoReady
                ? $"✔ UEFI Storage Block I/O Ready: {storage.PrimaryControllerName} (Driver: {Path.GetFileName(storage.DriverPath)})"
                : "⚠ Storage Controller in Legacy Mode.";
            diag.DiagnosticChecklist.Add(diag.StorageBlockIoDetail);

            // 4. EFI System Partition & Bootloader
            diag.EfiPartitionReady = bios.IsUefi && (efi.HasStandardBootloader || efi.HasHiddenBootloader || efi.EspVolumeGuid != "Undetected");
            diag.EfiPartitionDetail = diag.EfiPartitionReady
                ? $"✔ EFI Bootloader Structure Ready: {(efi.HasHiddenBootloader ? "Cloaked (Pre-Boot Active)" : "Factory Standard")} [ESP: {efi.FirmwareBootDevice}]"
                : "ℹ EFI Partition: Administrator elevation recommended for full direct volume access.";
            diag.DiagnosticChecklist.Add(diag.EfiPartitionDetail);

            diag.AllPrebootAssetsReady = diag.GraphicsGopReady && diag.StorageBlockIoReady && diag.EfiPartitionReady;
            diag.ReadinessScore = diag.AllPrebootAssetsReady ? "100% PRE-BOOT READY" : "ENTERPRISE HYBRID READY";

            return diag;
        }

        public static CompatibilityAssessment EvaluateCompatibility(
            MotherboardInfo mb,
            BiosInfo bios,
            SystemUserInfo sys,
            NetworkConnectivityInfo net,
            GraphicsHardwareInfo gfx,
            StorageHardwareInfo storage,
            EfiPrebootEnvironmentInfo efi)
        {
            var eval = new CompatibilityAssessment();

            // 1. Architecture Check (Must be x64 / 64-bit)
            eval.Is64BitCompatible = Environment.Is64BitOperatingSystem;
            if (eval.Is64BitCompatible)
            {
                eval.CompatibilityNotes.Add("✔ OS Architecture: 64-bit (x64) verified.");
            }
            else
            {
                eval.CompatibilityNotes.Add("❌ OS Architecture: 32-bit detected. 64-bit is required.");
            }

            // 2. Boot Mode Evaluation
            eval.IsBootModeCompatible = true;
            if (bios.IsUefi)
            {
                if (bios.SecureBootEnabled)
                {
                    eval.CompatibilityNotes.Add("✔ Firmware: UEFI Native with Secure Boot Active.");
                    eval.CompatibilityNotes.Add("ℹ Recommendation: Enterprise Zero-Risk Deployment (Zero Boot Risk) is optimal.");
                    eval.RecommendedMode = "Enterprise Zero-Risk (Optimal for Secure Boot)";
                }
                else
                {
                    eval.CompatibilityNotes.Add("✔ Firmware: UEFI Native (Secure Boot Disabled / Custom).");
                    eval.CompatibilityNotes.Add("✔ Both Enterprise Zero-Risk and UEFI Pre-boot modes are fully supported.");
                    eval.RecommendedMode = "Enterprise Zero-Risk (Recommended)";
                }
            }
            else
            {
                eval.CompatibilityNotes.Add("⚠ Firmware: Legacy BIOS mode detected.");
                eval.CompatibilityNotes.Add("ℹ Recommendation: Deploy Enterprise Zero-Risk (Windows Kernel/Service Mode). UEFI Pre-boot is bypassed.");
                eval.RecommendedMode = "Enterprise Zero-Risk (Legacy BIOS Compatible)";
            }

            // 3. Network Compatibility
            eval.IsNetworkCompatible = net.HasActiveNetwork;
            if (eval.IsNetworkCompatible)
            {
                eval.CompatibilityNotes.Add($"✔ Network Hardware: Active adapter ({net.PrimaryMacAddress}) with IP {net.PrimaryIpAddress}.");
                if (net.InternetReachable)
                {
                    eval.CompatibilityNotes.Add("✔ Cloud Connectivity: Online & reachable for Mobile App Remote Control.");
                }
                else
                {
                    eval.CompatibilityNotes.Add("⚠ Cloud Connectivity: Local Network / Offline (Local PINs remain functional).");
                }
            }
            else
            {
                eval.CompatibilityNotes.Add("⚠ Network: No active network adapter with IPv4 detected. Connect LAN/Wi-Fi for remote unlock.");
            }

            // 4. Driver & Hardware Notes
            if (!string.IsNullOrEmpty(gfx.DriverPath) && gfx.DriverPath != "Unknown")
            {
                eval.CompatibilityNotes.Add($"✔ Display Driver Located: {Path.GetFileName(gfx.DriverPath)} ({gfx.GpuName})");
            }

            if (!string.IsNullOrEmpty(storage.DriverPath) && storage.DriverPath != "Unknown")
            {
                eval.CompatibilityNotes.Add($"✔ Storage Driver Located: {Path.GetFileName(storage.DriverPath)} ({storage.ControllerType})");
            }

            // 5. TPM 2.0 Presence
            eval.IsTpmPresent = AuditHelper.CheckTpmPresence();
            if (eval.IsTpmPresent)
            {
                eval.CompatibilityNotes.Add("✔ Hardware Security: TPM 2.0 Hardware Cryptographic Vault detected.");
            }
            else
            {
                eval.CompatibilityNotes.Add("ℹ Hardware Security: Standard Windows DPAPI Vault (TPM 2.0 not enabled).");
            }

            // Overall Score
            eval.IsFullyCompatible = eval.Is64BitCompatible;
            eval.CompatibilityScore = eval.IsFullyCompatible ? "100% COMPATIBLE" : "INCOMPATIBLE";

            return eval;
        }
    }
}
