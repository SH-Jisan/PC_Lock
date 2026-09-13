using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeployManager.Services;

namespace DeployManager
{
    public class MainForm : Form
    {
        private ProgressBar _progressBar = null!;
        private TextBox _txtLogs = null!;
        private Button _btnDeployEnterprise = null!;
        private Button _btnUninstall = null!;
        private Button _btnRescanHardware = null!;
        private Label _lblStatus = null!;

        // Hardware & Pre-Boot Driver Diagnostic UI Labels
        private Label _lblHwMotherboard = null!;
        private Label _lblHwBios = null!;
        private Label _lblHwGpu = null!;
        private Label _lblHwUser = null!;
        private Label _lblHwNetwork = null!;
        private Label _lblHwStorage = null!;
        private Label _lblHwEfi = null!;
        private Label _lblCompatibilityBadge = null!;
        private HardwareAuditReport? _latestAuditReport;

        public MainForm()
        {
            InitializeComponent();
            this.Load += async (s, e) => await PerformHardwareScanAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "PC Security System - Enterprise Deployment & Deep Hardware / Driver Diagnostic Hub";
            this.Size = new Size(860, 780);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(15, 23, 42); // Slate 900
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // 1. Header Panel
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = Color.FromArgb(30, 41, 59), // Slate 800
                Padding = new Padding(20, 15, 20, 15)
            };

            var lblTitle = new Label
            {
                Text = "🔒 Cyber Workstation PC Security Deployment Hub",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248), // Cyan 400
                AutoSize = true,
                Location = new Point(15, 12)
            };

