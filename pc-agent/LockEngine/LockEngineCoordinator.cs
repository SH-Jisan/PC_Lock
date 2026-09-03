using System;
using System.Diagnostics;
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

            Console.WriteLine("[LockEngine] Activating Topmost Cyber Shield Lock Engine...");

            // 1. Enforce system policy (Disable Task Manager, Change Password, Logoff)
            TaskManagerPolicy.DisableTaskManager();

            // 2. Launch Fullscreen Cyber UI on dedicated STA UI thread with message pump
            _uiThread = new Thread(() =>
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    _activeForm = new LockScreenForm(() =>
                    {
                        HideLockScreen();
                        _externalUnlockCallback?.Invoke();
                    });

                    // 3. Install Keyboard Hook on this exact UI thread so Application.Run message pump drives it!
                    KeyboardHook.InstallHook();

                    Application.Run(_activeForm);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LockEngine Error] UI thread exception: {ex.Message}");
                    HideLockScreen();
                }
                finally
                {
                    KeyboardHook.UninstallHook();
                }
            })
            {
                IsBackground = true
            };

            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            Console.WriteLine("[LockEngine] Topmost Cyber Lock Screen live and active on screen.");
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
                        _activeForm.AllowUnlockAndClose();
                    }));
                }
            }
            catch { }
            _activeForm = null;

            // 2. Remove Keyboard Hook
            KeyboardHook.UninstallHook();

            // 3. Re-enable Task Manager and System Policies
            TaskManagerPolicy.EnableTaskManager();

            Console.WriteLine("[LockEngine] Desktop restored to normal state.");
        }
    }
}
