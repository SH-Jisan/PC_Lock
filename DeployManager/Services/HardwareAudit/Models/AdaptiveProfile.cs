using System;
using System.Collections.Generic;

namespace DeployManager.Services.HardwareAudit.Models
{
    public enum AdaptiveProfileType
    {
        MaxSecDualPlane,        // UEFI + GPT + GOP Graphics + Permissive Boot
        EnterpriseSecureBoot,   // UEFI + SecureBoot Active (Keeps Factory Microsoft Bootloader)
        LegacySafeShield        // Legacy BIOS / MBR / Missing GOP (0% Boot Risk, Kernel Shield)
    }

    public class AdaptiveProfile
    {
        public AdaptiveProfileType ProfileType { get; set; } = AdaptiveProfileType.EnterpriseSecureBoot;
        public string ProfileName { get; set; } = "Enterprise Zero-Risk Profile";
        public string ShortBadge { get; set; } = "ENTERPRISE SAFE";
        public string Rationale { get; set; } = "Adaptive auto-configuration pending hardware audit.";
        
        // Dynamic Execution Flags tailored to this machine
        public bool EnablePrebootFirmware { get; set; } = false;
        public bool EnableWifiPrebootSync { get; set; } = false;
        public bool EnableDesktopSessionShield { get; set; } = true;
        public bool EnableKernelRunKey { get; set; } = true;
        public bool PreserveFactoryBootloader { get; set; } = true;

        public string HardwareSummary { get; set; } = "Generic Workstation";
        public List<string> SetupHighlights { get; set; } = new();
    }
}
