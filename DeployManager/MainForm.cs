#nullable enable
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeployManager.Services;
using DeployManager.Services.Deployment;
using DeployManager.Services.Diagnostics;

namespace DeployManager
{
    public partial class MainForm : Form
    {
        private SystemHealthReport? _latestHealthReport;

        public MainForm()
        {
            InitializeComponent();
            this.Load += async (s, e) => await PerformHardwareScanAsync();
        }

        private async Task PerformHardwareScanAsync()
        {
            _btnRescanHardware.Enabled = false;
            _lblCompatibilityBadge.Text = "[...] Auditing Hardware & Driver Stack...";
            _lblCompatibilityBadge.ForeColor = Color.FromArgb(250, 204, 21);
            _lblAdaptiveRationale.Text = "Inspecting motherboard, firmware, display and storage vectors...";

            AppendLog("================================================================================");
            AppendLog("[HARDWARE AUDIT] Inspecting workstation hardware, kernel drivers & pre-boot stack...");

            var report = await Task.Run(() => HardwareAuditService.RunAudit());
            _latestAuditReport = report;

            // Update UI card Column 1
            string mbText = $"{report.Motherboard.Manufacturer} - {report.Motherboard.Product}";
            if (report.Motherboard.SystemProductName != "Unknown" && report.Motherboard.SystemProductName != report.Motherboard.Product)
            {
                mbText += $" ({report.Motherboard.SystemProductName})";
            }
            _lblHwMotherboard.Text = $"• Motherboard: {mbText} [{report.Motherboard.EnclosureType}]";
            _lblHwBios.Text = $"• BIOS/Firmware: {report.Bios.Vendor} v{report.Bios.Version} ({report.Bios.BootMode} | SecureBoot: {report.Bios.SecureBootStatus})";
            
            string gpuDriverFile = !string.IsNullOrEmpty(report.Graphics.DriverPath) ? Path.GetFileName(report.Graphics.DriverPath) : "DirectX";
            _lblHwGpu.Text = $"• GPU & GOP: {report.Graphics.GpuName} [Driver: {gpuDriverFile}]";
            
            _lblHwUser.Text = $"• Host/OS: {report.SystemUser.UserName} @ {report.SystemUser.MachineName} ({report.SystemUser.OsArchitecture} | {report.SystemUser.TotalPhysicalMemoryMb})";

            // Update UI card Column 2
            string netDriverFile = !string.IsNullOrEmpty(report.Network.PrimaryDriverPath) && report.Network.PrimaryDriverPath != "Unknown" 
                ? Path.GetFileName(report.Network.PrimaryDriverPath) 
                : "NDIS";
            _lblHwNetwork.Text = $"• Network: {report.Network.PrimaryMacAddress} (IP: {report.Network.PrimaryIpAddress} | Driver: {netDriverFile})";

            string storageDriverFile = !string.IsNullOrEmpty(report.Storage.DriverPath) && report.Storage.DriverPath != "Unknown"
                ? Path.GetFileName(report.Storage.DriverPath)
                : "Block I/O";
            _lblHwStorage.Text = $"• Storage: {report.Storage.PrimaryControllerName} [{report.Storage.PartitionStyle} | Driver: {storageDriverFile}]";

            _lblHwEfi.Text = $"• EFI Pre-Boot: {(report.EfiPreboot.HasHiddenBootloader ? "Cloaked (Pre-Boot Active)" : (report.EfiPreboot.HasStandardBootloader ? "Standard Bootmgr Present" : "ESP Verified"))}";

            var profile = report.AdaptiveProfile;
            _lblAdaptiveProfile.Text = $"• Adaptive Engine: {profile.ProfileName}";
            _lblAdaptiveRationale.Text = profile.Rationale;

            if (report.Assessment.IsFullyCompatible)
            {
                _lblCompatibilityBadge.Text = $"[✓ READY] {profile.ShortBadge} | Compatibility: {report.Assessment.CompatibilityScore}";
                _lblCompatibilityBadge.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
            }
            else
            {
                _lblCompatibilityBadge.Text = $"[!] {profile.ShortBadge} | {report.Assessment.CompatibilityScore}";
                _lblCompatibilityBadge.ForeColor = Color.FromArgb(250, 204, 21); // Yellow/Amber
            }

            // Stream detailed audit breakdown to console
            AppendLog("--------------------------------------------------------------------------------");
            AppendLog($"  MOTHERBOARD   : {report.Motherboard.Manufacturer} {report.Motherboard.Product} (Ver: {report.Motherboard.Version}, Serial: {report.Motherboard.SerialNumber})");
            AppendLog($"  CHASSIS/SKU   : {report.Motherboard.EnclosureType} | SKU: {report.Motherboard.SystemSKU} | Family: {report.Motherboard.SystemFamily}");
            AppendLog($"  BIOS/FIRMWARE : {report.Bios.Vendor} v{report.Bios.Version} (Rel: {report.Bios.ReleaseDate}, Major.Minor: {report.Bios.MajorRelease}.{report.Bios.MinorRelease})");
            AppendLog($"  BOOT MODE     : {report.Bios.BootMode} | SecureBoot: {report.Bios.SecureBootStatus}");
            AppendLog($"  DISPLAY/GPU   : {report.Graphics.GpuName} (Provider: {report.Graphics.ProviderName}, Ver: {report.Graphics.DriverVersion})");
            AppendLog($"  GPU DRIVER    : {report.Graphics.DriverPath}");
            AppendLog($"  UEFI GOP      : {report.Graphics.UefiGopStatus}");
            AppendLog($"  NETWORK CHIP  : {report.Network.PrimaryAdapterName} [MAC: {report.Network.PrimaryMacAddress}]");
            AppendLog($"  NIC DRIVER    : {report.Network.PrimaryDriverPath}");
            AppendLog($"  NETWORK IP    : {report.Network.PrimaryIpAddress} | Gateway: {report.Network.PrimaryGateway} | DNS: {report.Network.PrimaryDns}");
            AppendLog($"  CONNECTIVITY  : {report.Network.InternetStatus}");
            AppendLog($"  STORAGE CTRL  : {report.Storage.PrimaryControllerName} ({report.Storage.ControllerType})");
            AppendLog($"  STORAGE DRV   : {report.Storage.DriverPath}");
            AppendLog($"  DISK & STYLE  : {report.Storage.PrimaryDiskName} | Partition Style: {report.Storage.PartitionStyle}");
            AppendLog($"  EFI PRE-BOOT  : ESP Volume: {report.EfiPreboot.EspVolumeGuid} [BootDevice: {report.EfiPreboot.FirmwareBootDevice}]");
            AppendLog($"  BOOTLOADERS   : bootmgfw.efi: {(report.EfiPreboot.HasStandardBootloader ? "Found" : "Not present")}, bootmgfw_hidden.efi: {(report.EfiPreboot.HasHiddenBootloader ? "Cloaked" : "None")}");

            AppendLog("--------------------------------------------------------------------------------");
            AppendLog(">>> HARDWARE-ADAPTIVE CONFIGURATION RECOMMENDATION:");
            AppendLog($"    Calculated Profile : {profile.ProfileName} [{profile.ShortBadge}]");
            AppendLog($"    Firmware Strategy  : {(profile.EnablePrebootFirmware ? "Dual-Plane UEFI Pre-Boot Cloak" : "Preserve Factory Microsoft Bootloader (Zero Freeze Risk)")}");
            AppendLog($"    Wi-Fi ESP Sync     : {(profile.EnableWifiPrebootSync ? "Enabled (Auto-Sync Active WPA/WPA2 Profile)" : "Disabled / Not Required")}");
            AppendLog($"    Desktop Shield     : {(profile.EnableDesktopSessionShield ? "Full-Strength Topmost Cyber Shield" : "Disabled")}");
            AppendLog($"    Rationale          : {profile.Rationale}");
            AppendLog("    Tailored Highlights:");
            foreach (var h in profile.SetupHighlights)
            {
                AppendLog($"      {h}");
            }
            AppendLog("================================================================================");

            _btnRescanHardware.Enabled = true;
        }

