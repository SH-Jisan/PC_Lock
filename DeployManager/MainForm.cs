using System;
using System.Drawing;
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

        // Hardware Profile UI Controls
        private Label _lblHwMotherboard = null!;
        private Label _lblHwBios = null!;
        private Label _lblHwUser = null!;
        private Label _lblHwNetwork = null!;
        private Label _lblCompatibilityBadge = null!;
        private HardwareAuditReport? _latestAuditReport;

        public MainForm()
        {
            InitializeComponent();
            this.Load += async (s, e) => await PerformHardwareScanAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "PC Security & Remote Lock Controller - Deployment & Hardware Diagnostic Hub";
            this.Size = new Size(820, 680);
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
                Text = "Intelligent Pre-Flight Hardware Profiling • Motherboard, BIOS & Network Compatibility Engine",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(148, 163, 184), // Slate 400
                AutoSize = true,
                Location = new Point(16, 42)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // 2. Hardware Audit & Compatibility Dashboard Card
            var pnlHardwareCard = new Panel
            {
                Location = new Point(20, 95),
                Size = new Size(765, 140),
                BackColor = Color.FromArgb(24, 33, 47), // Slate 850
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblCardTitle = new Label
            {
                Text = "🖥️ Workstation Hardware & Firmware Environment Profile",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                Location = new Point(12, 10),
                AutoSize = true
            };
            pnlHardwareCard.Controls.Add(lblCardTitle);

            _btnRescanHardware = new Button
            {
                Text = "🔄 Re-Scan",
                Location = new Point(660, 8),
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

            // Column 1: Motherboard & BIOS
            _lblHwMotherboard = new Label
            {
                Text = "• Motherboard: Scanning hardware...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(12, 38),
                Size = new Size(390, 20)
            };
            pnlHardwareCard.Controls.Add(_lblHwMotherboard);

            _lblHwBios = new Label
            {
                Text = "• BIOS/Firmware: Detecting boot mode & Secure Boot...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(12, 62),
                Size = new Size(390, 20)
            };
            pnlHardwareCard.Controls.Add(_lblHwBios);

            _lblHwUser = new Label
            {
                Text = "• Operator/Host: Detecting current logged-in user...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(12, 86),
                Size = new Size(390, 20)
            };
            pnlHardwareCard.Controls.Add(_lblHwUser);

            // Column 2: Network & Compatibility
            _lblHwNetwork = new Label
            {
                Text = "• Network: Auditing MAC & IP addresses...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(410, 38),
                Size = new Size(345, 42)
            };
            pnlHardwareCard.Controls.Add(_lblHwNetwork);

            var lblBadgeHeader = new Label
            {
                Text = "Deployment Assessment:",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(410, 84),
                AutoSize = true
            };
            pnlHardwareCard.Controls.Add(lblBadgeHeader);

            _lblCompatibilityBadge = new Label
            {
                Text = "⏳ AUDITING COMPATIBILITY...",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(250, 204, 21), // Yellow 400
                Location = new Point(410, 104),
                AutoSize = true
            };
            pnlHardwareCard.Controls.Add(_lblCompatibilityBadge);

            this.Controls.Add(pnlHardwareCard);

            // 3. Action Buttons & Status Panel
            var pnlActions = new Panel
            {
                Location = new Point(20, 245),
                Size = new Size(765, 88)
            };

            _btnDeployEnterprise = new Button
            {
                Text = "🚀 Deploy Enterprise Security (Zero Boot Risk)",
                Location = new Point(0, 5),
                Size = new Size(375, 46),
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
                Location = new Point(390, 5),
                Size = new Size(375, 46),
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
                Location = new Point(20, 340),
                Size = new Size(765, 10),
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };
            this.Controls.Add(_progressBar);

            // 5. Live Terminal Console Box
            var lblLogsTitle = new Label
            {
                Text = "📋 Live Deployment & Diagnostics Stream:",
                Location = new Point(20, 356),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            this.Controls.Add(lblLogsTitle);

            _txtLogs = new TextBox
            {
                Location = new Point(20, 378),
                Size = new Size(765, 210),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(2, 6, 23), // Slate 950
                ForeColor = Color.FromArgb(52, 211, 153), // Emerald 400
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_txtLogs);

            // 6. Footer Bar
            var btnCopy = new Button
            {
                Text = "📋 Copy Logs",
                Location = new Point(20, 598),
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
                Text = "Cyber Workstation Guard • Hardware-Compatible Enterprise Zero-Risk Security Engine",
                Location = new Point(240, 605),
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
            _lblCompatibilityBadge.Text = "⏳ AUDITING HARDWARE...";
            _lblCompatibilityBadge.ForeColor = Color.FromArgb(250, 204, 21);

            AppendLog("🔍 [HARDWARE AUDIT] Inspecting workstation hardware profile...");

            var report = await Task.Run(() => HardwareAuditService.RunAudit());
            _latestAuditReport = report;

            // Update UI card
            string mbText = $"{report.Motherboard.Manufacturer} - {report.Motherboard.Product}";
            if (report.Motherboard.SystemProductName != "Unknown" && report.Motherboard.SystemProductName != report.Motherboard.Product)
            {
                mbText += $" ({report.Motherboard.SystemProductName})";
            }
            _lblHwMotherboard.Text = $"• Motherboard: {mbText}";

            _lblHwBios.Text = $"• BIOS/Firmware: {report.Bios.Vendor} v{report.Bios.Version} ({report.Bios.BootMode} | SecureBoot: {report.Bios.SecureBootStatus})";
            _lblHwUser.Text = $"• Operator/Host: {report.SystemUser.UserName} @ {report.SystemUser.MachineName} ({report.SystemUser.OsArchitecture} | {report.SystemUser.TotalPhysicalMemoryMb})";

            _lblHwNetwork.Text = $"• Network: {report.Network.PrimaryMacAddress} (IP: {report.Network.PrimaryIpAddress})\n  Internet Status: {report.Network.InternetStatus}";

            if (report.Assessment.IsFullyCompatible)
            {
                _lblCompatibilityBadge.Text = $"🟢 {report.Assessment.CompatibilityScore} ({report.Assessment.RecommendedMode})";
                _lblCompatibilityBadge.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
            }
            else
            {
                _lblCompatibilityBadge.Text = $"⚠ {report.Assessment.CompatibilityScore}";
                _lblCompatibilityBadge.ForeColor = Color.FromArgb(248, 113, 113); // Red
            }

            // Stream detailed audit breakdown to console
            AppendLog($"🖥️  Motherboard : {report.Motherboard.Manufacturer} {report.Motherboard.Product} [Ver: {report.Motherboard.Version}]");
            AppendLog($"⚙️  BIOS/Firmware: {report.Bios.Vendor} v{report.Bios.Version} [Boot: {report.Bios.BootMode}, SecureBoot: {report.Bios.SecureBootStatus}]");
            AppendLog($"👤  User/Host    : {report.SystemUser.UserName} @ {report.SystemUser.MachineName} ({report.SystemUser.OsDescription})");
            AppendLog($"🧠  CPU / RAM    : {report.SystemUser.ProcessorName} ({report.SystemUser.ProcessorCount} Cores) | {report.SystemUser.TotalPhysicalMemoryMb}");
            AppendLog($"🌐  Network MAC  : {report.Network.PrimaryMacAddress} | Gateway: {report.Network.PrimaryGateway} | DNS: {report.Network.PrimaryDns}");
            AppendLog($"📡  Active IP    : {report.Network.PrimaryIpAddress} (Status: {report.Network.InternetStatus})");

            foreach (var note in report.Assessment.CompatibilityNotes)
            {
                AppendLog($"    {note}");
            }

            AppendLog($"🎯 [ASSESSMENT] Workstation Compatibility: {report.Assessment.CompatibilityScore}");
            AppendLog($"    Recommended Profile: {report.Assessment.RecommendedMode}");

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
                    "• Hardware Verified: Motherboard, BIOS & Network profile locked\n" +
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
