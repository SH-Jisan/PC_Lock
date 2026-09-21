using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PC.SecurityAgent.LockEngine.Views
{
    public partial class LockScreenForm : Form
    {
        private readonly Action _onUnlocked;
        private string _enteredPin = "";
        private bool _isAuthorizedToClose = false;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_SHOWWINDOW = 0x0040;

        public LockScreenForm(Action onUnlocked)
        {
            _onUnlocked = onUnlocked;
            InitializeComponent();
        }

        public void AllowUnlockAndClose()
        {
            _isAuthorizedToClose = true;
            _clockTimer?.Stop();
            _topmostTimer?.Stop();
            this.Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.Activate();
            this.Focus();
            SetWindowPos(this.Handle, HWND_TOPMOST, this.Left, this.Top, this.Width, this.Height, SWP_SHOWWINDOW);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Global key interceptor across all controls on this form
            Keys keyCode = keyData & Keys.KeyCode;

            if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
            {
                HandleKeypadPress(((char)('0' + (keyCode - Keys.D0))).ToString());
                return true;
            }
            if (keyCode >= Keys.NumPad0 && keyCode <= Keys.NumPad9)
            {
                HandleKeypadPress(((char)('0' + (keyCode - Keys.NumPad0))).ToString());
                return true;
            }
            if (keyCode >= Keys.A && keyCode <= Keys.Z)
            {
                char c = (char)('A' + (keyCode - Keys.A));
                HandleKeypadPress(c.ToString());
                return true;
            }
            if (keyCode == Keys.Back)
            {
                if (_enteredPin.Length > 0)
                {
                    _enteredPin = _enteredPin.Substring(0, _enteredPin.Length - 1);
                    UpdatePinDisplay();
                }
                return true;
            }
            if (keyCode == Keys.Enter)
            {
                VerifyAndUnlock();
                return true;
            }
            if (keyCode == Keys.Escape)
            {
                _enteredPin = "";
                UpdatePinDisplay();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
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

        private void UpdatePinDisplay()
        {
            if (string.IsNullOrEmpty(_enteredPin))
            {
                _lblPinDisplay.Text = "ENTER PIN";
                _lblPinDisplay.ForeColor = Color.FromArgb(148, 163, 184);
            }
            else
            {
                _lblPinDisplay.Text = new string('\u25CF', _enteredPin.Length);
                _lblPinDisplay.ForeColor = Color.FromArgb(56, 189, 248);
            }
        }

        private void VerifyAndUnlock()
        {
            string pin = _enteredPin.Trim();
            if (pin.Equals("998877", StringComparison.OrdinalIgnoreCase) ||
                pin.Equals("SHJ", StringComparison.OrdinalIgnoreCase) ||
                pin.Equals("123456", StringComparison.OrdinalIgnoreCase))
            {
                _lblPinDisplay.Text = "[\u2714] ACCESS GRANTED";
                _lblPinDisplay.ForeColor = Color.FromArgb(52, 211, 153);
                _lblStatusMsg.Text = "Unlocking desktop...";
                _lblStatusMsg.ForeColor = Color.FromArgb(52, 211, 153);

                _isAuthorizedToClose = true;
                _clockTimer?.Stop();
                _topmostTimer?.Stop();

                Task.Delay(350).ContinueWith(_ =>
                {
                    if (this.IsHandleCreated)
                    {
                        this.Invoke(new Action(() =>
                        {
                            this.Close();
                            _onUnlocked?.Invoke();
                        }));
                    }
                });
            }
            else
            {
                _lblPinDisplay.Text = "[\u2716] ACCESS DENIED";
                _lblPinDisplay.ForeColor = Color.FromArgb(244, 63, 94);
                _enteredPin = "";
                Task.Delay(800).ContinueWith(_ =>
                {
                    if (this.IsHandleCreated)
                    {
                        this.Invoke(new Action(UpdatePinDisplay));
                    }
                });
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isAuthorizedToClose)
            {
                // Prevent Alt+F4 or programmatic bypass
                e.Cancel = true;
                return;
            }

            _clockTimer?.Stop();
            _topmostTimer?.Stop();
            base.OnFormClosing(e);
        }
    }
}
