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
        private Label _lblStatus = null!;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "PC Security & Remote Lock Controller - Deployment Hub";
            this.Size = new Size(740, 580);
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
                Text = "Enterprise Zero-Risk Architecture: 0% Motherboard Freeze Risk + Full Remote Lock",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(148, 163, 184), // Slate 400
                AutoSize = true,
                Location = new Point(16, 42)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // 2. Buttons & Actions Panel
            var pnlActions = new Panel
            {
                Location = new Point(20, 100),
                Size = new Size(685, 90)
            };

            _btnDeployEnterprise = new Button
            {
                Text = "🚀 Deploy Enterprise Security (Zero Boot Risk)",
                Location = new Point(0, 5),
                Size = new Size(335, 48),
                BackColor = Color.FromArgb(14, 165, 233), // Sky 500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnDeployEnterprise.FlatAppearance.BorderSize = 0;
            _btnDeployEnterprise.Click += async (s, e) => await HandleDeployEnterpriseAsync();

            _btnUninstall = new Button
            {
                Text = "🗑️ Completely Uninstall & Restore",
                Location = new Point(350, 5),
                Size = new Size(335, 48),
                BackColor = Color.FromArgb(225, 29, 72), // Rose 600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnUninstall.FlatAppearance.BorderSize = 0;
            _btnUninstall.Click += async (s, e) => await HandleUninstallAsync();

            _lblStatus = new Label
            {
                Text = "Ready to deploy or manage workstation security.",
                Location = new Point(2, 60),
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225)
            };

            pnlActions.Controls.Add(_btnDeployEnterprise);
            pnlActions.Controls.Add(_btnUninstall);
            pnlActions.Controls.Add(_lblStatus);
            this.Controls.Add(pnlActions);

            // 3. Progress Bar
            _progressBar = new ProgressBar
            {
                Location = new Point(20, 200),
                Size = new Size(685, 12),
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };
            this.Controls.Add(_progressBar);

            // 4. Live Terminal Console Box
            var lblLogsTitle = new Label
            {
                Text = "📋 Live Deployment & Diagnostics Stream:",
                Location = new Point(20, 222),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            this.Controls.Add(lblLogsTitle);

            _txtLogs = new TextBox
            {
                Location = new Point(20, 245),
                Size = new Size(685, 235),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(2, 6, 23), // Slate 950
                ForeColor = Color.FromArgb(52, 211, 153), // Emerald 400
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_txtLogs);

            // 5. Footer Bar
            var btnCopy = new Button
            {
                Text = "📋 Copy Logs",
                Location = new Point(20, 490),
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
                Text = "Cyber Workstation Guard • Enterprise Zero-Risk Remote Security Engine",
                Location = new Point(220, 497),
                AutoSize = true,
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            this.Controls.Add(lblFooter);

            AppendLog("✅ DeployManager GUI initialized. Ready.");
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
            _lblStatus.Text = "⏳ Deploying Enterprise Zero-Risk Security...";
            _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

            var progress = new Progress<int>(v => _progressBar.Value = v);

            bool success = await Task.Run(() => DeploymentEngine.DeployEnterpriseZeroRiskAsync(AppendLog, progress));

            _btnDeployEnterprise.Enabled = true;
            _btnUninstall.Enabled = true;

            if (success)
            {
                _lblStatus.Text = "🟢 Enterprise Security Active! (0% Boot Risk)";
                _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
                MessageBox.Show(
                    "Enterprise Zero-Risk Security System is successfully deployed and active!\n\n" +
                    "• 0% BIOS/Boot Freeze Risk (Standard Factory Microsoft Bootloader)\n" +
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
                "• Device records will be purged from Supabase Cloud\n" +
                "• Background security agent will be removed",
                "Confirm Uninstallation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm != DialogResult.Yes) return;

            _btnDeployEnterprise.Enabled = false;
            _btnUninstall.Enabled = false;
            _lblStatus.Text = "⏳ Uninstalling and restoring system...";
            _lblStatus.ForeColor = Color.FromArgb(244, 63, 94);

            var progress = new Progress<int>(v => _progressBar.Value = v);

            bool success = await Task.Run(() => DeploymentEngine.UninstallAsync(AppendLog, progress));

            _btnDeployEnterprise.Enabled = true;
            _btnUninstall.Enabled = true;

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
