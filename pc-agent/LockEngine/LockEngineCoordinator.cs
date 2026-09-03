using System;
using System.Threading;
using System.Windows.Forms;
using PC.SecurityAgent.LockEngine.Views;

namespace PC.SecurityAgent.LockEngine
{
    public static class LockEngineCoordinator
    {
        private static readonly object _syncLock = new object();
        private static bool _isLocked = false;
        private static Thread? _uiThread;
        private static LockScreenForm? _activeForm;
        private static Action? _externalUnlockCallback;

        public static bool IsLocked
        {
            get
            {
                lock (_syncLock) return _isLocked;
            }
        }

        public static void RegisterExternalUnlockCallback(Action callback)
        {
            _externalUnlockCallback = callback;
        }

        public static void ShowLockScreen()
        {
            lock (_syncLock)
            {
                if (_isLocked) return;
                _isLocked = true;
            }

            Console.WriteLine("[LockEngine] Activating Hybrid Dual-Plane Custom Lock Engine...");

            // 1. Disable Task Manager via registry policy
            TaskManagerPolicy.DisableTaskManager();

            // 2. Install Low-Level Keyboard Shortcut Shield (Alt+Tab, WinKey, Alt+F4)
            KeyboardHook.InstallHook();

            // 3. Switch Desktop to PCSecurityDesktop
            DesktopManager.SwitchToSecurityDesktop();

            // 4. Launch Fullscreen Cyber UI on dedicated STA thread
            _uiThread = new Thread(() =>
            {
                try
                {
                    DesktopManager.AssignCurrentThreadToSecurityDesktop();
                    _activeForm = new LockScreenForm(() =>
                    {
                        HideLockScreen();
                        _externalUnlockCallback?.Invoke();
                    });

                    Application.Run(_activeForm);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LockEngine Error] UI thread failure: {ex.Message}");
                }
            })
            {
                IsBackground = true
            };

            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            Console.WriteLine("[LockEngine] Custom Cyber Lock Screen live and active.");
        }

        public static void HideLockScreen()
        {
            lock (_syncLock)
            {
                if (!_isLocked) return;
                _isLocked = false;
            }

            Console.WriteLine("[LockEngine] Deactivating Custom Lock Engine...");

            // 1. Close active UI form
            try
            {
                if (_activeForm != null && _activeForm.IsHandleCreated)
                {
                    _activeForm.Invoke(new Action(() =>
                    {
                        _activeForm.Close();
                    }));
                }
            }
            catch { }
            _activeForm = null;

            // 2. Remove Keyboard Hook
            KeyboardHook.UninstallHook();

            // 3. Re-enable Task Manager
            TaskManagerPolicy.EnableTaskManager();

            // 4. Restore original Windows Desktop
            DesktopManager.RestoreOriginalDesktop();

            Console.WriteLine("[LockEngine] Desktop restored to normal state.");
        }
    }
}
