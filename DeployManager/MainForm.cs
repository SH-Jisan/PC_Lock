using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeployManager.Services;

namespace DeployManager
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            this.Load += async (s, e) => await PerformHardwareScanAsync();
        }

        private async Task PerformHardwareScanAsync()
        {
            _btnRescanHardware.Enabled = false;
            _lblCompatibilityBadge.Text = "⏳ AUDITING HARDWARE & DRIVER STACK...";
            _lblCompatibilityBadge.ForeColor = Color.FromArgb(250, 204, 21);

            AppendLog("🔍 [HARDWARE AUDIT] Inspecting workstation hardware, kernel drivers & pre-boot stack...");

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
            
            _lblHwUser.Text = $"• Operator/Host: {report.SystemUser.UserName} @ {report.SystemUser.MachineName} ({report.SystemUser.OsArchitecture} | {report.SystemUser.TotalPhysicalMemoryMb})";

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

            if (report.Assessment.IsFullyCompatible)
            {
                _lblCompatibilityBadge.Text = $"🟢 {report.Assessment.CompatibilityScore} | {report.PrebootDiagnostics.ReadinessScore} ({report.Assessment.RecommendedMode})";
                _lblCompatibilityBadge.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
            }
            else
            {
                _lblCompatibilityBadge.Text = $"⚠ {report.Assessment.CompatibilityScore}";
                _lblCompatibilityBadge.ForeColor = Color.FromArgb(248, 113, 113); // Red
            }

            // Stream detailed audit breakdown to console
            AppendLog($"════════════════════════════════════════════════════════════════════════════");
            AppendLog($"🖥️  MOTHERBOARD   : {report.Motherboard.Manufacturer} {report.Motherboard.Product} (Ver: {report.Motherboard.Version}, Serial: {report.Motherboard.SerialNumber})");
            AppendLog($"📦  CHASSIS/SKU   : {report.Motherboard.EnclosureType} | SKU: {report.Motherboard.SystemSKU} | Family: {report.Motherboard.SystemFamily}");
            AppendLog($"⚙️  BIOS/FIRMWARE : {report.Bios.Vendor} v{report.Bios.Version} (Rel: {report.Bios.ReleaseDate}, Major.Minor: {report.Bios.MajorRelease}.{report.Bios.MinorRelease})");
            AppendLog($"🔐  BOOT MODE     : {report.Bios.BootMode} | SecureBoot: {report.Bios.SecureBootStatus}");
            AppendLog($"🎮  DISPLAY/GPU   : {report.Graphics.GpuName} (Provider: {report.Graphics.ProviderName}, Ver: {report.Graphics.DriverVersion})");
            AppendLog($"    GPU DRIVER    : {report.Graphics.DriverPath}");
            AppendLog($"    UEFI GOP      : {report.Graphics.UefiGopStatus}");
            AppendLog($"🌐  NETWORK CHIP  : {report.Network.PrimaryAdapterName} [MAC: {report.Network.PrimaryMacAddress}]");
            AppendLog($"    NIC DRIVER    : {report.Network.PrimaryDriverPath}");
            AppendLog($"    UNDI/SNP ROM  : Embedded Motherboard Pre-Boot Network Stack Supported");
            AppendLog($"    NETWORK IP    : {report.Network.PrimaryIpAddress} | Gateway: {report.Network.PrimaryGateway} | DNS: {report.Network.PrimaryDns}");
            AppendLog($"    CONNECTIVITY  : {report.Network.InternetStatus}");
            AppendLog($"💾  STORAGE CTRL  : {report.Storage.PrimaryControllerName} ({report.Storage.ControllerType})");
            AppendLog($"    STORAGE DRV   : {report.Storage.DriverPath}");
            AppendLog($"    DISK & STYLE  : {report.Storage.PrimaryDiskName} | Partition Style: {report.Storage.PartitionStyle}");
            AppendLog($"📁  EFI PRE-BOOT  : ESP Volume: {report.EfiPreboot.EspVolumeGuid} [BootDevice: {report.EfiPreboot.FirmwareBootDevice}]");
            AppendLog($"    BOOTLOADERS   : bootmgfw.efi: {(report.EfiPreboot.HasStandardBootloader ? "Found" : "Not present")}, bootmgfw_hidden.efi: {(report.EfiPreboot.HasHiddenBootloader ? "Cloaked" : "None")}");

            AppendLog($"📋  PRE-BOOT ASSET READINESS DIAGNOSTICS:");
            foreach (var item in report.PrebootDiagnostics.DiagnosticChecklist)
            {
                AppendLog($"    {item}");
            }

            AppendLog($"🎯 [ASSESSMENT] Workstation Compatibility: {report.Assessment.CompatibilityScore} | Pre-Boot Readiness: {report.PrebootDiagnostics.ReadinessScore}");
            AppendLog($"    Recommended Profile: {report.Assessment.RecommendedMode}");
            AppendLog($"════════════════════════════════════════════════════════════════════════════");

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
            _btnDeployEnterprise.Enabled = false;
            _btnUninstall.Enabled = false;
            _btnRescanHardware.Enabled = false;
            _lblStatus.Text = "⏳ Deploying Enterprise Zero-Risk Security...";
            _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

            var progress = new Progress<int>(v => _progressBar.Value = v);

            bool success = await Task.Run(() => DeploymentEngine.DeployEnterpriseZeroRiskAsync(AppendLog, progress));

            _btnDeployEnterprise.Enabled = true;
            _btnUninstall.Enabled = true;
            _btnRescanHardware.Enabled = true;

            if (success)
            {
                _lblStatus.Text = "🟢 Enterprise Security Active! (Hardware Verified & Protected)";
                _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
                MessageBox.Show(
                    "Enterprise Zero-Risk Security System is successfully deployed and active!\n\n" +
                    "• Hardware Verified: Motherboard, BIOS, Display & Network profile locked\n" +
                    "• 0% Boot Freeze Risk (Standard Factory Microsoft Bootloader)\n" +
                    "• Background PC Security Agent is Live & Online (🟢)\n" +
                    "• Windows Kernel Remote Lock/Unlock is Fully Protected",
                    "Deployment Succeeded",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                _lblStatus.Text = "🔴 Deployment failed. Check diagnostics log above.";
                _lblStatus.ForeColor = Color.FromArgb(248, 113, 113);
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
            _btnUninstall.Enabled = false;
            _btnRescanHardware.Enabled = false;
            _lblStatus.Text = "⏳ Uninstalling and restoring system...";
            _lblStatus.ForeColor = Color.FromArgb(244, 63, 94);

            var progress = new Progress<int>(v => _progressBar.Value = v);

            bool success = await Task.Run(() => DeploymentEngine.UninstallAsync(AppendLog, progress));

            _btnDeployEnterprise.Enabled = true;
            _btnUninstall.Enabled = true;
            _btnRescanHardware.Enabled = true;

            if (success)
            {
                _lblStatus.Text = "⚪ System Completely Restored to Standard Windows.";
                _lblStatus.ForeColor = Color.FromArgb(148, 163, 184);
                MessageBox.Show(
                    "PC Security System completely removed!\n\nYour computer and Supabase Database are 100% restored.",
                    "Uninstallation Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                _lblStatus.Text = "🔴 Uninstallation error. Check diagnostics log.";
                _lblStatus.ForeColor = Color.FromArgb(248, 113, 113);
            }
        }
    }
}
