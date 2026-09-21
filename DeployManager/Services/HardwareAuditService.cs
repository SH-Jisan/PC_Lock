using System;
using DeployManager.Services.HardwareAudit.Auditors;

namespace DeployManager.Services
{
    /// <summary>
    /// Master Hardware Audit Service Facade.
    /// Coordinates modular hardware domain auditors for deep pre-flight diagnosis and adaptive profile calculation.
    /// </summary>
    public static class HardwareAuditService
    {
        public static HardwareAuditReport RunAudit()
        {
            var report = new HardwareAuditReport();

            // 1. Audit Motherboard & Enclosure
            report.Motherboard = MotherboardAuditor.Audit();

            // 2. Audit BIOS / Firmware
            report.Bios = BiosAuditor.Audit();

            // 3. Audit User & Operating System
            report.SystemUser = UserSystemAuditor.Audit();

            // 4. Audit Graphics Hardware & Pre-Boot GOP Driver
            report.Graphics = GraphicsAuditor.Audit(report.Bios.IsUefi);

            // 5. Audit Network Hardware, Drivers & Connectivity
            report.Network = NetworkAuditor.Audit();

            // 6. Audit Storage Controllers & NVMe/SATA Drivers
            report.Storage = StorageAuditor.Audit(report.Bios.IsUefi);

            // 7. Audit EFI Pre-Boot Partition & Bootloader Files
            report.EfiPreboot = EfiPartitionAuditor.Audit(report.Bios.IsUefi);

            // 8. Run Comprehensive Pre-Boot Readiness Asset Evaluation
            report.PrebootDiagnostics = AssessmentCalculator.EvaluatePrebootAssets(
                report.Bios, report.Graphics, report.Network, report.Storage, report.EfiPreboot);

            // 9. Overall System Compatibility Assessment
            report.Assessment = AssessmentCalculator.EvaluateCompatibility(
                report.Motherboard, report.Bios, report.SystemUser, report.Network, report.Graphics, report.Storage, report.EfiPreboot);

            // 10. Calculate Hardware-Adaptive Deployment Profile tailored to this specific workstation
            report.AdaptiveProfile = AdaptiveProfileCalculator.Calculate(report);

            return report;
        }
    }
}
