using System;
using System.Drawing;
using System.Windows.Forms;

namespace PC.SecurityAgent.LockEngine.Views
{
    public partial class LockScreenForm
    {
        private Label _lblClock = null!;
        private Label _lblDate = null!;
        private Label _lblPinDisplay = null!;
        private Label _lblStatusMsg = null!;
        private System.Windows.Forms.Timer _clockTimer = null!;
        private System.Windows.Forms.Timer _topmostTimer = null!;

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Normal;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.ControlBox = false;
            this.BackColor = Color.FromArgb(10, 15, 29); // Cyber Deep Slate
            this.ForeColor = Color.White;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            int screenWidth = this.Width;
            int screenHeight = this.Height;

            // 1. Cyber Header Panel
            var pnlHeader = new Panel
            {
                Size = new Size(screenWidth, 90),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(15, 23, 42)
            };

            var lblBadge = new Label
            {
                Text = "CYBER WORKSTATION SECURITY GUARD",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                AutoSize = true,
                Location = new Point(40, 20)
            };

            var lblSubtitle = new Label
            {
                Text = "Protected by Hybrid Dual-Plane Architecture • Desktop Session Locked",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Location = new Point(40, 48)
            };

            var lblPulsingLock = new Label
            {
                Text = "\uD83D\uDD12 SYSTEM LOCKED",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(244, 63, 94),
                BackColor = Color.FromArgb(30, 27, 46),
                Padding = new Padding(12, 6, 12, 6),
                AutoSize = true,
                Location = new Point(screenWidth - 260, 26)
            };

            pnlHeader.Controls.Add(lblBadge);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(lblPulsingLock);
            this.Controls.Add(pnlHeader);

            // 2. Center Container
            int containerWidth = 980;
            int containerHeight = 560;
            int containerX = (screenWidth - containerWidth) / 2;
            int containerY = (screenHeight - containerHeight) / 2 + 20;

            var pnlContainer = new Panel
            {
                Size = new Size(containerWidth, containerHeight),
                Location = new Point(containerX, containerY),
                BackColor = Color.Transparent
            };

            // 3. Digital Clock & Date
            _lblClock = new Label
            {
                Text = DateTime.Now.ToString("HH:mm:ss"),
                Font = new Font("Segoe UI", 48f, FontStyle.Bold),
                ForeColor = Color.FromArgb(241, 245, 249),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(containerWidth, 85),
                Location = new Point(0, 0)
            };

            _lblDate = new Label
            {
                Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy"),
                Font = new Font("Segoe UI", 13f, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(containerWidth, 30),
                Location = new Point(0, 85)
            };

            pnlContainer.Controls.Add(_lblClock);
            pnlContainer.Controls.Add(_lblDate);

            // 4. Split Cards: Mobile Controller (Left) + Keypad (Right)
            int cardY = 135;
            int cardWidth = 460;
            int cardHeight = 380;

            // Card A: Mobile Push / QR Card
            var pnlMobile = new Panel
            {
                Size = new Size(cardWidth, cardHeight),
                Location = new Point(10, cardY),
                BackColor = Color.FromArgb(19, 29, 49)
            };

            var lblMobileTitle = new Label
            {
                Text = "\uD83D\uDCF1 Remote Mobile Authorization",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Location = new Point(25, 20),
                AutoSize = true
            };

            var lblMobileDesc = new Label
            {
                Text = "Tap UNLOCK in your Mobile App to unlock this workstation remotely.\r\nOr type master emergency PIN on the keypad to unlock:",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(25, 52),
                Size = new Size(410, 40)
            };

            var picQr = new PictureBox
            {
                Size = new Size(180, 180),
                Location = new Point((cardWidth - 180) / 2, 105),
                BackColor = Color.FromArgb(15, 23, 42),
                BorderStyle = BorderStyle.FixedSingle
            };
            picQr.Paint += (s, e) => QrCodeRenderer.RenderMockQrBadge(e.Graphics, 180);

            _lblStatusMsg = new Label
            {
                Text = "• Listening for live cloud unlock command...",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 211, 153),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(cardWidth, 35),
                Location = new Point(0, 310)
            };

            pnlMobile.Controls.Add(lblMobileTitle);
            pnlMobile.Controls.Add(lblMobileDesc);
            pnlMobile.Controls.Add(picQr);
            pnlMobile.Controls.Add(_lblStatusMsg);
            pnlContainer.Controls.Add(pnlMobile);

            // Card B: Emergency PIN Keypad Card
            var pnlKeypad = new Panel
            {
                Size = new Size(cardWidth, cardHeight),
                Location = new Point(510, cardY),
                BackColor = Color.FromArgb(19, 29, 49)
            };

            var lblKeypadTitle = new Label
            {
                Text = "\uD83D\uDD11 Emergency Master PIN",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(244, 114, 182),
                Location = new Point(25, 20),
                AutoSize = true
            };

            _lblPinDisplay = new Label
            {
                Text = "ENTER PIN",
                Font = new Font("Consolas", 15f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.FromArgb(15, 23, 42),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(390, 45),
                Location = new Point(35, 55),
                BorderStyle = BorderStyle.FixedSingle
            };

            // 3x4 Keypad Grid
            string[,] buttons = new string[,]
            {
                { "1", "2", "3" },
                { "4", "5", "6" },
                { "7", "8", "9" },
                { "C", "0", "OK" }
            };

            int btnW = 120;
            int btnH = 45;
            int startX = 35;
            int startY = 115;
            int gap = 15;

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    string txt = buttons[r, c];
                    var btn = new Button
                    {
                        Text = txt,
                        Size = new Size(btnW, btnH),
                        Location = new Point(startX + c * (btnW + gap), startY + r * (btnH + gap)),
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                        BackColor = (txt == "OK") ? Color.FromArgb(16, 185, 129) :
                                    (txt == "C")  ? Color.FromArgb(239, 68, 68) : Color.FromArgb(30, 41, 59),
                        ForeColor = Color.White,
                        Cursor = Cursors.Hand,
                        TabStop = false
                    };
                    btn.FlatAppearance.BorderSize = 0;
                    btn.Click += (s, e) => HandleKeypadPress(txt);
                    pnlKeypad.Controls.Add(btn);
                }
            }

            pnlKeypad.Controls.Add(lblKeypadTitle);
            pnlKeypad.Controls.Add(_lblPinDisplay);
            pnlContainer.Controls.Add(pnlKeypad);

            this.Controls.Add(pnlContainer);

            // 5. Timers
            _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _clockTimer.Tick += (s, e) =>
            {
                _lblClock.Text = DateTime.Now.ToString("HH:mm:ss");
                _lblDate.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            };
            _clockTimer.Start();

            // Periodic TopMost Guard
            _topmostTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _topmostTimer.Tick += (s, e) =>
            {
                if (!_isAuthorizedToClose && this.IsHandleCreated)
                {
                    SetWindowPos(this.Handle, HWND_TOPMOST, this.Left, this.Top, this.Width, this.Height, SWP_SHOWWINDOW);
                }
            };
            _topmostTimer.Start();
        }
    }
}
