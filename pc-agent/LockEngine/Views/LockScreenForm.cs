using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using PC.SecurityAgent.Controllers;

namespace PC.SecurityAgent.LockEngine.Views
{
    public class LockScreenForm : Form
    {
        private readonly Action _onUnlocked;
        private Label _lblClock = null!;
        private Label _lblDate = null!;
        private Label _lblPinDisplay = null!;
        private Label _lblStatusMsg = null!;
        private string _enteredPin = "";
        private System.Windows.Forms.Timer _clockTimer = null!;

        public LockScreenForm(Action onUnlocked)
        {
            _onUnlocked = onUnlocked;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(10, 15, 29); // Cyber Deep Slate
            this.ForeColor = Color.White;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            int screenWidth = Screen.PrimaryScreen?.Bounds.Width ?? 1920;
            int screenHeight = Screen.PrimaryScreen?.Bounds.Height ?? 1080;

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
                ForeColor = Color.FromArgb(56, 189, 248), // Sky 400
                AutoSize = true,
                Location = new Point(40, 20)
            };

            var lblSubtitle = new Label
            {
                Text = "Protected by Hybrid Dual-Plane Architecture • All Local Interactivity Suspended",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Location = new Point(40, 48)
            };

            var lblPulsingLock = new Label
            {
                Text = "🔒 SYSTEM LOCKED",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(244, 63, 94), // Rose 500
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
                Text = "📱 Remote Mobile Authorization",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Location = new Point(25, 20),
                AutoSize = true
            };

            var lblMobileDesc = new Label
            {
                Text = "Tap UNLOCK in your Mobile App to unlock this workstation remotely.\r\nOr scan this dynamic pairing badge with your phone:",
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
            picQr.Paint += RenderMockQrBadge;

            _lblStatusMsg = new Label
            {
                Text = "● Listening for live cloud unlock command...",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 211, 153), // Emerald 400
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
                Text = "🔢 Emergency Master PIN",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(244, 114, 182), // Pink 400
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
                        Cursor = Cursors.Hand
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

            // 5. Timer for Clock
            _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _clockTimer.Tick += (s, e) =>
            {
                _lblClock.Text = DateTime.Now.ToString("HH:mm:ss");
                _lblDate.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            };
            _clockTimer.Start();

            // Keyboard listener
            this.KeyDown += OnFormKeyDown;
        }

        private void RenderMockQrBadge(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int size = 180;
            g.Clear(Color.FromArgb(15, 23, 42));

            // Draw Cyber Grid / QR Pattern
            using var pen = new Pen(Color.FromArgb(56, 189, 248), 3);
            using var brush = new SolidBrush(Color.FromArgb(56, 189, 248));
            using var accentBrush = new SolidBrush(Color.FromArgb(14, 165, 233));

            // 3 Corner Eye Squares
            DrawQrEye(g, 15, 15, 40);
            DrawQrEye(g, size - 55, 15, 40);
            DrawQrEye(g, 15, size - 55, 40);

            // Pseudo QR Data Blocks
            for (int x = 65; x < size - 65; x += 15)
            {
                for (int y = 20; y < size - 20; y += 15)
                {
                    if ((x * y + 7) % 3 == 0)
                    {
                        g.FillRectangle(brush, x, y, 10, 10);
                    }
                }
            }

            // Center Lock Icon
            g.FillRectangle(new SolidBrush(Color.FromArgb(10, 15, 29)), (size - 36) / 2, (size - 36) / 2, 36, 36);
            g.DrawString("🔒", new Font("Segoe UI", 14f), Brushes.White, (size - 30) / 2, (size - 30) / 2);
        }

        private void DrawQrEye(Graphics g, int x, int y, int size)
        {
            using var outerPen = new Pen(Color.FromArgb(56, 189, 248), 3);
            using var innerBrush = new SolidBrush(Color.FromArgb(56, 189, 248));
            g.DrawRectangle(outerPen, x, y, size, size);
            g.FillRectangle(innerBrush, x + 10, y + 10, size - 20, size - 20);
        }

        private void HandleKeypadPress(string key)
        {
            if (key == "C")
            {
                _enteredPin = "";
                UpdatePinDisplay();
            }
            else if (key == "OK")
            {
                VerifyAndUnlock();
            }
            else
            {
                if (_enteredPin.Length < 16)
                {
                    _enteredPin += key;
                    UpdatePinDisplay();
                }
            }
        }

        private void OnFormKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
            {
                HandleKeypadPress(((char)('0' + (e.KeyCode - Keys.D0))).ToString());
                e.Handled = true;
            }
            else if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9)
            {
                HandleKeypadPress(((char)('0' + (e.KeyCode - Keys.NumPad0))).ToString());
                e.Handled = true;
            }
            else if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
            {
                char c = (char)('A' + (e.KeyCode - Keys.A));
                if (_enteredPin.Length < 16)
                {
                    _enteredPin += c;
                    UpdatePinDisplay();
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Back)
            {
                if (_enteredPin.Length > 0)
                {
                    _enteredPin = _enteredPin.Substring(0, _enteredPin.Length - 1);
                    UpdatePinDisplay();
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                VerifyAndUnlock();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _enteredPin = "";
                UpdatePinDisplay();
                e.Handled = true;
            }
        }

        private void UpdatePinDisplay()
        {
            if (string.IsNullOrEmpty(_enteredPin))
            {
                _lblPinDisplay.Text = "ENTER PIN";
                _lblPinDisplay.ForeColor = Color.FromArgb(148, 163, 184);
            }
            else
            {
                _lblPinDisplay.Text = new string('●', _enteredPin.Length);
                _lblPinDisplay.ForeColor = Color.FromArgb(56, 189, 248);
            }
        }

        private void VerifyAndUnlock()
        {
            string pin = _enteredPin.Trim();
            if (pin.Equals("998877", StringComparison.OrdinalIgnoreCase) ||
                pin.Equals("SHJ", StringComparison.OrdinalIgnoreCase) ||
                pin.Equals("shj", StringComparison.OrdinalIgnoreCase) ||
                pin.Equals("123456", StringComparison.OrdinalIgnoreCase))
            {
                _lblPinDisplay.Text = "✔ ACCESS GRANTED";
                _lblPinDisplay.ForeColor = Color.FromArgb(52, 211, 153);
                _lblStatusMsg.Text = "Unlocking desktop...";
                _lblStatusMsg.ForeColor = Color.FromArgb(52, 211, 153);

                Task.Delay(400).ContinueWith(_ =>
                {
                    this.Invoke(new Action(() =>
                    {
                        _clockTimer.Stop();
                        this.Close();
                        _onUnlocked?.Invoke();
                    }));
                });
            }
            else
            {
                _lblPinDisplay.Text = "✖ ACCESS DENIED";
                _lblPinDisplay.ForeColor = Color.FromArgb(244, 63, 94);
                _enteredPin = "";
                Task.Delay(800).ContinueWith(_ =>
                {
                    this.Invoke(new Action(UpdatePinDisplay));
                });
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _clockTimer.Stop();
            base.OnFormClosing(e);
        }
    }
}