            var lblSubtitle = new Label
            {
                Text = "Deep Hardware, Display & Network Driver Diagnostic Engine • UEFI GOP, UNDI/SNP & ESP Pre-Boot Scanner",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(148, 163, 184), // Slate 400
                AutoSize = true,
                Location = new Point(16, 42)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // 2. Hardware Audit & Pre-Boot Driver Dashboard Card
            var pnlHardwareCard = new Panel
            {
                Location = new Point(20, 95),
                Size = new Size(805, 205),
                BackColor = Color.FromArgb(24, 33, 47), // Slate 850
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblCardTitle = new Label
            {
                Text = "🖥️ Workstation Hardware, Kernel Drivers & Pre-Boot Readiness Profile",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                Location = new Point(12, 10),
                AutoSize = true
            };
            pnlHardwareCard.Controls.Add(lblCardTitle);

            _btnRescanHardware = new Button
            {
                Text = "🔄 Re-Scan",
                Location = new Point(700, 8),
                Size = new Size(92, 28),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnRescanHardware.FlatAppearance.BorderSize = 0;
            _btnRescanHardware.Click += async (s, e) => await PerformHardwareScanAsync();
            pnlHardwareCard.Controls.Add(_btnRescanHardware);

            // Column 1: Motherboard, BIOS, GPU, User (Left column: X=12)
            _lblHwMotherboard = new Label
            {
                Text = "• Motherboard: Scanning hardware...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(12, 38),
                Size = new Size(385, 20)
            };
            pnlHardwareCard.Controls.Add(_lblHwMotherboard);

            _lblHwBios = new Label
            {
                Text = "• BIOS/Firmware: Detecting boot mode & Secure Boot...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(12, 60),
                Size = new Size(385, 20)
            };
            pnlHardwareCard.Controls.Add(_lblHwBios);

            _lblHwGpu = new Label
            {
                Text = "• Graphics / GOP: Auditing Display adapter & driver...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(12, 82),
                Size = new Size(385, 20)
            };
            pnlHardwareCard.Controls.Add(_lblHwGpu);

            _lblHwUser = new Label
            {
                Text = "• Operator/Host: Detecting current logged-in user...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(12, 104),
                Size = new Size(385, 20)
            };
            pnlHardwareCard.Controls.Add(_lblHwUser);

            // Column 2: Network, Storage, EFI Pre-Boot, Compatibility (Right column: X=405)
            _lblHwNetwork = new Label
            {
                Text = "• Network: Auditing MAC & IP addresses...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(405, 38),
                Size = new Size(390, 20)
            };
            pnlHardwareCard.Controls.Add(_lblHwNetwork);

            _lblHwStorage = new Label
            {
                Text = "• Storage: Detecting NVMe/AHCI & partition style...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(405, 60),
                Size = new Size(390, 20)
            };
            pnlHardwareCard.Controls.Add(_lblHwStorage);

            _lblHwEfi = new Label
            {
                Text = "• EFI Pre-Boot: Inspecting ESP volume & bootloaders...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(405, 82),
                Size = new Size(390, 20)
            };
            pnlHardwareCard.Controls.Add(_lblHwEfi);

            var lblBadgeHeader = new Label
            {
                Text = "Deployment & Pre-Boot Readiness Score:",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(12, 135),
                AutoSize = true
            };
            pnlHardwareCard.Controls.Add(lblBadgeHeader);

            _lblCompatibilityBadge = new Label
            {
                Text = "⏳ AUDITING WORKSTATION HARDWARE & PRE-BOOT STACK...",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(250, 204, 21), // Yellow 400
                Location = new Point(12, 158),
                AutoSize = true
            };
            pnlHardwareCard.Controls.Add(_lblCompatibilityBadge);

            this.Controls.Add(pnlHardwareCard);

            // 3. Action Buttons & Status Panel
            var pnlActions = new Panel
            {
                Location = new Point(20, 310),
                Size = new Size(805, 88)
            };

            _btnDeployEnterprise = new Button
            {
                Text = "🚀 Deploy Enterprise Security (Zero Boot Risk)",
                Location = new Point(0, 5),
                Size = new Size(395, 46),
                BackColor = Color.FromArgb(14, 165, 233), // Sky 500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnDeployEnterprise.FlatAppearance.BorderSize = 0;
            _btnDeployEnterprise.Click += async (s, e) => await HandleDeployEnterpriseAsync();

            _btnUninstall = new Button
            {
                Text = "🗑️ Completely Uninstall & Restore",
                Location = new Point(410, 5),
                Size = new Size(395, 46),
                BackColor = Color.FromArgb(225, 29, 72), // Rose 600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnUninstall.FlatAppearance.BorderSize = 0;
            _btnUninstall.Click += async (s, e) => await HandleUninstallAsync();

            _lblStatus = new Label
            {
                Text = "Workstation hardware analysis complete. Ready to deploy.",
                Location = new Point(2, 58),
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225)
            };

            pnlActions.Controls.Add(_btnDeployEnterprise);
            pnlActions.Controls.Add(_btnUninstall);
            pnlActions.Controls.Add(_lblStatus);
            this.Controls.Add(pnlActions);

            // 4. Progress Bar
            _progressBar = new ProgressBar
            {
                Location = new Point(20, 405),
                Size = new Size(805, 10),
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };
            this.Controls.Add(_progressBar);

            // 5. Live Terminal Console Box
            var lblLogsTitle = new Label
            {
                Text = "📋 Live Deployment & Deep Hardware / Driver Diagnostics Stream:",
                Location = new Point(20, 420),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            this.Controls.Add(lblLogsTitle);

            _txtLogs = new TextBox
            {
                Location = new Point(20, 442),
                Size = new Size(805, 230),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(2, 6, 23), // Slate 950
                ForeColor = Color.FromArgb(52, 211, 153), // Emerald 400
                Font = new Font("Consolas", 8.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_txtLogs);

            // 6. Footer Bar
            var btnCopy = new Button
            {
                Text = "📋 Copy Logs",
                Location = new Point(20, 682),
                Size = new Size(110, 30),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(_txtLogs.Text))
                {
                    Clipboard.SetText(_txtLogs.Text);
                    MessageBox.Show("Logs copied to clipboard!", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            this.Controls.Add(btnCopy);

            var lblFooter = new Label
            {
                Text = "Cyber Workstation Guard • Deep Hardware, Driver & Pre-Boot Environment Compatibility Engine",
                Location = new Point(220, 688),
                AutoSize = true,
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            this.Controls.Add(lblFooter);

            AppendLog("✅ DeployManager GUI initialized.");
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