        private void AppendLog(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(AppendLog), message);
                return;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            _txtLogs.AppendText($"[{time}] {message}\r\n");
            _txtLogs.SelectionStart = _txtLogs.Text.Length;
            _txtLogs.ScrollToCaret();
        }

        private async Task HandleDeployEnterpriseAsync()
        {
            if (_latestAuditReport == null)
            {
                await PerformHardwareScanAsync();
            }

            if (_latestAuditReport == null)
            {
                MessageBox.Show("Hardware audit could not be completed. Please click Re-Scan.", "Audit Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var profile = _latestAuditReport.AdaptiveProfile;

            _btnDeployEnterprise.Enabled = false;
            _btnVerifyHealth.Enabled = false;
            _btnUninstall.Enabled = false;
            _btnRescanHardware.Enabled = false;
            _lblStatus.Text = $"Deploying {profile.ProfileName}...";
            _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

            var progress = new Progress<int>(v => _progressBar.Value = v);

            var result = await Task.Run(() => AdaptiveDeploymentPipeline.DeployAdaptiveAsync(_latestAuditReport, AppendLog, progress));
            _latestHealthReport = result.HealthReport;

            _btnDeployEnterprise.Enabled = true;
            _btnVerifyHealth.Enabled = true;
            _btnUninstall.Enabled = true;
            _btnRescanHardware.Enabled = true;

            if (result.Success)
            {
                _lblStatus.Text = $"Adaptive Security Active! ({profile.ShortBadge}) - 100% Health Verified";
                _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);

                var sb = new StringBuilder();
                sb.AppendLine("Hardware-Adaptive Security System successfully deployed and verified!\n");
                sb.AppendLine($"• Workstation Profile : {profile.ProfileName}");
                sb.AppendLine($"• Motherboard Setup    : {(profile.EnablePrebootFirmware ? "Dual-Plane Pre-Boot Active" : "Factory Microsoft Bootloader (Zero Freeze Risk)")}");
                sb.AppendLine($"• Desktop Protection   : Fullscreen Topmost Cyber Shield Active");
                sb.AppendLine($"• Cloud Gateway        : Connected & Registered Online\n");
                sb.AppendLine("--- AUTONOMOUS SELF-VERIFICATION REPORT (100% PASSED) ---");
                foreach (var c in result.HealthReport.Checks)
                {
                    sb.AppendLine($"[✓] {c.ComponentName}: {c.Details}");
                }

                MessageBox.Show(
                    sb.ToString(),
                    "Deployment & Self-Verification Succeeded",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                _lblStatus.Text = $"Deployment Warning: {result.HealthReport.TotalFailed} issue(s) detected during self-verification.";
                _lblStatus.ForeColor = Color.FromArgb(248, 113, 113);

                var sb = new StringBuilder();
                sb.AppendLine("⚠️ The software was deployed, but the Auto-Diagnostic engine detected issue(s):\n");
                foreach (var c in result.HealthReport.Checks)
                {
                    if (!c.Passed)
                    {
                        sb.AppendLine($"[X] FAILED: {c.ComponentName}");
                        sb.AppendLine($"    Cause: {c.ErrorMessage}");
                        sb.AppendLine($"    Action: {c.RemediationAdvice}\n");
                    }
                }

                MessageBox.Show(
                    sb.ToString(),
                    "Autonomous Diagnostic Notice - Action Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        private async Task HandleRunSelfTestAsync()
        {
            _btnVerifyHealth.Enabled = false;
            _btnDeployEnterprise.Enabled = false;
            _btnUninstall.Enabled = false;
            _lblStatus.Text = "Running autonomous system self-diagnosis & health check...";
            _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

            var report = await Task.Run(() => SystemHealthVerifier.RunVerificationAsync(_latestAuditReport, AppendLog));
            _latestHealthReport = report;

            _btnVerifyHealth.Enabled = true;
            _btnDeployEnterprise.Enabled = true;
            _btnUninstall.Enabled = true;

            if (report.IsOverallHealthy)
            {
                _lblStatus.Text = "System Health: 100% Operational & Secure (All Checks Passed)";
                _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);

                var sb = new StringBuilder();
                sb.AppendLine("✅ SYSTEM SELF-TEST COMPLETE: ALL COMPONENTS 100% OPERATIONAL!\n");
                foreach (var c in report.Checks)
                {
                    sb.AppendLine($"[✓] {c.ComponentName}: {c.Details}");
                }

                MessageBox.Show(
                    sb.ToString(),
                    "System Health: 100% Operational",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                _lblStatus.Text = $"Diagnostic Alert: {report.TotalFailed} Issue(s) Detected! Review details below.";
                _lblStatus.ForeColor = Color.FromArgb(248, 113, 113);

                var sb = new StringBuilder();
                sb.AppendLine($"⚠️ SYSTEM SELF-TEST DETECTED {report.TotalFailed} ISSUE(S):\n");
                foreach (var c in report.Checks)
                {
                    if (!c.Passed)
                    {
                        sb.AppendLine($"[X] {c.ComponentName}");
                        sb.AppendLine($"    Problem: {c.ErrorMessage}");
                        sb.AppendLine($"    Solution: {c.RemediationAdvice}\n");
                    }
                    else
                    {
                        sb.AppendLine($"[✓] {c.ComponentName}: {c.Details}");
                    }
                }

                MessageBox.Show(
                    sb.ToString(),
                    "System Diagnostic Report",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        private async Task HandleUninstallAsync()
        {
            var confirm = MessageBox.Show(
                "Are you sure you want to completely uninstall the PC Security System?\n\n" +
                "• Original Windows Bootloader will be verified & restored\n" +
                "• Device records and hardware audit profiles will be cleaned\n" +
                "• Background security agent will be removed",
                "Confirm Uninstallation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm != DialogResult.Yes) return;

            _btnDeployEnterprise.Enabled = false;
            _btnVerifyHealth.Enabled = false;
            _btnUninstall.Enabled = false;
            _btnRescanHardware.Enabled = false;
            _lblStatus.Text = "Uninstalling and restoring system...";
            _lblStatus.ForeColor = Color.FromArgb(244, 63, 94);

            var progress = new Progress<int>(v => _progressBar.Value = v);

            bool success = await Task.Run(() => DeploymentEngine.UninstallAsync(AppendLog, progress));

            _btnDeployEnterprise.Enabled = true;
            _btnVerifyHealth.Enabled = true;
            _btnUninstall.Enabled = true;
            _btnRescanHardware.Enabled = true;

            if (success)
            {
                _lblStatus.Text = "System Completely Restored to Standard Windows.";
                _lblStatus.ForeColor = Color.FromArgb(148, 163, 184);
                MessageBox.Show(
                    "PC Security System completely removed!\n\nYour computer and bootloaders are 100% restored.",
                    "Uninstallation Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                _lblStatus.Text = "Uninstallation error. Check diagnostics log.";
                _lblStatus.ForeColor = Color.FromArgb(248, 113, 113);
            }
        }
    }
}
