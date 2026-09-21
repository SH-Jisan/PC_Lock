#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace DeployManager
{
    public partial class MainForm
    {
        private ProgressBar _progressBar = null!;
        private TextBox _txtLogs = null!;
        private Button _btnDeployEnterprise = null!;
        private Button _btnVerifyHealth = null!;
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
        private Label _lblAdaptiveProfile = null!;
        private Label _lblAdaptiveRationale = null!;
        private Label _lblCompatibilityBadge = null!;
        private Services.HardwareAuditReport? _latestAuditReport;

        private void InitializeComponent()
        {
            this.Text = "PC Security System - Hardware-Adaptive Deployment & Diagnostic Hub";
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
                Text = "PC Security - Hardware-Adaptive Auto-Configuration Hub",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248), // Cyan 400
                AutoSize = true,
                Location = new Point(15, 12)
            };

            var lblSubtitle = new Label
            {
                Text = "Deep Hardware Diagnostics • Tailored Setup • Autonomous Self-Verification & Health Engine",
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
                Text = "Workstation Hardware Profile & Adaptive Strategy Assessment",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                Location = new Point(12, 10),
                AutoSize = true
            };
            pnlHardwareCard.Controls.Add(lblCardTitle);

            _btnRescanHardware = new Button
            {
                Text = "Re-Scan",
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

            // Column 2: Network, Storage, EFI Pre-Boot, Adaptive Profile (Right column: X=405)
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

            _lblAdaptiveProfile = new Label
            {
                Text = "• Adaptive Engine: Evaluating hardware profile...",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Location = new Point(405, 104),
                Size = new Size(390, 20)
            };
            pnlHardwareCard.Controls.Add(_lblAdaptiveProfile);

            var lblBadgeHeader = new Label
            {
                Text = "Deployment & Adaptive Readiness Profile:",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(12, 130),
                AutoSize = true
            };
            pnlHardwareCard.Controls.Add(lblBadgeHeader);

            _lblCompatibilityBadge = new Label
            {
                Text = "AUDITING WORKSTATION HARDWARE & PRE-BOOT STACK...",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(250, 204, 21), // Yellow 400
                Location = new Point(12, 150),
                AutoSize = true
            };
            pnlHardwareCard.Controls.Add(_lblCompatibilityBadge);

            _lblAdaptiveRationale = new Label
            {
                Text = "Inspecting hardware configuration vectors...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(12, 175),
                Size = new Size(780, 22)
            };
            pnlHardwareCard.Controls.Add(_lblAdaptiveRationale);

            this.Controls.Add(pnlHardwareCard);

            // 3. Action Buttons & Status Panel
            var pnlActions = new Panel
            {
                Location = new Point(20, 310),
                Size = new Size(805, 88)
            };

            _btnDeployEnterprise = new Button
            {
                Text = "Deploy Adaptive Security",
                Location = new Point(0, 5),
                Size = new Size(270, 46),
                BackColor = Color.FromArgb(14, 165, 233), // Sky 500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnDeployEnterprise.FlatAppearance.BorderSize = 0;
            _btnDeployEnterprise.Click += async (s, e) => await HandleDeployEnterpriseAsync();

            _btnVerifyHealth = new Button
            {
                Text = "Run Self-Diagnosis Check",
                Location = new Point(280, 5),
                Size = new Size(260, 46),
                BackColor = Color.FromArgb(16, 185, 129), // Emerald 500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnVerifyHealth.FlatAppearance.BorderSize = 0;
            _btnVerifyHealth.Click += async (s, e) => await HandleRunSelfTestAsync();

            _btnUninstall = new Button
            {
                Text = "Uninstall & Restore",
                Location = new Point(550, 5),
                Size = new Size(255, 46),
                BackColor = Color.FromArgb(225, 29, 72), // Rose 600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnUninstall.FlatAppearance.BorderSize = 0;
            _btnUninstall.Click += async (s, e) => await HandleUninstallAsync();

            _lblStatus = new Label
            {
                Text = "Workstation hardware analysis complete. Ready to deploy or run self-diagnosis.",
                Location = new Point(2, 58),
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225)
            };

            pnlActions.Controls.Add(_btnDeployEnterprise);
            pnlActions.Controls.Add(_btnVerifyHealth);
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
                Text = "Live Deployment & Autonomous Diagnostic Health Stream:",
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
                Text = "Copy Logs",
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

            AppendLog("DeployManager GUI initialized with Auto-Diagnostics.");
        }
    }
}
