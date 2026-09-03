using System;
using System.Runtime.InteropServices;

namespace PC.SecurityAgent.LockEngine
{
    public static class DesktopManager
    {
        private const uint DESKTOP_ALL_ACCESS = 0x01FF;
        private const string SECURITY_DESKTOP_NAME = "PCSecurityDesktop";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateDesktop(
            string lpszDesktop,
            IntPtr lpszDevice,
            IntPtr pDevmode,
            int dwFlags,
            uint dwDesiredAccess,
            IntPtr lpsa);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenDesktop(
            string lpszDesktop,
            int dwFlags,
            bool fInherit,
            uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenInputDesktop(
            int dwFlags,
            bool fInherit,
            uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SwitchDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetThreadDesktop(int dwThreadId);

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        private static IntPtr _securityDesktopHandle = IntPtr.Zero;
        private static IntPtr _originalDesktopHandle = IntPtr.Zero;

        public static IntPtr EnsureSecurityDesktop()
        {
            if (_securityDesktopHandle == IntPtr.Zero)
            {
                _securityDesktopHandle = OpenDesktop(SECURITY_DESKTOP_NAME, 0, false, DESKTOP_ALL_ACCESS);
                if (_securityDesktopHandle == IntPtr.Zero)
                {
                    _securityDesktopHandle = CreateDesktop(
                        SECURITY_DESKTOP_NAME,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        0,
                        DESKTOP_ALL_ACCESS,
                        IntPtr.Zero);
                }
            }
            return _securityDesktopHandle;
        }

        public static bool SwitchToSecurityDesktop()
        {
            try
            {
                if (_originalDesktopHandle == IntPtr.Zero)
                {
                    _originalDesktopHandle = OpenInputDesktop(0, false, DESKTOP_ALL_ACCESS);
                }

                IntPtr secDesk = EnsureSecurityDesktop();
                if (secDesk != IntPtr.Zero)
                {
                    return SwitchDesktop(secDesk);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DesktopManager Error] Failed to switch to security desktop: {ex.Message}");
            }
            return false;
        }

        public static bool AssignCurrentThreadToSecurityDesktop()
        {
            try
            {
                IntPtr secDesk = EnsureSecurityDesktop();
                if (secDesk != IntPtr.Zero)
                {
                    return SetThreadDesktop(secDesk);
                }
            }
            catch { }
            return false;
        }

        public static bool RestoreOriginalDesktop()
        {
            try
            {
                if (_originalDesktopHandle != IntPtr.Zero)
                {
                    bool switched = SwitchDesktop(_originalDesktopHandle);
                    _originalDesktopHandle = IntPtr.Zero;
                    return switched;
                }

                IntPtr defaultDesk = OpenDesktop("Default", 0, false, DESKTOP_ALL_ACCESS);
                if (defaultDesk != IntPtr.Zero)
                {
                    bool switched = SwitchDesktop(defaultDesk);
                    CloseDesktop(defaultDesk);
                    return switched;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DesktopManager Error] Failed to restore original desktop: {ex.Message}");
            }
            return false;
        }
    }
}
